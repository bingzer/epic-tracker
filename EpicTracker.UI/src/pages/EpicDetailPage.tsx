import { useCallback, useEffect, useRef, useState } from 'react';
import { marked } from 'marked';
import { useNavigate, useParams } from 'react-router-dom';
import { EpicApi, SpecApi, DocApi } from '../epicApi';
import type { Epic, AuditLog, AgentAgreement, Spec } from '../types';
import { AuditLogPanel } from '../components/AuditLogPanel';
import { StateBadge } from '../components/StateBadge';
import { EscalationPanel } from '../components/EscalationPanel';
import { useSignalR } from '../hooks/useSignalR';

const PIPELINE_STATES = ['drafting', 'mockup', 'waterproofing', 'spec_writing', 'implementation', 'closed'] as const;

const ALL_EPIC_STATES = [
  'drafting', 'mockup', 'waterproofing', 'spec_writing',
  'implementation', 'human_in_loop', 'agent_swarm', 'closed',
] as const;

const SPEC_STATES = ['drafting', 'ready', 'coding', 'ac', 'code_review', 'human_in_loop', 'done'] as const;

const SPEC_STATE_PROGRESS: Record<string, number> = {
  spec_drafting: 5,
  drafting: 5,
  ready: 20,
  coding: 55,
  code_review: 75,
  ac: 88,
  done: 100,
};

function specDisplayName(id: string, epicId: string): string {
  const prefix = epicId + '-';
  const slug = id.startsWith(prefix) ? id.slice(prefix.length) : id;
  return slug.replace(/-/g, ' ').replace(/\b\w/g, c => c.toUpperCase());
}

// ---- Sub-components ----

function MarkdownDrawer({ path, onClose }: { path: string | null; onClose: () => void }) {
  const [content, setContent] = useState('');
  const [loading, setLoading] = useState(false);
  const overlayRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!path) return;

    setContent('');
    setLoading(true);

    DocApi.get(path)
      .then(text => setContent(String(marked.parse(text))))
      .catch(e => setContent(`<p class="text-red-500">${String(e)}</p>`))
      .finally(() => setLoading(false));
  }, [path]);

  if (!path) return null;

  function handleOverlayClick(e: React.MouseEvent<HTMLDivElement>) {
    if (e.target === overlayRef.current) onClose();
  }

  return (
    <div
      ref={overlayRef}
      onClick={handleOverlayClick}
      className="fixed inset-0 z-50 bg-black/40 flex justify-end"
    >
      <div className="w-full md:w-1/2 bg-zinc-900 h-full flex flex-col shadow-xl border-l border-zinc-800">
        <div className="flex items-center justify-between px-4 py-3 border-b border-zinc-800">
          <span className="text-xs font-mono text-zinc-400 truncate max-w-xs">{path}</span>
          <button onClick={onClose} className="text-zinc-400 hover:text-zinc-200 text-lg leading-none ml-4">×</button>
        </div>
        <div className="flex-1 overflow-auto p-5">
          {loading && <p className="text-sm text-zinc-500">Loading…</p>}

          {!loading && (
            <div
              className="prose prose-sm prose-invert max-w-none"
              dangerouslySetInnerHTML={{ __html: content }}
            />
          )}
        </div>
      </div>
    </div>
  );
}

function StatePipeline({ currentState, lastKnownState }: { currentState: string; lastKnownState: string | null }) {
  const pipelineList = Array.from(PIPELINE_STATES);
  const inPipeline = (pipelineList as string[]).includes(currentState);
  const displayState = inPipeline ? currentState : (lastKnownState ?? 'drafting');
  const activeIndex = (pipelineList as string[]).indexOf(displayState);

  return (
    <div className="bg-zinc-900 border border-zinc-800 rounded-xl px-8 py-5 mb-4">
      <p className="text-[10px] font-bold tracking-widest uppercase text-zinc-600 mb-4">Epic Flow</p>

      <div className="relative flex items-start">
        <div className="absolute top-[5px] left-0 right-0 h-px bg-zinc-700" />

        {pipelineList.map((state, i) => {
          const isDone = i < activeIndex;
          const isActive = i === activeIndex;

          let dotCls = 'w-2.5 h-2.5 rounded-full border-2 border-zinc-600 bg-zinc-600 relative z-10';

          if (isDone) {
            dotCls = 'w-2.5 h-2.5 rounded-full relative z-10 bg-cyan-400 border-2 border-cyan-400 shadow-[0_0_8px_rgba(34,211,238,0.4)]';
          }

          if (isActive) {
            if (state === 'closed') {
              dotCls = 'w-3.5 h-3.5 rounded-full relative z-10 bg-green-500 border-2 border-green-500 shadow-[0_0_8px_rgba(34,197,94,0.4)]';
            } else {
              dotCls = 'w-3.5 h-3.5 rounded-full relative z-10 bg-indigo-500 border-2 border-indigo-500 shadow-[0_0_0_4px_rgba(99,102,241,0.2),0_0_16px_rgba(99,102,241,0.5)] animate-pulse';
            }
          }

          let labelCls = 'text-[10px] tracking-wider uppercase font-semibold mt-1.5 text-zinc-600 whitespace-nowrap';

          if (isDone) {
            labelCls = 'text-[10px] tracking-wider uppercase font-semibold mt-1.5 text-cyan-400 whitespace-nowrap';
          }

          if (isActive) {
            labelCls = state === 'closed'
              ? 'text-[10px] tracking-wider uppercase font-bold mt-1.5 text-green-400 whitespace-nowrap'
              : 'text-[10px] tracking-wider uppercase font-bold mt-1.5 text-indigo-400 whitespace-nowrap';
          }

          return (
            <div key={state} className="flex-1 flex flex-col items-center">
              <div className={dotCls} />
              <span className={labelCls}>{state.replace(/_/g, ' ')}</span>
            </div>
          );
        })}
      </div>
    </div>
  );
}

