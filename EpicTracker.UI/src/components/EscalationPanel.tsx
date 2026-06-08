import { useState } from 'react';
import { marked } from 'marked';
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
      <div className="flex items-center gap-2">
        <button
          onClick={() => handleResolve(true)}
          disabled={busy}
          className="text-xs px-3 py-1.5 rounded border border-emerald-500/30 bg-emerald-500/10 text-emerald-400 hover:bg-emerald-500/20 disabled:opacity-50 transition-colors font-semibold"
        >
          {busy ? '…' : '✓ Approve'}
        </button>
        <button
          onClick={() => handleResolve(false)}
          disabled={busy}
          className="text-xs px-3 py-1.5 rounded border border-red-500/30 bg-red-500/[0.08] text-red-400 hover:bg-red-500/15 disabled:opacity-50 transition-colors font-semibold"
        >
          {busy ? '…' : '✗ Reject'}
        </button>
      </div>
    </div>
  );
}
