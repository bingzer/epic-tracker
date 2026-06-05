import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { EpicApi, type CreateEpicPayload } from '../epicApi';
import type { Epic } from '../types';
import { StateBadge } from '../components/StateBadge';
import { useSignalR } from '../hooks/useSignalR';

function CreateEpicForm({ onCreated }: { onCreated: (epic: Epic) => void }) {
  const [open, setOpen] = useState(false);
  const [name, setName] = useState('');
  const [epicAgentName, setEpicAgentName] = useState('');
  const [brief, setBrief] = useState('');
  const [agentInput, setAgentInput] = useState('');
  const [codingAgentNames, setCodingAgentNames] = useState<string[]>([]);
  const [needsMockup, setNeedsMockup] = useState(false);
  const [reviewerAgentName, setReviewerAgentName] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function reset() {
    setName('');
    setEpicAgentName('');
    setBrief('');
    setAgentInput('');
    setCodingAgentNames([]);
    setNeedsMockup(false);
    setReviewerAgentName('');
    setError(null);
  }

  function handleAddAgent() {
    const parts = agentInput.split(',').map(s => s.trim()).filter(Boolean);
    const next = [...codingAgentNames];
    for (const p of parts) {
      if (!next.includes(p)) next.push(p);
    }
    setCodingAgentNames(next);
    setAgentInput('');
  }

  function handleAgentKeyDown(e: React.KeyboardEvent) {
    if (e.key === 'Enter') { e.preventDefault(); handleAddAgent(); }
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

  if (!open) {
    return (
      <button
        onClick={() => setOpen(true)}
        className="text-sm font-medium text-blue-600 dark:text-blue-400 hover:underline"
      >
        + New epic
      </button>
    );
  }

  const inputCls = "w-full text-sm rounded-lg border border-gray-200 dark:border-zinc-700 bg-gray-50 dark:bg-zinc-800 px-3 py-1.5 text-gray-900 dark:text-zinc-100 focus:outline-none focus:ring-2 focus:ring-blue-500";
  const labelCls = "block text-xs text-gray-500 dark:text-zinc-400 mb-1";

  return (
    <form onSubmit={handleSubmit} className="bg-white dark:bg-zinc-900 rounded-xl border border-gray-200 dark:border-zinc-700 p-4 space-y-3">
      <h2 className="text-sm font-semibold text-gray-900 dark:text-zinc-100">New epic</h2>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
        <div>
          <label className={labelCls}>Name</label>
          <input value={name} onChange={e => setName(e.target.value)} placeholder="Price Quote" className={inputCls} />
        </div>
        <div>
          <label className={labelCls}>Epic Agent *</label>
          <input required value={epicAgentName} onChange={e => setEpicAgentName(e.target.value)} placeholder="epic-agent-id" className={inputCls} />
        </div>
      </div>
      <div>
        <label className={labelCls}>Brief *</label>
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
          <input
            value={agentInput}
            onChange={e => setAgentInput(e.target.value)}
            onKeyDown={handleAgentKeyDown}
            placeholder="agent-id (comma-separated or press Enter)"
            className={`${inputCls} flex-1`}
          />
          <button
            type="button"
            onClick={handleAddAgent}
            className="text-xs font-medium px-2 py-1.5 rounded-lg border border-gray-200 dark:border-zinc-700 text-gray-600 dark:text-zinc-400 hover:bg-gray-50 dark:hover:bg-zinc-800 shrink-0"
          >
            Add
          </button>
        </div>
        {codingAgentNames.length > 0 && (
          <div className="flex flex-wrap gap-1.5 mt-1.5">
            {codingAgentNames.map(a => (
              <span key={a} className="inline-flex items-center gap-1 text-xs bg-gray-100 dark:bg-zinc-800 text-gray-700 dark:text-zinc-300 px-2 py-0.5 rounded font-mono">
                {a}
                <button type="button" onClick={() => setCodingAgentNames(codingAgentNames.filter(x => x !== a))} className="text-gray-400 dark:text-zinc-500 hover:text-red-500 dark:hover:text-red-400 leading-none">×</button>
              </span>
            ))}
          </div>
        )}
      </div>
      <div>
        <label className={labelCls}>Code reviewer</label>
        <input value={reviewerAgentName} onChange={e => setReviewerAgentName(e.target.value)} placeholder="reviewer-agent-id" className={inputCls} />
      </div>
      <div className="flex items-center gap-2">
        <input
          type="checkbox"
          id="needsMockup"
          checked={needsMockup}
          onChange={e => setNeedsMockup(e.target.checked)}
          className="rounded border-gray-300 dark:border-zinc-600"
        />
        <label htmlFor="needsMockup" className="text-sm text-gray-700 dark:text-zinc-300">Needs mockup</label>
      </div>
      {error && <p className="text-xs text-red-500">{error}</p>}
      <div className="flex gap-2">
        <button
          type="submit"
          disabled={submitting}
          className="text-sm font-medium px-3 py-1.5 rounded-lg bg-blue-600 text-white hover:bg-blue-700 disabled:opacity-50"
        >
          {submitting ? 'Creating…' : 'Create'}
        </button>
        <button
          type="button"
          onClick={() => { reset(); setOpen(false); }}
          className="text-sm font-medium px-3 py-1.5 rounded-lg border border-gray-200 dark:border-zinc-700 text-gray-600 dark:text-zinc-400 hover:bg-gray-50 dark:hover:bg-zinc-800"
        >
          Cancel
        </button>
      </div>
    </form>
  );
}

function EpicRow({ epic, onDelete }: { epic: Epic; onDelete?: () => void }) {
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
        <button
          type="button"
          onClick={onDelete}
          className="text-xs text-gray-400 dark:text-zinc-600 hover:text-red-500 dark:hover:text-red-400 transition-colors px-1.5 py-0.5 rounded hover:bg-red-50 dark:hover:bg-red-950/30"
          aria-label={`Delete ${epic.name}`}
        >
          Delete
        </button>
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

      <div className="space-y-2 mb-4">
        <CreateEpicForm onCreated={epic => setEpics(prev => sortByCreatedDesc([epic, ...prev]))} />
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
                if (!window.confirm(`Delete "${epic.name}"? This cannot be undone.`)) return;
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
