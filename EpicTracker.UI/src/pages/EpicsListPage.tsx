import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { EpicApi, AgentApi, type CreateEpicPayload } from '../epicApi';
import type { Epic } from '../types';
import { StateBadge } from '../components/StateBadge';
import { useSignalR } from '../hooks/useSignalR';

function useAgentNames() {
  const [names, setNames] = useState<string[]>([]);
  useEffect(() => {
    AgentApi.list()
      .then(list => setNames(list.map(a => a.sessionName).sort()))
      .catch(() => {});
  }, []);
  return names;
}

function AgentSelect({ value, onChange, placeholder, required, className }: {
  value: string;
  onChange: (v: string) => void;
  placeholder?: string;
  required?: boolean;
  className?: string;
}) {
  const names = useAgentNames();
  return (
    <select
      required={required}
      value={value}
      onChange={e => onChange(e.target.value)}
      className={className}
    >
      <option value="">{placeholder ?? 'Select agent…'}</option>
      {names.map(n => <option key={n} value={n}>{n}</option>)}
    </select>
  );
}

function Toggle({ id, checked, onChange, label, sublabel }: {
  id: string;
  checked: boolean;
  onChange: (v: boolean) => void;
  label: string;
  sublabel?: string;
}) {
  return (
    <label htmlFor={id} className="flex items-start gap-3 cursor-pointer group">
      <div className="relative mt-0.5 shrink-0">
        <input
          type="checkbox"
          id={id}
          checked={checked}
          onChange={e => onChange(e.target.checked)}
          className="sr-only peer"
        />
        <div className={`w-9 h-5 rounded-full transition-colors duration-200 ${checked ? 'bg-blue-600' : 'bg-gray-200 dark:bg-zinc-700'} peer-focus-visible:ring-2 peer-focus-visible:ring-blue-500 peer-focus-visible:ring-offset-1`} />
        <div className={`absolute top-0.5 left-0.5 w-4 h-4 rounded-full bg-white shadow-sm transition-transform duration-200 ${checked ? 'translate-x-4' : 'translate-x-0'}`} />
      </div>
      <div>
        <div className="text-sm font-medium text-gray-800 dark:text-zinc-200 leading-tight">{label}</div>
        {sublabel && <div className="text-xs text-gray-400 dark:text-zinc-500 mt-0.5">{sublabel}</div>}
      </div>
    </label>
  );
}

