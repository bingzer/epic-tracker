// TODO: unused — superseded by SpecTableRow in EpicDetailPage, delete when confirmed safe
import { useState } from 'react';
import type { Spec } from '../types';
import { StateBadge } from './StateBadge';
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
  { field: 'isCodeReviewRequired', label: 'Code Review Required' },
];

function flagPillCls(active: boolean) {
  if (active) {
    return 'px-2 py-0.5 rounded-full text-xs font-medium cursor-pointer transition-colors bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300 hover:bg-emerald-200 dark:hover:bg-emerald-900/60';
  }
  return 'px-2 py-0.5 rounded-full text-xs font-medium cursor-pointer transition-colors bg-gray-100 text-gray-500 dark:bg-zinc-800 dark:text-zinc-400 hover:bg-gray-200 dark:hover:bg-zinc-700';
}

function calcProgress(spec: Spec): number {
  const flags = [
    spec.isSpecDrafted,
    spec.isCodeDone,
    spec.isAcPassed === true,
    spec.isCodeReviewRequired ? spec.isCodeReviewApproved === true : true,
  ];
  return flags.filter(Boolean).length / flags.length;
}

export function SpecRow({ spec, onApproveHumanInLoop, onForceState, onViewDoc, onUpdated }: Props) {
  const [feedback, setFeedback] = useState('');
  const [codingNow, setCodingNow] = useState(false);
  const [detailsOpen, setDetailsOpen] = useState(false);
  const [specDocPath, setSpecDocPath] = useState(spec.specDocPath ?? '');
  const [assignedAgentName, setAssignedAgentName] = useState(spec.assignedAgentName ?? '');
  const [reviewerAgentName, setReviewerAgentName] = useState(spec.reviewerAgentName ?? '');

  const needsHumanReview = spec.humanInLoop !== null && spec.humanInLoop.isApproved === null;
  const progress = calcProgress(spec);
  const progressPct = Math.round(progress * 100);
  const isDone = spec.currentStateName === 'done';

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
    <div
      className="rounded-xl border border-gray-100 dark:border-zinc-800 bg-white dark:bg-zinc-900 p-4 transition-colors hover:border-gray-200 dark:hover:border-zinc-700"
    >
      {/* Header row: title/id + state badge */}
      <div className="flex items-start justify-between gap-3 mb-3">
        <div className="min-w-0">
          <div className="text-sm font-semibold text-gray-900 dark:text-zinc-100 font-mono truncate">
            {spec.id}
          </div>
          {spec.specDocPath && (
            <button
              onClick={() => onViewDoc(spec.specDocPath!)}
              className="text-xs text-indigo-500 hover:text-indigo-700 dark:text-indigo-400 dark:hover:text-indigo-300 mt-0.5"
            >
              View spec doc
            </button>
          )}
        </div>
        <StateBadge state={spec.currentStateName} />
      </div>

      {/* Agent row */}
      <div className="flex items-center gap-2 mb-3 flex-wrap">
        {spec.assignedAgentName && (
          <span className="inline-flex items-center gap-1 text-xs font-mono bg-indigo-50 dark:bg-indigo-900/20 border border-indigo-100 dark:border-indigo-800 text-indigo-600 dark:text-indigo-400 px-2 py-0.5 rounded-full">
            {spec.assignedAgentName}
            <a
              href={`openterm:${spec.assignedAgentName}`}
              className="text-indigo-400 hover:text-indigo-600 dark:hover:text-indigo-300 leading-none"
              title="Open chat"
            >↗</a>
          </span>
        )}
        {spec.reviewerAgentName && (
          <span className="inline-flex items-center gap-1 text-xs font-mono bg-orange-50 dark:bg-orange-900/20 border border-orange-100 dark:border-orange-800 text-orange-600 dark:text-orange-400 px-2 py-0.5 rounded-full">
            <span className="text-gray-400 dark:text-zinc-500 font-sans">rev:</span>
            {spec.reviewerAgentName}
          </span>
        )}
      </div>

      {/* Progress bar */}
      <div className="flex items-center gap-2 mb-3">
        <div className="flex-1 h-1 rounded-full bg-gray-100 dark:bg-zinc-800 overflow-hidden">
          <div
            className="h-full rounded-full bg-gradient-to-r from-indigo-500 to-cyan-400 transition-all"
            style={{ width: `${progressPct}%` }}
          />
        </div>
        <span className="text-xs text-gray-400 dark:text-zinc-500 w-10 text-right shrink-0">
          {isDone ? 'Done' : progressPct === 0 ? '—' : `${progressPct}%`}
        </span>
      </div>

      {/* Human-in-loop panel */}
      {needsHumanReview && spec.humanInLoop && (
        <div className="rounded-lg border border-amber-200 dark:border-amber-700 bg-amber-50 dark:bg-amber-900/20 p-3 mb-3">
          <p className="text-xs font-semibold text-amber-700 dark:text-amber-300 mb-1">⚠ Human Review Required</p>
          <p className="text-xs text-amber-600 dark:text-amber-400 mb-2">{spec.humanInLoop.questions}</p>
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
            className="w-full text-xs rounded border border-amber-200 dark:border-amber-700 bg-white dark:bg-zinc-800 text-gray-800 dark:text-zinc-200 px-2 py-1 resize-none focus:outline-none focus:ring-1 focus:ring-amber-400 mb-2"
          />
          <div className="flex gap-1.5">
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

      {/* Action buttons */}
      <div className="flex items-center gap-2 flex-wrap">
        {spec.currentStateName === 'ready' && (
          <button
            onClick={handleCodeNow}
            disabled={codingNow}
            className="text-xs px-2.5 py-1 rounded-md font-semibold transition-colors bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300 hover:bg-emerald-200 dark:hover:bg-emerald-900/60 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {codingNow ? '…' : '▶ Code Now'}
          </button>
        )}
        <select
          defaultValue=""
          onChange={e => { if (e.target.value) { handleForceState(e.target.value); e.target.value = ''; } }}
          className="text-xs rounded border border-gray-200 dark:border-zinc-700 bg-gray-50 dark:bg-zinc-800 text-gray-600 dark:text-zinc-400 px-1.5 py-1 focus:outline-none focus:ring-1 focus:ring-indigo-500"
        >
          <option value="" disabled>Force state →</option>
          {SPEC_STATES.map(s => (
            <option key={s} value={s}>{s}</option>
          ))}
        </select>
        <button
          onClick={() => setDetailsOpen(o => !o)}
          className="text-xs text-gray-400 dark:text-zinc-500 hover:text-gray-600 dark:hover:text-zinc-300 ml-auto"
        >
          {detailsOpen ? 'Hide details ▲' : 'Details ▼'}
        </button>
      </div>

      {/* Collapsible details */}
      {detailsOpen && (
        <div className="mt-3 pt-3 border-t border-gray-100 dark:border-zinc-800 space-y-3">
          <div className="flex flex-wrap gap-1.5">
            {FLAG_CONFIGS.map(cfg => (
              <button
                key={cfg.field}
                onClick={() => handleToggleFlag(cfg.field)}
                className={flagPillCls(spec[cfg.field] as boolean)}
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
                onChange={e => setAssignedAgentName(e.target.value)}
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
                onChange={e => setReviewerAgentName(e.target.value)}
                onBlur={() => saveTextField('reviewerAgentName', reviewerAgentName)}
                onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); saveTextField('reviewerAgentName', reviewerAgentName); } }}
                placeholder="reviewer-agent-id"
                className="w-full text-xs rounded border border-gray-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 px-2 py-1 text-gray-900 dark:text-zinc-100 focus:outline-none focus:ring-1 focus:ring-blue-500"
              />
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export { SpecApi };