function InstructionBlock({ instruction, epicAgentName }: { instruction: string | null; epicAgentName: string }) {
  return (
    <div className="bg-zinc-900 border border-zinc-800 rounded-xl p-4">
      <p className="text-[10px] font-bold tracking-widest uppercase text-zinc-600 mb-3">Now — Agent Instruction</p>

      {!instruction && (
        <p className="text-xs text-zinc-600">No active agent instruction.</p>
      )}

      {instruction && (
        <>
          <div className="border-l-[3px] border-indigo-500 bg-indigo-500/[0.06] rounded-r-lg px-4 py-3">
            <p className="font-mono text-[11.5px] leading-relaxed text-indigo-200 whitespace-pre-wrap">{instruction}</p>
          </div>

          <div className="mt-2.5 flex items-center gap-2">
            <span className="text-[10px] text-zinc-600">Epic Agent:</span>
            <span className="font-mono text-[10px] text-indigo-400">{epicAgentName}</span>
            <a href={`openterm:${epicAgentName}`} className="text-base leading-none hover:opacity-70 transition-opacity ml-1" title="Open chat">💬</a>
          </div>
        </>
      )}
    </div>
  );
}

function SwarmPanelBlock({ epic }: { epic: Epic }) {
  if (!epic.agentSwarm) return null;

  const swarm = epic.agentSwarm;

  function agreementIcon(a: AgentAgreement): string {
    if (a.hasAgreed === true) return '✓';
    if (a.hasAgreed === false) return '✗';
    return '…';
  }

  function iconBgCls(a: AgentAgreement): string {
    if (a.hasAgreed === true) return 'bg-emerald-500/20 border border-emerald-500/40 text-emerald-400';
    if (a.hasAgreed === false) return 'bg-red-500/15 border border-red-500/30 text-red-400';
    return 'bg-white/5 border border-white/10 text-zinc-500';
  }

  return (
    <div className="bg-violet-500/[0.07] border border-violet-500/25 rounded-xl p-4">
      <div className="flex items-center justify-between mb-2.5">
        <span className="text-[11px] font-bold text-violet-300 uppercase tracking-wider">Agent Swarm</span>
        <span className="text-[10px] text-violet-600 bg-violet-500/15 px-2 py-0.5 rounded-full">
          Iteration {swarm.iteration + 1}
        </span>
      </div>

      <p className="text-xs text-violet-200/80 mb-3 leading-relaxed">{swarm.objective}</p>

      <div>
        {swarm.agreements.map(a => (
          <div key={a.agentId} className="flex items-center gap-2.5 py-1.5 border-b border-violet-500/10 last:border-b-0">
            <span className={`w-4 h-4 rounded-full flex items-center justify-center text-[9px] flex-shrink-0 ${iconBgCls(a)}`}>
              {agreementIcon(a)}
            </span>
            <span className="font-mono text-xs text-zinc-300 flex-1">{a.agentId}</span>
            {!a.note && a.hasAgreed === null && (
              <span className="text-[11px] text-zinc-600">Waiting…</span>
            )}
          </div>
        ))}
      </div>

      {swarm.hasConsensus && (
        <p className="text-xs text-emerald-400 font-medium mt-2">Consensus reached</p>
      )}

      {swarm.hasDisagreement && (
        <>
          <p className="text-xs text-red-400 font-medium mt-3 mb-2">Disagreements</p>
          {swarm.agreements.filter(a => a.hasAgreed === false && a.note).map(a => (
            <div key={a.agentId} className="mb-2 border border-red-500/20 bg-red-500/[0.05] rounded-lg px-3 py-2">
              <p className="font-mono text-[10px] text-red-400 mb-1">{a.agentId}</p>
              <p className="text-[11px] text-red-300/80 whitespace-pre-wrap leading-relaxed">{a.note}</p>
            </div>
          ))}
        </>
      )}
    </div>
  );
}

function AgentPill({ name }: { name: string }) {
  return (
    <span className="inline-flex items-center gap-1.5 bg-indigo-500/10 border border-indigo-500/20 rounded-full pl-2.5 pr-1 py-1 text-xs font-medium text-indigo-300">
      {name}
      <a
        href={`openterm:${name}`}
        className="text-base leading-none hover:opacity-70 transition-opacity"
        title={`Chat with ${name}`}
      >
        💬
      </a>
    </span>
  );
}

