import { useCallback, useEffect, useRef, useState } from 'react';
import { marked } from 'marked';
import { useNavigate, useParams } from 'react-router-dom';
import { EpicApi, SpecApi, DocApi, AgentApi } from '../epicApi';
import type { AgentStatus } from '../epicApi';
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

const TRANSIENT_SPEC_STATES = new Set(['spec_human_in_loop', 'agent_swarm']);

function specProgress(spec: { currentStateName: string; lastKnownStateName?: string | null }): number {
  const stateForProgress = TRANSIENT_SPEC_STATES.has(spec.currentStateName)
    ? (spec.lastKnownStateName ?? spec.currentStateName)
    : spec.currentStateName;
  return SPEC_STATE_PROGRESS[stateForProgress] ?? 0;
}

function specDisplayName(id: string, epicId: string): string {
  const prefix = epicId + '-';
  const slug = id.startsWith(prefix) ? id.slice(prefix.length) : id;
  return slug.replace(/-/g, ' ').replace(/\b\w/g, c => c.toUpperCase());
}

function useAgentStatuses() {
  const [statuses, setStatuses] = useState<Record<string, AgentStatus>>({});

  useEffect(() => {
    function fetch() {
      AgentApi.list()
        .then(list => {
          const map: Record<string, AgentStatus> = {};
          for (const a of list) map[a.sessionName] = a;
          setStatuses(map);
        })
        .catch(() => {});
    }
    fetch();
    const id = setInterval(fetch, 20000);
    return () => clearInterval(id);
  }, []);

  return statuses;
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

function GovernanceEditor({ path, onClose }: { path: string; onClose: () => void }) {
  const [content, setContent] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [dirty, setDirty] = useState(false);
  const overlayRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setLoading(true);
    DocApi.get(path)
      .then(text => { setContent(text); setDirty(false); })
      .catch(e => alert(String(e)))
      .finally(() => setLoading(false));
  }, [path]);

  async function handleSave() {
    setSaving(true);
    try {
      await DocApi.save(path, content);
      setDirty(false);
    } catch (e) {
      alert(String(e));
    } finally {
      setSaving(false);
    }
  }

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
          <div className="flex items-center gap-2 ml-4 shrink-0">
            {dirty && (
              <button
                onClick={handleSave}
                disabled={saving}
                className="text-xs px-3 py-1 rounded-md bg-indigo-600 text-white hover:bg-indigo-500 disabled:opacity-50 transition-colors font-semibold"
              >
                {saving ? 'Saving…' : 'Save'}
              </button>
            )}
            <button onClick={onClose} className="text-zinc-400 hover:text-zinc-200 text-lg leading-none">×</button>
          </div>
        </div>
        <div className="flex-1 overflow-hidden">
          {loading ? (
            <p className="text-sm text-zinc-500 p-5">Loading…</p>
          ) : (
            <textarea
              value={content}
              onChange={e => { setContent(e.target.value); setDirty(true); }}
              className="w-full h-full bg-transparent text-xs font-mono text-zinc-200 p-5 resize-none focus:outline-none leading-relaxed"
              spellCheck={false}
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

function AgentPill({ name, statuses }: { name: string; statuses?: Record<string, AgentStatus> }) {
  const agent = statuses?.[name];
  const status = agent?.status ?? 'offline';
  const dotCls =
    status === 'running'                                ? 'bg-emerald-400 shadow-[0_0_5px_1px_rgba(52,211,153,0.5)]' :
    status === 'idle' || status === 'online'            ? 'bg-zinc-400' :
    status === 'waiting_permission'                     ? 'bg-blue-400' :
    status === 'interrupted' || status === 'stale_permission' ? 'bg-red-400' :
                                                          'bg-zinc-600';
  return (
    <span className="inline-flex items-center gap-1.5 bg-indigo-500/10 border border-indigo-500/20 rounded-full pl-2 pr-1 py-1 text-xs font-medium text-indigo-300">
      <span className={`w-1.5 h-1.5 rounded-full flex-shrink-0 ${dotCls}`} title={status} />
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
  const [allAgents, setAllAgents] = useState<string[]>([]);
  const overlayRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    AgentApi.list()
      .then(list => setAllAgents(list.map(a => a.sessionName).sort()))
      .catch(() => {});
  }, []);

  const isClosed = epic.currentStateName === 'closed';
  const inputCls = 'w-full text-xs rounded-lg border border-zinc-700 bg-zinc-800 px-2.5 py-1.5 text-zinc-100 font-mono focus:outline-none focus:ring-1 focus:ring-indigo-500 focus:border-indigo-500 disabled:opacity-40 disabled:cursor-not-allowed';

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
    const name = agentInput.trim();
    if (!name || epic.codingAgentNames.includes(name)) return;
    const next = [...epic.codingAgentNames, name];
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

        {isClosed && (
          <div className="mx-5 mt-4 px-3 py-2 rounded-lg bg-zinc-800 border border-zinc-700 text-xs text-zinc-400">
            This epic is closed — editing is disabled.
          </div>
        )}

        <div className="overflow-y-auto flex-1 px-5 py-4 space-y-4">

          <div>
            <label className="text-[10px] font-semibold tracking-widest uppercase text-zinc-600 block mb-1.5">Name</label>
            <input
              disabled={isClosed}
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
              disabled={isClosed}
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
              disabled={isClosed}
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
              <button disabled={isClosed} onClick={() => handleToggle('needsMockup')} className={flagCls(epic.needsMockup) + (isClosed ? ' opacity-40 cursor-not-allowed' : '')}>
                {epic.needsMockup ? '✓' : '○'} Needs Mockup
              </button>
              <button disabled={isClosed} onClick={() => handleToggle('isDocDrafted')} className={flagCls(epic.isDocDrafted) + (isClosed ? ' opacity-40 cursor-not-allowed' : '')}>
                {epic.isDocDrafted ? '✓' : '○'} Doc Drafted
              </button>
              {epic.needsMockup && (
                <button disabled={isClosed} onClick={() => handleToggle('isMockupDone')} className={flagCls(epic.isMockupDone) + (isClosed ? ' opacity-40 cursor-not-allowed' : '')}>
                  {epic.isMockupDone ? '✓' : '○'} Mockup Done
                </button>
              )}
              <button disabled={isClosed} onClick={() => handleToggle('isACRequired')} className={flagCls(epic.isACRequired) + (isClosed ? ' opacity-40 cursor-not-allowed' : '')}>
                {epic.isACRequired ? '✓' : '○'} AC Required
              </button>
              <button disabled={isClosed} onClick={() => handleToggle('isCodeReviewRequired')} className={flagCls(epic.isCodeReviewRequired) + (isClosed ? ' opacity-40 cursor-not-allowed' : '')}>
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
                  {!isClosed && <button onClick={() => handleRemoveAgent(a)} className="text-zinc-500 hover:text-red-400 leading-none">×</button>}
                </span>
              ))}
            </div>
            {!isClosed && (
              <div className="flex gap-2">
                <select
                  value={agentInput}
                  onChange={e => setAgentInput(e.target.value)}
                  className={inputCls + ' flex-1'}
                >
                  <option value="">Add coding agent…</option>
                  {allAgents.filter(n => !epic.codingAgentNames.includes(n)).map(n => (
                    <option key={n} value={n}>{n}</option>
                  ))}
                </select>
                <button
                  onClick={handleAddAgent}
                  className="text-xs px-3 py-1.5 rounded-lg border border-zinc-700 bg-white/[0.04] text-zinc-400 hover:text-zinc-200 hover:bg-white/[0.07] transition-colors flex-shrink-0"
                >
                  Add
                </button>
              </div>
            )}
          </div>

          <div>
            <label className="text-[10px] font-semibold tracking-widest uppercase text-zinc-600 block mb-1.5">Reviewer</label>
            <div className="flex gap-2">
              <select
                disabled={isClosed}
                value={reviewerInput}
                onChange={e => setReviewerInput(e.target.value)}
                className={inputCls + ' flex-1'}
              >
                <option value="">None</option>
                {allAgents.map(n => <option key={n} value={n}>{n}</option>)}
              </select>
              {!isClosed && (
                <button
                  onClick={handleSaveReviewer}
                  className="text-xs px-3 py-1.5 rounded-lg border border-zinc-700 bg-white/[0.04] text-zinc-400 hover:text-zinc-200 hover:bg-white/[0.07] transition-colors flex-shrink-0"
                >
                  Save
                </button>
              )}
            </div>
          </div>

          {epic.needsMockup && (
            <div>
              <label className="text-[10px] font-semibold tracking-widest uppercase text-zinc-600 block mb-1.5">Mockup Path</label>
              <input
                disabled={isClosed}
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
  onEditGovernance,
  onForceState,
  agentStatuses,
}: {
  epic: Epic;
  onUpdated: (e: Epic) => void;
  onViewDoc: (path: string) => void;
  onEditGovernance: () => void;
  onForceState: (state: string) => void;
  agentStatuses: Record<string, AgentStatus>;
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
        <div className="flex items-start justify-between mb-4">
          <p className="text-base font-bold text-zinc-100 leading-snug pr-2">{epic.name}</p>
          <button
            onClick={() => setEditOpen(true)}
            className="text-[11px] px-2.5 py-1 rounded-lg bg-white/[0.04] border border-zinc-700 text-zinc-400 hover:text-zinc-200 hover:bg-white/[0.07] transition-colors shrink-0"
          >
            Edit
          </button>
        </div>

        <div className="space-y-3">
          <div>
            <p className="text-[10px] font-semibold tracking-widest uppercase text-zinc-600 mb-1.5">Epic Agent</p>
            <AgentPill name={epic.epicAgentName} statuses={agentStatuses} />
          </div>

          <div>
            <p className="text-[10px] font-semibold tracking-widest uppercase text-zinc-600 mb-1.5">Coding Agents</p>
            <div className="flex flex-wrap gap-1.5">
              {epic.codingAgentNames.map(a => <AgentPill key={a} name={a} statuses={agentStatuses} />)}
            </div>
          </div>

          {epic.reviewerAgentName && (
            <div>
              <p className="text-[10px] font-semibold tracking-widest uppercase text-zinc-600 mb-1.5">Reviewer</p>
              <AgentPill name={epic.reviewerAgentName} statuses={agentStatuses} />
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
            <button
              onClick={() => EpicApi.openDirectory(epic.id)}
              className="text-[10px] px-2.5 py-1 rounded-md bg-white/[0.04] border border-zinc-700 text-zinc-500 hover:text-zinc-300 transition-colors"
            >
              📁 Epic Directory
            </button>
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
                onClick={onEditGovernance}
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

function HilDialog({
  spec,
  onClose,
  onApproveHumanInLoop,
}: {
  spec: Spec;
  onClose: () => void;
  onApproveHumanInLoop: (specId: string, isApproved: boolean, feedback: string | null) => void;
}) {
  const [feedback, setFeedback] = useState('');
  const [pending, setPending] = useState<boolean | null>(null);
  const overlayRef = useRef<HTMLDivElement>(null);

  function handleOverlayClick(e: React.MouseEvent<HTMLDivElement>) {
    if (e.target === overlayRef.current) onClose();
  }

  function handleConfirm() {
    if (pending === null) return;
    onApproveHumanInLoop(spec.id, pending, feedback.trim() || null);
    onClose();
  }

  return (
    <div ref={overlayRef} onClick={handleOverlayClick} className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4">
      <div className="w-full max-w-md bg-zinc-900 border border-zinc-700 rounded-2xl shadow-2xl">
        <div className="flex items-center justify-between px-5 py-4 border-b border-zinc-800">
          <p className="text-sm font-bold text-zinc-100">Human Review</p>
          <button onClick={onClose} className="text-zinc-500 hover:text-zinc-200 text-xl leading-none">×</button>
        </div>
        <div className="px-5 py-4 space-y-3">
          {spec.humanInLoop?.questions && (
            <p className="text-xs text-zinc-400 leading-relaxed">{spec.humanInLoop.questions}</p>
          )}
          <textarea
            value={feedback}
            onChange={e => setFeedback(e.target.value)}
            placeholder="Reason (optional)…"
            rows={3}
            className="w-full text-xs rounded-lg border border-zinc-700 bg-white/5 text-zinc-200 px-3 py-2 resize-none focus:outline-none focus:ring-1 focus:ring-zinc-600 placeholder:text-zinc-600"
          />
          {pending === null ? (
            <div className="flex gap-2">
              <button
                onClick={() => setPending(true)}
                className="flex-1 text-xs font-semibold py-2 rounded-lg bg-emerald-500/10 border border-emerald-500/25 text-emerald-400 hover:bg-emerald-500/20 transition-colors"
              >
                ✓ Approve
              </button>
              <button
                onClick={() => setPending(false)}
                className="flex-1 text-xs font-semibold py-2 rounded-lg bg-red-500/10 border border-red-500/25 text-red-400 hover:bg-red-500/20 transition-colors"
              >
                ✗ Reject
              </button>
            </div>
          ) : (
            <div className="flex items-center gap-2">
              <span className="text-xs text-zinc-400 flex-1">{pending ? 'Approve' : 'Reject'} — are you sure?</span>
              <button
                onClick={handleConfirm}
                className={`text-xs px-3 py-1.5 rounded-lg font-semibold transition-colors ${pending ? 'bg-emerald-500/10 border border-emerald-500/25 text-emerald-400 hover:bg-emerald-500/20' : 'bg-red-500/10 border border-red-500/25 text-red-400 hover:bg-red-500/20'}`}
              >
                Yes
              </button>
              <button
                onClick={() => setPending(null)}
                className="text-xs px-3 py-1.5 rounded-lg border border-zinc-700 bg-white/[0.04] text-zinc-400 hover:text-zinc-200 transition-colors"
              >
                No
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

function CodeNowDialog({
  spec,
  epicId,
  onClose,
  onUpdated,
}: {
  spec: Spec;
  epicId: string;
  onClose: () => void;
  onUpdated: () => void;
}) {
  const [busy, setBusy] = useState(false);
  const overlayRef = useRef<HTMLDivElement>(null);

  function handleOverlayClick(e: React.MouseEvent<HTMLDivElement>) {
    if (e.target === overlayRef.current) onClose();
  }

  async function handleConfirm() {
    setBusy(true);
    try {
      await SpecApi.codeNow(spec.id);
      onUpdated();
      onClose();
    } catch (e) {
      alert(String(e));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div ref={overlayRef} onClick={handleOverlayClick} className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4">
      <div className="w-full max-w-sm bg-zinc-900 border border-zinc-700 rounded-2xl shadow-2xl">
        <div className="flex items-center justify-between px-5 py-4 border-b border-zinc-800">
          <p className="text-sm font-bold text-zinc-100">Start Coding</p>
          <button onClick={onClose} className="text-zinc-500 hover:text-zinc-200 text-xl leading-none">×</button>
        </div>
        <div className="px-5 py-4 space-y-4">
          <p className="text-xs text-zinc-400">Start coding <span className="font-semibold text-zinc-200">{specDisplayName(spec.id, epicId)}</span>?</p>
          <div className="flex gap-2">
            <button
              onClick={handleConfirm}
              disabled={busy}
              className="flex-1 text-xs font-semibold py-2 rounded-lg bg-emerald-500/10 border border-emerald-500/25 text-emerald-400 hover:bg-emerald-500/20 disabled:opacity-50 transition-colors"
            >
              {busy ? '…' : '▶ Yes, start coding'}
            </button>
            <button
              onClick={onClose}
              className="text-xs px-4 py-2 rounded-lg border border-zinc-700 text-zinc-400 hover:text-zinc-200 transition-colors"
            >
              Cancel
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

function AbandonButton({ spec, onUpdated }: { spec: Spec; onUpdated: () => void }) {
  const [busy, setBusy] = useState(false);

  async function handleClick() {
    const msg = spec.isAbandoned
      ? `Restore spec "${spec.id}"?`
      : `Abandon spec "${spec.id}"? It will be hidden from the board.`;
    if (!window.confirm(msg)) return;
    setBusy(true);
    try {
      await SpecApi.abandon(spec.id, !spec.isAbandoned);
      onUpdated();
    } catch (e) {
      alert(String(e));
    } finally {
      setBusy(false);
    }
  }

  if (spec.isAbandoned) {
    return (
      <button
        onClick={handleClick}
        disabled={busy}
        className="text-sm px-3 py-1.5 rounded-lg border border-zinc-600 text-zinc-400 hover:text-zinc-200 hover:border-zinc-500 disabled:opacity-40 transition-colors"
      >
        {busy ? '…' : 'Restore'}
      </button>
    );
  }

  return (
    <button
      onClick={handleClick}
      disabled={busy}
      className="text-sm px-3 py-1.5 rounded-lg border border-red-800 text-red-400 hover:bg-red-900/30 disabled:opacity-40 transition-colors"
    >
      {busy ? '…' : 'Abandon'}
    </button>
  );
}

function SpecDetailDialog({
  spec,
  epicId,
  allSpecs,
  onClose,
  onViewDoc,
  onForceState,
  onUpdated,
}: {
  spec: Spec;
  epicId: string;
  allSpecs: Spec[];
  onClose: () => void;
  onViewDoc: (path: string) => void;
  onForceState: (specId: string, stateName: string) => void;
  onUpdated: () => void;
}) {
  const overlayRef = useRef<HTMLDivElement>(null);
  const progress = specProgress(spec);
  const isDone = spec.currentStateName === 'done';
  const lbl = 'text-xs font-semibold tracking-widest uppercase text-zinc-500';
  const [allAgents, setAllAgents] = useState<string[]>([]);

  useEffect(() => {
    AgentApi.list()
      .then(list => setAllAgents(list.map(a => a.sessionName).sort()))
      .catch(() => {});
  }, []);

  const FLAG_CONFIGS: { field: keyof Spec; label: string }[] = [
    { field: 'isSpecDrafted', label: 'Spec Drafted' },
    { field: 'isSpecApproved', label: 'Spec Approved' },
    { field: 'isCodeDone', label: 'Code Done' },
    { field: 'isAcPassed', label: 'AC Passed' },
    { field: 'isACRequired', label: 'AC Required' },
    { field: 'isCodeReviewRequired', label: 'Code Review Required' },
    { field: 'isCodeReviewApproved', label: 'Code Review Approved' },
    { field: 'isAbandoned', label: 'Abandoned' },
  ];

  async function handleToggleFlag(field: keyof Spec) {
    const current = spec[field] as boolean | null;
    try {
      await SpecApi.update(spec.id, { ...spec, [field]: !current });
      onUpdated();
    } catch (e) {
      alert(String(e));
    }
  }

  const siblings = allSpecs.filter(s => s.id !== spec.id && !s.isAbandoned);
  const currentDeps = spec.dependsOn ?? [];
  const availableToAdd = siblings.filter(s => !currentDeps.includes(s.id));
  const [selectedDep, setSelectedDep] = useState('');

  async function saveDeps(next: string[]) {
    try {
      await SpecApi.update(spec.id, { ...spec, dependsOn: next });
      onUpdated();
    } catch (e) {
      alert(String(e));
    }
  }

  async function handleAddDep() {
    if (!selectedDep) return;
    setSelectedDep('');
    await saveDeps([...currentDeps, selectedDep]);
  }

  async function handleRemoveDep(depId: string) {
    await saveDeps(currentDeps.filter(id => id !== depId));
  }

  function handleOverlayClick(e: React.MouseEvent<HTMLDivElement>) {
    if (e.target === overlayRef.current) onClose();
  }

  function handleForceStateSelect(e: React.ChangeEvent<HTMLSelectElement>) {
    const stateName = e.target.value;
    if (!stateName) return;
    if (!window.confirm(`Force spec to state "${stateName}"?`)) return;
    onForceState(spec.id, stateName);
  }

  return (
    <div ref={overlayRef} onClick={handleOverlayClick} className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4">
      <div className="w-full max-w-lg bg-zinc-900 border border-zinc-700 rounded-2xl shadow-2xl flex flex-col max-h-[85vh]">

        <div className="flex items-start justify-between px-5 py-4 border-b border-zinc-800">
          <div>
            <p className="text-sm font-bold text-zinc-100 leading-snug">{specDisplayName(spec.id, epicId)}</p>
            <p className="font-mono text-xs text-zinc-500 mt-0.5">{spec.id}</p>
          </div>
          <div className="flex items-center gap-2 ml-4 shrink-0">
            <StateBadge state={spec.currentStateName} />
            <button onClick={onClose} className="text-zinc-500 hover:text-zinc-200 text-xl leading-none ml-1">×</button>
          </div>
        </div>

        <div className="overflow-y-auto flex-1 px-5 py-4 space-y-4">

          <div>
            <p className={lbl + ' mb-1.5'}>Progress</p>
            <div className="flex items-center gap-2">
              <div className="flex-1 h-[3px] rounded-full bg-zinc-800 overflow-hidden">
                <div className="h-full rounded-full bg-gradient-to-r from-indigo-500 to-cyan-400 transition-all" style={{ width: `${progress}%` }} />
              </div>
              <span className="text-xs text-zinc-500 flex-shrink-0">{isDone ? '✓ Done' : `${progress}%`}</span>
            </div>
          </div>

          <div>
            <p className={lbl + ' mb-1.5'}>Flags</p>
            <div className="flex flex-wrap gap-1.5">
              {FLAG_CONFIGS.map(cfg => {
                const val = spec[cfg.field] as boolean | null;
                const active = val === true;
                const cls = active
                  ? 'px-2.5 py-1 rounded-full text-xs font-medium cursor-pointer transition-colors bg-emerald-500/20 text-emerald-300 border border-emerald-500/40 hover:bg-emerald-500/30'
                  : 'px-2.5 py-1 rounded-full text-xs font-medium cursor-pointer transition-colors bg-zinc-800 text-zinc-500 border border-zinc-700 hover:bg-zinc-700 hover:text-zinc-300';
                return (
                  <button key={cfg.field} onClick={() => handleToggleFlag(cfg.field)} className={cls}>
                    {active ? '✓ ' : ''}{cfg.label}
                  </button>
                );
              })}
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <p className={lbl + ' mb-1'}>Assigned Agent</p>
              <div className="flex items-center gap-1.5">
                <select
                  value={spec.assignedAgentName ?? ''}
                  onChange={async e => {
                    if (!e.target.value) return;
                    try {
                      await SpecApi.update(spec.id, { ...spec, assignedAgentName: e.target.value });
                      onUpdated();
                    } catch (err) { alert(String(err)); }
                  }}
                  className="flex-1 text-xs rounded border border-zinc-700 bg-zinc-800 text-zinc-300 font-mono px-2 py-1 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                >
                  <option value="human">👤 human</option>
                  {allAgents.map(a => <option key={a} value={a}>{a}</option>)}
                  {spec.assignedAgentName && spec.assignedAgentName !== 'human' && !allAgents.includes(spec.assignedAgentName) && (
                    <option value={spec.assignedAgentName}>{spec.assignedAgentName}</option>
                  )}
                </select>
                {spec.assignedAgentName && spec.assignedAgentName !== 'human' && (
                  <a href={`openterm:${spec.assignedAgentName}`} className="text-sm leading-none hover:opacity-70">💬</a>
                )}
              </div>
            </div>
            <div>
              <p className={lbl + ' mb-1'}>Reviewer</p>
              <select
                value={spec.reviewerAgentName ?? ''}
                onChange={async e => {
                  try {
                    await SpecApi.update(spec.id, { ...spec, reviewerAgentName: e.target.value || null });
                    onUpdated();
                  } catch (err) { alert(String(err)); }
                }}
                className="w-full text-xs rounded border border-zinc-700 bg-zinc-800 text-orange-300 font-mono px-2 py-1 focus:outline-none focus:ring-1 focus:ring-indigo-500"
              >
                <option value="">— none —</option>
                {allAgents.map(a => <option key={a} value={a}>{a}</option>)}
                {spec.reviewerAgentName && !allAgents.includes(spec.reviewerAgentName) && (
                  <option value={spec.reviewerAgentName}>{spec.reviewerAgentName}</option>
                )}
              </select>
            </div>
          </div>

          {spec.specDocPath && (
            <div>
              <p className={lbl + ' mb-1'}>Spec Doc</p>
              <button onClick={() => { onViewDoc(spec.specDocPath!); onClose(); }} className="text-sm text-indigo-400 hover:text-indigo-300 font-mono truncate max-w-full text-left">
                {spec.specDocPath}
              </button>
            </div>
          )}

          {siblings.length > 0 && (
            <div>
              <p className={lbl + ' mb-1.5'}>Dependencies</p>
              {currentDeps.length > 0 && (
                <div className="flex flex-wrap gap-1.5 mb-2">
                  {currentDeps.map(depId => (
                    <span key={depId} className="inline-flex items-center gap-1 text-xs font-mono px-2 py-1 rounded border bg-amber-500/15 border-amber-500/40 text-amber-300">
                      {specDisplayName(depId, epicId)}
                      <button onClick={() => handleRemoveDep(depId)} className="hover:text-red-400 transition-colors leading-none" title="Remove">×</button>
                    </span>
                  ))}
                </div>
              )}
              {availableToAdd.length > 0 && (
                <div className="flex gap-1.5">
                  <select
                    value={selectedDep}
                    onChange={e => setSelectedDep(e.target.value)}
                    className="flex-1 text-sm rounded border border-zinc-700 bg-zinc-800 text-zinc-400 px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  >
                    <option value="">Add dependency…</option>
                    {availableToAdd.map(s => (
                      <option key={s.id} value={s.id}>{specDisplayName(s.id, epicId)}</option>
                    ))}
                  </select>
                  <button
                    onClick={handleAddDep}
                    disabled={!selectedDep}
                    className="text-sm px-3 py-1.5 rounded border border-zinc-700 bg-zinc-800 text-zinc-400 hover:text-zinc-200 hover:border-zinc-600 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                  >
                    Add
                  </button>
                </div>
              )}
            </div>
          )}

          <div>
            <p className={lbl + ' mb-1.5'}>Force State</p>
            <select defaultValue="" onChange={handleForceStateSelect} className="w-full text-sm rounded-lg border border-zinc-700 bg-zinc-800 text-zinc-400 px-2.5 py-1.5 focus:outline-none cursor-pointer">
              <option value="" disabled>Select state…</option>
              {SPEC_STATES.map(s => <option key={s} value={s}>{s}</option>)}
            </select>
          </div>

        </div>

        <div className="px-5 py-4 border-t border-zinc-800 flex justify-between items-center">
          <AbandonButton spec={spec} onUpdated={onUpdated} />
          <button onClick={onClose} className="text-sm px-4 py-2 rounded-lg bg-zinc-700 text-zinc-300 hover:bg-zinc-600 transition-colors">Close</button>
        </div>

      </div>
    </div>
  );
}

function NewSpecDialog({
  epicId,
  onCreated,
  onClose,
}: {
  epicId: string;
  onCreated: () => void;
  onClose: () => void;
}) {
  const [specName, setSpecName] = useState('');
  const [agentName, setAgentName] = useState('');
  const [specDocPath, setSpecDocPath] = useState('');
  const [codeReviewRequired, setCodeReviewRequired] = useState(false);
  const [reviewerAgent, setReviewerAgent] = useState('');
  const [allAgents, setAllAgents] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);
  const overlayRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    AgentApi.list()
      .then(list => setAllAgents(list.map(a => a.sessionName).sort()))
      .catch(() => {});
  }, []);

  function handleOverlayClick(e: React.MouseEvent<HTMLDivElement>) {
    if (e.target === overlayRef.current) onClose();
  }

  async function handleCreate() {
    if (!specName.trim() || !agentName) return;

    setBusy(true);

    try {
      await EpicApi.createSpec(
        epicId,
        specName.trim(),
        agentName,
        specDocPath.trim() || null,
        codeReviewRequired,
        reviewerAgent || null,
      );
      onCreated();
      onClose();
    } catch (e) {
      alert(String(e));
    } finally {
      setBusy(false);
    }
  }

  const inputCls = 'w-full text-xs rounded-lg border border-zinc-700 bg-zinc-800 px-2.5 py-1.5 text-zinc-100 font-mono focus:outline-none focus:ring-1 focus:ring-indigo-500 focus:border-indigo-500';

  return (
    <div
      ref={overlayRef}
      onClick={handleOverlayClick}
      className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4"
    >
      <div className="w-full max-w-md bg-zinc-900 border border-zinc-700 rounded-2xl shadow-2xl flex flex-col">

        <div className="flex items-center justify-between px-5 py-4 border-b border-zinc-800">
          <p className="text-sm font-bold text-zinc-100">New Spec</p>
          <button onClick={onClose} className="text-zinc-500 hover:text-zinc-200 text-xl leading-none">×</button>
        </div>

        <div className="px-5 py-4 space-y-4">

          <div>
            <label className="text-[10px] font-semibold tracking-widest uppercase text-zinc-600 block mb-1.5">Spec Name</label>
            <input
              value={specName}
              onChange={e => setSpecName(e.target.value)}
              placeholder="e.g. add-login-page"
              className={inputCls}
              autoFocus
            />
          </div>

          <div>
            <label className="text-[10px] font-semibold tracking-widest uppercase text-zinc-600 block mb-1.5">Assigned Agent</label>
            <select
              value={agentName}
              onChange={e => setAgentName(e.target.value)}
              className={inputCls}
            >
              <option value="">Select agent…</option>
              <option value="human">👤 human</option>
              {allAgents.map(n => <option key={n} value={n}>{n}</option>)}
            </select>
          </div>

          <div>
            <label className="text-[10px] font-semibold tracking-widest uppercase text-zinc-600 block mb-1.5">Spec Doc Path <span className="text-zinc-700 normal-case font-normal">(optional)</span></label>
            <input
              value={specDocPath}
              onChange={e => setSpecDocPath(e.target.value)}
              placeholder="/absolute/path/to/spec.md"
              className={inputCls}
            />
          </div>

          <div className="flex items-center gap-3">
            <button
              onClick={() => setCodeReviewRequired(v => !v)}
              className={`inline-flex items-center gap-1.5 text-xs font-medium px-3 py-1 rounded-full border transition-colors ${codeReviewRequired ? 'bg-emerald-500/10 text-emerald-400 border-emerald-500/30' : 'bg-white/[0.04] text-zinc-400 border-zinc-700'}`}
            >
              {codeReviewRequired ? '✓' : '○'} Code Review Required
            </button>
          </div>

          {codeReviewRequired && (
            <div>
              <label className="text-[10px] font-semibold tracking-widest uppercase text-zinc-600 block mb-1.5">Reviewer Agent <span className="text-zinc-700 normal-case font-normal">(optional)</span></label>
              <select
                value={reviewerAgent}
                onChange={e => setReviewerAgent(e.target.value)}
                className={inputCls}
              >
                <option value="">None</option>
                {allAgents.map(n => <option key={n} value={n}>{n}</option>)}
              </select>
            </div>
          )}

        </div>

        <div className="px-5 py-4 border-t border-zinc-800 flex justify-end gap-2">
          <button
            onClick={onClose}
            className="text-xs px-4 py-2 rounded-lg border border-zinc-700 text-zinc-400 hover:text-zinc-200 transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={handleCreate}
            disabled={busy || !specName.trim() || !agentName}
            className="text-xs px-4 py-2 rounded-lg bg-indigo-600 text-white hover:bg-indigo-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            {busy ? 'Creating…' : 'Create Spec'}
          </button>
        </div>

      </div>
    </div>
  );
}

function SpecTableRow({
  spec,
  epicId,
  allSpecs,
  onUpdated,
  onViewDoc,
  onApproveHumanInLoop,
  onForceState,
}: {
  spec: Spec;
  epicId: string;
  allSpecs: Spec[];
  onUpdated: () => void;
  onViewDoc: (path: string) => void;
  onApproveHumanInLoop: (specId: string, isApproved: boolean, feedback: string | null) => void;
  onForceState: (specId: string, stateName: string) => void;
}) {
  const [dialog, setDialog] = useState<'detail' | 'hil' | 'code' | null>(null);

  const progress = specProgress(spec);
  const needsHumanReview = spec.currentStateName === 'human_in_loop' || (spec.humanInLoop !== null && spec.humanInLoop.isApproved === null);
  const isReady = spec.currentStateName === 'ready';

  const blockingDeps = isReady
    ? spec.dependsOn
        .map(depId => allSpecs.find(s => s.id === depId))
        .filter((dep): dep is Spec => {
          if (dep === undefined) return false;
          const resolved = ['ac', 'done'];
          return !resolved.includes(dep.currentStateName) && !resolved.includes(dep.lastKnownStateName ?? '');
        })
    : [];
  const blockedByDeps = blockingDeps.length > 0;
  const rowOpacity = spec.isAbandoned ? 'opacity-35' : '';

  const btnBase = 'text-[10px] px-2 py-0.5 rounded border transition-colors';

  return (
    <>
      {dialog === 'detail' && (
        <SpecDetailDialog spec={spec} epicId={epicId} allSpecs={allSpecs} onClose={() => setDialog(null)} onViewDoc={onViewDoc} onForceState={onForceState} onUpdated={onUpdated} />
      )}
      {dialog === 'hil' && (
        <HilDialog spec={spec} onClose={() => setDialog(null)} onApproveHumanInLoop={(id, approved, feedback) => { onApproveHumanInLoop(id, approved, feedback); setDialog(null); }} />
      )}
      {dialog === 'code' && (
        <CodeNowDialog spec={spec} epicId={epicId} onClose={() => setDialog(null)} onUpdated={onUpdated} />
      )}

      <tr className={`border-b border-zinc-800 hover:bg-white/[0.02] transition-colors ${rowOpacity}`}>
        {/* Name */}
        <td className="py-2 pl-4 pr-3">
          <div className="text-[12px] font-medium text-zinc-200 leading-snug">{specDisplayName(spec.id, epicId)}</div>
          <div className="font-mono text-[10px] text-zinc-500 leading-snug">{spec.id}</div>
        </td>

        {/* Agent */}
        <td className="py-2 px-3 whitespace-nowrap">
          {spec.assignedAgentName ? (
            spec.assignedAgentName === 'human' ? (
              <span className="font-mono text-[11px] text-amber-400">👤 human</span>
            ) : (
              <div className="flex items-center gap-1.5">
                <span className="font-mono text-[11px] text-indigo-300 truncate max-w-[120px]">{spec.assignedAgentName}</span>
                <a href={`openterm:${spec.assignedAgentName}`} className="text-sm leading-none hover:opacity-70 transition-opacity flex-shrink-0" title={`Chat with ${spec.assignedAgentName}`}>💬</a>
              </div>
            )
          ) : <span className="text-[11px] text-zinc-600">—</span>}
        </td>

        {/* State */}
        <td className="py-2 px-3 whitespace-nowrap">
          <StateBadge state={spec.currentStateName} />
        </td>

        {/* Progress — bar only, no % */}
        <td className="py-2 px-3">
          <div className="w-24 h-[3px] rounded-full bg-zinc-800 overflow-hidden">
            <div className="h-full rounded-full bg-gradient-to-r from-indigo-500 to-cyan-400 transition-all" style={{ width: `${progress}%` }} />
          </div>
        </td>

        {/* Actions */}
        <td className="py-2 pl-3 pr-4 whitespace-nowrap">
          <div className="flex items-center gap-1.5 justify-end">
            {needsHumanReview && (
              <button onClick={() => setDialog('hil')} className={`${btnBase} bg-sky-500/10 border-sky-500/30 text-sky-400 hover:bg-sky-500/20`}>human</button>
            )}
            {isReady && blockedByDeps && (
              <button
                disabled
                title={`Blocked by: ${blockingDeps.map(d => d.id).join(', ')}`}
                className={`${btnBase} bg-emerald-500/10 border-emerald-500/25 text-emerald-400 cursor-not-allowed opacity-50`}
              >code</button>
            )}
            {isReady && !blockedByDeps && (
              <button onClick={() => setDialog('code')} className={`${btnBase} bg-emerald-500/10 border-emerald-500/25 text-emerald-400 hover:bg-emerald-500/20`}>code</button>
            )}
            {spec.specDocPath && (
              <button onClick={() => onViewDoc(spec.specDocPath!)} className={`${btnBase} bg-white/[0.04] border-zinc-700 text-zinc-500 hover:text-zinc-300`}>doc</button>
            )}
            <button onClick={() => setDialog('detail')} className={`${btnBase} bg-white/[0.04] border-zinc-700 text-zinc-500 hover:text-zinc-300`}>…</button>
          </div>
        </td>
      </tr>
    </>
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
  const [governanceEditorOpen, setGovernanceEditorOpen] = useState(false);
  const [swarmObjective, setSwarmObjective] = useState('');
  const [swarmToState, setSwarmToState] = useState('');
  const [swarmSubmitting, setSwarmSubmitting] = useState(false);
  const [nudgeSent, setNudgeSent] = useState(false);
  const [deleteConfirm, setDeleteConfirm] = useState(false);
  const [newSpecOpen, setNewSpecOpen] = useState(false);
  const agentStatuses = useAgentStatuses();

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

            <EpicSummaryCard epic={epic} onUpdated={setEpic} onViewDoc={setDrawerPath} onEditGovernance={() => setGovernanceEditorOpen(true)} onForceState={handleForceEpicState} agentStatuses={agentStatuses} />

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
          <div className="bg-zinc-900 border border-zinc-800 rounded-xl overflow-hidden">
            <div className="flex items-center justify-between px-4 py-3 border-b border-zinc-800">
              <p className="text-[10px] font-bold tracking-widest uppercase text-zinc-600">Epic Board — Specs</p>
              <button
                disabled
                className="text-[11px] px-2.5 py-1 rounded-lg bg-white/[0.04] border border-zinc-700 text-zinc-600 cursor-not-allowed"
              >
                + New Spec
              </button>
            </div>

            {epic.specs.length === 0 ? (
              <p className="text-sm text-zinc-600 py-8 text-center">No specs yet.</p>
            ) : (
              <>
                <table className="w-full border-collapse">
                  <thead>
                    <tr className="border-b border-zinc-800">
                      <th className="text-left text-[10px] font-semibold tracking-widest uppercase text-zinc-600 py-2 pl-4 pr-3">Spec</th>
                      <th className="text-left text-[10px] font-semibold tracking-widest uppercase text-zinc-600 py-2 px-3">Agent</th>
                      <th className="text-left text-[10px] font-semibold tracking-widest uppercase text-zinc-600 py-2 px-3">State</th>
                      <th className="text-left text-[10px] font-semibold tracking-widest uppercase text-zinc-600 py-2 px-3">Progress</th>
                      <th className="py-2 pl-3 pr-4" />
                    </tr>
                  </thead>
                  <tbody>
                    {epic.specs.map(s => (
                      <SpecTableRow
                        key={s.id}
                        spec={s}
                        epicId={epic.id}
                        allSpecs={epic.specs}
                        onUpdated={load}
                        onViewDoc={setDrawerPath}
                        onApproveHumanInLoop={handleApproveSpecHumanInLoop}
                        onForceState={handleForceSpecState}
                      />
                    ))}
                  </tbody>
                </table>

                <div className="px-4 py-2 border-t border-zinc-800 flex items-center gap-3 flex-wrap">
                  <span className="text-[10px] text-zinc-600">{epic.specs.length} specs</span>
                  {(['drafting', 'ready', 'coding', 'code_review', 'ac', 'done'] as const).map(state => {
                    const count = epic.specs.filter(s => s.currentStateName === state).length;
                    if (count === 0) return null;
                    return (
                      <span key={state} className="text-[10px] text-zinc-500">
                        {count} <span className="text-zinc-600">{state.replace('_', ' ')}</span>
                      </span>
                    );
                  })}
                  {epic.specs.filter(s => s.humanInLoop?.isApproved === null).length > 0 && (
                    <span className="text-[10px] text-amber-500 ml-auto">
                      ⚠ {epic.specs.filter(s => s.humanInLoop?.isApproved === null).length} need review
                    </span>
                  )}
                </div>
              </>
            )}
          </div>

        </div>

        {/* Audit log */}
        <div className="bg-zinc-900 border border-zinc-800 rounded-xl px-5 py-4">
          <p className="text-[10px] font-bold tracking-widest uppercase text-zinc-600 mb-3">Activity Log</p>
          <AuditLogPanel entries={auditLog} />
        </div>

      </div>

      {newSpecOpen && (
        <NewSpecDialog epicId={epic.id} onCreated={load} onClose={() => setNewSpecOpen(false)} />
      )}
      <MarkdownDrawer path={drawerPath} onClose={() => setDrawerPath(null)} />
      {governanceEditorOpen && epic.epicGovernancePath && (
        <GovernanceEditor path={epic.epicGovernancePath} onClose={() => setGovernanceEditorOpen(false)} />
      )}

    </div>
  );
}
