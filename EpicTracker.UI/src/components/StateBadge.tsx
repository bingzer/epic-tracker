const stateColors: Record<string, string> = {
  drafting:           'bg-gray-100 text-gray-600 dark:bg-zinc-800 dark:text-zinc-400',
  mockup:             'bg-sky-100 text-sky-700 dark:bg-sky-900/40 dark:text-sky-300',
  waterproofing:      'bg-cyan-100 text-cyan-700 dark:bg-cyan-900/40 dark:text-cyan-300',
  spec_writing:       'bg-indigo-100 text-indigo-700 dark:bg-indigo-900/40 dark:text-indigo-300',
  agent_swarm:        'bg-violet-100 text-violet-700 dark:bg-violet-900/40 dark:text-violet-300',
  human_in_loop:      'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300',
  implementation:     'bg-yellow-100 text-yellow-700 dark:bg-yellow-900/40 dark:text-yellow-300',
  closed:             'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300',
  spec_drafting:      'bg-gray-100 text-gray-600 dark:bg-zinc-800 dark:text-zinc-400',
  coding:             'bg-yellow-100 text-yellow-700 dark:bg-yellow-900/40 dark:text-yellow-300',
  code_review:        'bg-cyan-100 text-cyan-700 dark:bg-cyan-900/40 dark:text-cyan-300',
  ac:                 'bg-purple-100 text-purple-700 dark:bg-purple-900/40 dark:text-purple-300',
  spec_human_in_loop: 'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300',
  done:               'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300',
};

export function StateBadge({ state }: { state: string }) {
  const cls = stateColors[state] ?? 'bg-gray-100 text-gray-600 dark:bg-zinc-800 dark:text-zinc-400';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${cls}`}>
      {state.replace(/_/g, ' ')}
    </span>
  );
}
