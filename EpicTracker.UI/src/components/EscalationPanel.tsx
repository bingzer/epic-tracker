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

  async function handleConfirm() {
    if (pending === null) return;
    setBusy(true);
    try {
      const updated = await EpicApi.approveHumanInLoop(epic.id, pending, feedback.trim() || null);
      onUpdated(updated);
    } catch (err) {
      alert(String(err));
      setPending(null);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="border-l-2 border-amber-500/50 bg-white/[0.02] rounded-r-lg pl-4 pr-3 py-3 space-y-2.5">
      <p className="text-[10px] font-semibold tracking-widest uppercase text-zinc-500">Human Review Required</p>
      <div
        className="prose prose-sm prose-invert max-w-none text-zinc-400 [&_p]:leading-relaxed [&_ul]:mt-1 [&_li]:my-0.5"
        dangerouslySetInnerHTML={{ __html: marked.parse(epic.humanInLoop.questions) as string }}
      />
      <textarea
        value={feedback}
        onChange={e => setFeedback(e.target.value)}
        placeholder="Reason (optional)…"
        rows={2}
        className="w-full text-xs rounded border border-zinc-700 bg-white/5 text-zinc-200 px-2 py-1.5 resize-none focus:outline-none focus:ring-1 focus:ring-zinc-500 placeholder:text-zinc-600"
      />

      {pending === null ? (
        <div className="flex items-center gap-2">
          <button
            onClick={() => setPending(true)}
            disabled={busy}
            className="text-xs px-3 py-1.5 rounded border border-emerald-500/30 bg-emerald-500/10 text-emerald-400 hover:bg-emerald-500/20 disabled:opacity-50 transition-colors font-semibold"
          >
            ✓ Approve
          </button>
          <button
            onClick={() => setPending(false)}
            disabled={busy}
            className="text-xs px-3 py-1.5 rounded border border-red-500/30 bg-red-500/[0.08] text-red-400 hover:bg-red-500/15 disabled:opacity-50 transition-colors font-semibold"
          >
            ✗ Reject
          </button>
        </div>
      ) : (
        <div className="flex items-center gap-2">
          <span className="text-xs text-zinc-400">
            {pending ? 'Approve' : 'Reject'} — are you sure?
          </span>
          <button
            onClick={handleConfirm}
            disabled={busy}
            className={`text-xs px-3 py-1.5 rounded font-semibold disabled:opacity-50 transition-colors ${pending ? 'border border-emerald-500/30 bg-emerald-500/10 text-emerald-400 hover:bg-emerald-500/20' : 'border border-red-500/30 bg-red-500/[0.08] text-red-400 hover:bg-red-500/15'}`}
          >
            {busy ? '…' : 'Yes'}
          </button>
          <button
            onClick={() => setPending(null)}
            disabled={busy}
            className="text-xs px-3 py-1.5 rounded border border-zinc-700 bg-white/[0.04] text-zinc-400 hover:text-zinc-200 disabled:opacity-50 transition-colors"
          >
            No
          </button>
        </div>
      )}
    </div>
  );
}