function CreateEpicModal({ onCreated }: { onCreated: (epic: Epic) => void; }) {
  const [open, setOpen] = useState(false);
  const [name, setName] = useState('');
  const [epicAgentName, setEpicAgentName] = useState('');
  const [brief, setBrief] = useState('');
  const [agentInput, setAgentInput] = useState('');
  const [codingAgentNames, setCodingAgentNames] = useState<string[]>([]);
  const [needsMockup, setNeedsMockup] = useState(false);
  const [reviewerAgentName, setReviewerAgentName] = useState('');
  const [isACRequired, setIsACRequired] = useState(true);
  const [isCodeReviewRequired, setIsCodeReviewRequired] = useState(false);
  const [codeReviewOverridden, setCodeReviewOverridden] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function handleReviewerChange(value: string) {
    setReviewerAgentName(value);
    if (!codeReviewOverridden) {
      setIsCodeReviewRequired(value.trim().length > 0);
    }
  }

  function handleCodeReviewToggle(value: boolean) {
    setIsCodeReviewRequired(value);
    setCodeReviewOverridden(true);
  }

  function reset() {
    setName('');
    setEpicAgentName('');
    setBrief('');
    setAgentInput('');
    setCodingAgentNames([]);
    setNeedsMockup(false);
    setReviewerAgentName('');
    setIsACRequired(true);
    setIsCodeReviewRequired(false);
    setCodeReviewOverridden(false);
    setError(null);
  }

  function handleClose() {
    reset();
    setOpen(false);
  }

  function handleAddAgent() {
    const name = agentInput.trim();
    if (!name || codingAgentNames.includes(name)) return;
    setCodingAgentNames([...codingAgentNames, name]);
    setAgentInput('');
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      const payload: CreateEpicPayload = {
        epicAgentName: epicAgentName.trim(),
        brief: brief.trim(),
        name: name.trim() || undefined,
        codingAgentNames: codingAgentNames.length > 0 ? codingAgentNames : undefined,
        needsMockup,
        reviewerAgentName: reviewerAgentName.trim() || undefined,
        isACRequired,
        isCodeReviewRequired,
      };
      const epic = await EpicApi.create(payload);
      onCreated(epic);
      reset();
      setOpen(false);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to create epic');
    } finally {
      setSubmitting(false);
    }
  }

  const inputCls = "w-full text-sm rounded-lg border border-gray-200 dark:border-zinc-700 bg-white dark:bg-zinc-800/60 px-3 py-2 text-gray-900 dark:text-zinc-100 placeholder:text-gray-400 dark:placeholder:text-zinc-500 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-shadow";
  const labelCls = "block text-xs font-medium text-gray-500 dark:text-zinc-400 mb-1.5 uppercase tracking-wide";

  return (
    <>
      <button
        onClick={() => setOpen(true)}
        className="inline-flex items-center gap-1.5 text-sm font-medium px-3 py-1.5 rounded-lg bg-blue-600 text-white hover:bg-blue-700 transition-colors"
      >
        <span className="text-base leading-none">+</span>
        New epic
      </button>

      {open && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center p-4"
          onClick={e => { if (e.target === e.currentTarget) handleClose(); }}
        >
          <div className="absolute inset-0 bg-black/40 dark:bg-black/60 backdrop-blur-sm" />
          <div className="relative w-full max-w-xl bg-white dark:bg-zinc-900 rounded-2xl shadow-2xl border border-gray-200 dark:border-zinc-700/80 overflow-hidden">
            <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100 dark:border-zinc-800">
              <h2 className="text-base font-semibold text-gray-900 dark:text-zinc-100">New epic</h2>
              <button
                type="button"
                onClick={handleClose}
                className="text-gray-400 dark:text-zinc-500 hover:text-gray-600 dark:hover:text-zinc-300 transition-colors p-1 rounded-lg hover:bg-gray-100 dark:hover:bg-zinc-800"
                aria-label="Close"
              >
                <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
                  <path d="M3 3l10 10M13 3L3 13" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round"/>
                </svg>
              </button>
            </div>

            <form onSubmit={handleSubmit}>
              <div className="px-6 py-5 space-y-4 max-h-[70vh] overflow-y-auto">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className={labelCls}>Name</label>
                    <input value={name} onChange={e => setName(e.target.value)} placeholder="Price Quote" className={inputCls} />
                  </div>
                  <div>
                    <label className={labelCls}>Epic Agent <span className="text-red-500">*</span></label>
                    <AgentSelect required value={epicAgentName} onChange={setEpicAgentName} placeholder="Select epic agent…" className={inputCls} />
                  </div>
                </div>

                <div>
                  <label className={labelCls}>Brief <span className="text-red-500">*</span></label>
                  <textarea
                    required
                    value={brief}
                    onChange={e => setBrief(e.target.value)}
                    placeholder="Describe the purpose, scope, and goals of this epic…"
                    rows={3}
                    className={`${inputCls} resize-none`}
                  />
                </div>

                <div>
                  <label className={labelCls}>Coding agents</label>
                  <div className="flex gap-2">
                    <AgentSelect value={agentInput} onChange={setAgentInput} placeholder="Add coding agent…" className={`${inputCls} flex-1`} />
                    <button
                      type="button"
                      onClick={handleAddAgent}
                      className="text-xs font-medium px-3 py-2 rounded-lg border border-gray-200 dark:border-zinc-700 text-gray-600 dark:text-zinc-400 hover:bg-gray-50 dark:hover:bg-zinc-800 shrink-0 transition-colors"
                    >
                      Add
                    </button>
                  </div>
                  {codingAgentNames.length > 0 && (
                    <div className="flex flex-wrap gap-1.5 mt-2">
                      {codingAgentNames.map(a => (
                        <span key={a} className="inline-flex items-center gap-1 text-xs bg-gray-100 dark:bg-zinc-800 text-gray-700 dark:text-zinc-300 px-2 py-0.5 rounded-md font-mono">
                          {a}
                          <button type="button" onClick={() => setCodingAgentNames(codingAgentNames.filter(x => x !== a))} className="text-gray-400 dark:text-zinc-500 hover:text-red-500 dark:hover:text-red-400 leading-none ml-0.5">×</button>
                        </span>
                      ))}
                    </div>
                  )}
                </div>

                <div>
                  <label className={labelCls}>Code reviewer</label>
                  <AgentSelect value={reviewerAgentName} onChange={handleReviewerChange} placeholder="Select reviewer…" className={inputCls} />
                </div>

                <div className="rounded-xl border border-gray-100 dark:border-zinc-800 bg-gray-50 dark:bg-zinc-800/40 p-4 space-y-3.5">
                  <p className="text-xs font-medium text-gray-400 dark:text-zinc-500 uppercase tracking-wide">Governance</p>
                  <Toggle
                    id="needsMockup"
                    checked={needsMockup}
                    onChange={setNeedsMockup}
                    label="Needs mockup"
                    sublabel="Require a mockup phase before spec writing"
                  />
                  <Toggle
                    id="isACRequired"
                    checked={isACRequired}
                    onChange={setIsACRequired}
                    label="AC required"
                    sublabel="Run acceptance criteria checks before marking specs done"
                  />
                  <Toggle
                    id="isCodeReviewRequired"
                    checked={isCodeReviewRequired}
                    onChange={handleCodeReviewToggle}
                    label="Code review required"
                    sublabel={codeReviewOverridden
                      ? 'Manually overridden'
                      : reviewerAgentName.trim()
                        ? 'Auto-enabled — code reviewer is set'
                        : 'Auto-enabled when a code reviewer is assigned'}
                  />
                </div>
              </div>

              <div className="px-6 py-4 border-t border-gray-100 dark:border-zinc-800 flex items-center justify-between gap-3">
                {error
                  ? <p className="text-xs text-red-500 flex-1">{error}</p>
                  : <span />
                }
                <div className="flex gap-2 shrink-0">
                  <button
                    type="button"
                    onClick={handleClose}
                    className="text-sm font-medium px-4 py-2 rounded-lg border border-gray-200 dark:border-zinc-700 text-gray-600 dark:text-zinc-400 hover:bg-gray-50 dark:hover:bg-zinc-800 transition-colors"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    disabled={submitting}
                    className="text-sm font-medium px-4 py-2 rounded-lg bg-blue-600 text-white hover:bg-blue-700 disabled:opacity-50 transition-colors"
                  >
                    {submitting ? 'Creating…' : 'Create epic'}
                  </button>
                </div>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}

function EpicRow({ epic, onDelete }: { epic: Epic; onDelete?: () => void }) {
  const [deleteConfirm, setDeleteConfirm] = useState(false);

  return (
    <div className="bg-white dark:bg-zinc-900 rounded-xl border border-gray-100 dark:border-zinc-800 p-4 shadow-sm hover:border-gray-300 dark:hover:border-zinc-600 transition-colors">
      <div className="flex items-center gap-3 flex-wrap">
        <Link to={`/epics/${epic.id}`} className="font-medium text-gray-900 dark:text-zinc-100 hover:underline">
          {epic.name}
        </Link>
        <StateBadge state={epic.currentStateName} />
        {epic.humanInLoop && epic.humanInLoop.isApproved === null && (
          <span className="text-xs font-medium bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300 px-1.5 py-0.5 rounded">
            HUMAN REVIEW
          </span>
        )}
        <span className="text-xs text-gray-400 dark:text-zinc-600 font-mono ml-auto">{epic.slug}</span>
        {deleteConfirm ? (
          <div className="flex items-center gap-1.5">
            <span className="text-xs text-gray-500 dark:text-zinc-400">Delete?</span>
            <button
              type="button"
              onClick={onDelete}
              className="text-xs font-semibold px-2 py-0.5 rounded bg-red-600 text-white hover:bg-red-500 transition-colors"
            >
              Confirm
            </button>
            <button
              type="button"
              onClick={() => setDeleteConfirm(false)}
              className="text-xs px-2 py-0.5 rounded border border-gray-200 dark:border-zinc-700 text-gray-500 dark:text-zinc-400 hover:text-gray-700 dark:hover:text-zinc-200 transition-colors"
            >
              Cancel
            </button>
          </div>
        ) : (
          <button
            type="button"
            onClick={() => setDeleteConfirm(true)}
            className="text-xs text-gray-400 dark:text-zinc-600 hover:text-red-500 dark:hover:text-red-400 transition-colors px-1.5 py-0.5 rounded hover:bg-red-50 dark:hover:bg-red-950/30"
            aria-label={`Delete ${epic.name}`}
          >
            Delete
          </button>
        )}
      </div>
      {epic.brief && (
        <p className="mt-1 text-xs text-gray-500 dark:text-zinc-400 line-clamp-2">{epic.brief}</p>
      )}
      <div className="mt-1.5 flex gap-3 text-xs text-gray-400 dark:text-zinc-500">
        <span>{epic.specs.length} spec{epic.specs.length !== 1 ? 's' : ''}</span>
        {epic.codingAgentNames.length > 0 && <span>{epic.codingAgentNames.join(', ')}</span>}
        <span>Agent: {epic.epicAgentName}</span>
      </div>
    </div>
  );
}

function sortByCreatedDesc(list: Epic[]): Epic[] {
  return [...list].sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
}

type StatusFilter = 'all' | 'open' | 'closed';

const filterTabs: { label: string; value: StatusFilter }[] = [
  { label: 'All', value: 'all' },
  { label: 'Open', value: 'open' },
  { label: 'Closed', value: 'closed' },
];

export default function EpicsListPage() {
  const navigate = useNavigate();
  const [epics, setEpics] = useState<Epic[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [activeFilter, setActiveFilter] = useState<StatusFilter>('open');

  const visible = useMemo(() => {
    const q = search.trim().toLowerCase();
    return epics.filter(epic => {
      if (activeFilter === 'open' && epic.currentStateName === 'closed') return false;
      if (activeFilter === 'closed' && epic.currentStateName !== 'closed') return false;
      if (q && !(epic.name ?? '').toLowerCase().includes(q) && !(epic.brief ?? '').toLowerCase().includes(q)) return false;
      return true;
    });
  }, [epics, search, activeFilter]);

  useEffect(() => {
    EpicApi.list()
      .then(list => setEpics(sortByCreatedDesc(list)))
      .finally(() => setLoading(false));
  }, []);

  useSignalR('/hubs/epic', {
    EpicUpdated: (...args: unknown[]) => {
      const updated = args[0] as Epic;
      setEpics(prev => {
        const exists = prev.some(e => e.id === updated.id);
        if (exists) {
          return sortByCreatedDesc(prev.map(e => e.id === updated.id ? updated : e));
        }
        return sortByCreatedDesc([updated, ...prev]);
      });
    },
    EpicDeleted: (...args: unknown[]) => {
      const deletedId = args[0] as string;
      setEpics(prev => prev.filter(e => e.id !== deletedId));
    },
  });

  if (loading) return <div className="p-6 text-sm text-gray-400 dark:text-zinc-500">Loading…</div>;

  return (
    <div className="p-5 max-w-4xl mx-auto">
      <div className="flex items-center justify-between mb-4">
        <h1 className="text-lg font-semibold text-gray-900 dark:text-zinc-100">Epics</h1>
      </div>

      <div className="mb-4">
        <CreateEpicModal onCreated={epic => navigate(`/epics/${epic.id}`)} />
      </div>

      <div className="mb-3">
        <input
          type="search"
          value={search}
          onChange={e => setSearch(e.target.value)}
          placeholder="Search epics…"
          className="w-full text-sm rounded-lg border border-gray-200 dark:border-zinc-700 bg-gray-50 dark:bg-zinc-800 px-3 py-1.5 text-gray-900 dark:text-zinc-100 focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      <div role="tablist" aria-label="Filter by status" className="flex gap-1 mb-4">
        {filterTabs.map(tab => (
          <button
            key={tab.value}
            role="tab"
            aria-selected={activeFilter === tab.value}
            onClick={() => setActiveFilter(tab.value)}
            className={`text-xs font-medium px-3 py-1.5 rounded-lg transition-colors ${
              activeFilter === tab.value
                ? 'bg-blue-600 text-white'
                : 'text-gray-600 dark:text-zinc-400 hover:bg-gray-100 dark:hover:bg-zinc-800'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {visible.length === 0 ? (
        <div className="bg-white dark:bg-zinc-900 rounded-xl border border-gray-100 dark:border-zinc-800 p-8 text-center">
          <p className="text-sm text-gray-400 dark:text-zinc-500">{epics.length === 0 ? 'No epics yet.' : 'No epics match your filters.'}</p>
        </div>
      ) : (
        <div className="space-y-2">
          {visible.map(epic => (
            <EpicRow
              key={epic.id}
              epic={epic}
              onDelete={async () => {
                try {
                  await EpicApi.delete(epic.id);
                  setEpics(prev => prev.filter(e => e.id !== epic.id));
                } catch (err) {
                  console.error('Failed to delete epic:', err);
                  alert(err instanceof Error ? err.message : 'Delete failed');
                }
              }}
            />
          ))}
        </div>
      )}
    </div>
  );
}
