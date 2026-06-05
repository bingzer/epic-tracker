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
        onKeyDown={e => {
          if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
            e.preventDefault();
            handleResolve(true);
          }
        }}
        placeholder="Optional feedback… (Ctrl+Enter to approve)"
        rows={3}
        className="w-full text-sm rounded-md border border-amber-500/20 bg-white/5 text-zinc-200 px-3 py-2 resize-none focus:outline-none focus:ring-1 focus:ring-amber-400 mb-3 placeholder:text-zinc-600"
      />
      <div className="flex items-center gap-2">
        <button
          onClick={() => handleResolve(true)}
          disabled={busy}
          className="flex-1 text-sm px-3 py-1.5 rounded-md border border-emerald-500/30 bg-emerald-500/10 text-emerald-400 hover:bg-emerald-500/20 disabled:opacity-50 transition-colors font-semibold"
        >
          ✓ Approve
        </button>
        <button
          onClick={() => handleResolve(false)}
          disabled={busy}
          className="text-sm px-4 py-1.5 rounded-md border border-red-500/30 bg-red-500/[0.08] text-red-400 hover:bg-red-500/15 disabled:opacity-50 transition-colors font-semibold"
        >
          ✗ Reject
        </button>
      </div>
    </div>
  );
}
