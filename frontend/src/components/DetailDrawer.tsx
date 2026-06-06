import { useState, type ReactNode } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import type { Evaluation } from '../types';
import { ScoreBadge } from './ScoreBadge';
import { formatCandidateName } from '../utils/formatName';
import { updateAdjustment } from '../api/evaluations';

interface Props {
  evaluation: Evaluation | null;
  positionId?: string;          // required to enable the recruiter-override editor
  candidateFileUrl?: string;
  onViewResume?: () => void;
  onClose: () => void;
}

export function DetailDrawer({ evaluation, positionId, candidateFileUrl, onViewResume, onClose }: Props) {
  if (!evaluation) return null;

  return (
    <>
      <div className="fixed inset-0 bg-black/50 z-40" onClick={onClose} />
      <aside className="fixed right-0 top-0 h-full w-full md:w-[480px] bg-gray-900 border-l border-gray-700 z-50 overflow-y-auto p-6 pb-24 md:pb-6 flex flex-col gap-5">
        <div className="flex items-start justify-between gap-4">
          <div>
            <h2 className="text-lg font-semibold text-gray-100">{formatCandidateName(evaluation.candidateName)}</h2>
            {candidateFileUrl ? (
              <a
                href={candidateFileUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="text-sm text-blue-400 hover:text-blue-300 hover:underline transition-colors mt-0.5 inline-block"
                title="Open CV PDF"
              >
                {evaluation.candidateFileName}
              </a>
            ) : onViewResume ? (
              <button
                type="button"
                onClick={onViewResume}
                className="text-sm text-purple-400 hover:text-purple-300 hover:underline transition-colors mt-0.5 text-left"
              >
                View generated CV
              </button>
            ) : (
              <p className="text-sm text-gray-400 mt-0.5">{evaluation.candidateFileName}</p>
            )}
          </div>
          <div className="flex items-center gap-3 shrink-0">
            <ScoreBadge score={evaluation.score} matchLevel={evaluation.matchLevel} />
            <button
              onClick={onClose}
              className="text-gray-400 hover:text-gray-100 text-2xl leading-none"
              aria-label="Close"
            >
              ×
            </button>
          </div>
        </div>

        {positionId && (
          <RecruiterOverridePanel
            evaluation={evaluation}
            positionId={positionId}
          />
        )}

        {evaluation.isStale && (
          <div className="flex items-start gap-2 px-3 py-2.5 rounded-lg bg-amber-900/20 border border-amber-700/40 text-amber-300 text-xs">
            <span className="mt-0.5">⚠</span>
            <span>Position changed after this evaluation was created. Results may no longer reflect current requirements.</span>
          </div>
        )}

        <Section title="Reasoning">
          <p className="text-gray-300 text-sm leading-relaxed">{evaluation.reasoning}</p>
        </Section>

        {evaluation.strengths.length > 0 && (
          <Section title="Strengths">
            <TagList items={evaluation.strengths} color="green" />
          </Section>
        )}

        {evaluation.weaknesses.length > 0 && (
          <Section title="Weaknesses">
            <TagList items={evaluation.weaknesses} color="red" />
          </Section>
        )}

        {evaluation.matchedSkills.length > 0 && (
          <Section title="Matched Skills">
            <TagList items={evaluation.matchedSkills} color="blue" />
          </Section>
        )}

        {evaluation.missingSkills.length > 0 && (
          <Section title="Missing Skills">
            <TagList items={evaluation.missingSkills} color="orange" />
          </Section>
        )}

        {evaluation.redFlags.length > 0 && (
          <Section title="Red Flags">
            <TagList items={evaluation.redFlags} color="red" />
          </Section>
        )}

        {evaluation.interviewQuestions.length > 0 && (
          <Section title="Interview Questions">
            <ul className="list-disc list-inside space-y-1.5">
              {evaluation.interviewQuestions.map((q, i) => (
                <li key={i} className="text-gray-300 text-sm">{q}</li>
              ))}
            </ul>
          </Section>
        )}

        <div className="mt-auto pt-4 border-t border-gray-800 text-xs text-gray-500">
          {evaluation.aiModel}
          {evaluation.inputTokens != null && (
            <> · {(evaluation.inputTokens + (evaluation.outputTokens ?? 0)).toLocaleString()} tokens</>
          )}
          {evaluation.estimatedCost != null && (
            <> · ${evaluation.estimatedCost.toFixed(4)}</>
          )}
        </div>
      </aside>
    </>
  );
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div>
      <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-2">{title}</h3>
      {children}
    </div>
  );
}

