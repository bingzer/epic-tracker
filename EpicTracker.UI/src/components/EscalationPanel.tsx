import { useState } from 'react';
import { EpicApi } from '../epicApi';
import type { Epic } from '../types';

export function EscalationPanel({ epic, onUpdated }: { epic: Epic; onUpdated: (e: Epic) => void }) {
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState('');

  if (!epic.humanInLoop || epic.humanInLoop.isApproved !== null) {
    return null;
  }

  async function handleResolve(isApproved: boolean) {
    setBusy(true);
    try {
      const updated = await EpicApi.approveHumanInLoop(epic.id, isApproved, feedback.trim() || null);
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
      <p className="text-sm text-amber-700 dark:text-amber-400 whitespace-pre-wrap mb-3">{epic.humanInLoop.questions}</p>
      <textarea
        value={feedback}
        onChange={e => setFeedback(e.target.value)}
        onKeyDown={e => {
          if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
            e.preventDefault();
            handleResolve(true);
          }
        }}
        placeholder="Optional feedback… (Ctrl+Enter to approve)"
        rows={3}
        className="w-full text-sm rounded-md border border-amber-200 dark:border-amber-700 bg-white dark:bg-zinc-800 text-gray-800 dark:text-zinc-200 px-3 py-2 resize-none focus:outline-none focus:ring-1 focus:ring-amber-400 mb-3"
      />
      <div className="flex items-center gap-2">
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
        <span className="text-xs text-amber-600 dark:text-amber-500">Ctrl+Enter to approve</span>
      </div>
    </div>
  );
}
