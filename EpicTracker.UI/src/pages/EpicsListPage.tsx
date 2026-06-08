import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { EpicApi, AgentApi, type CreateEpicPayload } from '../epicApi';
import type { Epic } from '../types';
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

function needsAttention(epic: Epic): boolean {
  return epic.currentStateName === 'human_in_loop' ||
    (epic.humanInLoop != null && epic.humanInLoop.isApproved === null);
}

function SpecProgress({ epic }: { epic: Epic }) {
  const total = epic.specs.length;
  const done = epic.specs.filter(s => s.currentStateName === 'done').length;
  const pct = total === 0 ? 0 : Math.round((done / total) * 100);
  const isDone = epic.currentStateName === 'closed';
  const color = isDone ? 'bg-emerald-500' : 'bg-blue-500';

  return (
    <div className="flex items-center gap-2.5">
      <div className="w-24 h-1 bg-zinc-800 rounded-full overflow-hidden shrink-0">
        <div className={`h-full rounded-full ${color}`} style={{ width: `${pct}%` }} />
      </div>
      <span className="text-xs text-zinc-500 whitespace-nowrap">{done} / {total}</span>
    </div>
  );
}

function EpicStateBadge({ epic }: { epic: Epic }) {
  const state = epic.currentStateName;
  const attention = needsAttention(epic);

  if (state === 'closed') {
    return (
      <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded bg-emerald-500/10 border border-emerald-500/20 text-emerald-500 text-xs font-semibold">
        ✓ done
      </span>
    );
  }

  if (attention) {
    return (
      <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded bg-amber-500/15 border border-amber-500/30 text-amber-400 text-xs font-semibold">
        <span className="w-1.5 h-1.5 rounded-full bg-amber-400 animate-pulse shrink-0" />
        {state.replace(/_/g, ' ')}
      </span>
    );
  }

  const colorMap: Record<string, string> = {
    implementation: 'bg-orange-500/15 border-orange-500/25 text-orange-400',
    spec_writing:   'bg-blue-500/15 border-blue-500/25 text-blue-400',
    waterproofing:  'bg-purple-500/15 border-purple-500/25 text-purple-400',
    mockup:         'bg-pink-500/15 border-pink-500/25 text-pink-400',
    drafting:       'bg-zinc-700/60 border-zinc-600 text-zinc-400',
    agent_swarm:    'bg-cyan-500/15 border-cyan-500/25 text-cyan-400',
  };
  const cls = colorMap[state] ?? 'bg-zinc-700/60 border-zinc-600 text-zinc-400';

  return (
    <span className={`inline-flex items-center gap-1.5 px-2 py-0.5 rounded border text-xs font-semibold ${cls}`}>
      <span className="w-1.5 h-1.5 rounded-full bg-current shrink-0 opacity-80" />
      {state.replace(/_/g, ' ')}
    </span>
  );
}

function AgentDot({ name, agentStatuses }: { name: string; agentStatuses: Map<string, string> }) {
  const status = agentStatuses.get(name);
  const online = status && status !== 'offline';
  return (
    <div className="flex items-center gap-1.5">
      <span className={`w-2 h-2 rounded-full shrink-0 ${online ? 'bg-emerald-400 shadow-[0_0_4px_rgba(52,211,153,0.5)]' : 'bg-zinc-600'}`} />
      <span className="text-xs text-zinc-500">{name}</span>
    </div>
  );
}

function CodingAgentChips({ names }: { names: string[] }) {
  const MAX = 3;
  const visible = names.slice(0, MAX);
  const extra = names.length - MAX;
  return (
    <div className="flex items-center gap-1 flex-wrap">
      {visible.map(n => (
        <span key={n} className="text-[11px] px-1.5 py-0.5 rounded border border-zinc-800 bg-zinc-900 text-zinc-400 whitespace-nowrap">{n}</span>
      ))}
      {extra > 0 && (
        <span className="text-[11px] px-1.5 py-0.5 rounded border border-zinc-800 bg-zinc-900 text-zinc-500">+{extra}</span>
      )}
    </div>
  );
}

