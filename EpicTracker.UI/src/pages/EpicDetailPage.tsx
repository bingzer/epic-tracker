import { useCallback, useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { EpicApi, SpecApi } from '../epicApi';
import type { Epic, EpicAudit, AgentAgreement } from '../types';
import { StateBadge } from '../components/StateBadge';
import { StateBreadcrumb } from '../components/StateBreadcrumb';
import { SpecRow } from '../components/SpecRow';
import { AuditLogPanel } from '../components/AuditLogPanel';
import { EscalationPanel } from '../components/EscalationPanel';
import { useSignalR } from '../hooks/useSignalR';

type Tab = 'board' | 'audit';

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
        Agent Swarm — Iteration {swarm.iteration + 1}
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
  const [epic, setEpic] = useState<Epic | null>(null);
  const [auditLog, setAuditLog] = useState<EpicAudit[]>([]);
  const [loading, setLoading] = useState(true);
  const [tab, setTab] = useState<Tab>('board');
  const [error, setError] = useState<string | null>(null);

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
      if (e?.id === epicId) setEpic(e);
    },
    SpecUpdated: () => {
      load();
    },
  });

  useEffect(() => { load(); }, [load]);

  useEffect(() => {
    if (tab === 'audit') loadAudit();
  }, [tab, loadAudit]);

  async function handleApproveSpecHumanInLoop(specId: string, isApproved: boolean, feedback: string | null) {
    try {
      await SpecApi.approveHumanInLoop(specId, isApproved, feedback);
      load();
    } catch (e) {
      alert(String(e));
    }
  }

  if (loading) return <div className="p-6 text-sm text-gray-400 dark:text-zinc-500">Loading…</div>;
  if (error) return <div className="p-6 text-sm text-red-500">{error}</div>;
  if (!epic) return null;

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
      </div>

      <StateBreadcrumb state={epic.currentStateName} type="epic" />

      <EscalationPanel key={epic.humanInLoop?.questions ?? 'none'} epic={epic} onUpdated={setEpic} />

      <AgentSwarmPanel epic={epic} />

      {epic.epicAgentInstruction && (
        <div className="rounded-lg border border-blue-200 dark:border-blue-800 bg-blue-50 dark:bg-blue-900/20 px-4 py-3">
          <span className="text-xs font-semibold text-blue-600 dark:text-blue-400 uppercase tracking-wide block mb-1">
            Epic Agent Instruction
          </span>
          <p className="text-sm text-blue-800 dark:text-blue-200 whitespace-pre-wrap">{epic.epicAgentInstruction}</p>
        </div>
      )}

      {/* Metadata panel */}
      <div className="bg-white dark:bg-zinc-900 rounded-xl border border-gray-100 dark:border-zinc-800 shadow-sm p-4 grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">
        <div className="space-y-2">
          {epic.brief && (
            <div>
              <span className="text-xs font-medium text-gray-500 dark:text-zinc-400 block">Brief</span>
              <span className="text-gray-800 dark:text-zinc-200">{epic.brief}</span>
            </div>
          )}
          <div>
            <span className="text-xs font-medium text-gray-500 dark:text-zinc-400 block">Epic Agent</span>
            <span className="text-gray-800 dark:text-zinc-200 font-mono text-sm">{epic.epicAgent}</span>
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
        </div>
        <div className="space-y-2">
          <div>
            <span className="text-xs font-medium text-gray-500 dark:text-zinc-400 block">Epic Document</span>
            <span className="text-gray-800 dark:text-zinc-200 font-mono text-xs break-all">{epic.epicDocumentPath || '—'}</span>
          </div>
          <div>
            <span className="text-xs font-medium text-gray-500 dark:text-zinc-400 block">Governance Path</span>
            <span className="text-gray-800 dark:text-zinc-200 font-mono text-xs break-all">{epic.epicGovernancePath || '—'}</span>
          </div>
          {epic.codingAgents.length > 0 && (
            <div>
              <span className="text-xs font-medium text-gray-500 dark:text-zinc-400 block">Coding Agents</span>
              <div className="flex flex-wrap gap-1 mt-0.5">
                {epic.codingAgents.map(a => (
                  <span key={a} className="text-xs font-mono bg-gray-100 dark:bg-zinc-800 text-gray-700 dark:text-zinc-300 px-1.5 py-0.5 rounded">
                    {a}
                  </span>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>

      <div className="flex gap-1">
        <button className={tabCls('board')} onClick={() => setTab('board')}>Epic Board</button>
        <button className={tabCls('audit')} onClick={() => setTab('audit')}>Audit Log</button>
      </div>

      <div className="bg-white dark:bg-zinc-900 rounded-xl border border-gray-100 dark:border-zinc-800 shadow-sm">
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
                    <SpecRow key={s.id} spec={s} onApproveHumanInLoop={handleApproveSpecHumanInLoop} />
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
      </div>
    </div>
  );
}