function EpicEditDialog({
  epic,
  onUpdated,
  onForceState,
  onClose,
}: {
  epic: Epic;
  onUpdated: (e: Epic) => void;
  onForceState: (state: string) => void;
  onClose: () => void;
}) {
  const [epicName, setEpicName] = useState(epic.name ?? '');
  const [epicBrief, setEpicBrief] = useState(epic.brief ?? '');
  const [epicSlug, setEpicSlug] = useState(epic.slug ?? '');
  const [epicMockupPath, setEpicMockupPath] = useState(epic.mockupPath ?? '');
  const [agentInput, setAgentInput] = useState('');
  const [reviewerInput, setReviewerInput] = useState(epic.reviewerAgentName ?? '');
  const overlayRef = useRef<HTMLDivElement>(null);

  const inputCls = 'w-full text-xs rounded-lg border border-zinc-700 bg-white/[0.04] px-2.5 py-1.5 text-zinc-100 font-mono focus:outline-none focus:ring-1 focus:ring-indigo-500 focus:border-indigo-500';

  function flagCls(active: boolean): string {
    if (active) return 'inline-flex items-center gap-1 text-xs font-medium px-3 py-1 rounded-full bg-emerald-500/10 text-emerald-400 border border-emerald-500/30';
    return 'inline-flex items-center gap-1 text-xs font-medium px-3 py-1 rounded-full bg-white/[0.04] text-zinc-400 border border-zinc-700';
  }

  async function handleTextField(field: keyof Epic, value: string) {
    try {
      const updated = await EpicApi.update(epic.id, { ...epic, [field]: value || null });
      onUpdated(updated);
    } catch (e) {
      alert(String(e));
    }
  }

  async function handleToggle(field: keyof Epic) {
    try {
      const updated = await EpicApi.update(epic.id, { ...epic, [field]: !(epic[field] as boolean) });
      onUpdated(updated);
    } catch (e) {
      alert(String(e));
    }
  }

  async function handleAddAgent() {
    if (!agentInput.trim()) return;
    const parts = agentInput.split(',').map(s => s.trim()).filter(Boolean);
    const next = [...epic.codingAgentNames];
    for (const p of parts) {
      if (!next.includes(p)) next.push(p);
    }
    setAgentInput('');
    try {
      const updated = await EpicApi.update(epic.id, { ...epic, codingAgentNames: next });
      onUpdated(updated);
    } catch (e) {
      alert(String(e));
    }
  }

  async function handleRemoveAgent(agent: string) {
    try {
      const updated = await EpicApi.update(epic.id, { ...epic, codingAgentNames: epic.codingAgentNames.filter(a => a !== agent) });
      onUpdated(updated);
    } catch (e) {
      alert(String(e));
    }
  }

  async function handleSaveReviewer() {
    try {
      const updated = await EpicApi.update(epic.id, { ...epic, reviewerAgentName: reviewerInput.trim() || null });
      onUpdated(updated);
    } catch (e) {
      alert(String(e));
    }
  }

  function handleOverlayClick(e: React.MouseEvent<HTMLDivElement>) {
    if (e.target === overlayRef.current) onClose();
  }

  return (
    <div
      ref={overlayRef}
      onClick={handleOverlayClick}
      className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4"
    >
      <div className="w-full max-w-lg bg-zinc-900 border border-zinc-700 rounded-2xl shadow-2xl flex flex-col max-h-[90vh]">

        <div className="flex items-center justify-between px-5 py-4 border-b border-zinc-800">
          <p className="text-sm font-bold text-zinc-100">Edit Epic</p>
          <button onClick={onClose} className="text-zinc-500 hover:text-zinc-200 text-xl leading-none">×</button>
        </div>

        <div className="overflow-y-auto flex-1 px-5 py-4 space-y-4">

          <div>
            <label className="text-[10px] font-semibold tracking-widest uppercase text-zinc-600 block mb-1.5">Name</label>
            <input
              value={epicName}
              onChange={e => setEpicName(e.target.value)}
              onBlur={() => handleTextField('name', epicName)}
              onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); handleTextField('name', epicName); } }}
              className={inputCls}
            />
          </div>

          <div>
            <label className="text-[10px] font-semibold tracking-widest uppercase text-zinc-600 block mb-1.5">Slug</label>
            <input
              value={epicSlug}
              onChange={e => setEpicSlug(e.target.value)}
              onBlur={() => handleTextField('slug', epicSlug)}
              onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); handleTextField('slug', epicSlug); } }}
              className={inputCls}
            />
          </div>

          <div>
            <label className="text-[10px] font-semibold tracking-widest uppercase text-zinc-600 block mb-1.5">Brief</label>
            <textarea
              value={epicBrief}
              onChange={e => setEpicBrief(e.target.value)}
              onBlur={() => handleTextField('brief', epicBrief)}
              rows={3}
              className={inputCls + ' resize-y'}
            />
          </div>

          <div>
            <label className="text-[10px] font-semibold tracking-widest uppercase text-zinc-600 block mb-1.5">Flags</label>
            <div className="flex flex-wrap gap-1.5">
              <button onClick={() => handleToggle('needsMockup')} className={flagCls(epic.needsMockup)}>
                {epic.needsMockup ? '✓' : '○'} Needs Mockup
              </button>
              <button onClick={() => handleToggle('isDocDrafted')} className={flagCls(epic.isDocDrafted)}>
                {epic.isDocDrafted ? '✓' : '○'} Doc Drafted
              </button>
              {epic.needsMockup && (
                <button onClick={() => handleToggle('isMockupDone')} className={flagCls(epic.isMockupDone)}>
                  {epic.isMockupDone ? '✓' : '○'} Mockup Done
                </button>
              )}
              <button onClick={() => handleToggle('isACRequired')} className={flagCls(epic.isACRequired)}>
                {epic.isACRequired ? '✓' : '○'} AC Required
              </button>
              <button onClick={() => handleToggle('isCodeReviewRequired')} className={flagCls(epic.isCodeReviewRequired)}>
                {epic.isCodeReviewRequired ? '✓' : '○'} Code Review Required
              </button>
            </div>
          </div>

          <div>
            <label className="text-[10px] font-semibold tracking-widest uppercase text-zinc-600 block mb-1.5">Coding Agents</label>
            <div className="flex flex-wrap gap-1.5 mb-2">
              {epic.codingAgentNames.map(a => (
                <span key={a} className="inline-flex items-center gap-1.5 bg-indigo-500/10 border border-indigo-500/20 rounded-full px-2.5 py-1 text-xs font-medium text-indigo-300">
                  {a}
                  <button onClick={() => handleRemoveAgent(a)} className="text-zinc-500 hover:text-red-400 leading-none">×</button>
                </span>
              ))}
            </div>
            <div className="flex gap-2">
              <input
                value={agentInput}
                onChange={e => setAgentInput(e.target.value)}
                onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); handleAddAgent(); } }}
                placeholder="agent-id"
                className={inputCls + ' flex-1'}
              />
              <button
                onClick={handleAddAgent}
                className="text-xs px-3 py-1.5 rounded-lg border border-zinc-700 bg-white/[0.04] text-zinc-400 hover:text-zinc-200 hover:bg-white/[0.07] transition-colors flex-shrink-0"
              >
                Add
              </button>
            </div>
          </div>

          <div>
            <label className="text-[10px] font-semibold tracking-widest uppercase text-zinc-600 block mb-1.5">Reviewer</label>
            <div className="flex gap-2">
              <input
                value={reviewerInput}
                onChange={e => setReviewerInput(e.target.value)}
                placeholder="reviewer-agent-id"
                className={inputCls + ' flex-1'}
              />
              <button
                onClick={handleSaveReviewer}
                className="text-xs px-3 py-1.5 rounded-lg border border-zinc-700 bg-white/[0.04] text-zinc-400 hover:text-zinc-200 hover:bg-white/[0.07] transition-colors flex-shrink-0"
              >
                Save
              </button>
            </div>
          </div>

          {epic.needsMockup && (
            <div>
              <label className="text-[10px] font-semibold tracking-widest uppercase text-zinc-600 block mb-1.5">Mockup Path</label>
              <input
                value={epicMockupPath}
                onChange={e => setEpicMockupPath(e.target.value)}
                onBlur={() => handleTextField('mockupPath', epicMockupPath)}
                onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); handleTextField('mockupPath', epicMockupPath); } }}
                placeholder="/path/to/mockup"
                className={inputCls}
              />
            </div>
          )}

          <div>
            <label className="text-[10px] font-semibold tracking-widest uppercase text-zinc-600 block mb-1.5">Force State</label>
            <select
              defaultValue=""
              onChange={e => { if (e.target.value) { onForceState(e.target.value); e.currentTarget.value = ''; } }}
              className="w-full text-xs rounded-lg border border-zinc-700 bg-zinc-800 text-zinc-400 px-2.5 py-1.5 focus:outline-none cursor-pointer"
            >
              <option value="" disabled>Select state…</option>
              {ALL_EPIC_STATES.map(s => (
                <option key={s} value={s}>{s}</option>
              ))}
            </select>
          </div>

        </div>

        <div className="px-5 py-4 border-t border-zinc-800 flex justify-end">
          <button
            onClick={onClose}
            className="text-xs px-4 py-2 rounded-lg bg-indigo-600 text-white hover:bg-indigo-500 transition-colors"
          >
            Done
          </button>
        </div>

      </div>
    </div>
  );
}

