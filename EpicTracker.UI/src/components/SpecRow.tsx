import { useState } from 'react';
import type { Spec } from '../types';
import { StateBadge } from './StateBadge';
import { StateBreadcrumb } from './StateBreadcrumb';
import { SpecApi } from '../epicApi';

const SPEC_STATES = ['drafting', 'ready', 'coding', 'ac', 'code_review', 'human_in_loop', 'done'] as const;

interface Props {
  spec: Spec;
  onApproveHumanInLoop: (specId: string, isApproved: boolean, feedback: string | null) => void;
  onForceState: (specId: string, stateName: string) => void;
  onViewDoc: (path: string) => void;
  onUpdated: () => void;
}

const FLAG_CONFIGS: { field: keyof Spec; label: string }[] = [
  { field: 'isSpecDrafted', label: 'Spec Drafted' },
  { field: 'isSpecApproved', label: 'Spec Approved' },
  { field: 'isCodeDone', label: 'Code Done' },
  { field: 'isAcPassed', label: 'AC Passed' },
  { field: 'isCodeReviewApproved', label: 'Code Review Approved' },
  { field: 'codeReviewRequired', label: 'Code Review Required' },
];

function pillCls(active: boolean) {
  if (active) {
    return 'px-2 py-0.5 rounded-full text-xs font-medium cursor-pointer transition-colors bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300 hover:bg-emerald-200 dark:hover:bg-emerald-900/60';
  }
  return 'px-2 py-0.5 rounded-full text-xs font-medium cursor-pointer transition-colors bg-gray-100 text-gray-500 dark:bg-zinc-800 dark:text-zinc-400 hover:bg-gray-200 dark:hover:bg-zinc-700';
}

