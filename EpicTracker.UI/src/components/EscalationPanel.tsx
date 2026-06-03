import { useState } from 'react';
import { EpicApi } from '../epicApi';
import type { Epic } from '../types';

export function EscalationPanel({ epic, onUpdated }: { epic: Epic; onUpdated: (e: Epic) => void }) {
  const [busy, setBusy] = useState(false);

  if (!epic.humanInLoop || epic.humanInLoop.isApproved !== null) {
    return null;
  }

  async function handleResolve(isApproved: boolean) {
    setBusy(true);
    try {
      const updated = await EpicApi.approveHumanInLoop(epic.id, isApproved, null);
      onUpdated(updated);
    } catch (err) {
      alert(String(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="rounded-lg border border-amber-300 dark:border-amber-700 bg-amber-50 dark:bg-amber-900/20 p-4">
      <p className="text-sm font-semibold text-amber-800 dark:text-amber-300 mb-1">Human Review Required</p>
      <p className="text-sm text-amber-700 dark:text-amber-400 whitespace-pre-wrap">{epic.humanInLoop.questions}</p>
      <div className="flex items-center gap-2 mt-3">
        <button
          onClick={() => handleResolve(true)}
          disabled={busy}
          className="text-sm px-3 py-1.5 rounded-md bg-emerald-600 text-white hover:bg-emerald-700 disabled:opacity-50 transition-colors"
        >
          Approve
        </button>
        <button
          onClick={() => handleResolve(false)}
          disabled={busy}
          className="text-sm px-3 py-1.5 rounded-md bg-red-600 text-white hover:bg-red-700 disabled:opacity-50 transition-colors"
        >
          Reject
        </button>
      </div>
    </div>
  );
}
