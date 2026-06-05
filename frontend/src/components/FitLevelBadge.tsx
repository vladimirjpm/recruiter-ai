const fitColors: Record<string, string> = {
  excellent_fit:           'bg-green-900/50 text-green-400 border-green-700',
  strong_fit:              'bg-emerald-900/50 text-emerald-400 border-emerald-700',
  medium_fit:              'bg-yellow-900/50 text-yellow-400 border-yellow-700',
  weak_fit:                'bg-orange-900/50 text-orange-400 border-orange-700',
  overqualified:           'bg-blue-900/50 text-blue-400 border-blue-700',
  underqualified:          'bg-orange-900/50 text-orange-400 border-orange-700',
  missing_key_requirement: 'bg-red-900/50 text-red-400 border-red-700',
  career_switcher:         'bg-purple-900/50 text-purple-400 border-purple-700',
  related_industry:        'bg-teal-900/50 text-teal-400 border-teal-700',
  risk_profile:            'bg-rose-900/50 text-rose-400 border-rose-700',
};

export function FitLevelBadge({ level }: { level: string }) {
  const color = fitColors[level] ?? 'bg-gray-800 text-gray-400 border-gray-600';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded border text-xs font-medium ${color}`}>
      {level.replace(/_/g, ' ')}
    </span>
  );
}
