import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { getPositions, getPosition, deletePosition } from '../api/positions';
import { CreatePositionModal } from '../components/CreatePositionModal';
import { WorkflowHint } from '../components/WorkflowHint';
import type { Position, PositionSummary } from '../types';

export function PositionsPage() {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const [showCreate, setShowCreate] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);

  const { data: positions = [], isLoading } = useQuery({
    queryKey: ['positions'],
    queryFn: getPositions,
  });

  const { data: editing } = useQuery({
    queryKey: ['position', editingId],
    queryFn: () => getPosition(editingId!),
    enabled: !!editingId,
  });

  const deleteMutation = useMutation({
    mutationFn: deletePosition,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['positions'] });
      toast.success('Position deleted');
    },
    onError: () => toast.error('Delete failed'),
  });

  const onDelete = (p: PositionSummary) => {
    if (!confirm(`Delete position "${p.title}"? Evaluations stay in history.`)) return;
    deleteMutation.mutate(p.id);
  };

  return (
    <div className="p-6 max-w-5xl mx-auto flex flex-col gap-5">
      <WorkflowHint current="positions" />
      <PageHeader
        title="Positions"
        subtitle="Job descriptions you screen candidates against"
        action={
          positions.length > 0 ? (
            <button onClick={() => setShowCreate(true)} className="btn-primary">
              + New Position
            </button>
          ) : null
        }
      />

      {isLoading ? (
        <div className="card text-sm text-gray-500">Loading positions…</div>
      ) : positions.length === 0 ? (
        <EmptyPositionsState onCreate={() => setShowCreate(true)} />
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {positions.map(p => (
            <PositionCard
              key={p.id}
              position={p}
              onEdit={() => setEditingId(p.id)}
              onDelete={() => onDelete(p)}
              onOpenCandidates={() => navigate(`/candidates?positionId=${p.id}`)}
              onOpenScreening={() => navigate(`/screening?positionId=${p.id}`)}
            />
          ))}
        </div>
      )}

      {showCreate && (
        <CreatePositionModal
          onClose={() => setShowCreate(false)}
          onCreated={(p: Position) => {
            setShowCreate(false);
            qc.invalidateQueries({ queryKey: ['positions'] });
            navigate(`/candidates?positionId=${p.id}`);
          }}
        />
      )}

      {editingId && editing && (
        <CreatePositionModal
          position={editing}
          onClose={() => setEditingId(null)}
          onCreated={() => {
            setEditingId(null);
            qc.invalidateQueries({ queryKey: ['positions'] });
          }}
        />
      )}
    </div>
  );
}

function PageHeader({
  title, subtitle, action,
}: { title: string; subtitle?: string; action?: React.ReactNode }) {
  return (
    <div className="flex items-end justify-between gap-4 flex-wrap">
      <div>
        <h1 className="text-2xl font-semibold text-gray-100">{title}</h1>
        {subtitle && <p className="text-sm text-gray-500 mt-1">{subtitle}</p>}
      </div>
      {action}
    </div>
  );
}

function EmptyPositionsState({ onCreate }: { onCreate: () => void }) {
  return (
    <div className="card flex flex-col items-center text-center py-14 gap-4 border-dashed">
      <div className="w-14 h-14 rounded-2xl bg-blue-600/15 border border-blue-500/30 flex items-center justify-center text-blue-400">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" className="w-7 h-7">
          <path d="M4 6h16M4 12h16M4 18h10" strokeLinecap="round" />
        </svg>
      </div>
      <div className="max-w-md">
        <h2 className="text-base font-semibold text-gray-100">No positions yet</h2>
        <p className="text-sm text-gray-500 mt-1">
          Create your first position to start screening candidates. Paste a job description and the
          AI will extract title, seniority, country and required skills for you.
        </p>
      </div>
      <button onClick={onCreate} className="btn-primary">
        + Create Position
      </button>
      <p className="text-xs text-gray-600 mt-2">
        Next: upload candidates → run screening → review ranked results.
      </p>
    </div>
  );
}

function PositionCard({
  position, onEdit, onDelete, onOpenCandidates, onOpenScreening,
}: {
  position: PositionSummary;
  onEdit: () => void;
  onDelete: () => void;
  onOpenCandidates: () => void;
  onOpenScreening: () => void;
}) {
  return (
    <div className="card flex flex-col gap-3 hover:border-gray-700 transition-colors">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <h3 className="text-base font-semibold text-gray-100 truncate" title={position.title}>
            {position.title}
          </h3>
          <div className="flex items-center gap-2 mt-1 text-xs text-gray-500 flex-wrap">
            {position.seniorityLevel && (
              <span className="px-1.5 py-0.5 rounded bg-gray-800 text-gray-300">
                {position.seniorityLevel}
              </span>
            )}
            {position.country && <span>· {position.country}</span>}
            <span>· Created {new Date(position.createdAt).toLocaleDateString()}</span>
            {position.updatedAt && (
              <span className="text-amber-500/80">· Edited</span>
            )}
          </div>
        </div>
        <button
          onClick={onDelete}
          className="text-gray-600 hover:text-red-400 transition-colors text-lg leading-none shrink-0"
          title="Delete position"
        >
          ×
        </button>
      </div>

      <div className="flex items-center gap-2 mt-1">
        <button onClick={onOpenCandidates} className="btn-secondary text-xs py-1.5">
          Candidates →
        </button>
        <button onClick={onOpenScreening} className="btn-secondary text-xs py-1.5">
          Screening →
        </button>
        <button onClick={onEdit} className="btn-secondary text-xs py-1.5 ml-auto">
          Edit
        </button>
      </div>
    </div>
  );
}
