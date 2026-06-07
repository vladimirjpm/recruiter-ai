import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { findMatchingCandidates } from '../api/positions';
import { attachCandidate } from '../api/candidates';
import { formatCandidateName } from '../utils/formatName';
import type { CandidateMatch } from '../types';

interface Props {
  positionId: string;
  onClose: () => void;
}

export function FindMatchesModal({ positionId, onClose }: Props) {
  const qc = useQueryClient();
  const [selected, setSelected] = useState<Set<string>>(new Set());

  const { data: matches = [], isLoading, isError } = useQuery({
    queryKey: ['find-matches', positionId],
    queryFn: () => findMatchingCandidates(positionId),
  });

  const attachMutation = useMutation({
    // Attach selected candidates one-by-one; endpoint is idempotent so safe to retry.
    mutationFn: (ids: string[]) =>
      Promise.all(ids.map(id => attachCandidate(positionId, id))),
    onSuccess: (_, ids) => {
      qc.invalidateQueries({ queryKey: ['position-candidates', positionId] });
      toast.success(`${ids.length} candidate${ids.length !== 1 ? 's' : ''} attached`);
      onClose();
    },
    onError: () => toast.error('Could not attach candidates'),
  });

  const toggle = (id: string) =>
    setSelected(prev => {
      const next = new Set(prev);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });

  const toggleAll = () => {
    if (selected.size === matches.length) setSelected(new Set());
    else setSelected(new Set(matches.map(m => m.id)));
  };

  const handleAttach = () => {
    if (selected.size === 0) return;
    attachMutation.mutate(Array.from(selected));
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4">
      <div className="bg-gray-900 border border-gray-700 rounded-xl shadow-2xl w-full max-w-2xl flex flex-col max-h-[80vh]">
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-800">
          <div>
            <h2 className="text-base font-semibold text-gray-100">
              Find Matching Existing Candidates
            </h2>
            <p className="text-xs text-gray-500 mt-0.5">
              Ranked by required-skill overlap. AI screening happens after you attach.
            </p>
          </div>
          <button
            onClick={onClose}
            className="text-gray-500 hover:text-gray-300 transition-colors text-xl leading-none"
          >
            ×
          </button>
        </div>

        {/* Body */}
        <div className="overflow-y-auto flex-1 px-5 py-3">
          {isLoading && (
            <p className="text-sm text-gray-500 py-6 text-center">Searching…</p>
          )}
          {isError && (
            <p className="text-sm text-red-400 py-6 text-center">Failed to load matches.</p>
          )}
          {!isLoading && !isError && matches.length === 0 && (
            <p className="text-sm text-gray-500 py-6 text-center">
              No unattached candidates found in the global pool.
            </p>
          )}
          {!isLoading && matches.length > 0 && (
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-800">
                  <th className="w-8 pb-2">
                    <input
                      type="checkbox"
                      checked={selected.size === matches.length && matches.length > 0}
                      onChange={toggleAll}
                      className="accent-blue-500"
                    />
                  </th>
                  <th className="pb-2 text-left text-gray-400 font-medium">Candidate</th>
                  <th className="pb-2 text-left text-gray-400 font-medium w-20">Match</th>
                  <th className="pb-2 text-left text-gray-400 font-medium">Skills</th>
                </tr>
              </thead>
              <tbody>
                {matches.map(m => (
                  <MatchRow
                    key={m.id}
                    match={m}
                    selected={selected.has(m.id)}
                    onToggle={() => toggle(m.id)}
                  />
                ))}
              </tbody>
            </table>
          )}
        </div>

        {/* Footer */}
        <div className="flex items-center justify-between gap-3 px-5 py-4 border-t border-gray-800">
          <span className="text-xs text-gray-500">
            {matches.length > 0
              ? `${matches.length} match${matches.length !== 1 ? 'es' : ''} found`
              : ''}
          </span>
          <div className="flex items-center gap-2">
            <button onClick={onClose} className="btn-secondary text-sm">
              Cancel
            </button>
            <button
              onClick={handleAttach}
              disabled={selected.size === 0 || attachMutation.isPending}
              className="btn-primary text-sm disabled:opacity-50"
            >
              {attachMutation.isPending
                ? 'Attaching…'
                : `Attach selected (${selected.size})`}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

function MatchRow({
  match: m,
  selected,
  onToggle,
}: {
  match: CandidateMatch;
  selected: boolean;
  onToggle: () => void;
}) {
  const pctColor =
    m.skillOverlapPct >= 75
      ? 'text-green-400'
      : m.skillOverlapPct >= 50
        ? 'text-yellow-400'
        : 'text-gray-400';

  return (
    <tr
      onClick={onToggle}
      className={`cursor-pointer border-b border-gray-800/50 transition-colors ${
        selected ? 'bg-blue-900/20' : 'hover:bg-gray-800/40'
      }`}
    >
      <td className="py-2.5 pr-2">
        <input
          type="checkbox"
          checked={selected}
          onChange={onToggle}
          onClick={e => e.stopPropagation()}
          className="accent-blue-500"
        />
      </td>
      <td className="py-2.5 pr-3">
        <div className="text-gray-100 truncate max-w-[180px]">
          {formatCandidateName(m.name)}
        </div>
        <div className="text-[11px] text-gray-500 truncate max-w-[180px]">{m.fileName}</div>
      </td>
      <td className="py-2.5 pr-3">
        <span
          className={`font-mono font-semibold ${pctColor}`}
          title={`${m.matchedSkills.length} of ${m.matchedSkills.length + m.missingSkills.length} required skills found in CV text`}
        >
          {m.skillOverlapPct}%
        </span>
      </td>
      <td className="py-2.5">
        <div className="flex flex-wrap gap-1">
          {m.matchedSkills.map(s => (
            <span
              key={s}
              className="text-[10px] px-1.5 py-0.5 rounded bg-green-900/30 text-green-400 border border-green-800/40"
            >
              {s}
            </span>
          ))}
          {m.missingSkills.map(s => (
            <span
              key={s}
              className="text-[10px] px-1.5 py-0.5 rounded bg-gray-800 text-gray-500 border border-gray-700"
            >
              {s}
            </span>
          ))}
        </div>
      </td>
    </tr>
  );
}
