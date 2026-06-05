interface ScoreBadgeProps {
  score: number;
  matchLevel: string;
}

export function ScoreBadge({ score, matchLevel }: ScoreBadgeProps) {
  const color =
    matchLevel === 'strong'
      ? 'bg-green-900/50 text-green-400 border-green-700'
      : matchLevel === 'medium'
        ? 'bg-yellow-900/50 text-yellow-400 border-yellow-700'
        : 'bg-red-900/50 text-red-400 border-red-700';

  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded border text-sm font-semibold ${color}`}>
      {score}
    </span>
  );
}
