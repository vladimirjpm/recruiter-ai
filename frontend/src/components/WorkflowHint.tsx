import { Link, useSearchParams } from 'react-router-dom';

// 4-step progress indicator that shows where the user is in the recruiter workflow.
// Steps after the current one stay clickable so the user can jump ahead — they will
// land on an empty state if prerequisites aren't met, which is itself a signal.
type Step = 'positions' | 'candidates' | 'screening' | 'results';

const STEPS: { key: Step; label: string; route: string }[] = [
  { key: 'positions',  label: 'Position',   route: '/positions'  },
  { key: 'candidates', label: 'Candidates', route: '/candidates' },
  { key: 'screening',  label: 'Screening',  route: '/screening'  },
  { key: 'results',    label: 'Results',    route: '/screening'  },
];

interface Props {
  current: Step;
}

// Keeps ?positionId= when navigating between steps so the workflow stays scoped.
export function WorkflowHint({ current }: Props) {
  const [params] = useSearchParams();
  const positionId = params.get('positionId') ?? '';
  const query = positionId ? `?positionId=${positionId}` : '';
  const currentIdx = STEPS.findIndex(s => s.key === current);

  return (
    <ol className="flex items-center gap-1 text-xs text-gray-500">
      {STEPS.map((s, i) => {
        const isCurrent = i === currentIdx;
        const isDone    = i < currentIdx;
        return (
          <li key={s.key} className="flex items-center gap-1">
            <Link
              to={`${s.route}${query}`}
              className={`flex items-center gap-1.5 px-2 py-1 rounded transition-colors ${
                isCurrent
                  ? 'text-blue-300 bg-blue-600/15'
                  : isDone
                    ? 'text-gray-400 hover:text-gray-200'
                    : 'text-gray-600 hover:text-gray-400'
              }`}
            >
              <span
                className={`w-4 h-4 rounded-full text-[10px] flex items-center justify-center font-semibold ${
                  isCurrent
                    ? 'bg-blue-500 text-white'
                    : isDone
                      ? 'bg-gray-700 text-gray-300'
                      : 'bg-gray-800 text-gray-500'
                }`}
              >
                {i + 1}
              </span>
              {/* Hide the label on narrow screens — keeps the chain on one line at 375px. */}
              <span className="hidden sm:inline">{s.label}</span>
              <span className="sm:hidden">{isCurrent ? s.label : ''}</span>
            </Link>
            {i < STEPS.length - 1 && <span className="text-gray-700">→</span>}
          </li>
        );
      })}
    </ol>
  );
}
