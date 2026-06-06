import { useState } from 'react';
import { marked } from 'marked';
import { EpicApi } from '../epicApi';
import type { Epic } from '../types';

export function EscalationPanel({ epic, onUpdated }: { epic: Epic; onUpdated: (e: Epic) => void }) {
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState('');
  const [pending, setPending] = useState<boolean | null>(null);

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
      setPending(null);
    }
  }

  return (
    <div className="rounded-lg border border-amber-500/30 bg-amber-500/[0.07] p-4">
      <div className="flex items-center gap-2 mb-3">
        <span className="text-[11px] font-bold text-amber-300 uppercase tracking-widest">⚠ Human Review Required</span>
      </div>
      <div
        className="prose prose-sm prose-invert prose-amber max-w-none mb-3 text-amber-200/80 [&_p]:leading-relaxed [&_ul]:mt-1 [&_li]:my-0.5"
        dangerouslySetInnerHTML={{ __html: marked.parse(epic.humanInLoop.questions) as string }}
      />
      <textarea
        value={feedback}
        onChange={e => setFeedback(e.target.value)}
        placeholder="Optional feedback…"
        rows={3}
        className="w-full text-sm rounded-md border border-amber-500/20 bg-white/5 text-zinc-200 px-3 py-2 resize-none focus:outline-none focus:ring-1 focus:ring-amber-400 mb-3 placeholder:text-zinc-600"
      />
      {pending === null ? (
        <div className="flex items-center gap-2">
          <button
            onClick={() => setPending(true)}
            disabled={busy}
            className="flex-1 text-sm px-3 py-1.5 rounded-md border border-emerald-500/30 bg-emerald-500/10 text-emerald-400 hover:bg-emerald-500/20 disabled:opacity-50 transition-colors font-semibold"
          >
            ✓ Approve
          </button>
          <button
            onClick={() => setPending(false)}
            disabled={busy}
            className="text-sm px-4 py-1.5 rounded-md border border-red-500/30 bg-red-500/[0.08] text-red-400 hover:bg-red-500/15 disabled:opacity-50 transition-colors font-semibold"
          >
            ✗ Reject
          </button>
        </div>
      ) : (
        <div className="flex items-center gap-2">
          <span className="text-xs text-zinc-400 flex-1">
            Confirm <span className={pending ? 'text-emerald-400 font-semibold' : 'text-red-400 font-semibold'}>{pending ? 'approval' : 'rejection'}</span>?
          </span>
          <button
            onClick={() => handleResolve(pending)}
            disabled={busy}
            className={`text-sm px-4 py-1.5 rounded-md font-semibold disabled:opacity-50 transition-colors ${pending ? 'bg-emerald-600 text-white hover:bg-emerald-500' : 'bg-red-600 text-white hover:bg-red-500'}`}
          >
            {busy ? '…' : 'Confirm'}
          </button>
          <button
            onClick={() => setPending(null)}
            disabled={busy}
            className="text-sm px-3 py-1.5 rounded-md border border-zinc-700 text-zinc-400 hover:text-zinc-200 disabled:opacity-50 transition-colors"
          >
            Cancel
          </button>
        </div>
      )}
    </div>
  );
}
