import { useQuery } from '@tanstack/react-query';
import { getCandidateResumeText } from '../api/candidates';

interface Props {
  candidateId: string;
  candidateName: string;
  onClose: () => void;
}

export function ResumeTextModal({ candidateId, candidateName, onClose }: Props) {
  const { data: text, isPending, isError } = useQuery({
    queryKey: ['resume-text', candidateId],
    queryFn: () => getCandidateResumeText(candidateId),
    // Resume text is stable — generated once, never changes.
    staleTime: Infinity,
  });

  return (
    <div
      className="fixed inset-0 bg-black/60 z-50 flex items-center justify-center p-4"
      onClick={onClose}
    >
      <div
        className="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-2xl flex flex-col max-h-[85vh]"
        onClick={e => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-700 shrink-0">
          <div>
            <p className="text-xs text-purple-400 font-medium mb-0.5">Generated CV</p>
            <h2 className="text-base font-semibold text-gray-100">{candidateName}</h2>
          </div>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-100 text-2xl leading-none"
          >
            ×
          </button>
        </div>

        {/* Body */}
        <div className="overflow-y-auto px-5 py-4 flex-1">
          {isPending && (
            <p className="text-gray-500 text-sm">Loading…</p>
          )}
          {isError && (
            <p className="text-red-400 text-sm">Failed to load resume text.</p>
          )}
          {text && (
            <pre className="text-sm text-gray-300 whitespace-pre-wrap leading-relaxed font-mono">
              {text}
            </pre>
          )}
        </div>
      </div>
    </div>
  );
}
