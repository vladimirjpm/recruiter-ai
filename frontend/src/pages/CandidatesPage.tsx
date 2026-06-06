import { useMemo } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { usePersistedPositionId } from '../utils/usePersistedPositionId';
import { WorkflowHint } from '../components/WorkflowHint';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import {
  getCandidatesForPosition,
  getCandidateFileUrl,
  uploadCandidates,
  deleteCandidate,
} from '../api/candidates';
import { getPosition } from '../api/positions';
import { DropZone } from '../components/DropZone';
import { PositionSelector } from '../components/PositionSelector';
import { EmptyState } from '../components/EmptyState';
import { formatCandidateName } from '../utils/formatName';

export function CandidatesPage() {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const [positionId, setPositionId] = usePersistedPositionId();

  const { data: position } = useQuery({
    queryKey: ['position', positionId],
    queryFn: () => getPosition(positionId),
    enabled: !!positionId,
  });

  const { data: page, isLoading } = useQuery({
    queryKey: ['position-candidates', positionId],
    queryFn: () => getCandidatesForPosition(positionId),
    enabled: !!positionId,
  });

  const candidates = page?.items ?? [];

  const uploadMutation = useMutation({
    mutationFn: (files: File[]) => uploadCandidates(files, positionId),
    onSuccess: results => {
      qc.invalidateQueries({ queryKey: ['position-candidates', positionId] });
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
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['position-candidates', positionId] });
      toast.success('Candidate removed');
    },
    onError: () => toast.error('Delete failed'),
  });

  const headerSubtitle = useMemo(() => {
    if (!position) return 'Upload CVs or attach generated candidates to a position';
    return position.title;
  }, [position]);

  return (
    <div className="p-6 max-w-5xl mx-auto flex flex-col gap-5">
      <WorkflowHint current="candidates" />
      <div className="flex items-end justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-2xl font-semibold text-gray-100">Candidates</h1>
          <p className="text-sm text-gray-500 mt-1 truncate max-w-xl" title={headerSubtitle}>
            {headerSubtitle}
          </p>
        </div>
      </div>

      <div className="card flex items-center gap-3 flex-wrap">
        <label className="text-sm font-medium text-gray-300 shrink-0">Position</label>
        <PositionSelector
          value={positionId}
          onChange={setPositionId}
          className="max-w-sm"
        />
        {positionId && (
          <button
            onClick={() => navigate(`/screening?positionId=${positionId}`)}
            className="btn-secondary ml-auto"
          >
            Go to Screening →
          </button>
        )}
      </div>

      {!positionId ? (
        <EmptyState
          icon={
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" className="w-7 h-7">
              <circle cx="12" cy="9" r="3.5" />
              <path d="M5 19c0-3.3 3.1-6 7-6s7 2.7 7 6" strokeLinecap="round" />
            </svg>
          }
          title="Select a position"
          description="Candidates are scoped per position. Pick one above, or create a new position first."
          action={
            <Link to="/positions" className="btn-secondary text-sm">
              Manage Positions →
            </Link>
          }
        />
      ) : isLoading ? (
        <div className="card text-sm text-gray-500">Loading candidates…</div>
      ) : candidates.length === 0 ? (
        <div className="card flex flex-col gap-4">
          <DropZone
            onFiles={files => uploadMutation.mutate(files)}
            isUploading={uploadMutation.isPending}
          />
          <EmptyState
            icon={
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" className="w-7 h-7">
                <path d="M12 4v9M8 8l4-4 4 4" strokeLinecap="round" strokeLinejoin="round" />
                <path d="M4 16v3a1 1 0 001 1h14a1 1 0 001-1v-3" strokeLinecap="round" />
              </svg>
            }
            title="No candidates for this position yet"
            description="Upload PDFs above, or use the Generator to produce synthetic CVs for fast validation."
            action={
              <Link
                to={`/generator?positionId=${positionId}`}
                className="btn-secondary text-sm"
              >
                Open Generator →
              </Link>
            }
          />
        </div>
      ) : (
        <div className="card flex flex-col gap-4">
          <DropZone
            onFiles={files => uploadMutation.mutate(files)}
            isUploading={uploadMutation.isPending}
          />

          <div className="border border-gray-800 rounded-lg overflow-hidden">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-800 bg-gray-800/50">
                  <th className="px-3 py-2 text-left text-gray-400 font-medium">Name</th>
                  <th className="px-3 py-2 text-left text-gray-400 font-medium">File</th>
                  <th className="px-3 py-2 text-left text-gray-400 font-medium">Source</th>
                  <th className="px-3 py-2 text-left text-gray-400 font-medium">Attached</th>
                  <th className="w-8 px-3 py-2" />
                </tr>
              </thead>
              <tbody>
                {candidates.map(c => (
                  <tr key={c.id} className="border-b border-gray-800/50 hover:bg-gray-800/40 transition-colors">
                    <td className="px-3 py-2.5 text-gray-100">{formatCandidateName(c.name)}</td>
                    <td className="px-3 py-2.5 max-w-[260px] truncate">
                      {c.source === 'Uploaded' ? (
                        <a
                          href={getCandidateFileUrl(c.id)}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="text-blue-400 hover:text-blue-300 hover:underline transition-colors"
                          title="Open CV PDF"
                        >
                          {c.fileName}
                        </a>
                      ) : (
                        <span className="text-purple-400">Generated CV</span>
                      )}
                    </td>
                    <td className="px-3 py-2.5">
                      <SourceBadge context={c.attachSourceContext} />
                    </td>
                    <td className="px-3 py-2.5 text-gray-400">
                      {new Date(c.attachedAt).toLocaleDateString()}
                    </td>
                    <td className="px-3 py-2.5">
                      <button
                        onClick={() => {
                          if (!confirm(`Remove "${formatCandidateName(c.name)}"? This deletes the candidate globally.`)) return;
                          deleteMutation.mutate(c.id);
                        }}
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

          <div className="flex items-center justify-between text-sm text-gray-500">
            <span>{candidates.length} candidate{candidates.length !== 1 ? 's' : ''} attached</span>
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

function SourceBadge({ context }: { context: 'Uploaded' | 'Generated' | 'ManuallyAttached' }) {
  const styles =
    context === 'Generated'
      ? 'bg-purple-900/40 text-purple-300'
      : context === 'Uploaded'
        ? 'bg-blue-900/40 text-blue-300'
        : 'bg-gray-700 text-gray-300';
  const label = context === 'ManuallyAttached' ? 'Attached' : context;
  return (
    <span className={`text-xs px-2 py-0.5 rounded ${styles}`}>{label}</span>
  );
}
