const EPIC_STATES = ['drafting', 'mockup', 'waterproofing', 'spec_writing', 'implementation', 'closed'];
const SPEC_STATES = ['spec_drafting', 'coding', 'code_review', 'ac', 'done'];

interface Props {
  state: string;
  type: 'epic' | 'spec';
}

export function StateBreadcrumb({ state, type }: Props) {
  const states = type === 'epic' ? EPIC_STATES : SPEC_STATES;
  const currentIdx = states.indexOf(state);
  const isOff = ['agent_swarm', 'human_in_loop', 'spec_human_in_loop'].includes(state);

  if (isOff) {
    return (
      <div className="flex items-center gap-1 flex-wrap">
        <span className="text-xs px-2 py-0.5 rounded bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300 font-medium">
          {state.replace(/_/g, ' ')}
        </span>
      </div>
    );
  }

  return (
    <div className="flex items-center gap-0.5 flex-wrap">
      {states.map((s, i) => {
        const isCurrent = s === state;
        const isPast = currentIdx >= 0 && i < currentIdx;
        const isFuture = currentIdx >= 0 && i > currentIdx;

        let cls = 'text-xs px-2 py-0.5 rounded font-medium transition-colors ';
        if (isCurrent) {
          cls += 'bg-blue-600 text-white dark:bg-blue-500';
        } else if (isPast) {
          cls += 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400';
        } else {
          cls += isFuture
            ? 'bg-gray-100 text-gray-400 dark:bg-zinc-800 dark:text-zinc-600'
            : 'bg-gray-100 text-gray-500 dark:bg-zinc-800 dark:text-zinc-500';
        }

        return (
          <span key={s} className="flex items-center gap-0.5">
            <span className={cls}>{s.replace(/_/g, ' ')}</span>
            {i < states.length - 1 && (
              <span className={`text-xs ${isPast ? 'text-emerald-400 dark:text-emerald-600' : 'text-gray-300 dark:text-zinc-700'}`}>›</span>
            )}
          </span>
        );
      })}
    </div>
  );
}