function EpicSummaryCard({
  epic,
  onUpdated,
  onViewDoc,
  onForceState,
}: {
  epic: Epic;
  onUpdated: (e: Epic) => void;
  onViewDoc: (path: string) => void;
  onForceState: (state: string) => void;
}) {
  const [editOpen, setEditOpen] = useState(false);

  function flagCls(active: boolean): string {
    if (active) return 'inline-flex items-center gap-1 text-xs font-medium px-3 py-1 rounded-full bg-emerald-500/10 text-emerald-400 border border-emerald-500/30';
    return 'inline-flex items-center gap-1 text-xs font-medium px-3 py-1 rounded-full bg-white/[0.04] text-zinc-400 border border-zinc-700';
  }

  return (
    <>
      {editOpen && (
        <EpicEditDialog
          epic={epic}
          onUpdated={onUpdated}
          onForceState={onForceState}
          onClose={() => setEditOpen(false)}
        />
      )}

      <div className="bg-zinc-900 border border-zinc-800 rounded-xl p-4">
        <div className="flex items-center justify-between mb-3">
          <p className="text-[10px] font-bold tracking-widest uppercase text-zinc-600">Epic</p>
          <button
            onClick={() => setEditOpen(true)}
            className="text-[11px] px-2.5 py-1 rounded-lg bg-white/[0.04] border border-zinc-700 text-zinc-400 hover:text-zinc-200 hover:bg-white/[0.07] transition-colors"
          >
            Edit
          </button>
        </div>

        <p className="text-sm font-bold text-zinc-100 mb-3 leading-snug">{epic.name}</p>

        <div className="space-y-3">
          <div>
            <p className="text-[10px] font-semibold tracking-widest uppercase text-zinc-600 mb-1.5">Epic Agent</p>
            <AgentPill name={epic.epicAgentName} />
          </div>

          <div>
            <p className="text-[10px] font-semibold tracking-widest uppercase text-zinc-600 mb-1.5">Coding Agents</p>
            <div className="flex flex-wrap gap-1.5">
              {epic.codingAgentNames.map(a => <AgentPill key={a} name={a} />)}
            </div>
          </div>

          {epic.reviewerAgentName && (
            <div>
              <p className="text-[10px] font-semibold tracking-widest uppercase text-zinc-600 mb-1.5">Reviewer</p>
              <AgentPill name={epic.reviewerAgentName} />
            </div>
          )}

          <div>
            <p className="text-[10px] font-semibold tracking-widest uppercase text-zinc-600 mb-1.5">Flags</p>
            <div className="flex flex-wrap gap-1.5">
              <span className={flagCls(epic.needsMockup)}>{epic.needsMockup ? '✓' : '○'} Needs Mockup</span>
              <span className={flagCls(epic.isDocDrafted)}>{epic.isDocDrafted ? '✓' : '○'} Doc Drafted</span>
              {epic.needsMockup && (
                <span className={flagCls(epic.isMockupDone)}>{epic.isMockupDone ? '✓' : '○'} Mockup Done</span>
              )}
            </div>
          </div>

          <div className="flex flex-wrap gap-2 pt-1">
            {epic.epicDocumentPath && (
              <button
                onClick={() => onViewDoc(epic.epicDocumentPath)}
                className="text-[10px] px-2.5 py-1 rounded-md bg-white/[0.04] border border-zinc-700 text-zinc-500 hover:text-zinc-300 transition-colors"
              >
                📄 Epic Doc
              </button>
            )}
            {epic.epicGovernancePath && (
              <button
                onClick={() => onViewDoc(epic.epicGovernancePath)}
                className="text-[10px] px-2.5 py-1 rounded-md bg-white/[0.04] border border-zinc-700 text-zinc-500 hover:text-zinc-300 transition-colors"
              >
                📋 Governance
              </button>
            )}
          </div>
        </div>
      </div>
    </>
  );
}

