import type { EpicAudit } from '../types';

export function AuditLogPanel({ entries }: { entries: EpicAudit[] }) {
  if (entries.length === 0) {
    return <p className="text-sm text-gray-400 dark:text-zinc-500 py-4 text-center">No audit events yet.</p>;
  }

  return (
    <ul className="divide-y divide-gray-100 dark:divide-zinc-800">
      {[...entries].reverse().map(entry => (
        <li key={entry.id} className="py-2.5 flex gap-3 items-start">
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <span className="text-xs font-semibold text-blue-600 dark:text-blue-400">
                {entry.fromState.replace(/_/g, ' ')}
              </span>
              <span className="text-xs text-gray-400 dark:text-zinc-600">→</span>
              <span className="text-xs font-semibold text-emerald-600 dark:text-emerald-400">
                {entry.toState.replace(/_/g, ' ')}
              </span>
            </div>
            <div className="flex gap-2 mt-0.5 text-xs text-gray-400 dark:text-zinc-600">
              {entry.epicAgentId && <span>agent: {entry.epicAgentId}</span>}
              <span>{new Date(entry.timestamp).toLocaleString()}</span>
            </div>
            {entry.epicAgentInstruction && (
              <p className="text-xs text-gray-600 dark:text-zinc-400 mt-0.5 whitespace-pre-wrap">{entry.epicAgentInstruction}</p>
            )}
          </div>
        </li>
      ))}
    </ul>
  );
}
