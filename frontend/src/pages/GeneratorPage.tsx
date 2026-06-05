import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { getPositions } from '../api/positions';
import { generateCandidates } from '../api/generator';
import { FitLevelBadge } from '../components/FitLevelBadge';
import { CreatePositionModal } from '../components/CreatePositionModal';
import type { GeneratedCandidate, Position } from '../types';

const QUICK_COUNTS = [5, 10, 20, 30];

export function GeneratorPage() {
  const qc = useQueryClient();
  const [positionId, setPositionId] = useState('');
  const [count, setCount] = useState(10);
  const [showCreate, setShowCreate] = useState(false);
  const [results, setResults] = useState<GeneratedCandidate[]>([]);

  const { data: positions = [] } = useQuery({ queryKey: ['positions'], queryFn: getPositions });

  const mutation = useMutation({
    mutationFn: () => generateCandidates(positionId, count),
    onSuccess: data => {
      // Generated candidates are saved to the shared pool — invalidate so Screening page reflects them.
      qc.invalidateQueries({ queryKey: ['candidates'] });
      setResults(data);
      toast.success(`${data.length} CVs generated`);
    },
    onError: () => toast.error('Generation failed'),
  });

  return (
    <div className="p-6 max-w-4xl mx-auto flex flex-col gap-5">
      <div>
        <h1 className="text-xl font-semibold text-gray-100">Generator</h1>
        <p className="text-sm text-gray-400 mt-0.5">
          Generate synthetic CVs to validate screening quality across any job domain.
        </p>
      </div>

      {/* Settings */}
      <div className="card flex flex-col gap-5">
        {/* Position */}
        <div className="flex items-center gap-3 flex-wrap">
          <label className="text-sm font-medium text-gray-300 w-20 shrink-0">Position</label>
          <select
            value={positionId}
            onChange={e => setPositionId(e.target.value)}
            className="input max-w-sm"
          >
            <option value="">— Select a position —</option>
            {positions.map(p => (
              <option key={p.id} value={p.id}>
                {p.title}
                {p.seniorityLevel ? ` · ${p.seniorityLevel}` : ''}
              </option>
            ))}
          </select>
          <button onClick={() => setShowCreate(true)} className="btn-secondary shrink-0">
            + New
          </button>
        </div>

        {/* Count slider */}
        <div className="flex items-center gap-3">
          <label className="text-sm font-medium text-gray-300 w-20 shrink-0">Count</label>
          <input
            type="range"
            min={1}
            max={30}
            value={count}
            onChange={e => setCount(Number(e.target.value))}
            className="flex-1 accent-blue-500"
          />
          <span className="text-sm font-semibold text-gray-100 w-6 text-right">{count}</span>
          <div className="flex gap-1.5 ml-2">
            {QUICK_COUNTS.map(n => (
              <button
                key={n}
                onClick={() => setCount(n)}
                className={`px-2.5 py-1 text-xs rounded transition-colors border ${
                  count === n
                    ? 'bg-blue-600 text-white border-blue-600'
                    : 'bg-gray-800 text-gray-400 border-gray-600 hover:text-gray-200'
                }`}
              >
                {n}
              </button>
            ))}
          </div>
        </div>

        <div className="flex items-center justify-between pt-2 border-t border-gray-800 gap-4">
          <p className="text-xs text-gray-500 leading-relaxed">
            Requirements are inferred from the job description — no hardcoded domain assumptions.
            Generated candidates enter the shared pool and can be screened on the Screening page.
          </p>
          <button
            onClick={() => mutation.mutate()}
            disabled={!positionId || mutation.isPending}
            className="btn-primary shrink-0 disabled:opacity-50"
          >
            {mutation.isPending ? 'Generating...' : `Generate ${count} CVs`}
          </button>
        </div>
      </div>

      {/* Results */}
      {results.length > 0 && (
        <div className="card flex flex-col gap-4">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-semibold text-gray-300">
              Generated — {results.length} candidates
            </h2>
            <span className="text-xs text-gray-500 font-mono">
              batch {results[0].batchId.slice(0, 8)}
            </span>
          </div>

          <div className="border border-gray-800 rounded-lg overflow-hidden">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-800 bg-gray-800/50">
                  <th className="w-8 px-3 py-2 text-left text-gray-400 font-medium">#</th>
                  <th className="px-3 py-2 text-left text-gray-400 font-medium">Name</th>
                  <th className="px-3 py-2 text-left text-gray-400 font-medium">Expected Fit</th>
                  <th className="px-3 py-2 text-left text-gray-400 font-medium">Score Range</th>
                  <th className="px-3 py-2 text-left text-gray-400 font-medium">Email</th>
                </tr>
              </thead>
              <tbody>
                {results.map((c, idx) => (
                  <tr key={c.id} className="border-b border-gray-800/50">
                    <td className="px-3 py-2.5 text-gray-500">{idx + 1}</td>
                    <td className="px-3 py-2.5 text-gray-100 font-medium">{c.name}</td>
                    <td className="px-3 py-2.5">
                      <FitLevelBadge level={c.expectedFitLevel} />
                    </td>
                    <td className="px-3 py-2.5 text-gray-400">
                      {c.expectedScoreMin}–{c.expectedScoreMax}
                    </td>
                    <td className="px-3 py-2.5 text-gray-400">{c.email ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <p className="text-xs text-gray-500">
            These candidates are now in the shared pool. Go to Screening to evaluate them against the position.
          </p>
        </div>
      )}

      {showCreate && (
        <CreatePositionModal
          onClose={() => setShowCreate(false)}
          onCreated={(p: Position) => {
            setShowCreate(false);
            setPositionId(p.id);
          }}
        />
      )}
    </div>
  );
}
