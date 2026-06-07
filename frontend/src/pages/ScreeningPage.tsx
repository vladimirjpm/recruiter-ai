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
  detachCandidate,
  type AttachedCandidate,
} from '../api/candidates';
import { getEvaluations, screenCandidates, getEvaluationsCsvUrl } from '../api/evaluations';
import { formatCandidateName } from '../utils/formatName';
import { ScoreBadge } from '../components/ScoreBadge';
import { DetailDrawer } from '../components/DetailDrawer';
import { CreatePositionModal } from '../components/CreatePositionModal';
import { ResumeTextModal } from '../components/ResumeTextModal';
import { FindMatchesModal } from '../components/FindMatchesModal';
import { PositionSelector } from '../components/PositionSelector';
import { EmptyState } from '../components/EmptyState';
import type { Evaluation } from '../types';

// Unified pipeline view: one row per candidate, screened ones show score/level/reasoning,
// un-screened and stale ones expose a checkbox so the recruiter can select & screen.
interface Row {
  candidate: AttachedCandidate;
  evaluation: Evaluation | null;   // latest evaluation for this candidate, if any
  status: 'unscreened' | 'screened' | 'stale';
  displayName: string;
}

export function ScreeningPage() {
  const qc = useQueryClient();
  const [positionId, setPositionId] = usePersistedPositionId();

  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [showEdit, setShowEdit] = useState(false);
  // Store the candidate id, not the evaluation object — otherwise the drawer keeps
  // a stale snapshot after an adjustment PATCH refreshes the evaluations query.
  const [activeCandidateId, setActiveCandidateId] = useState<string | null>(null);
  const [resumeModal, setResumeModal] = useState<{ id: string; name: string } | null>(null);
  const [showFindMatches, setShowFindMatches] = useState(false);
  // Recruiter can sort by raw AI score or by Final (AI + adjustment). Default: Final, so
  // the recruiter's own corrections drive the ranking.
  const [sortBy, setSortBy] = useState<'ai' | 'final'>('final');

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

  const detachMutation = useMutation({
    mutationFn: (candidateId: string) => detachCandidate(positionId, candidateId),
    onSuccess: (_, id) => {
      qc.invalidateQueries({ queryKey: ['position-candidates', positionId] });
      qc.invalidateQueries({ queryKey: ['evaluations', positionId] });
      setSelected(prev => { const next = new Set(prev); next.delete(id); return next; });
      toast.success('Removed from this position');
    },
    onError: () => toast.error('Could not remove candidate'),
  });

  const screenMutation = useMutation({
    mutationFn: (ids: string[]) => screenCandidates(positionId, ids),
    onSuccess: results => {
      qc.invalidateQueries({ queryKey: ['evaluations', positionId] });
      setSelected(new Set());
      toast.success(`${results.length} candidate${results.length !== 1 ? 's' : ''} screened`);
    },
    onError: () => toast.error('Screening failed'),
  });

  // Latest evaluation per candidate. evaluations endpoint already returns latest-per-candidate
  // (GROUP BY MAX(createdAt)) so a simple Map is enough.
  const evalByCandidate = useMemo(
    () => new Map(evaluations.map(e => [e.candidateId, e])),
    [evaluations]
  );

  // Disambiguate identical display names (e.g. two "John Smith" CVs).
  const displayNames = useMemo(() => {
    const map = new Map<string, string>();
    const groups = new Map<string, AttachedCandidate[]>();
    for (const c of candidates) {
      const key = formatCandidateName(c.name);
      (groups.get(key) ?? groups.set(key, []).get(key)!).push(c);
    }
    for (const [key, group] of groups) {
      if (group.length === 1) map.set(group[0].id, key);
      else {
        const sorted = [...group].sort(
          (a, b) => new Date(a.uploadedAt).getTime() - new Date(b.uploadedAt).getTime()
        );
        sorted.forEach((c, i) => map.set(c.id, `${key} (v${i + 1})`));
      }
    }
    return map;
  }, [candidates]);

  // Build the unified row model and split into two sections.
  const { toScreen, screened } = useMemo(() => {
    const rows: Row[] = candidates.map(c => {
      const ev = evalByCandidate.get(c.id) ?? null;
      const status: Row['status'] = !ev ? 'unscreened' : ev.isStale ? 'stale' : 'screened';
      return { candidate: c, evaluation: ev, status, displayName: displayNames.get(c.id) ?? formatCandidateName(c.name) };
    });
    const scoreOf = (r: Row) =>
      sortBy === 'final'
        ? r.evaluation?.finalScore ?? r.evaluation?.score ?? 0
        : r.evaluation?.score ?? 0;
    return {
      toScreen: rows.filter(r => r.status === 'unscreened'),
      screened: rows
        .filter(r => r.status !== 'unscreened')
        .sort((a, b) => scoreOf(b) - scoreOf(a)),
    };
  }, [candidates, evalByCandidate, displayNames, sortBy]);

  const candidatesById = useMemo(
    () => new Map(candidates.map(c => [c.id, c])),
    [candidates]
  );

  const staleRows = screened.filter(r => r.status === 'stale');
  const actionableIds = useMemo(
    () => [...toScreen.map(r => r.candidate.id), ...staleRows.map(r => r.candidate.id)],
    [toScreen, staleRows]
  );

  const toggle = (id: string) =>
    setSelected(prev => {
      const next = new Set(prev);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });

  const selectAllActionable = () => {
    if (selected.size === actionableIds.length && actionableIds.length > 0) {
      setSelected(new Set());
    } else {
      setSelected(new Set(actionableIds));
    }
  };

  const selectAllUnscreened = () => setSelected(new Set(toScreen.map(r => r.candidate.id)));
  const selectAllStale     = () => setSelected(new Set(staleRows.map(r => r.candidate.id)));

  const runScreen = () => {
    if (selected.size === 0) return;
    screenMutation.mutate(Array.from(selected));
  };

  const onRemove = (row: Row) => {
    if (!confirm(`Remove "${row.displayName}" from this position? The candidate stays in the global pool.`)) return;
    detachMutation.mutate(row.candidate.id);
  };

  const onRowClick = (row: Row) => {
    // Actionable rows toggle selection; screened rows open the drawer.
    if (row.status === 'unscreened') {
      toggle(row.candidate.id);
    } else if (row.evaluation) {
      setActiveCandidateId(row.candidate.id);
    }
  };

  // Always pull the freshest evaluation from the query cache so the drawer
  // re-renders after a recruiter adjustment PATCH invalidates ['evaluations', positionId].
  const activeEval: Evaluation | null = activeCandidateId
    ? evalByCandidate.get(activeCandidateId) ?? null
    : null;

  return (
    <div className="p-6 max-w-5xl mx-auto flex flex-col gap-5">
      <WorkflowHint current={screened.length > 0 ? 'results' : 'screening'} />

      <div className="flex items-end justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-2xl font-semibold text-gray-100">Screening</h1>
          <p className="text-sm text-gray-500 mt-1 truncate max-w-xl">
            {position?.title ?? 'Run AI evaluation for a selected position'}
          </p>
        </div>
      </div>

      {/* Position picker */}
      <div className="card flex flex-col md:flex-row md:items-center gap-3 md:flex-wrap">
        <div className="flex items-center gap-3 flex-1 min-w-0">
          <label className="text-sm font-medium text-gray-300 shrink-0">Position</label>
          <PositionSelector value={positionId} onChange={setPositionId} className="flex-1 md:max-w-sm" />
        </div>
        {position && (
          <div className="flex items-center gap-2 shrink-0">
            <button onClick={() => setShowFindMatches(true)} className="btn-secondary text-sm">
              Find Matching Existing Candidates
            </button>
            <button onClick={() => setShowEdit(true)} className="btn-secondary shrink-0">
              Edit
            </button>
          </div>
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
          action={<Link to="/positions" className="btn-secondary text-sm">Manage Positions →</Link>}
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
              <Link to={`/candidates?positionId=${positionId}`} className="btn-secondary text-sm">Upload CVs →</Link>
              <Link to={`/generator?positionId=${positionId}`} className="btn-secondary text-sm">Generate CVs →</Link>
            </div>
          }
        />
      ) : (
        <div className="card flex flex-col gap-4">
          {/* Stale banner — unchanged from the previous layout */}
          {staleRows.length > 0 && (
            <div className="flex items-center gap-2 px-3 py-2 rounded-lg bg-amber-900/20 border border-amber-700/40 text-amber-300 text-xs">
              <span>⚠</span>
              <span>Position was edited after some evaluations were created. Re-screen to get up-to-date results.</span>
            </div>
          )}

          {/* CTA bar */}
          <div className="flex items-center gap-2 flex-wrap">
            <button
              onClick={runScreen}
              disabled={selected.size === 0 || screenMutation.isPending}
              className="btn-primary disabled:opacity-50"
            >
              {screenMutation.isPending
                ? 'Screening selected candidates…'
                : `Screen selected (${selected.size})`}
            </button>
            {toScreen.length > 0 && selected.size === 0 && (
              <button onClick={selectAllUnscreened} className="btn-secondary text-xs">
                Select {toScreen.length} unscreened
              </button>
            )}
            {staleRows.length > 0 && (
              <button
                onClick={selectAllStale}
                className="btn-secondary text-xs border-amber-700/60 text-amber-300 hover:bg-amber-900/20"
              >
                ↺ Re-screen {staleRows.length} stale
              </button>
            )}
            <div className="ml-auto flex items-center gap-2">
              {screened.length > 0 && (
                <>
                  <div className="flex items-center text-xs border border-gray-700 rounded overflow-hidden">
                    <span className="px-2 py-1 text-gray-500 bg-gray-800/50">Sort by</span>
                    <button
                      type="button"
                      onClick={() => setSortBy('ai')}
                      className={`px-2 py-1 transition-colors ${
                        sortBy === 'ai'
                          ? 'bg-blue-900/40 text-blue-200'
                          : 'text-gray-400 hover:text-gray-200'
                      }`}
                      title="Rank by raw AI score, ignoring recruiter overrides"
                    >
                      AI
                    </button>
                    <button
                      type="button"
                      onClick={() => setSortBy('final')}
                      className={`px-2 py-1 transition-colors ${
                        sortBy === 'final'
                          ? 'bg-blue-900/40 text-blue-200'
                          : 'text-gray-400 hover:text-gray-200'
                      }`}
                      title="Rank by AI + recruiter adjustment"
                    >
                      Final
                    </button>
                  </div>
                  <a
                    href={getEvaluationsCsvUrl(positionId)}
                    download
                    className="btn-secondary text-xs"
                  >
                    Export CSV
                  </a>
                </>
              )}
            </div>
          </div>

          {screenMutation.isPending && (
            <p className="text-xs text-gray-500 -mt-2">
              AI evaluation may take 10–30 seconds per candidate.
            </p>
          )}

          {/* Unified pipeline table — desktop only (lg+) */}
          <div className="hidden lg:block border border-gray-800 rounded-lg overflow-hidden">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-800 bg-gray-800/50">
                  <th className="w-10 px-3 py-2">
                    <input
                      type="checkbox"
                      checked={actionableIds.length > 0 && selected.size === actionableIds.length}
                      onChange={selectAllActionable}
                      disabled={actionableIds.length === 0}
                      className="accent-blue-500"
                      title="Select all actionable candidates"
                    />
                  </th>
                  <th className="px-3 py-2 text-left text-gray-400 font-medium">Name</th>
                  <th className="px-3 py-2 text-left text-gray-400 font-medium">Status</th>
                  <th className="px-3 py-2 text-left text-gray-400 font-medium">Score</th>
                  <th className="px-3 py-2 text-left text-gray-400 font-medium">Level</th>
                  <th className="px-3 py-2 text-left text-gray-400 font-medium">Source</th>
                  <th className="w-8 px-3 py-2" />
                </tr>
              </thead>
              <tbody>
                {toScreen.length > 0 && (
                  <>
                    <SectionRow
                      label={`To screen (${toScreen.length})`}
                      tone="blue"
                    />
                    {toScreen.map(row => (
                      <PipelineRow
                        key={row.candidate.id}
                        row={row}
                        selected={selected.has(row.candidate.id)}
                        onToggle={() => toggle(row.candidate.id)}
                        onClick={() => onRowClick(row)}
                        onRemove={() => onRemove(row)}
                        onViewResume={() =>
                          setResumeModal({ id: row.candidate.id, name: row.displayName })
                        }
                        disabled={detachMutation.isPending}
                      />
                    ))}
                  </>
                )}
                {screened.length > 0 && (
                  <>
                    <SectionRow
                      label={`Screened (${screened.length}) — sorted by score`}
                      tone="gray"
                    />
                    {screened.map(row => (
                      <PipelineRow
                        key={row.candidate.id}
                        row={row}
                        selected={selected.has(row.candidate.id)}
                        onToggle={() => toggle(row.candidate.id)}
                        onClick={() => onRowClick(row)}
                        onRemove={() => onRemove(row)}
                        onViewResume={() =>
                          setResumeModal({ id: row.candidate.id, name: row.displayName })
                        }
                        disabled={detachMutation.isPending}
                      />
                    ))}
                  </>
                )}
                {toScreen.length === 0 && screened.length === 0 && (
                  <tr>
                    <td colSpan={7} className="px-3 py-6 text-center text-sm text-gray-500">
                      {loadingCandidates || loadingEvals ? 'Loading…' : 'No candidates.'}
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          {/* Mobile + tablet card list — same data, vertical layout. */}
          <div className="lg:hidden flex flex-col gap-3">
            {toScreen.length === 0 && screened.length === 0 ? (
              <div className="text-center text-sm text-gray-500 py-6">
                {loadingCandidates || loadingEvals ? 'Loading…' : 'No candidates.'}
              </div>
            ) : (
              <>
                {toScreen.length > 0 && (
                  <SectionHeader
                    label={`To screen (${toScreen.length})`}
                    tone="blue"
                  />
                )}
                {toScreen.map(row => (
                  <PipelineCard
                    key={row.candidate.id}
                    row={row}
                    selected={selected.has(row.candidate.id)}
                    onToggle={() => toggle(row.candidate.id)}
                    onClick={() => onRowClick(row)}
                    onRemove={() => onRemove(row)}
                    onViewResume={() =>
                      setResumeModal({ id: row.candidate.id, name: row.displayName })
                    }
                    disabled={detachMutation.isPending}
                  />
                ))}
                {screened.length > 0 && (
                  <SectionHeader
                    label={`Screened (${screened.length}) — sorted by score`}
                    tone="gray"
                  />
                )}
                {screened.map(row => (
                  <PipelineCard
                    key={row.candidate.id}
                    row={row}
                    selected={selected.has(row.candidate.id)}
                    onToggle={() => toggle(row.candidate.id)}
                    onClick={() => onRowClick(row)}
                    onRemove={() => onRemove(row)}
                    onViewResume={() =>
                      setResumeModal({ id: row.candidate.id, name: row.displayName })
                    }
                    disabled={detachMutation.isPending}
                  />
                ))}
              </>
            )}
          </div>
        </div>
      )}

      <DetailDrawer
        evaluation={activeEval}
        positionId={positionId || undefined}
        candidateFileUrl={
          activeEval && candidatesById.get(activeEval.candidateId)?.source === 'Uploaded'
            ? getCandidateFileUrl(activeEval.candidateId)
            : undefined
        }
        onViewResume={
          activeEval && candidatesById.get(activeEval.candidateId)?.source === 'Generated'
            ? () =>
                setResumeModal({
                  id: activeEval.candidateId,
                  name: displayNames.get(activeEval.candidateId) ?? formatCandidateName(activeEval.candidateName),
                })
            : undefined
        }
        onClose={() => setActiveCandidateId(null)}
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

      {showFindMatches && positionId && (
        <FindMatchesModal
          positionId={positionId}
          onClose={() => setShowFindMatches(false)}
        />
      )}
    </div>
  );
}

function SectionRow({ label, tone }: { label: string; tone: 'blue' | 'gray' }) {
  const cls =
    tone === 'blue'
      ? 'bg-blue-900/15 text-blue-300 border-blue-900/40'
      : 'bg-gray-800/40 text-gray-400 border-gray-800';
  return (
    <tr>
      <td
        colSpan={7}
        className={`px-3 py-1.5 text-[11px] uppercase tracking-wider font-semibold border-y ${cls}`}
      >
        {label}
      </td>
    </tr>
  );
}

function PipelineRow({
  row, selected, onToggle, onClick, onRemove, onViewResume, disabled,
}: {
  row: Row;
  selected: boolean;
  onToggle: () => void;
  onClick: () => void;
  onRemove: () => void;
  onViewResume: () => void;
  disabled: boolean;
}) {
  const { candidate: c, evaluation: e, status, displayName } = row;
  const actionable = status !== 'screened';

  // Adjacent <tr>s sharing the same hover/selected state so they read as one row.
  const rowBg = selected ? 'bg-blue-900/20' : 'hover:bg-gray-800/40';

  return (
    <>
    <tr
      onClick={onClick}
      className={`cursor-pointer transition-colors ${rowBg} ${e?.reasoning ? '' : 'border-b border-gray-800/50'}`}
    >
      <td className="px-3 py-2.5">
        {actionable ? (
          <input
            type="checkbox"
            checked={selected}
            onChange={onToggle}
            onClick={ev => ev.stopPropagation()}
            className="accent-blue-500"
          />
        ) : null}
      </td>
      <td className="px-3 py-2.5 text-gray-100">
        <div className="flex items-center gap-2 min-w-0">
          <span className="truncate">{displayName}</span>
          {c.source === 'Uploaded' ? (
            <a
              href={getCandidateFileUrl(c.id)}
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
              onClick={ev => { ev.stopPropagation(); onViewResume(); }}
              className="text-gray-500 hover:text-purple-400 transition-colors shrink-0"
              title="View generated CV"
            >
              <svg xmlns="http://www.w3.org/2000/svg" className="w-3.5 h-3.5" viewBox="0 0 20 20" fill="currentColor">
                <path d="M10 12a2 2 0 100-4 2 2 0 000 4z" />
                <path fillRule="evenodd" d="M.458 10C1.732 5.943 5.522 3 10 3s8.268 2.943 9.542 7c-1.274 4.057-5.064 7-9.542 7S1.732 14.057.458 10zM14 10a4 4 0 11-8 0 4 4 0 018 0z" clipRule="evenodd" />
              </svg>
            </button>
          )}
        </div>
      </td>
      <td className="px-3 py-2.5 whitespace-nowrap">
        <StatusBadge status={status} />
      </td>
      <td className="px-3 py-2.5">
        {e ? (
          <div className="flex items-center gap-1.5">
            <ScoreBadge score={e.score} matchLevel={e.matchLevel} />
            {e.recruiterAdjustment !== 0 && (
              <span
                className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-gray-800 text-gray-300 border border-gray-700"
                title={`Final: ${e.finalScore} (AI ${e.score}${
                  e.recruiterAdjustment >= 0 ? '+' : ''
                }${e.recruiterAdjustment})${
                  e.isAdjustmentStale ? ' — adjustment predates latest screen' : ''
                }`}
              >
                →{e.finalScore}
                {e.isAdjustmentStale && <span className="ml-1 text-amber-400">⚠</span>}
              </span>
            )}
          </div>
        ) : (
          <span className="text-gray-600">—</span>
        )}
      </td>
      <td className="px-3 py-2.5">
        {e ? (
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
        ) : (
          <span className="text-gray-600">—</span>
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
      <td className="px-3 py-2.5">
        <button
          onClick={ev => { ev.stopPropagation(); onRemove(); }}
          disabled={disabled}
          className="text-gray-600 hover:text-red-400 transition-colors text-lg leading-none"
          title="Remove from this position"
        >
          ×
        </button>
      </td>
    </tr>
    {e?.reasoning && (
      <tr
        onClick={onClick}
        className={`border-b border-gray-800/50 cursor-pointer transition-colors ${rowBg}`}
      >
        {/* Empty leading cells line up the reasoning under the Name column,
            full-width content area gives the text room to breathe without being clipped. */}
        <td />
        <td colSpan={6} className="px-3 pb-2.5 pt-0 text-xs text-gray-400 leading-relaxed">
          {e.reasoning}
        </td>
      </tr>
    )}
    </>
  );
}

function SectionHeader({ label, tone }: { label: string; tone: 'blue' | 'gray' }) {
  const cls =
    tone === 'blue'
      ? 'bg-blue-900/15 text-blue-300 border-blue-900/40'
      : 'bg-gray-800/40 text-gray-400 border-gray-800';
  return (
    <div
      className={`px-3 py-1.5 text-[11px] uppercase tracking-wider font-semibold border rounded-md ${cls}`}
    >
      {label}
    </div>
  );
}

// Vertical card variant of PipelineRow for the mobile breakpoint.
function PipelineCard({
  row, selected, onToggle, onClick, onRemove, onViewResume, disabled,
}: {
  row: Row;
  selected: boolean;
  onToggle: () => void;
  onClick: () => void;
  onRemove: () => void;
  onViewResume: () => void;
  disabled: boolean;
}) {
  const { candidate: c, evaluation: e, status, displayName } = row;
  const actionable = status !== 'screened';

  return (
    <div
      onClick={onClick}
      className={`border rounded-lg p-3 transition-colors ${
        selected
          ? 'border-blue-700/60 bg-blue-900/20'
          : 'border-gray-800 hover:border-gray-700'
      }`}
    >
      <div className="flex items-start gap-3">
        {actionable && (
          <input
            type="checkbox"
            checked={selected}
            onChange={onToggle}
            onClick={ev => ev.stopPropagation()}
            className="accent-blue-500 mt-1"
          />
        )}
        <div className="min-w-0 flex-1">
          <div className="flex items-start justify-between gap-2">
            <div className="min-w-0">
              <div className="flex items-center gap-2 text-gray-100 font-medium">
                <span className="truncate">{displayName}</span>
                {c.source === 'Uploaded' ? (
                  <a
                    href={getCandidateFileUrl(c.id)}
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
                    onClick={ev => { ev.stopPropagation(); onViewResume(); }}
                    className="text-gray-500 hover:text-purple-400 transition-colors shrink-0"
                    title="View generated CV"
                  >
                    <svg xmlns="http://www.w3.org/2000/svg" className="w-3.5 h-3.5" viewBox="0 0 20 20" fill="currentColor">
                      <path d="M10 12a2 2 0 100-4 2 2 0 000 4z" />
                      <path fillRule="evenodd" d="M.458 10C1.732 5.943 5.522 3 10 3s8.268 2.943 9.542 7c-1.274 4.057-5.064 7-9.542 7S1.732 14.057.458 10zM14 10a4 4 0 11-8 0 4 4 0 018 0z" clipRule="evenodd" />
                    </svg>
                  </button>
                )}
              </div>
              <div className="flex items-center gap-2 mt-1 flex-wrap">
                <StatusBadge status={status} />
                <span
                  className={`text-[10px] px-1.5 py-0.5 rounded ${
                    c.source === 'Generated'
                      ? 'bg-purple-900/40 text-purple-300'
                      : 'bg-gray-700 text-gray-300'
                  }`}
                >
                  {c.source}
                </span>
              </div>
            </div>
            <button
              onClick={ev => { ev.stopPropagation(); onRemove(); }}
              disabled={disabled}
              className="text-gray-600 hover:text-red-400 transition-colors text-lg leading-none shrink-0"
              title="Remove from this position"
            >
              ×
            </button>
          </div>

          {e && (
            <div className="flex items-center gap-2 mt-2.5 flex-wrap">
              <ScoreBadge score={e.score} matchLevel={e.matchLevel} />
              {e.recruiterAdjustment !== 0 && (
                <span
                  className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-gray-800 text-gray-300 border border-gray-700"
                  title={`Final: ${e.finalScore} (AI ${e.score}${
                    e.recruiterAdjustment >= 0 ? '+' : ''
                  }${e.recruiterAdjustment})`}
                >
                  →{e.finalScore}
                  {e.isAdjustmentStale && <span className="ml-1 text-amber-400">⚠</span>}
                </span>
              )}
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
            </div>
          )}

          {e?.reasoning && (
            <p className="text-xs text-gray-400 mt-2 line-clamp-2">{e.reasoning}</p>
          )}
        </div>
      </div>
    </div>
  );
}

function StatusBadge({ status }: { status: Row['status'] }) {
  if (status === 'unscreened') {
    return (
      <span className="text-xs px-2 py-0.5 rounded bg-blue-900/30 text-blue-300 border border-blue-700/30">
        To screen
      </span>
    );
  }
  if (status === 'stale') {
    return (
      <span className="text-xs px-2 py-0.5 rounded bg-amber-900/40 text-amber-300 border border-amber-700/40">
        ⚠ Outdated
      </span>
    );
  }
  return (
    <span className="text-xs px-2 py-0.5 rounded bg-green-900/30 text-green-400 border border-green-700/30">
      ✓ Screened
    </span>
  );
}