function EpicsTable({ epics, agentStatuses, onNavigate }: {
  epics: Epic[];
  agentStatuses: Map<string, string>;
  onNavigate: (id: string) => void;
}) {
  const attention = epics.filter(needsAttention);
  const rest = epics.filter(e => !needsAttention(e));
  const attentionCount = attention.length;

  return (
    <div className="rounded-lg border border-zinc-800 overflow-hidden">
      <table className="w-full border-collapse">
        <thead>
          <tr className="border-b border-zinc-800 bg-zinc-900/60">
            <th className="text-left text-xs text-zinc-500 font-semibold px-4 py-2.5 w-[38%]">name</th>
            <th className="text-left text-xs text-zinc-500 font-semibold px-4 py-2.5 w-[16%]">state</th>
            <th className="text-left text-xs text-zinc-500 font-semibold px-4 py-2.5 w-[14%]">progress</th>
            <th className="text-left text-xs text-zinc-500 font-semibold px-4 py-2.5 w-[10%]">agent</th>
            <th className="text-left text-xs text-zinc-500 font-semibold px-4 py-2.5">coding agents</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-zinc-800/60">
          {attentionCount > 0 && (
            <tr>
              <td colSpan={5} className="px-4 py-1.5 bg-amber-500/5 border-b border-amber-500/10">
                <span className="text-xs text-amber-500/70 font-semibold uppercase tracking-wider">
                  {attentionCount} {attentionCount === 1 ? 'epic needs' : 'epics need'} attention
                </span>
              </td>
            </tr>
          )}
          {[...attention, ...rest].map(epic => {
            const isDone = epic.currentStateName === 'closed';
            const isAttention = needsAttention(epic);
            return (
              <tr
                key={epic.id}
                onClick={() => onNavigate(epic.id)}
                className={`cursor-pointer transition-colors ${
                  isAttention ? 'bg-amber-500/[0.03] hover:bg-amber-500/[0.06]' :
                  isDone      ? 'opacity-50 hover:opacity-75 hover:bg-white/[0.02]' :
                                'hover:bg-white/[0.02]'
                }`}
              >
                <td className="px-4 py-3.5">
                  <span className={`font-bold text-sm ${isDone ? 'text-zinc-400' : 'text-zinc-100'}`}>
                    {epic.name ?? epic.id}
                  </span>
                  {epic.brief && (
                    <p className="text-xs text-zinc-400 mt-0.5 line-clamp-2 leading-relaxed" style={{ minHeight: '2.5rem' }}>{epic.brief}</p>
                  )}
                </td>
                <td className="px-4 py-3.5">
                  <EpicStateBadge epic={epic} />
                </td>
                <td className="px-4 py-3.5">
                  <SpecProgress epic={epic} />
                </td>
                <td className="px-4 py-3.5">
                  <AgentDot name={epic.epicAgentName} agentStatuses={agentStatuses} />
                </td>
                <td className="px-4 py-3.5">
                  <CodingAgentChips names={epic.codingAgentNames} />
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
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
  const [agentStatuses, setAgentStatuses] = useState<Map<string, string>>(new Map());

  useEffect(() => {
    AgentApi.list()
      .then(list => setAgentStatuses(new Map(list.map(a => [a.sessionName, a.status]))))
      .catch(() => {});
  }, []);

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
        if (exists) return sortByCreatedDesc(prev.map(e => e.id === updated.id ? updated : e));
        return sortByCreatedDesc([updated, ...prev]);
      });
    },
    EpicDeleted: (...args: unknown[]) => {
      const deletedId = args[0] as string;
      setEpics(prev => prev.filter(e => e.id !== deletedId));
    },
  });

  if (loading) return <div className="p-6 text-sm text-zinc-500">Loading…</div>;

  return (
    <div className="p-6 max-w-6xl mx-auto">
      <div className="flex items-end justify-between mb-8">
        <div>
          <h1 className="text-2xl font-extrabold text-zinc-100 tracking-tight">Epics</h1>
          <p className="text-xs text-zinc-500 mt-0.5">
            {epics.length} total{visible.length !== epics.length ? ` · ${visible.length} shown` : ''}
          </p>
        </div>
        <CreateEpicModal onCreated={epic => navigate(`/epics/${epic.id}`)} />
      </div>

      <div className="flex items-center gap-3 mb-6">
        <div className="relative flex-1">
          <svg className="absolute left-3 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-zinc-600" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
            <circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/>
          </svg>
          <input
            type="search"
            value={search}
            onChange={e => setSearch(e.target.value)}
            placeholder="Search epics…"
            className="w-full text-sm rounded-md border border-zinc-800 bg-zinc-900 pl-9 pr-4 py-2 text-zinc-300 placeholder:text-zinc-500 focus:outline-none focus:border-zinc-600 focus:ring-1 focus:ring-zinc-600"
          />
        </div>
        <div className="flex items-center gap-1 bg-zinc-900 border border-zinc-800 rounded-md p-1">
          {filterTabs.map(tab => (
            <button
              key={tab.value}
              onClick={() => setActiveFilter(tab.value)}
              className={`px-3 py-1 text-xs rounded transition-colors font-semibold ${
                activeFilter === tab.value
                  ? 'bg-zinc-800 text-zinc-100'
                  : 'text-zinc-500 hover:text-zinc-300'
              }`}
            >
              {tab.label}
            </button>
          ))}
        </div>
      </div>

      {visible.length === 0 ? (
        <div className="rounded-lg border border-zinc-800 p-10 text-center">
          <p className="font-mono text-sm text-zinc-600">{epics.length === 0 ? 'No epics yet.' : 'No epics match your filters.'}</p>
        </div>
      ) : (
        <EpicsTable epics={visible} agentStatuses={agentStatuses} onNavigate={id => navigate(`/epics/${id}`)} />
      )}
    </div>
  );
}