type TagColor = 'green' | 'red' | 'blue' | 'orange';

const tagColorMap: Record<TagColor, string> = {
  green:  'bg-green-900/40 text-green-300',
  red:    'bg-red-900/40 text-red-300',
  blue:   'bg-blue-900/40 text-blue-300',
  orange: 'bg-orange-900/40 text-orange-300',
};

function TagList({ items, color }: { items: string[]; color: TagColor }) {
  return (
    <div className="flex flex-wrap gap-1.5">
      {items.map((item, i) => (
        <span key={i} className={`px-2 py-0.5 rounded text-xs ${tagColorMap[color]}`}>
          {item}
        </span>
      ))}
    </div>
  );
}

// ── Recruiter override panel ───────────────────────────────────────────────────
// Local form state mirrors backend rules:
//   - adjustment ∈ [-30, +30]
//   - non-zero adjustment requires comment ≥ 10 chars (validated client-side too)
//   - adjustment === 0 resets everything (comment cleared on the server)
// Backend is the source of truth — client validation only prevents obvious errors
// before a round-trip, the API still returns 400/422 on its own checks.

const ADJUSTMENT_MIN = -30;
const ADJUSTMENT_MAX = 30;
const COMMENT_MIN_LENGTH = 10;

function RecruiterOverridePanel({
  evaluation,
  positionId,
}: {
  evaluation: Evaluation;
  positionId: string;
}) {
  const qc = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [adjustment, setAdjustment] = useState<number>(evaluation.recruiterAdjustment);
  const [comment, setComment] = useState<string>(evaluation.recruiterComment ?? '');
  const [adjustedBy, setAdjustedBy] = useState<string>(evaluation.adjustedBy ?? '');

  const mutation = useMutation({
    mutationFn: () =>
      updateAdjustment(positionId, evaluation.candidateId, {
        recruiterAdjustment: adjustment,
        recruiterComment: adjustment === 0 ? null : comment.trim(),
        adjustedBy: adjustedBy.trim() ? adjustedBy.trim() : null,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['evaluations', positionId] });
      setEditing(false);
      toast.success(adjustment === 0 ? 'Adjustment cleared' : 'Adjustment saved');
    },
    onError: () => toast.error('Could not save adjustment'),
  });

  // Validation matches the server: non-zero adjustment ⇒ comment ≥ 10 chars.
  const trimmedCommentLength = comment.trim().length;
  const isValid =
    adjustment >= ADJUSTMENT_MIN &&
    adjustment <= ADJUSTMENT_MAX &&
    (adjustment === 0 || trimmedCommentLength >= COMMENT_MIN_LENGTH);

  const hasAdjustment = evaluation.recruiterAdjustment !== 0;
  const finalScoreDelta = evaluation.finalScore - evaluation.score;

  return (
    <div className="rounded-lg border border-gray-800 bg-gray-900/40 p-3 flex flex-col gap-3">
      <div className="flex items-center justify-between gap-2">
        <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider">
          Recruiter override
        </h3>
        {!editing && (
          <button
            type="button"
            onClick={() => {
              setAdjustment(evaluation.recruiterAdjustment);
              setComment(evaluation.recruiterComment ?? '');
              setAdjustedBy(evaluation.adjustedBy ?? '');
              setEditing(true);
            }}
            className="text-xs text-blue-400 hover:text-blue-300"
          >
            {hasAdjustment ? 'Edit' : '+ Add'}
          </button>
        )}
      </div>

      {/* Read-only score summary — always visible so AI score stays explicit. */}
      <div className="grid grid-cols-3 gap-2 text-center">
        <div className="rounded bg-gray-800/60 px-2 py-1.5">
          <div className="text-[10px] uppercase tracking-wider text-gray-500">AI score</div>
          <div className="text-lg font-semibold text-gray-100">{evaluation.score}</div>
        </div>
        <div className="rounded bg-gray-800/60 px-2 py-1.5">
          <div className="text-[10px] uppercase tracking-wider text-gray-500">Adjustment</div>
          <div
            className={`text-lg font-semibold ${
              evaluation.recruiterAdjustment > 0
                ? 'text-green-400'
                : evaluation.recruiterAdjustment < 0
                  ? 'text-red-400'
                  : 'text-gray-500'
            }`}
          >
            {evaluation.recruiterAdjustment > 0 ? '+' : ''}
            {evaluation.recruiterAdjustment}
          </div>
        </div>
        <div className="rounded bg-blue-900/30 px-2 py-1.5">
          <div className="text-[10px] uppercase tracking-wider text-blue-300">Final score</div>
          <div className="text-lg font-semibold text-blue-200">{evaluation.finalScore}</div>
        </div>
      </div>

      {hasAdjustment && finalScoreDelta !== (evaluation.recruiterAdjustment) && (
        <p className="text-[11px] text-gray-500">
          Clamped to 0–100 range (adjustment {evaluation.recruiterAdjustment > 0 ? '+' : ''}
          {evaluation.recruiterAdjustment}, applied {finalScoreDelta > 0 ? '+' : ''}
          {finalScoreDelta}).
        </p>
      )}

      {evaluation.isAdjustmentStale && (
        <div className="flex items-start gap-2 px-2.5 py-2 rounded bg-amber-900/20 border border-amber-700/40 text-amber-300 text-xs">
          <span className="mt-0.5">⚠</span>
          <span>
            Recruiter adjustment was made before the latest AI re-screen. Please review.
          </span>
        </div>
      )}

      {!editing && hasAdjustment && evaluation.recruiterComment && (
        <div className="text-xs text-gray-300 italic border-l-2 border-gray-700 pl-2.5">
          “{evaluation.recruiterComment}”
          {(evaluation.adjustedBy || evaluation.adjustedAt) && (
            <div className="not-italic text-[10px] text-gray-500 mt-1">
              {evaluation.adjustedBy ?? 'anonymous'}
              {evaluation.adjustedAt && (
                <> · {new Date(evaluation.adjustedAt).toLocaleString()}</>
              )}
            </div>
          )}
        </div>
      )}

      {editing && (
        <div className="flex flex-col gap-2">
          <label className="text-xs text-gray-400">
            Adjustment ({ADJUSTMENT_MIN} … +{ADJUSTMENT_MAX})
            <input
              type="number"
              min={ADJUSTMENT_MIN}
              max={ADJUSTMENT_MAX}
              value={adjustment}
              onChange={e => {
                const raw = Number.parseInt(e.target.value, 10);
                setAdjustment(Number.isNaN(raw) ? 0 : raw);
              }}
              className="mt-1 w-full bg-gray-800 border border-gray-700 rounded px-2 py-1 text-sm text-gray-100 focus:outline-none focus:border-blue-600"
            />
          </label>

          {adjustment !== 0 && (
            <>
              <label className="text-xs text-gray-400">
                Comment (min {COMMENT_MIN_LENGTH} chars, required)
                <textarea
                  value={comment}
                  onChange={e => setComment(e.target.value)}
                  rows={3}
                  placeholder="e.g. Strong domain experience not visible in CV"
                  className="mt-1 w-full bg-gray-800 border border-gray-700 rounded px-2 py-1 text-sm text-gray-100 focus:outline-none focus:border-blue-600 resize-none"
                />
                <span
                  className={`text-[10px] mt-0.5 block ${
                    trimmedCommentLength >= COMMENT_MIN_LENGTH ? 'text-gray-500' : 'text-amber-400'
                  }`}
                >
                  {trimmedCommentLength}/{COMMENT_MIN_LENGTH}
                </span>
              </label>

              <label className="text-xs text-gray-400">
                Adjusted by (optional)
                <input
                  type="text"
                  value={adjustedBy}
                  onChange={e => setAdjustedBy(e.target.value)}
                  placeholder="Your name"
                  className="mt-1 w-full bg-gray-800 border border-gray-700 rounded px-2 py-1 text-sm text-gray-100 focus:outline-none focus:border-blue-600"
                />
              </label>
            </>
          )}

          {adjustment === 0 && evaluation.recruiterAdjustment !== 0 && (
            <p className="text-[11px] text-gray-500">
              Saving with 0 clears the comment and resets the override.
            </p>
          )}

          <div className="flex items-center gap-2 pt-1">
            <button
              type="button"
              onClick={() => mutation.mutate()}
              disabled={!isValid || mutation.isPending}
              className="btn-primary text-xs py-1 px-3 disabled:opacity-50"
            >
              {mutation.isPending ? 'Saving…' : 'Save'}
            </button>
            <button
              type="button"
              onClick={() => setEditing(false)}
              disabled={mutation.isPending}
              className="btn-secondary text-xs py-1 px-3"
            >
              Cancel
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
