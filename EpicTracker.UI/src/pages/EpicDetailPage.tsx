import { useCallback, useEffect, useRef, useState } from 'react';
import { marked } from 'marked';
import { useNavigate, useParams } from 'react-router-dom';
import { EpicApi, SpecApi, DocApi } from '../epicApi';
import type { Epic, EpicAudit, AgentAgreement } from '../types';
import { StateBadge } from '../components/StateBadge';
import { StateBreadcrumb } from '../components/StateBreadcrumb';
import { SpecRow } from '../components/SpecRow';
import { AuditLogPanel } from '../components/AuditLogPanel';
import { EscalationPanel } from '../components/EscalationPanel';
import { useSignalR } from '../hooks/useSignalR';

type Tab = 'details' | 'board' | 'audit' | 'agent';

const TRANSIENT_STATES = new Set(['agent_swarm', 'human_in_loop', 'spec_human_in_loop']);

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
      <div className="w-full md:w-1/2 bg-white dark:bg-zinc-900 h-full flex flex-col shadow-xl">
        <div className="flex items-center justify-between px-4 py-3 border-b border-gray-200 dark:border-zinc-700">
          <span className="text-xs font-mono text-gray-500 dark:text-zinc-400 truncate max-w-xs">{path}</span>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 dark:hover:text-zinc-200 text-lg leading-none ml-4"
          >
            ×
          </button>
        </div>
        <div className="flex-1 overflow-auto p-5">
          {loading && <p className="text-sm text-gray-400 dark:text-zinc-500">Loading…</p>}
          {!loading && (
            <div
              className="prose prose-sm dark:prose-invert max-w-none"
              dangerouslySetInnerHTML={{ __html: content }}
            />
          )}
        </div>
      </div>
    </div>
  );
}

const SWARM_STATES = new Set(['waterproofing', 'agent_swarm']);

function AgentSwarmPanel({ epic }: { epic: Epic }) {
  if (!epic.agentSwarm) return null;
  if (epic.agentSwarm.isComplete && !SWARM_STATES.has(epic.currentStateName)) return null;

  const swarm = epic.agentSwarm;

  function agreementIcon(agreement: AgentAgreement) {
    if (agreement.hasAgreed === true) return '✓';
    if (agreement.hasAgreed === false) return '✗';
    return '…';
  }

  function agreementColor(agreement: AgentAgreement) {
    if (agreement.hasAgreed === true) return 'text-emerald-600 dark:text-emerald-400';
    if (agreement.hasAgreed === false) return 'text-red-600 dark:text-red-400';
    return 'text-gray-400 dark:text-zinc-500';
  }

  return (
    <div className="rounded-lg border border-violet-200 dark:border-violet-800 bg-violet-50 dark:bg-violet-900/20 p-4">
      <p className="text-sm font-semibold text-violet-800 dark:text-violet-300 mb-1">
        Agent Swarm — Iteration {swarm.iteration}
      </p>
      <p className="text-sm text-violet-700 dark:text-violet-400 mb-2">{swarm.objective}</p>
      {swarm.agreements.length > 0 && (
        <ul className="space-y-0.5">
          {swarm.agreements.map(a => (
            <li key={a.agentId} className="flex items-center gap-2 text-xs">
              <span className={`font-semibold ${agreementColor(a)}`}>{agreementIcon(a)}</span>
              <span className="font-mono text-gray-700 dark:text-zinc-300">{a.agentId}</span>
              {a.note && <span className="text-gray-500 dark:text-zinc-500">{a.note}</span>}
            </li>
          ))}
        </ul>
      )}
      <div className="mt-2 flex gap-2 text-xs">
        {swarm.hasConsensus && <span className="text-emerald-600 dark:text-emerald-400 font-medium">Consensus reached</span>}
        {swarm.hasDisagreement && <span className="text-red-600 dark:text-red-400 font-medium">Disagreement</span>}
        {swarm.isComplete && <span className="text-gray-500 dark:text-zinc-400">Complete</span>}
      </div>
    </div>
  );
}

