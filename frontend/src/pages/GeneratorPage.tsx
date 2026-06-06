import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { usePersistedPositionId } from '../utils/usePersistedPositionId';
import { WorkflowHint } from '../components/WorkflowHint';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { getPosition } from '../api/positions';
import { generateCandidates } from '../api/generator';
import { FitLevelBadge } from '../components/FitLevelBadge';
import { PositionSelector } from '../components/PositionSelector';
import { EmptyState } from '../components/EmptyState';
import type { GeneratedCandidate } from '../types';

const QUICK_COUNTS = [5, 10, 20, 30];

export function GeneratorPage() {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const [positionId, setPositionId] = usePersistedPositionId();

  const [count, setCount] = useState(10);
  const [results, setResults] = useState<GeneratedCandidate[]>([]);

  const { data: position } = useQuery({
    queryKey: ['position', positionId],
    queryFn: () => getPosition(positionId),
    enabled: !!positionId,
  });

  const mutation = useMutation({
    mutationFn: () => generateCandidates(positionId, count),
    onSuccess: data => {
      // Generator endpoint auto-attaches each candidate to the position via the junction.
      // Invalidate the scoped query so Candidates/Screening pages refetch.
      qc.invalidateQueries({ queryKey: ['position-candidates', positionId] });
      setResults(data);
      toast.success(
        t => (
          <span className="flex items-center gap-3">
            <span>{data.length} CV{data.length !== 1 ? 's' : ''} generated and attached</span>
            <button
              onClick={() => {
                toast.dismiss(t.id);
                navigate(`/screening?positionId=${positionId}`);
              }}
              className="text-blue-300 hover:text-blue-200 font-medium whitespace-nowrap"
            >
              Go to Screening →
            </button>
          </span>
        ),
        { duration: 8000 }
      );
    },
    onError: () => toast.error('Generation failed'),
  });

  return (
    <div className="p-6 max-w-4xl mx-auto flex flex-col gap-5">
      <WorkflowHint current="candidates" />
      <div className="flex items-end justify-between gap-4 flex-wrap">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <h1 className="text-2xl font-semibold text-gray-100">Synthetic CV Generator</h1>
            <span className="text-[10px] font-bold uppercase tracking-wider px-1.5 py-0.5 rounded bg-amber-900/40 text-amber-400 border border-amber-700/40 leading-none self-center">
              dev tool
            </span>
          </div>
          <p className="text-sm text-gray-500 mt-1 max-w-xl">
            {position?.title
              ? `Generating synthetic CVs for: ${position.title}`
              : 'Not part of the recruiter workflow — used to test screening quality across fit levels and professions.'}
          </p>
        </div>
      </div>

      {/* Settings */}
      <div className="card flex flex-col gap-5">
        <div className="flex items-center gap-3 flex-wrap">
          <label className="text-sm font-medium text-gray-300 w-20 shrink-0">Position</label>
          <PositionSelector value={positionId} onChange={setPositionId} className="flex-1 md:max-w-sm" />
        </div>

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

        <div className="flex flex-col md:flex-row md:items-center md:justify-between pt-2 border-t border-gray-800 gap-3 md:gap-4">
          <p className="text-xs text-gray-500 leading-relaxed">
            Requirements are inferred from the job description — no hardcoded domain assumptions.
            Generated candidates are attached to the selected position automatically.
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

      {!positionId && results.length === 0 && (
        <EmptyState
          icon={
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" className="w-7 h-7">
              <path d="M12 3v3M12 18v3M3 12h3M18 12h3M6 6l2 2M16 16l2 2M6 18l2-2M16 8l2-2" strokeLinecap="round" />
            </svg>
          }
          title="Select a position to generate CVs"
          description="Generated candidates are tied to a position so you can validate screening quality end-to-end."
          action={
            <Link to="/positions" className="btn-secondary text-sm">
              Manage Positions →
            </Link>
          }
        />
      )}

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

          <div className="flex items-center justify-between gap-3 flex-wrap">
            <p className="text-xs text-gray-500">
              These candidates are attached to the selected position. Run screening to validate ranks.
            </p>
            <button
              onClick={() => navigate(`/screening?positionId=${positionId}`)}
              className="btn-primary"
            >
              Go to Screening →
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
