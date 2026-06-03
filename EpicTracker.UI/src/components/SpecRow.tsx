import { useState } from 'react';
import type { Spec } from '../types';
import { StateBadge } from './StateBadge';
import { StateBreadcrumb } from './StateBreadcrumb';
import { SpecApi } from '../epicApi';

interface Props {
  spec: Spec;
  onApproveHumanInLoop: (specId: string, isApproved: boolean, feedback: string | null) => void;
}

export function SpecRow({ spec, onApproveHumanInLoop }: Props) {
  const [feedback, setFeedback] = useState('');
  const needsHumanReview = spec.humanInLoop !== null && spec.humanInLoop.isApproved === null;

  function handleResolve(isApproved: boolean) {
    onApproveHumanInLoop(spec.id, isApproved, feedback.trim() || null);
    setFeedback('');
  }

  return (
    <tr className="border-t border-gray-100 dark:border-zinc-800 align-top">
      <td className="px-3 py-2 text-sm font-mono text-gray-800 dark:text-zinc-200">
        {spec.specDocPath ? (
          <span title={spec.specDocPath}>{spec.id}</span>
        ) : (
          spec.id
        )}
      </td>
      <td className="px-3 py-2 text-sm text-gray-600 dark:text-zinc-400">{spec.assignedAgentId}</td>
      <td className="px-3 py-2">
        <div className="flex items-center gap-1.5 flex-wrap">
          <StateBadge state={spec.currentStateName} />
          {needsHumanReview && (
            <span className="text-xs px-1.5 py-0.5 rounded bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300 font-medium">
              human review
            </span>
          )}
        </div>
        {needsHumanReview && spec.humanInLoop && (
          <p className="text-xs text-amber-600 dark:text-amber-400 mt-0.5">{spec.humanInLoop.questions}</p>
        )}
      </td>
      <td className="px-3 py-2">
        <StateBreadcrumb state={spec.currentStateName} type="spec" />
      </td>
      <td className="px-3 py-2">
        {needsHumanReview && (
          <div className="flex flex-col gap-1.5">
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
              rows={2}
              className="text-xs rounded border border-amber-200 dark:border-amber-700 bg-white dark:bg-zinc-800 text-gray-800 dark:text-zinc-200 px-2 py-1 resize-none focus:outline-none focus:ring-1 focus:ring-amber-400 w-48"
            />
            <div className="flex items-center gap-1.5">
              <button
                onClick={() => handleResolve(true)}
                className="text-xs px-2 py-1 rounded bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300 hover:bg-emerald-200 dark:hover:bg-emerald-900/60 transition-colors"
              >
                Approve
              </button>
              <button
                onClick={() => handleResolve(false)}
                className="text-xs px-2 py-1 rounded bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300 hover:bg-red-200 dark:hover:bg-red-900/60 transition-colors"
              >
                Reject
              </button>
            </div>
          </div>
        )}
      </td>
    </tr>
  );
}

export { SpecApi };