export default function EpicDetailPage() {
  const { epicId } = useParams<{ epicId: string }>();
  const navigate = useNavigate();
  const [epic, setEpic] = useState<Epic | null>(null);
  const [auditLog, setAuditLog] = useState<EpicAudit[]>([]);
  const [loading, setLoading] = useState(true);
  const [tab, setTab] = useState<Tab>('details');
  const [error, setError] = useState<string | null>(null);
  const [agentInput, setAgentInput] = useState('');
  const [reviewerInput, setReviewerInput] = useState<string>('');
  const [drawerPath, setDrawerPath] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!epicId) return;
    try {
      const data = await EpicApi.get(epicId);
      setEpic(data);
      setReviewerInput(data.reviewerAgentId ?? '');
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
      if (e?.id === epicId) setEpic(e);
    },
    SpecUpdated: () => {
      load();
    },
  });

  useEffect(() => { load(); loadAudit(); }, [load, loadAudit]);

  useEffect(() => {
    if (tab === 'audit') loadAudit();
  }, [tab, loadAudit]);

  async function handleWakeAgent() {
    if (!epicId) return;
    try {
      await EpicApi.wakeAgent(epicId);
    } catch (e) {
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

  async function handleDeleteEpic() {
    if (!epicId) return;
    if (!window.confirm('Delete this epic? This cannot be undone.')) return;
    try {
      await EpicApi.delete(epicId);
      navigate('/');
    } catch (e) {
      alert(String(e));
    }
  }

  async function handleAddCodingAgent() {
    if (!epic || !agentInput.trim()) return;
    const parts = agentInput.split(',').map(s => s.trim()).filter(Boolean);
    const next = [...epic.codingAgents];
    for (const p of parts) {
      if (!next.includes(p)) next.push(p);
    }
    setAgentInput('');
    try {
      const updated = await EpicApi.update(epic.id, { codingAgents: next });
      setEpic(updated);
    } catch (e) {
      alert(String(e));
    }
  }

  async function handleRemoveCodingAgent(agent: string) {
    if (!epic) return;
    const next = epic.codingAgents.filter(a => a !== agent);
    try {
      const updated = await EpicApi.update(epic.id, { codingAgents: next });
      setEpic(updated);
    } catch (e) {
      alert(String(e));
    }
  }

  async function handleSaveReviewer() {
    if (!epic) return;
    try {
      const updated = await EpicApi.update(epic.id, { reviewerAgentId: reviewerInput.trim() || null });
      setEpic(updated);
    } catch (e) {
      alert(String(e));
    }
  }

  if (loading) return <div className="p-6 text-sm text-gray-400 dark:text-zinc-500">Loading…</div>;
  if (error) return <div className="p-6 text-sm text-red-500">{error}</div>;
  if (!epic) return null;

  const lastRealState = [...auditLog].reverse().find(a => !TRANSIENT_STATES.has(a.toState))?.toState;

  const tabCls = (t: Tab) => `px-3 py-1.5 text-sm font-medium rounded-md transition-colors ${
    tab === t
      ? 'bg-gray-100 dark:bg-zinc-800 text-gray-900 dark:text-zinc-100'
      : 'text-gray-500 dark:text-zinc-400 hover:text-gray-700 dark:hover:text-zinc-200 hover:bg-gray-50 dark:hover:bg-zinc-800/50'
  }`;

  return (
    <div className="p-5 max-w-6xl mx-auto space-y-4">
      <div className="flex items-center gap-3 flex-wrap">
        <h1 className="text-lg font-semibold text-gray-900 dark:text-zinc-100">{epic.name}</h1>
        <StateBadge state={epic.currentStateName} />
        {epic.humanInLoop && epic.humanInLoop.isApproved === null && (
          <span className="text-xs font-medium bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300 px-2 py-0.5 rounded">
            HUMAN REVIEW
          </span>
        )}
        <span className="text-xs text-gray-400 dark:text-zinc-600 font-mono ml-1">{epic.id}</span>
        <div className="ml-auto flex items-center gap-2">
          <button
            onClick={handleWakeAgent}
            className="text-xs px-3 py-1.5 rounded-md bg-indigo-600 text-white hover:bg-indigo-700 transition-colors"
          >
            {epic.currentStateName === 'drafting' ? `Let's work on ${epic.name}` : 'Nudge agent'}
          </button>
          <button
            onClick={handleDeleteEpic}
            className="text-xs px-3 py-1.5 rounded-md bg-red-600 text-white hover:bg-red-700 transition-colors"
          >
            Delete
          </button>
        </div>
      </div>

      <StateBreadcrumb state={epic.currentStateName} type="epic" lastRealState={lastRealState} />

      <EscalationPanel key={epic.humanInLoop?.questions ?? 'none'} epic={epic} onUpdated={setEpic} />

      <div className="flex gap-1 border-b border-gray-200 dark:border-zinc-800 -mb-2">
        <button className={tabCls('details')} onClick={() => setTab('details')}>Epic Details</button>
        <button className={tabCls('board')} onClick={() => setTab('board')}>Epic Board</button>
        <button className={tabCls('audit')} onClick={() => setTab('audit')}>Audit Log</button>
        <button className={tabCls('agent')} onClick={() => setTab('agent')}>Agent</button>
      </div>

      <div className="bg-white dark:bg-zinc-900 rounded-xl border border-gray-100 dark:border-zinc-800 shadow-sm">
        {tab === 'details' && (
          <div className="p-4 grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">
            <div className="space-y-2">
              <div>
                <span className="text-xs font-medium text-gray-500 dark:text-zinc-400 block">Slug</span>
                <span className="text-gray-800 dark:text-zinc-200 font-mono text-xs">{epic.slug}</span>
              </div>
              <div>
                <span className="text-xs font-medium text-gray-500 dark:text-zinc-400 block">Created</span>
                <span className="text-gray-800 dark:text-zinc-200 text-xs">{new Date(epic.createdAt).toLocaleString()}</span>
              </div>
              {epic.brief && (
                <div>
                  <span className="text-xs font-medium text-gray-500 dark:text-zinc-400 block">Brief</span>
                  <span className="text-gray-800 dark:text-zinc-200">{epic.brief}</span>
                </div>
              )}
              <div>
                <span className="text-xs font-medium text-gray-500 dark:text-zinc-400 block">Epic Agent</span>
                <div className="flex items-center gap-2">
                  <span className="text-gray-800 dark:text-zinc-200 font-mono text-sm">{epic.epicAgent}</span>
                  <a href={`openterm:${epic.epicAgent}`} className="text-xs text-indigo-500 hover:text-indigo-700 dark:text-indigo-400 dark:hover:text-indigo-300">Open chat</a>
                </div>
              </div>
              <div>
                <span className="text-xs font-medium text-gray-500 dark:text-zinc-400 block">Needs Mockup</span>
                <span className="text-gray-800 dark:text-zinc-200">{epic.needsMockup ? 'Yes' : 'No'}</span>
              </div>
              <div>
                <span className="text-xs font-medium text-gray-500 dark:text-zinc-400 block">Doc Drafted</span>
                <span className="text-gray-800 dark:text-zinc-200">{epic.isDocDrafted ? 'Yes' : 'No'}</span>
              </div>
              {epic.needsMockup && (
                <>
                  <div>
                    <span className="text-xs font-medium text-gray-500 dark:text-zinc-400 block">Mockup Done</span>
                    <span className="text-gray-800 dark:text-zinc-200">{epic.isMockupDone ? 'Yes' : 'No'}</span>
                  </div>
                  {epic.mockupPath && (
                    <div>
                      <span className="text-xs font-medium text-gray-500 dark:text-zinc-400 block">Mockup Path</span>
                      <span className="text-gray-800 dark:text-zinc-200 font-mono text-xs break-all">{epic.mockupPath}</span>
                    </div>
                  )}
                </>
              )}
              {epic.humanInLoop && (
                <details>
                  <summary className="text-xs font-medium text-gray-500 dark:text-zinc-400 cursor-pointer">HumanInLoop</summary>
                  <pre className="text-xs bg-gray-50 dark:bg-zinc-800 rounded p-2 mt-1 overflow-auto">{JSON.stringify(epic.humanInLoop, null, 2)}</pre>
                </details>
              )}
              {epic.agentSwarm && (
                <details>
                  <summary className="text-xs font-medium text-gray-500 dark:text-zinc-400 cursor-pointer">AgentSwarm</summary>
                  <pre className="text-xs bg-gray-50 dark:bg-zinc-800 rounded p-2 mt-1 overflow-auto">{JSON.stringify(epic.agentSwarm, null, 2)}</pre>
                </details>
              )}
            </div>
            <div className="space-y-2">
              <div>
                <span className="text-xs font-medium text-gray-500 dark:text-zinc-400 block">Epic Document</span>
                <div className="flex items-center gap-2">
                  <span className="text-gray-800 dark:text-zinc-200 font-mono text-xs break-all">{epic.epicDocumentPath || '—'}</span>
                  {epic.epicDocumentPath && (
                    <button
                      onClick={() => setDrawerPath(epic.epicDocumentPath)}
                      className="text-xs text-indigo-500 hover:text-indigo-700 dark:text-indigo-400 dark:hover:text-indigo-300 shrink-0"
                    >
                      View
                    </button>
                  )}
                </div>
              </div>
              <div>
                <span className="text-xs font-medium text-gray-500 dark:text-zinc-400 block">Governance Path</span>
                <div className="flex items-center gap-2">
                  <span className="text-gray-800 dark:text-zinc-200 font-mono text-xs break-all">{epic.epicGovernancePath || '—'}</span>
                  {epic.epicGovernancePath && (
                    <button
                      onClick={() => setDrawerPath(epic.epicGovernancePath)}
                      className="text-xs text-indigo-500 hover:text-indigo-700 dark:text-indigo-400 dark:hover:text-indigo-300 shrink-0"
                    >
                      View
                    </button>
                  )}
                </div>
              </div>
              <div>
                <span className="text-xs font-medium text-gray-500 dark:text-zinc-400 block mb-1">Coding Agents</span>
                <div className="flex flex-wrap gap-1 mb-1">
                  {epic.codingAgents.map(a => (
                    <span key={a} className="inline-flex items-center gap-1 text-xs font-mono bg-gray-100 dark:bg-zinc-800 text-gray-700 dark:text-zinc-300 px-1.5 py-0.5 rounded">
                      {a}
                      <a href={`openterm:${a}`} className="text-indigo-400 hover:text-indigo-600 dark:hover:text-indigo-300 leading-none" title="Open chat">↗</a>
                      <button
                        onClick={() => handleRemoveCodingAgent(a)}
                        className="text-gray-400 dark:text-zinc-500 hover:text-red-500 dark:hover:text-red-400 leading-none"
                      >
                        ×
                      </button>
                    </span>
                  ))}
                </div>
                <div className="flex gap-1.5">
                  <input
                    value={agentInput}
                    onChange={e => setAgentInput(e.target.value)}
                    onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); handleAddCodingAgent(); } }}
                    placeholder="agent-id"
                    className="text-xs rounded border border-gray-200 dark:border-zinc-700 bg-gray-50 dark:bg-zinc-800 px-2 py-1 text-gray-900 dark:text-zinc-100 focus:outline-none focus:ring-1 focus:ring-blue-500 flex-1 min-w-0"
                  />
                  <button
                    type="button"
                    onClick={handleAddCodingAgent}
                    className="text-xs px-2 py-1 rounded border border-gray-200 dark:border-zinc-700 text-gray-600 dark:text-zinc-400 hover:bg-gray-50 dark:hover:bg-zinc-800 shrink-0"
                  >
                    Add
                  </button>
                </div>
              </div>
              <div>
                <span className="text-xs font-medium text-gray-500 dark:text-zinc-400 block mb-1">Reviewer</span>
                {epic.reviewerAgentId && (
                  <a href={`openterm:${epic.reviewerAgentId}`} className="text-xs text-indigo-500 hover:text-indigo-700 dark:text-indigo-400 dark:hover:text-indigo-300 font-mono mr-2">{epic.reviewerAgentId} ↗</a>
                )}
                <div className="flex gap-1.5 mt-1">
                  <input
                    value={reviewerInput}
                    onChange={e => setReviewerInput(e.target.value)}
                    placeholder="reviewer-agent-id"
                    className="text-xs rounded border border-gray-200 dark:border-zinc-700 bg-gray-50 dark:bg-zinc-800 px-2 py-1 text-gray-900 dark:text-zinc-100 focus:outline-none focus:ring-1 focus:ring-blue-500 flex-1 min-w-0"
                  />
                  <button
                    type="button"
                    onClick={handleSaveReviewer}
                    className="text-xs px-2 py-1 rounded border border-gray-200 dark:border-zinc-700 text-gray-600 dark:text-zinc-400 hover:bg-gray-50 dark:hover:bg-zinc-800 shrink-0"
                  >
                    Save
                  </button>
                </div>
              </div>
            </div>
          </div>
        )}

        {tab === 'board' && (
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead>
                <tr className="text-left text-xs text-gray-400 dark:text-zinc-500">
                  <th className="px-3 py-2 font-medium">Spec</th>
                  <th className="px-3 py-2 font-medium">Agent</th>
                  <th className="px-3 py-2 font-medium">State</th>
                  <th className="px-3 py-2 font-medium">Progress</th>
                  <th className="px-3 py-2 font-medium">Actions</th>
                </tr>
              </thead>
              <tbody>
                {epic.specs.length === 0 ? (
                  <tr><td colSpan={5} className="px-3 py-6 text-center text-sm text-gray-400 dark:text-zinc-500">No specs yet.</td></tr>
                ) : (
                  epic.specs.map(s => (
                    <SpecRow key={s.id} spec={s} onApproveHumanInLoop={handleApproveSpecHumanInLoop} onForceState={handleForceSpecState} onViewDoc={setDrawerPath} />
                  ))
                )}
              </tbody>
            </table>
          </div>
        )}

        {tab === 'audit' && (
          <div className="p-4">
            <AuditLogPanel entries={auditLog} />
          </div>
        )}

        {tab === 'agent' && (
          <div className="p-4 space-y-4">
            <AgentSwarmPanel epic={epic} />
            {epic.epicAgentInstruction ? (
              <div className="rounded-lg border border-blue-200 dark:border-blue-800 bg-blue-50 dark:bg-blue-900/20 px-4 py-3">
                <span className="text-xs font-semibold text-blue-600 dark:text-blue-400 uppercase tracking-wide block mb-1">
                  Epic Agent Instruction
                </span>
                <p className="text-sm text-blue-800 dark:text-blue-200 whitespace-pre-wrap">{epic.epicAgentInstruction}</p>
              </div>
            ) : (
              <p className="text-sm text-gray-400 dark:text-zinc-500">No active agent instruction.</p>
            )}
          </div>
        )}
      </div>

      <MarkdownDrawer path={drawerPath} onClose={() => setDrawerPath(null)} />
    </div>
  );
}