function SpecCard({
  spec,
  epicId,
  onUpdated,
  onViewDoc,
  onApproveHumanInLoop,
  onForceState,
}: {
  spec: Spec;
  epicId: string;
  onUpdated: () => void;
  onViewDoc: (path: string) => void;
  onApproveHumanInLoop: (specId: string, isApproved: boolean, feedback: string | null) => void;
  onForceState: (specId: string, stateName: string) => void;
}) {
  const [codingNow, setCodingNow] = useState(false);
  const [showForceState, setShowForceState] = useState(false);
  const [pendingHIL, setPendingHIL] = useState<boolean | null>(null);
  const [hilFeedback, setHilFeedback] = useState('');

  const progress = SPEC_STATE_PROGRESS[spec.currentStateName] ?? 0;
  const isDone = spec.currentStateName === 'done';
  const isReady = spec.currentStateName === 'ready';
  const needsHumanReview = spec.humanInLoop !== null && spec.humanInLoop.isApproved === null;

  let cardCls = 'bg-zinc-900 border border-zinc-800 rounded-xl p-4 hover:border-zinc-700 transition-colors';

  if (isDone) {
    cardCls = 'bg-zinc-900 border border-zinc-800 rounded-xl p-4 opacity-[0.65]';
  }

  if (spec.isAbandoned) {
    cardCls = 'bg-zinc-900 border border-zinc-800 rounded-xl p-4 opacity-40';
  }

  let progressLabel = `${progress}%`;

  if (isDone) {
    progressLabel = 'Complete';
  }

  let codeNowLabel = '▶ Code Now';

  if (codingNow) {
    codeNowLabel = '…';
  }

  async function handleCodeNow() {
    setCodingNow(true);

    try {
      await SpecApi.codeNow(spec.id);
      onUpdated();
    } catch (e) {
      alert(String(e));
    } finally {
      setCodingNow(false);
    }
  }

  function handleForceStateSelect(e: React.ChangeEvent<HTMLSelectElement>) {
    const stateName = e.target.value;

    if (!stateName) return;
    if (!window.confirm(`Force spec to state "${stateName}"?`)) return;

    onForceState(spec.id, stateName);
    setShowForceState(false);
  }

  return (
    <div className={cardCls}>
      <div className="flex items-start justify-between gap-3 mb-2.5">
        <p className="text-[13px] font-semibold text-zinc-100 leading-snug">
          {specDisplayName(spec.id, epicId)}
        </p>
        <StateBadge state={spec.currentStateName} />
      </div>

      <div className="flex items-center gap-2 mb-3 flex-wrap">
        <span className="font-mono text-[11px] text-indigo-300">{spec.assignedAgentName}</span>
        <a href={`openterm:${spec.assignedAgentName}`} className="text-base leading-none hover:opacity-70 transition-opacity" title={`Chat with ${spec.assignedAgentName}`}>💬</a>

        {spec.reviewerAgentName && spec.currentStateName === 'code_review' && (
          <>
            <span className="text-[10px] text-zinc-600">Reviewer:</span>
            <span className="font-mono text-[11px] text-orange-400">{spec.reviewerAgentName}</span>
          </>
        )}
      </div>

      <div className="flex items-center gap-2.5 mb-3">
        <div className="flex-1 h-[3px] rounded-full bg-zinc-800 overflow-hidden">
          <div
            className="h-full rounded-full bg-gradient-to-r from-indigo-500 to-cyan-400 transition-all"
            style={{ width: `${progress}%` }}
          />
        </div>
        <span className="text-[10px] text-zinc-600 whitespace-nowrap flex-shrink-0">{progressLabel}</span>
      </div>

      {needsHumanReview && spec.humanInLoop && (
        <div className="mb-3 text-xs text-amber-400 bg-amber-500/10 border border-amber-500/25 rounded-lg px-3 py-2">
          <p className="mb-2">{spec.humanInLoop.questions}</p>
          {pendingHIL === null ? (
            <div className="flex gap-2">
              <button
                onClick={() => setPendingHIL(true)}
                className="text-xs px-2 py-1 rounded bg-emerald-500/15 border border-emerald-500/30 text-emerald-400 hover:bg-emerald-500/25"
              >
                Approve
              </button>
              <button
                onClick={() => setPendingHIL(false)}
                className="text-xs px-2 py-1 rounded bg-red-500/15 border border-red-500/30 text-red-400 hover:bg-red-500/25"
              >
                Reject
              </button>
            </div>
          ) : (
            <div className="space-y-2">
              <p className="text-xs text-zinc-400">
                Confirm <span className={pendingHIL ? 'text-emerald-400 font-semibold' : 'text-red-400 font-semibold'}>{pendingHIL ? 'approval' : 'rejection'}</span>:
              </p>
              <textarea
                value={hilFeedback}
                onChange={e => setHilFeedback(e.target.value)}
                placeholder="Optional feedback…"
                rows={2}
                className="w-full text-xs rounded border border-zinc-700 bg-white/5 text-zinc-200 px-2 py-1.5 resize-none focus:outline-none focus:ring-1 focus:ring-amber-400 placeholder:text-zinc-600"
              />
              <div className="flex gap-2">
                <button
                  onClick={() => { onApproveHumanInLoop(spec.id, pendingHIL, hilFeedback.trim() || null); setPendingHIL(null); setHilFeedback(''); }}
                  className={`text-xs px-2 py-1 rounded font-semibold ${pendingHIL ? 'bg-emerald-500/15 border border-emerald-500/30 text-emerald-400 hover:bg-emerald-500/25' : 'bg-red-500/15 border border-red-500/30 text-red-400 hover:bg-red-500/25'}`}
                >
                  Confirm
                </button>
                <button
                  onClick={() => { setPendingHIL(null); setHilFeedback(''); }}
                  className="text-xs px-2 py-1 rounded border border-zinc-700 text-zinc-500 hover:text-zinc-300"
                >
                  Cancel
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      <div className="flex gap-2 flex-wrap">
        {isReady && (
          <button
            onClick={handleCodeNow}
            disabled={codingNow}
            className="text-[10px] font-semibold px-2.5 py-1 rounded-md bg-emerald-500/10 border border-emerald-500/25 text-emerald-400 hover:bg-emerald-500/20 disabled:opacity-50 transition-colors"
          >
            {codeNowLabel}
          </button>
        )}

        {spec.specDocPath && (
          <button
            onClick={() => onViewDoc(spec.specDocPath!)}
            className="text-[10px] px-2.5 py-1 rounded-md bg-white/[0.04] border border-zinc-700 text-zinc-500 hover:text-zinc-300 transition-colors"
          >
            View spec doc
          </button>
        )}

        <button
          onClick={() => setShowForceState(v => !v)}
          className="text-[10px] px-2.5 py-1 rounded-md bg-white/[0.04] border border-zinc-700 text-zinc-500 hover:text-zinc-300 transition-colors"
        >
          Force state
        </button>
      </div>

      {showForceState && (
        <select
          defaultValue=""
          onChange={handleForceStateSelect}
          className="mt-2 text-xs rounded-lg border border-zinc-700 bg-zinc-800 text-zinc-300 px-2 py-1.5 focus:outline-none w-full"
        >
          <option value="" disabled>Select state →</option>
          {SPEC_STATES.map(s => (
            <option key={s} value={s}>{s}</option>
          ))}
        </select>
      )}
    </div>
  );
}


// ---- Main Page ----

export default function EpicDetailPage() {
  const { epicId } = useParams<{ epicId: string }>();
  const navigate = useNavigate();
  const [epic, setEpic] = useState<Epic | null>(null);
  const [auditLog, setAuditLog] = useState<AuditLog[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [drawerPath, setDrawerPath] = useState<string | null>(null);
  const [swarmObjective, setSwarmObjective] = useState('');
  const [swarmToState, setSwarmToState] = useState('');
  const [swarmSubmitting, setSwarmSubmitting] = useState(false);
  const [nudgeSent, setNudgeSent] = useState(false);
  const [deleteConfirm, setDeleteConfirm] = useState(false);

  const load = useCallback(async () => {
    if (!epicId) return;

    try {
      const data = await EpicApi.get(epicId);
      setEpic(data);
    } catch (e) {
      setError(String(e));
    } finally {
      setLoading(false);
    }
  }, [epicId]);

  const loadAudit = useCallback(async () => {
    if (!epicId) return;

    try {
      const entries = await EpicApi.getHistory(epicId);
      setAuditLog(entries);
    } catch {
      // non-fatal
    }
  }, [epicId]);

  useSignalR('/hubs/epic', {
    EpicUpdated: (...args: unknown[]) => {
      const e = args[0] as Epic;
      if (e?.id === epicId) { setEpic(e); loadAudit(); }
    },
    SpecUpdated: () => { load(); loadAudit(); },
  });

  useEffect(() => { load(); loadAudit(); }, [load, loadAudit]);

  async function handleWakeAgent() {
    if (!epicId) return;

    setNudgeSent(true);
    try {
      await EpicApi.wakeAgent(epicId);
    } catch (e) {
      setNudgeSent(false);
      alert(String(e));
    }
  }

  async function handleForceEpicState(stateName: string) {
    if (!epicId) return;
    if (!window.confirm(`Force epic to state "${stateName}"?`)) return;

    try {
      const updated = await EpicApi.forceState(epicId, stateName);
      setEpic(updated);
    } catch (e) {
      alert(String(e));
    }
  }

  async function handleDeleteEpic() {
    if (!epicId) return;

    try {
      await EpicApi.delete(epicId);
      navigate('/');
    } catch (e) {
      setDeleteConfirm(false);
      alert(String(e));
    }
  }

  async function handleApproveSpecHumanInLoop(specId: string, isApproved: boolean, feedback: string | null) {
    try {
      await SpecApi.approveHumanInLoop(specId, isApproved, feedback);
      load();
    } catch (e) {
      alert(String(e));
    }
  }

  async function handleForceSpecState(specId: string, stateName: string) {
    try {
      await SpecApi.forceState(specId, stateName);
      load();
    } catch (e) {
      alert(String(e));
    }
  }

  async function handleRaiseSwarm() {
    if (!epicId || !swarmObjective.trim() || !swarmToState.trim()) return;

    setSwarmSubmitting(true);

    try {
      await EpicApi.raiseAgentSwarm(epicId, swarmObjective.trim(), swarmToState.trim());
      setSwarmObjective('');
      setSwarmToState('');
      load();
    } catch (e) {
      alert(String(e));
    } finally {
      setSwarmSubmitting(false);
    }
  }

  if (loading) return <div className="p-6 text-sm text-zinc-500">Loading…</div>;
  if (error) return <div className="p-6 text-sm text-red-500">{error}</div>;
  if (!epic) return null;

  const showSwarm = epic.agentSwarm !== null && (
    epic.currentStateName === 'agent_swarm' || epic.currentStateName === 'waterproofing'
  );

  let nudgeLabel = 'Nudge Agent';

  if (epic.currentStateName === 'drafting') {
    nudgeLabel = `Let's work on ${epic.name}`;
  }

  let swarmBtnLabel = 'Raise Swarm';

  if (swarmSubmitting) {
    swarmBtnLabel = 'Raising…';
  }

  return (
    <div className="min-h-screen bg-zinc-950">

      {/* Sticky header */}
      <div className="sticky top-0 z-40 border-b border-zinc-800 bg-zinc-950/90 backdrop-blur-sm">
        <div className="max-w-[1280px] mx-auto px-6 py-3 flex items-center gap-3 flex-wrap">
          <button
            onClick={() => navigate('/')}
            className="text-xs text-zinc-600 hover:text-zinc-400 flex items-center gap-1"
          >
            ← Epics
          </button>

          <span className="text-zinc-700">/</span>

          <h1 className="text-[15px] font-bold text-zinc-100 flex-shrink-0">{epic.name}</h1>
          <StateBadge state={epic.currentStateName} />
          <span className="font-mono text-[10px] text-zinc-600">{epic.id}</span>

          <div className="ml-auto flex items-center gap-2">
            {!(nudgeSent && epic.currentStateName === 'drafting') && (
              <button
                onClick={handleWakeAgent}
                className="inline-flex items-center gap-1.5 text-xs font-semibold px-3 py-1.5 rounded-lg bg-indigo-600 text-white hover:bg-indigo-500 transition-colors"
              >
                {nudgeLabel}
              </button>
            )}

            {deleteConfirm ? (
              <div className="flex items-center gap-1.5">
                <span className="text-xs text-zinc-400">Delete epic?</span>
                <button
                  onClick={handleDeleteEpic}
                  className="text-xs px-2.5 py-1.5 rounded-lg bg-red-600 text-white hover:bg-red-500 transition-colors font-semibold"
                >
                  Confirm
                </button>
                <button
                  onClick={() => setDeleteConfirm(false)}
                  className="text-xs px-2.5 py-1.5 rounded-lg border border-zinc-700 text-zinc-400 hover:text-zinc-200 transition-colors"
                >
                  Cancel
                </button>
              </div>
            ) : (
              <button
                onClick={() => setDeleteConfirm(true)}
                className="text-xs px-3 py-1.5 rounded-lg bg-red-500/10 border border-red-500/25 text-red-400 hover:bg-red-500/20 transition-colors"
              >
                Delete
              </button>
            )}
          </div>
        </div>
      </div>

      {/* Main content */}
      <div className="max-w-[1280px] mx-auto px-6 py-6">

        {/* Pipeline */}
        <StatePipeline currentState={epic.currentStateName} lastKnownState={epic.lastKnownStateName} />

        {/* Two-column body */}
        <div className="grid gap-4 mb-4" style={{ gridTemplateColumns: '380px 1fr' }}>

          {/* Left column */}
          <div className="flex flex-col gap-3.5">

            <EscalationPanel
              key={epic.humanInLoop?.questions ?? 'none'}
              epic={epic}
              onUpdated={setEpic}
            />

            <EpicSummaryCard epic={epic} onUpdated={setEpic} onViewDoc={setDrawerPath} onForceState={handleForceEpicState} />

            <InstructionBlock instruction={epic.epicAgentInstruction} epicAgentName={epic.epicAgentName} />

            {showSwarm && (
              <SwarmPanelBlock epic={epic} />
            )}

            <details className="group">
              <summary className="text-[10px] font-bold tracking-widest uppercase text-zinc-700 hover:text-zinc-500 cursor-pointer select-none list-none flex items-center gap-1.5">
                <span className="group-open:rotate-90 transition-transform inline-block leading-none">›</span>
                Raise Agent Swarm
              </summary>

              <div className="mt-3 space-y-2.5 pl-3 border-l border-zinc-800">
                <div>
                  <label className="text-[10px] font-semibold tracking-widest uppercase text-zinc-600 block mb-1.5">Objective</label>
                  <textarea
                    value={swarmObjective}
                    onChange={e => setSwarmObjective(e.target.value)}
                    rows={3}
                    placeholder="Describe the swarm objective…"
                    className="w-full text-xs rounded-lg border border-zinc-700 bg-white/[0.04] px-2.5 py-1.5 text-zinc-100 focus:outline-none focus:ring-1 focus:ring-indigo-500 resize-y"
                  />
                </div>

                <div>
                  <label className="text-[10px] font-semibold tracking-widest uppercase text-zinc-600 block mb-1.5">To State (after consensus)</label>
                  <input
                    value={swarmToState}
                    onChange={e => setSwarmToState(e.target.value)}
                    placeholder="e.g. implementation"
                    className="w-full text-xs rounded-lg border border-zinc-700 bg-white/[0.04] px-2.5 py-1.5 text-zinc-100 font-mono focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  />
                </div>

                <button
                  onClick={handleRaiseSwarm}
                  disabled={swarmSubmitting || !swarmObjective.trim() || !swarmToState.trim()}
                  className="text-xs px-3 py-1.5 rounded-lg bg-violet-600 text-white hover:bg-violet-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                >
                  {swarmBtnLabel}
                </button>
              </div>
            </details>

          </div>

          {/* Right column — Epic Board */}
          <div className="bg-zinc-900 border border-zinc-800 rounded-xl p-4">
            <div className="flex items-center justify-between mb-4">
              <p className="text-[10px] font-bold tracking-widest uppercase text-zinc-600">Epic Board — Specs</p>
              <button
                disabled
                className="text-[11px] px-2.5 py-1 rounded-lg bg-white/[0.04] border border-zinc-700 text-zinc-600 cursor-not-allowed"
              >
                + New Spec
              </button>
            </div>

            {epic.specs.length === 0 && (
              <p className="text-sm text-zinc-600 py-6 text-center">No specs yet.</p>
            )}

            <div className="space-y-2.5">
              {epic.specs.map(s => (
                <SpecCard
                  key={s.id}
                  spec={s}
                  epicId={epic.id}
                  onUpdated={load}
                  onViewDoc={setDrawerPath}
                  onApproveHumanInLoop={handleApproveSpecHumanInLoop}
                  onForceState={handleForceSpecState}
                />
              ))}
            </div>
          </div>

        </div>

        {/* Audit log */}
        <div className="bg-zinc-900 border border-zinc-800 rounded-xl px-5 py-4">
          <p className="text-[10px] font-bold tracking-widest uppercase text-zinc-600 mb-3">Activity Log</p>
          <AuditLogPanel entries={auditLog} />
        </div>

      </div>

      <MarkdownDrawer path={drawerPath} onClose={() => setDrawerPath(null)} />

    </div>
  );
}
