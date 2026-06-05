import type { AgentAgreement, Epic } from '../types';

const SWARM_STATES = new Set(['waterproofing', 'agent_swarm']);

function statusIcon(a: AgentAgreement) {
  if (a.hasAgreed === true) return '✓';
  if (a.hasAgreed === false) return '✗';
  return '…';
}

function iconCls(a: AgentAgreement) {
  if (a.hasAgreed === true)
    return 'w-4 h-4 rounded-full flex items-center justify-center text-[9px] bg-emerald-500/20 border border-emerald-500/40 text-emerald-400 shrink-0';
  if (a.hasAgreed === false)
    return 'w-4 h-4 rounded-full flex items-center justify-center text-[9px] bg-red-500/15 border border-red-500/30 text-red-400 shrink-0';
  return 'w-4 h-4 rounded-full flex items-center justify-center text-[9px] bg-white/5 border border-white/12 text-zinc-500 shrink-0';
}

function noteCls(a: AgentAgreement) {
  if (a.hasAgreed === true) return 'text-emerald-300/80';
  if (a.hasAgreed === false) return 'text-red-300/80';
  return 'text-zinc-500';
}

export function AgentSwarmPanel({ epic }: { epic: Epic }) {
  if (!epic.agentSwarm) return null;
  if (epic.agentSwarm.isComplete && !SWARM_STATES.has(epic.currentStateName)) return null;

  const swarm = epic.agentSwarm;
  const maxIterations = 5;

  return (
    <div className="rounded-lg border border-violet-500/25 bg-violet-500/[0.07] p-4">
      <div className="flex items-center justify-between mb-2.5">
        <span className="text-[11px] font-bold text-violet-300 uppercase tracking-widest">Agent Swarm</span>
        <span className="text-[10px] text-violet-800 dark:text-violet-300 bg-violet-500/15 px-2 py-0.5 rounded-full">
          Iteration {swarm.iteration + 1} / {maxIterations}
        </span>
      </div>

      <p className="text-xs text-violet-200/80 mb-3 leading-relaxed">{swarm.objective}</p>

      {swarm.agreements.length > 0 && (
        <div className="divide-y divide-violet-500/10">
          {swarm.agreements.map(a => (
            <div key={a.agentId} className="flex items-center gap-2.5 py-1.5">
              <span className={iconCls(a)}>{statusIcon(a)}</span>
              <span className="font-mono text-xs text-zinc-300 flex-1">{a.agentId}</span>
              {a.note && <span className={`text-[11px] ${noteCls(a)}`}>{a.note}</span>}
            </div>
          ))}
        </div>
      )}

      {(swarm.hasConsensus || swarm.hasDisagreement || swarm.isComplete) && (
        <div className="mt-2.5 flex gap-3 text-xs">
          {swarm.hasConsensus && <span className="text-emerald-400 font-medium">Consensus reached</span>}
          {swarm.hasDisagreement && <span className="text-red-400 font-medium">Disagreement</span>}
          {swarm.isComplete && <span className="text-zinc-500">Complete</span>}
        </div>
      )}
    </div>
  );
}
