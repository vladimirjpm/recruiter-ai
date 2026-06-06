import type { ReactNode } from 'react';
import type { Evaluation } from '../types';
import { ScoreBadge } from './ScoreBadge';
import { formatCandidateName } from '../utils/formatName';

interface Props {
  evaluation: Evaluation | null;
  candidateFileUrl?: string;
  onViewResume?: () => void;
  onClose: () => void;
}

export function DetailDrawer({ evaluation, candidateFileUrl, onViewResume, onClose }: Props) {
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

        {/* TODO: Recruiter override panel
            - Text field: recruiter comment (free-form note, stored per evaluation)
            - Score adjustment: numeric input (+/- delta), final score = clamp(ai_score + delta, 0, 100)
            - Display both AI score and adjusted score separately so the original is preserved
            - Backend: add RecruiterComment (string?) + ScoreAdjustment (int, default 0) to Evaluation entity
            - API: PATCH /api/evaluations/{id}/review  { comment, scoreAdjustment }
            - UI: show adjusted score in ScoreBadge if adjustment != 0, with tooltip "AI: X | Adjusted: Y"
        */}

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
