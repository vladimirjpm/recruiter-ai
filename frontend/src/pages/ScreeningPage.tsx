import { useState, useMemo } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { getPositions, getPosition } from '../api/positions';
import { getCandidates, uploadCandidates, getCandidateFileUrl, deleteCandidate } from '../api/candidates';
import { getEvaluations, screenCandidates, getEvaluationsCsvUrl } from '../api/evaluations';
import { formatCandidateName } from '../utils/formatName';
import { DropZone } from '../components/DropZone';
import { ScoreBadge } from '../components/ScoreBadge';
import { DetailDrawer } from '../components/DetailDrawer';
import { CreatePositionModal } from '../components/CreatePositionModal';
import { ResumeTextModal } from '../components/ResumeTextModal';
import type { Evaluation, Position } from '../types';

export function ScreeningPage() {
  const qc = useQueryClient();
  const [positionId, setPositionId] = useState('');
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [showCreate, setShowCreate] = useState(false);
  const [showEdit, setShowEdit] = useState(false);
  const [activeEval, setActiveEval] = useState<Evaluation | null>(null);
  const [resumeModal, setResumeModal] = useState<{ id: string; name: string } | null>(null);
  const [sortByScore, setSortByScore] = useState(false);

  const { data: positions = [] } = useQuery({ queryKey: ['positions'], queryFn: getPositions });
  const { data: selectedPosition } = useQuery({
    queryKey: ['position', positionId],
    queryFn: () => getPosition(positionId),
    enabled: !!positionId,
  });
  const { data: candidates = [] } = useQuery({ queryKey: ['candidates'], queryFn: getCandidates });
  const { data: evaluations = [], isFetching: loadingEvals } = useQuery({
    queryKey: ['evaluations', positionId],
    queryFn: () => getEvaluations(positionId),
    enabled: !!positionId,
  });

  const uploadMutation = useMutation({
    mutationFn: uploadCandidates,
    onSuccess: results => {
      qc.invalidateQueries({ queryKey: ['candidates'] });
      setSelected(prev => {
        const next = new Set(prev);
        results.forEach(r => next.add(r.id));
        return next;
      });
      toast.success(`${results.length} CV${results.length !== 1 ? 's' : ''} uploaded`);
    },
    onError: (err: unknown) => {
      const msg =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ??
        'Upload failed';
      toast.error(msg);
    },
  });

  const deleteMutation = useMutation({
    mutationFn: deleteCandidate,
    onSuccess: (_, id) => {
      qc.invalidateQueries({ queryKey: ['candidates'] });
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

  // Precompute display names: disambiguate candidates with identical formatted names.
  // Sorted by uploadedAt so v1 is always the older upload.
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

  const isDuplicateName = useMemo(() => {
    const seen = new Map<string, number>();
    for (const c of candidates) {
      const key = formatCandidateName(c.name);
      seen.set(key, (seen.get(key) ?? 0) + 1);
    }
    return (id: string) => {
      const name = formatCandidateName(candidates.find(c => c.id === id)?.name ?? '');
      return (seen.get(name) ?? 0) > 1;
    };
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
      <h1 className="text-xl font-semibold text-gray-100">Screening</h1>

      {/* Position */}
      <div className="card flex items-center gap-3 flex-wrap">
        <label className="text-sm font-medium text-gray-300 shrink-0">Position</label>
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
              {p.country ? ` · ${p.country}` : ''}
            </option>
          ))}
        </select>
        <button onClick={() => setShowCreate(true)} className="btn-secondary shrink-0">
          + New Position
        </button>
        {selectedPosition && (
          <button onClick={() => setShowEdit(true)} className="btn-secondary shrink-0">
            Edit
          </button>
        )}
      </div>

      {/* Candidates */}
      <div className="card flex flex-col gap-4">
        <h2 className="text-sm font-semibold text-gray-300">
          Candidates {candidates.length > 0 && <span className="text-gray-500 font-normal">({candidates.length})</span>}
        </h2>

        <DropZone
          onFiles={files => uploadMutation.mutate(files)}
          isUploading={uploadMutation.isPending}
        />

        {candidates.length > 0 && (
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
                      <span className="flex items-center gap-2">
                        {candidateDisplayNames.get(c.id) ?? formatCandidateName(c.name)}
                        {isDuplicateName(c.id) && (
                          <span className="text-xs px-1.5 py-0.5 rounded bg-yellow-900/30 text-yellow-400 border border-yellow-700/30 shrink-0">
                            ⚠ Possible duplicate
                          </span>
                        )}
                      </span>
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
                            setResumeModal({ id: c.id, name: candidateDisplayNames.get(c.id) ?? formatCandidateName(c.name) });
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
                      {new Date(c.uploadedAt).toLocaleDateString()}
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
        )}

        <div className="flex items-center justify-between">
          <span className="text-sm text-gray-500">
            {screenMutation.isPending
              ? 'AI evaluation may take 10–30 seconds per candidate...'
              : selected.size > 0
                ? `${selected.size} selected`
                : candidates.length === 0
                  ? 'No candidates yet — upload CVs above'
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
      {positionId && (
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
            <p className="text-sm text-gray-500">No evaluations yet for this position.</p>
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
                          {candidatesById.get(e.candidateId)?.source === 'Uploaded' ? (
                            <a
                              href={getCandidateFileUrl(e.candidateId)}
                              target="_blank"
                              rel="noopener noreferrer"
                              onClick={ev => ev.stopPropagation()}
                              className="text-gray-500 hover:text-blue-400 transition-colors shrink-0"
                              title="Open CV PDF"
                            >
                              <svg xmlns="http://www.w3.org/2000/svg" className="w-3.5 h-3.5" viewBox="0 0 20 20" fill="currentColor">
                                <path fillRule="evenodd" d="M4 4a2 2 0 012-2h4.586A2 2 0 0112 2.586L15.414 6A2 2 0 0116 7.414V16a2 2 0 01-2 2H6a2 2 0 01-2-2V4zm2 6a1 1 0 011-1h6a1 1 0 110 2H7a1 1 0 01-1-1zm1 3a1 1 0 100 2h6a1 1 0 100-2H7z" clipRule="evenodd" />
                              </svg>
                            </a>
                          ) : (
                            <button
                              type="button"
                              onClick={ev => {
                                ev.stopPropagation();
                                setResumeModal({
                                  id: e.candidateId,
                                  name: candidateDisplayNames.get(e.candidateId) ?? formatCandidateName(e.candidateName),
                                });
                              }}
                              className="text-gray-500 hover:text-purple-400 transition-colors shrink-0"
                              title="View generated CV"
                            >
                              <svg xmlns="http://www.w3.org/2000/svg" className="w-3.5 h-3.5" viewBox="0 0 20 20" fill="currentColor">
                                <path d="M10 12a2 2 0 100-4 2 2 0 000 4z" />
                                <path fillRule="evenodd" d="M.458 10C1.732 5.943 5.522 3 10 3s8.268 2.943 9.542 7c-1.274 4.057-5.064 7-9.542 7S1.732 14.057.458 10zM14 10a4 4 0 11-8 0 4 4 0 018 0z" clipRule="evenodd" />
                              </svg>
                            </button>
                          )}
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

      {showCreate && (
        <CreatePositionModal
          onClose={() => setShowCreate(false)}
          onCreated={(p: Position) => {
            setShowCreate(false);
            setPositionId(p.id);
          }}
        />
      )}

      {showEdit && selectedPosition && (
        <CreatePositionModal
          position={selectedPosition}
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
