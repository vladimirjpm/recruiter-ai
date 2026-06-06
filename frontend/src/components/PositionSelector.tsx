import { useQuery } from '@tanstack/react-query';
import { getPositions } from '../api/positions';

// Shared position dropdown for Candidates / Screening / Generator pages.
// Controlled component — parent decides where the selection lives
// (URL query param + localStorage in Part 12).
interface Props {
  value: string;
  onChange: (positionId: string) => void;
  placeholder?: string;
  className?: string;
}

export function PositionSelector({
  value,
  onChange,
  placeholder = '— Select a position —',
  className = '',
}: Props) {
  const { data: positions = [] } = useQuery({
    queryKey: ['positions'],
    queryFn: getPositions,
  });

  return (
    <select
      value={value}
      onChange={e => onChange(e.target.value)}
      className={`input ${className}`}
    >
      <option value="">{placeholder}</option>
      {positions.map(p => (
        <option key={p.id} value={p.id}>
          {p.title}
          {p.seniorityLevel ? ` · ${p.seniorityLevel}` : ''}
          {p.country ? ` · ${p.country}` : ''}
        </option>
      ))}
    </select>
  );
}
