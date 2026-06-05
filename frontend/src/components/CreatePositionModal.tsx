import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { createPosition, updatePosition } from '../api/positions';
import type { Position } from '../types';
import type { ReactNode } from 'react';

interface Props {
  onClose: () => void;
  onCreated: (position: Position) => void;
  // When provided, the modal operates in edit mode.
  position?: Position;
}

export function CreatePositionModal({ onClose, onCreated, position }: Props) {
  const qc = useQueryClient();
  const isEdit = !!position;

  const [title, setTitle] = useState(position?.title ?? '');
  const [description, setDescription] = useState(position?.description ?? '');
  const [country, setCountry] = useState(position?.country ?? '');
  const [seniorityLevel, setSeniorityLevel] = useState(position?.seniorityLevel ?? '');
  const [requiredRaw, setRequiredRaw] = useState(position?.requiredSkills.join(', ') ?? '');
  const [niceRaw, setNiceRaw] = useState(position?.niceToHaveSkills.join(', ') ?? '');

  const payload = () => ({
    title,
    description,
    country: country.trim() || null,
    seniorityLevel: seniorityLevel.trim() || null,
    requiredSkills: requiredRaw.split(',').map(s => s.trim()).filter(Boolean),
    niceToHaveSkills: niceRaw.split(',').map(s => s.trim()).filter(Boolean),
  });

  const mutation = useMutation({
    mutationFn: () =>
      isEdit ? updatePosition(position!.id, payload()) : createPosition(payload()),
    onSuccess: updated => {
      qc.invalidateQueries({ queryKey: ['positions'] });
      qc.invalidateQueries({ queryKey: ['evaluations'] });
      toast.success(isEdit ? 'Position updated' : 'Position created');
      onCreated(updated);
    },
    onError: () => toast.error(isEdit ? 'Failed to update position' : 'Failed to create position'),
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    mutation.mutate();
  };

  return (
    <div className="fixed inset-0 bg-black/60 z-50 flex items-center justify-center p-4">
      <div className="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-lg p-6">
        <div className="flex items-center justify-between mb-5">
          <h2 className="text-lg font-semibold text-gray-100">
            {isEdit ? 'Edit Position' : 'New Position'}
          </h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-100 text-2xl leading-none">
            ×
          </button>
        </div>

        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <Field label="Title *">
            <input
              type="text"
              value={title}
              onChange={e => setTitle(e.target.value)}
              className="input"
              placeholder="e.g. Senior Backend Engineer"
              required
            />
          </Field>

          <Field label="Job Description *">
            <textarea
              value={description}
              onChange={e => setDescription(e.target.value)}
              className="input min-h-[90px] resize-y"
              placeholder="Describe the role, responsibilities, requirements..."
              required
            />
          </Field>

          <div className="grid grid-cols-2 gap-3">
            <Field label="Country">
              <input
                type="text"
                value={country}
                onChange={e => setCountry(e.target.value)}
                className="input"
                placeholder="e.g. Israel"
              />
            </Field>
            <Field label="Seniority">
              <input
                type="text"
                value={seniorityLevel}
                onChange={e => setSeniorityLevel(e.target.value)}
                className="input"
                placeholder="e.g. Senior"
              />
            </Field>
          </div>

          <Field label="Required Skills * (comma-separated)">
            <input
              type="text"
              value={requiredRaw}
              onChange={e => setRequiredRaw(e.target.value)}
              className="input"
              placeholder="e.g. C#, .NET, SQL Server"
              required
            />
          </Field>

          <Field label="Nice to Have (comma-separated)">
            <input
              type="text"
              value={niceRaw}
              onChange={e => setNiceRaw(e.target.value)}
              className="input"
              placeholder="e.g. Redis, Docker, Kubernetes"
            />
          </Field>

          <div className="flex justify-end gap-3 pt-1">
            <button type="button" onClick={onClose} className="btn-secondary">
              Cancel
            </button>
            <button type="submit" disabled={mutation.isPending} className="btn-primary">
              {mutation.isPending
                ? isEdit ? 'Saving...' : 'Creating...'
                : isEdit ? 'Save Changes' : 'Create Position'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <label className="block text-sm font-medium text-gray-300 mb-1">{label}</label>
      {children}
    </div>
  );
}