export function SpecRow({ spec, onApproveHumanInLoop, onForceState, onViewDoc, onUpdated }: Props) {
  const [feedback, setFeedback] = useState('');
  const [codingNow, setCodingNow] = useState(false);
  const [specDocPath, setSpecDocPath] = useState(spec.specDocPath ?? '');
  const [assignedAgentName, setassignedAgentName] = useState(spec.assignedAgentName ?? '');
  const [reviewerAgentName, setReviewerAgentId] = useState(spec.reviewerAgentName ?? '');

  const needsHumanReview = spec.humanInLoop !== null && spec.humanInLoop.isApproved === null;

  function handleForceState(stateName: string) {
    if (!window.confirm(`Force spec "${spec.id}" to state "${stateName}"?`)) return;
    onForceState(spec.id, stateName);
  }

  function handleResolve(isApproved: boolean) {
    onApproveHumanInLoop(spec.id, isApproved, feedback.trim() || null);
    setFeedback('');
  }

  async function handleToggleFlag(field: keyof Spec) {
    const current = spec[field] as boolean;
    try {
      await SpecApi.update(spec.id, { ...spec, [field]: !current });
      onUpdated();
    } catch (e) {
      alert(String(e));
    }
  }

  async function handleCodeNow() {
    setCodingNow(true);
    try {
      await SpecApi.codeNow(spec.id);
      onUpdated();
    } catch (e) {
      alert(String(e));
    } finally {
      setCodingNow(false);
    }
  }

  async function handleAbandon() {
    const msg = spec.isAbandoned
      ? `Restore spec "${spec.id}"?`
      : `Abandon spec "${spec.id}"? This will hide it from the board.`;
    if (!window.confirm(msg)) return;
    try {
      await SpecApi.update(spec.id, { ...spec, isAbandoned: !spec.isAbandoned });
      onUpdated();
    } catch (e) {
      alert(String(e));
    }
  }

  async function saveTextField(field: keyof Spec, value: string) {
    try {
      await SpecApi.update(spec.id, { ...spec, [field]: value || null });
      onUpdated();
    } catch (e) {
      alert(String(e));
    }
  }

  return (
    <>
      <tr className="border-t border-gray-100 dark:border-zinc-800 align-top">
        <td className="px-3 py-2 text-sm font-mono text-gray-800 dark:text-zinc-200">
          <span className="flex items-center gap-1.5">
            <span title={spec.specDocPath ?? undefined}>{spec.id}</span>
            {spec.specDocPath && (
              <button
                onClick={() => onViewDoc(spec.specDocPath!)}
                className="text-xs text-indigo-500 hover:text-indigo-700 dark:text-indigo-400 dark:hover:text-indigo-300"
              >
                View
              </button>
            )}
          </span>
        </td>
        <td className="px-3 py-2 text-sm text-gray-600 dark:text-zinc-400">{spec.assignedAgentName}</td>
        <td className="px-3 py-2">
          <div className="flex items-center gap-1.5 flex-wrap">
            <StateBadge state={spec.currentStateName} />
            {spec.currentStateName === 'ready' && (
              <button
                onClick={handleCodeNow}
                disabled={codingNow}
                className="text-xs px-2 py-0.5 rounded-full font-medium transition-colors bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300 hover:bg-emerald-200 dark:hover:bg-emerald-900/60 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {codingNow ? '…' : '▶ Code Now'}
              </button>
            )}
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
          <select
            defaultValue=""
            onChange={e => { if (e.target.value) { handleForceState(e.target.value); e.target.value = ''; } }}
            className="text-xs rounded border border-gray-200 dark:border-zinc-700 bg-gray-50 dark:bg-zinc-800 text-gray-700 dark:text-zinc-300 px-1.5 py-1 focus:outline-none focus:ring-1 focus:ring-indigo-500 w-36"
          >
            <option value="" disabled>Force state →</option>
            {SPEC_STATES.map(s => (
              <option key={s} value={s}>{s}</option>
            ))}
          </select>
          {needsHumanReview && (
            <div className="flex flex-col gap-1.5 mt-1.5">
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

      <tr className="bg-gray-50 dark:bg-zinc-800/30">
        <td colSpan={5} className="pl-8 pr-4 pb-3 pt-1">
          <div className="space-y-2">
            <div className="flex flex-wrap gap-1.5 items-center">
              {FLAG_CONFIGS.map(cfg => (
                <button
                  key={cfg.field}
                  onClick={() => handleToggleFlag(cfg.field)}
                  className={pillCls(spec[cfg.field] as boolean)}
                >
                  {cfg.label}
                </button>
              ))}
              <button
                onClick={handleAbandon}
                className={`px-2 py-0.5 rounded-full text-xs font-medium transition-colors ${
                  spec.isAbandoned
                    ? 'bg-gray-100 text-gray-600 dark:bg-zinc-800 dark:text-zinc-300 hover:bg-gray-200 dark:hover:bg-zinc-700'
                    : 'bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300 hover:bg-red-200 dark:hover:bg-red-900/60'
                }`}
              >
                {spec.isAbandoned ? 'Restore' : 'Abandon'}
              </button>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-2">
              <div>
                <label className="text-xs font-medium text-gray-500 dark:text-zinc-400 block mb-0.5">Spec Doc Path</label>
                <input
                  value={specDocPath}
                  onChange={e => setSpecDocPath(e.target.value)}
                  onBlur={() => saveTextField('specDocPath', specDocPath)}
                  onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); saveTextField('specDocPath', specDocPath); } }}
                  placeholder="/path/to/spec.md"
                  className="w-full text-xs rounded border border-gray-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 px-2 py-1 text-gray-900 dark:text-zinc-100 focus:outline-none focus:ring-1 focus:ring-blue-500"
                />
              </div>
              <div>
                <label className="text-xs font-medium text-gray-500 dark:text-zinc-400 block mb-0.5">Assigned Agent</label>
                <input
                  value={assignedAgentName}
                  onChange={e => setassignedAgentName(e.target.value)}
                  onBlur={() => saveTextField('assignedAgentName', assignedAgentName)}
                  onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); saveTextField('assignedAgentName', assignedAgentName); } }}
                  placeholder="agent-id"
                  className="w-full text-xs rounded border border-gray-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 px-2 py-1 text-gray-900 dark:text-zinc-100 focus:outline-none focus:ring-1 focus:ring-blue-500"
                />
              </div>
              <div>
                <label className="text-xs font-medium text-gray-500 dark:text-zinc-400 block mb-0.5">Reviewer Agent</label>
                <input
                  value={reviewerAgentName}
                  onChange={e => setReviewerAgentId(e.target.value)}
                  onBlur={() => saveTextField('reviewerAgentName', reviewerAgentName)}
                  onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); saveTextField('reviewerAgentName', reviewerAgentName); } }}
                  placeholder="reviewer-agent-id"
                  className="w-full text-xs rounded border border-gray-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 px-2 py-1 text-gray-900 dark:text-zinc-100 focus:outline-none focus:ring-1 focus:ring-blue-500"
                />
              </div>
            </div>
          </div>
        </td>
      </tr>
    </>
  );
}

export { SpecApi };
