import { useState, useMemo } from 'react';
import { Link } from 'react-router-dom';
import { usePersistedPositionId } from '../utils/usePersistedPositionId';
import { WorkflowHint } from '../components/WorkflowHint';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { getPosition } from '../api/positions';
import {
  getCandidatesForPosition,
  getCandidateFileUrl,
  deleteCandidate,
} from '../api/candidates';
import { getEvaluations, screenCandidates, getEvaluationsCsvUrl } from '../api/evaluations';
import { formatCandidateName } from '../utils/formatName';
import { ScoreBadge } from '../components/ScoreBadge';
import { DetailDrawer } from '../components/DetailDrawer';
import { CreatePositionModal } from '../components/CreatePositionModal';
import { ResumeTextModal } from '../components/ResumeTextModal';
import { PositionSelector } from '../components/PositionSelector';
import { EmptyState } from '../components/EmptyState';
import type { Evaluation } from '../types';

export function ScreeningPage() {
  const qc = useQueryClient();
  const [positionId, setPositionId] = usePersistedPositionId();

  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [showEdit, setShowEdit] = useState(false);
  const [activeEval, setActiveEval] = useState<Evaluation | null>(null);
  const [resumeModal, setResumeModal] = useState<{ id: string; name: string } | null>(null);
  const [sortByScore, setSortByScore] = useState(false);

  const { data: position } = useQuery({
    queryKey: ['position', positionId],
    queryFn: () => getPosition(positionId),
    enabled: !!positionId,
  });

  const { data: page, isFetching: loadingCandidates } = useQuery({
    queryKey: ['position-candidates', positionId],
    queryFn: () => getCandidatesForPosition(positionId),
    enabled: !!positionId,
  });
  const candidates = page?.items ?? [];

  const { data: evaluations = [], isFetching: loadingEvals } = useQuery({
    queryKey: ['evaluations', positionId],
    queryFn: () => getEvaluations(positionId),
    enabled: !!positionId,
  });

  const deleteMutation = useMutation({
    mutationFn: deleteCandidate,
    onSuccess: (_, id) => {
      qc.invalidateQueries({ queryKey: ['position-candidates', positionId] });
      qc.invalidateQueries({ queryKey: ['evaluations', positionId] });
      setSelected(prev => { const next = new Set(prev); next.delete(id); return next; });
      toast.success('Candidate removed');
    },
    onError: () => toast.error('Delete failed'),
  });

  const screenMutation = useMutation({
    mutationFn: () => screenCandidates(positionId, Array.from(selected)),
    onSuccess: results => {
      qc.invalidateQueries({ queryKey: ['evaluations', positionId] });
      setSelected(new Set());
      toast.success(`${results.length} candidate${results.length !== 1 ? 's' : ''} screened`);
    },
    onError: () => toast.error('Screening failed'),
  });

  const toggle = (id: string) =>
    setSelected(prev => {
      const next = new Set(prev);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });

  const toggleAll = () =>
    setSelected(
      selected.size === candidates.length && candidates.length > 0
        ? new Set()
        : new Set(candidates.map(c => c.id))
    );

  const canScreen = !!positionId && selected.size > 0 && !screenMutation.isPending;

  const candidateDisplayNames = useMemo(() => {
    const map = new Map<string, string>();
    const groups = new Map<string, typeof candidates>();
    for (const c of candidates) {
      const key = formatCandidateName(c.name);
      if (!groups.has(key)) groups.set(key, []);
      groups.get(key)!.push(c);
    }
    for (const [key, group] of groups) {
      if (group.length === 1) {
        map.set(group[0].id, key);
      } else {
        const sorted = [...group].sort(
          (a, b) => new Date(a.uploadedAt).getTime() - new Date(b.uploadedAt).getTime()
        );
        sorted.forEach((c, i) => map.set(c.id, `${key} (v${i + 1})`));
      }
    }
    return map;
  }, [candidates]);

  const candidatesById = useMemo(
    () => new Map(candidates.map(c => [c.id, c])),
    [candidates]
  );

  const sortedEvaluations = useMemo(
    () => sortByScore ? [...evaluations].sort((a, b) => b.score - a.score) : evaluations,
    [evaluations, sortByScore]
  );

  const staleEvaluations = evaluations.filter(e => e.isStale);
  const hasStale = staleEvaluations.length > 0;

  const rescreenStale = () => {
    const ids = new Set(staleEvaluations.map(e => e.candidateId));
    setSelected(ids);
  };

  return (
    <div className="p-6 max-w-5xl mx-auto flex flex-col gap-5">
      <WorkflowHint current={evaluations.length > 0 ? 'results' : 'screening'} />
      <div className="flex items-end justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-2xl font-semibold text-gray-100">Screening</h1>
          <p className="text-sm text-gray-500 mt-1 truncate max-w-xl">
            {position?.title ?? 'Run AI evaluation for a selected position'}
          </p>
        </div>
      </div>

      {/* Position */}
      <div className="card flex items-center gap-3 flex-wrap">
        <label className="text-sm font-medium text-gray-300 shrink-0">Position</label>
        <PositionSelector value={positionId} onChange={setPositionId} className="max-w-sm" />
        {position && (
          <button onClick={() => setShowEdit(true)} className="btn-secondary shrink-0">
            Edit
          </button>
        )}
      </div>

      {!positionId ? (
        <EmptyState
          icon={
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" className="w-7 h-7">
              <circle cx="11" cy="11" r="6" />
              <path d="M16 16l4 4" strokeLinecap="round" />
            </svg>
          }
          title="Select a position to start screening"
          description="Pick a position above. AI evaluation runs against its required and nice-to-have skills."
          action={
            <Link to="/positions" className="btn-secondary text-sm">
              Manage Positions →
            </Link>
          }
        />
      ) : candidates.length === 0 && !loadingCandidates ? (
        <EmptyState
          icon={
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" className="w-7 h-7">
              <path d="M12 4v9M8 8l4-4 4 4" strokeLinecap="round" strokeLinejoin="round" />
              <path d="M4 16v3a1 1 0 001 1h14a1 1 0 001-1v-3" strokeLinecap="round" />
            </svg>
          }
          title="No candidates attached to this position"
          description="Upload CVs or generate synthetic candidates first, then come back to screen them."
          action={
            <div className="flex items-center gap-2">
              <Link to={`/candidates?positionId=${positionId}`} className="btn-secondary text-sm">
                Upload CVs →
              </Link>
              <Link to={`/generator?positionId=${positionId}`} className="btn-secondary text-sm">
                Generate CVs →
              </Link>
            </div>
          }
        />
      ) : (
        <>
          {/* Candidates */}
          <div className="card flex flex-col gap-4">
            <h2 className="text-sm font-semibold text-gray-300">
              Candidates <span className="text-gray-500 font-normal">({candidates.length})</span>
            </h2>

            <div className="border border-gray-800 rounded-lg overflow-hidden">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-gray-800 bg-gray-800/50">
                    <th className="w-10 px-3 py-2">
                      <input
                        type="checkbox"
                        checked={selected.size === candidates.length && candidates.length > 0}
                        onChange={toggleAll}
                        className="accent-blue-500"
                      />
                    </th>
                    <th className="px-3 py-2 text-left text-gray-400 font-medium">Name</th>
                    <th className="px-3 py-2 text-left text-gray-400 font-medium">File</th>
                    <th className="px-3 py-2 text-left text-gray-400 font-medium">Source</th>
                    <th className="px-3 py-2 text-left text-gray-400 font-medium">Added</th>
                    <th className="w-8 px-3 py-2" />
                  </tr>
                </thead>
                <tbody>
                  {candidates.map(c => (
                    <tr
                      key={c.id}
                      onClick={() => toggle(c.id)}
                      className={`border-b border-gray-800/50 cursor-pointer transition-colors ${
                        selected.has(c.id) ? 'bg-blue-900/20' : 'hover:bg-gray-800/40'
                      }`}
                    >
                      <td className="px-3 py-2.5">
                        <input
                          type="checkbox"
                          checked={selected.has(c.id)}
                          onChange={() => toggle(c.id)}
                          onClick={e => e.stopPropagation()}
                          className="accent-blue-500"
                        />
                      </td>
                      <td className="px-3 py-2.5 text-gray-100">
                        {candidateDisplayNames.get(c.id) ?? formatCandidateName(c.name)}
                      </td>
                      <td className="px-3 py-2.5 max-w-[200px] truncate">
                        {c.source === 'Uploaded' ? (
                          <a
                            href={getCandidateFileUrl(c.id)}
                            target="_blank"
                            rel="noopener noreferrer"
                            onClick={e => e.stopPropagation()}
                            className="text-blue-400 hover:text-blue-300 hover:underline transition-colors"
                            title="Open CV PDF"
                          >
                            {c.fileName}
                          </a>
                        ) : (
                          <button
                            type="button"
                            onClick={e => {
                              e.stopPropagation();
                              setResumeModal({
                                id: c.id,
                                name: candidateDisplayNames.get(c.id) ?? formatCandidateName(c.name),
                              });
                            }}
                            className="text-purple-400 hover:text-purple-300 hover:underline transition-colors text-left truncate max-w-full"
                            title="View generated CV"
                          >
                            View generated CV
                          </button>
                        )}
                      </td>
                      <td className="px-3 py-2.5">
                        <span
                          className={`text-xs px-2 py-0.5 rounded ${
                            c.source === 'Generated'
                              ? 'bg-purple-900/40 text-purple-300'
                              : 'bg-gray-700 text-gray-300'
                          }`}
                        >
                          {c.source}
                        </span>
                      </td>
                      <td className="px-3 py-2.5 text-gray-400">
                        {new Date(c.attachedAt).toLocaleDateString()}
                      </td>
                      <td className="px-3 py-2.5">
                        <button
                          onClick={e => { e.stopPropagation(); deleteMutation.mutate(c.id); }}
                          disabled={deleteMutation.isPending}
                          className="text-gray-600 hover:text-red-400 transition-colors text-lg leading-none"
                          title="Remove candidate"
                        >
                          ×
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="flex items-center justify-between">
              <span className="text-sm text-gray-500">
                {screenMutation.isPending
                  ? 'AI evaluation may take 10–30 seconds per candidate...'
                  : selected.size > 0
                    ? `${selected.size} selected`
                    : 'Select candidates to screen'}
              </span>
              <button
                onClick={() => screenMutation.mutate()}
                disabled={!canScreen}
                className="btn-primary disabled:opacity-50 min-w-[220px]"
              >
                {screenMutation.isPending
                  ? `Screening selected candidates…`
                  : `Screen selected (${selected.size})`}
              </button>
            </div>
          </div>

          {/* Evaluations */}
          <div className="card flex flex-col gap-4">
            <div className="flex items-center justify-between">
              <h2 className="text-sm font-semibold text-gray-300">
                Evaluations{' '}
                {loadingEvals
                  ? <span className="text-xs font-normal text-gray-500">Loading...</span>
                  : evaluations.length > 0 && <span className="text-gray-500 font-normal">({evaluations.length})</span>}
              </h2>
              <div className="flex items-center gap-2">
                {evaluations.length > 0 && (
                  <button
                    onClick={() => setSortByScore(s => !s)}
                    className={`btn-secondary text-xs py-1.5 ${sortByScore ? 'border-blue-500 text-blue-400' : ''}`}
                  >
                    {sortByScore ? '↓ Score' : 'Sort by score'}
                  </button>
                )}
                {hasStale && (
                  <button onClick={rescreenStale} className="btn-secondary text-xs py-1.5 border-amber-600 text-amber-400 hover:bg-amber-900/20">
                    ↺ Re-screen stale ({staleEvaluations.length})
                  </button>
                )}
                {evaluations.length > 0 && (
                  <a
                    href={getEvaluationsCsvUrl(positionId)}
                    download
                    className="btn-secondary text-xs py-1.5"
                  >
                    Export CSV
                  </a>
                )}
              </div>
            </div>

            {hasStale && (
              <div className="flex items-center gap-2 px-3 py-2 rounded-lg bg-amber-900/20 border border-amber-700/40 text-amber-300 text-xs">
                <span>⚠</span>
                <span>Position was edited after some evaluations were created. Re-screen to get up-to-date results.</span>
              </div>
            )}

            {evaluations.length === 0 && !loadingEvals ? (
              <p className="text-sm text-gray-500">No evaluations yet — select candidates above and click "Screen selected".</p>
            ) : (
              <div className="border border-gray-800 rounded-lg overflow-hidden">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-gray-800 bg-gray-800/50">
                      <th className="w-10 px-3 py-2 text-left text-gray-400 font-medium">#</th>
                      <th className="px-3 py-2 text-left text-gray-400 font-medium">Name</th>
                      <th className="px-3 py-2 text-left text-gray-400 font-medium">Score</th>
                      <th className="px-3 py-2 text-left text-gray-400 font-medium">Level</th>
                      <th className="px-3 py-2 text-left text-gray-400 font-medium">Reasoning</th>
                    </tr>
                  </thead>
                  <tbody>
                    {sortedEvaluations.map((e, idx) => (
                      <tr
                        key={e.id}
                        onClick={() => setActiveEval(e)}
                        className="border-b border-gray-800/50 cursor-pointer hover:bg-gray-800/40 transition-colors"
                      >
                        <td className="px-3 py-2.5 text-gray-500">{idx + 1}</td>
                        <td className="px-3 py-2.5 text-gray-100 font-medium">
                          <span className="flex items-center gap-2">
                            {candidateDisplayNames.get(e.candidateId) ?? formatCandidateName(e.candidateName)}
                            {e.isStale && (
                              <span className="text-xs px-1.5 py-0.5 rounded bg-amber-900/40 text-amber-400 border border-amber-700/40 shrink-0">
                                Outdated
                              </span>
                            )}
                          </span>
                        </td>
                        <td className="px-3 py-2.5">
                          <ScoreBadge score={e.score} matchLevel={e.matchLevel} />
                        </td>
                        <td className="px-3 py-2.5">
                          <span
                            className={`text-xs px-2 py-0.5 rounded capitalize ${
                              e.matchLevel === 'strong'
                                ? 'bg-green-900/40 text-green-300'
                                : e.matchLevel === 'medium'
                                  ? 'bg-yellow-900/40 text-yellow-300'
                                  : 'bg-red-900/40 text-red-300'
                            }`}
                          >
                            {e.matchLevel}
                          </span>
                        </td>
                        <td className="px-3 py-2.5 text-gray-400 max-w-xs truncate">{e.reasoning}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </>
      )}

      <DetailDrawer
        evaluation={activeEval}
        candidateFileUrl={
          activeEval && candidatesById.get(activeEval.candidateId)?.source === 'Uploaded'
            ? getCandidateFileUrl(activeEval.candidateId)
            : undefined
        }
        onViewResume={
          activeEval && candidatesById.get(activeEval.candidateId)?.source === 'Generated'
            ? () => setResumeModal({
                id: activeEval.candidateId,
                name: candidateDisplayNames.get(activeEval.candidateId) ?? formatCandidateName(activeEval.candidateName),
              })
            : undefined
        }
        onClose={() => setActiveEval(null)}
      />

      {showEdit && position && (
        <CreatePositionModal
          position={position}
          onClose={() => setShowEdit(false)}
          onCreated={() => {
            setShowEdit(false);
            qc.invalidateQueries({ queryKey: ['position', positionId] });
            qc.invalidateQueries({ queryKey: ['evaluations', positionId] });
          }}
        />
      )}

      {resumeModal && (
        <ResumeTextModal
          candidateId={resumeModal.id}
          candidateName={resumeModal.name}
          onClose={() => setResumeModal(null)}
        />
      )}
    </div>
  );
}
