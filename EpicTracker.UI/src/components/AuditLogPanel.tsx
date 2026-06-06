import { useState } from 'react';
import type { AuditLog } from '../types';

const ACTION_COLORS: Record<string, string> = {
  'epic.created':              'text-cyan-400',
  'epic.move.next':            'text-indigo-400',
  'epic.human.loop':           'text-amber-400',
  'epic.human.loop.resolved':  'text-amber-300',
  'epic.swarm.raised':         'text-violet-400',
  'epic.swarm.vote':           'text-violet-300',
  'epic.nudged':               'text-zinc-400',
  'epic.force.state':          'text-orange-400',
  'epic.updated':              'text-zinc-400',
  'spec.created':              'text-emerald-400',
  'spec.updated':              'text-emerald-300',
  'spec.move.next':            'text-teal-400',
  'spec.force.state':          'text-orange-300',
  'spec.human.loop.resolved':  'text-amber-300',
};

function formatMessage(message: string | null): React.ReactNode {
  if (!message) return null;

  try {
    const parsed = JSON.parse(message);
    return (
      <pre className="mt-1.5 text-[10px] font-mono text-zinc-500 whitespace-pre-wrap leading-relaxed bg-white/[0.02] rounded px-2 py-1.5 border border-zinc-800">
        {JSON.stringify(parsed, null, 2)}
      </pre>
    );
  } catch {
    return <p className="mt-1 text-[11px] text-zinc-500">{message}</p>;
  }
}

function AuditEntry({ entry }: { entry: AuditLog }) {
  const [expanded, setExpanded] = useState(false);
  const actionColor = ACTION_COLORS[entry.action] ?? 'text-zinc-400';
  const hasMessage = !!entry.message;

  return (
    <div className="py-2 border-b border-zinc-800 last:border-b-0 text-xs">
      <div
        className={`flex items-start gap-3 ${hasMessage ? 'cursor-pointer hover:opacity-80' : ''}`}
        onClick={() => hasMessage && setExpanded(v => !v)}
      >
        <span className="font-mono text-zinc-600 whitespace-nowrap flex-shrink-0 pt-px">
          {new Date(entry.timestamp).toLocaleString()}
        </span>

        <span className={`font-mono font-semibold flex-shrink-0 pt-px ${actionColor}`}>
          {entry.action}
        </span>

        <div className="flex flex-wrap gap-x-3 gap-y-0.5 flex-1 min-w-0 pt-px">
          <span className="text-zinc-500">
            state: <span className="text-zinc-300">{entry.epicState}</span>
            {entry.specState && <> → <span className="text-teal-400">{entry.specState}</span></>}
          </span>

          {entry.specId && (
            <span className="text-zinc-600 font-mono truncate max-w-[160px]">{entry.specId}</span>
          )}

          {entry.actor && (
            <span className="text-zinc-600">by <span className="text-zinc-400">{entry.actor}</span></span>
          )}
        </div>

        {hasMessage && (
          <span className="text-zinc-700 text-[9px] flex-shrink-0 pt-px">{expanded ? '▲' : '▼'}</span>
        )}
      </div>

      {expanded && formatMessage(entry.message)}
    </div>
  );
}

export function AuditLogPanel({ entries }: { entries: AuditLog[] }) {
  const [showAll, setShowAll] = useState(false);
  const reversed = [...entries].reverse();
  const visible = showAll ? reversed : reversed.slice(0, 8);

  if (entries.length === 0) {
    return <p className="text-xs text-zinc-600 py-4 text-center">No activity yet.</p>;
  }

  return (
    <div>
      {visible.map(entry => (
        <AuditEntry key={entry.id} entry={entry} />
      ))}

      {entries.length > 8 && (
        <button
          onClick={() => setShowAll(v => !v)}
          className="mt-2 text-[11px] text-indigo-400 hover:text-indigo-300"
        >
          {showAll ? 'Show less' : `View all ${entries.length} →`}
        </button>
      )}
    </div>
  );
}
