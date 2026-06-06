import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { createPosition, updatePosition, extractPosition } from '../api/positions';
import type { Position, ExtractedSkill, ExtractionResult, ConfidenceLevel, MissingInfoField } from '../types';
import type { ReactNode } from 'react';

interface Props {
  onClose: () => void;
  onCreated: (position: Position) => void;
  position?: Position;
}

type Tab = 'manual' | 'paste';

const MISSING_LABELS: Record<MissingInfoField, string> = {
  Country: 'Country',
  Seniority: 'Seniority level',
  Salary: 'Salary',
  WorkingArrangement: 'Working arrangement',
  ContractType: 'Contract type',
  TeamSize: 'Team size',
};

export function CreatePositionModal({ onClose, onCreated, position }: Props) {
  const qc = useQueryClient();
  const isEdit = !!position;

  const [activeTab, setActiveTab] = useState<Tab>('manual');

  // Form fields
  const [title, setTitle] = useState(position?.title ?? '');
  const [description, setDescription] = useState(position?.description ?? '');
  const [country, setCountry] = useState(position?.country ?? '');
  const [seniorityLevel, setSeniorityLevel] = useState(position?.seniorityLevel ?? '');
  // Skills as { name, evidence } — evidence is '' for manually typed skills
  const [requiredSkills, setRequiredSkills] = useState<ExtractedSkill[]>(
    position?.requiredSkills.map(s => ({ name: s, evidence: '' })) ?? []
  );
  const [niceSkills, setNiceSkills] = useState<ExtractedSkill[]>(
    position?.niceToHaveSkills.map(s => ({ name: s, evidence: '' })) ?? []
  );
  // Raw inputs for the tag fields (what user types before pressing Enter/comma)
  const [requiredInput, setRequiredInput] = useState('');
  const [niceInput, setNiceInput] = useState('');

  // Confidence state — set after extraction to drive yellow highlights
  const [confidence, setConfidence] = useState<ExtractionResult['confidence'] | null>(null);
  const [missingInfo, setMissingInfo] = useState<MissingInfoField[]>([]);
  const [extractionMeta, setExtractionMeta] = useState<ExtractionResult['metadata'] | null>(null);
  const [metaExpanded, setMetaExpanded] = useState(false);

  // Paste JD state
  const [jdText, setJdText] = useState('');
  const [jdError, setJdError] = useState<string | null>(null);

  const extractMutation = useMutation({
    mutationFn: () => extractPosition(jdText),
    onSuccess: (data) => {
      setTitle(data.title);
      setDescription(data.description);
      setCountry(data.country ?? '');
      setSeniorityLevel(data.seniorityLevel ?? '');
      setRequiredSkills(data.requiredSkills);
      setNiceSkills(data.niceToHaveSkills);
      setConfidence(data.confidence);
      setMissingInfo(data.missingInformation);
      setExtractionMeta(data.metadata);
      setJdError(null);
    },
    onError: (err: unknown) => {
      const status = (err as { response?: { status: number; data?: { error?: string } } })?.response?.status;
      if (status === 400) {
        const msg = (err as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Invalid input';
        setJdError(msg);
      } else {
        toast.error('AI extraction failed, please try again or fill manually');
      }
    },
  });

  const submitMutation = useMutation({
    mutationFn: () => {
      const p = {
        title,
        description,
        country: country.trim() || null,
        seniorityLevel: seniorityLevel.trim() || null,
        requiredSkills: requiredSkills.map(s => s.name),
        niceToHaveSkills: niceSkills.map(s => s.name),
      };
      return isEdit ? updatePosition(position!.id, p) : createPosition(p);
    },
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
    submitMutation.mutate();
  };

  const addSkill = (
    input: string,
    setList: React.Dispatch<React.SetStateAction<ExtractedSkill[]>>,
    setInput: React.Dispatch<React.SetStateAction<string>>
  ) => {
    const name = input.trim().replace(/,$/, '');
    if (!name) return;
    setList(prev => [...prev, { name, evidence: '' }]);
    setInput('');
  };

  const handleSkillKeyDown = (
    e: React.KeyboardEvent<HTMLInputElement>,
    input: string,
    setList: React.Dispatch<React.SetStateAction<ExtractedSkill[]>>,
    setInput: React.Dispatch<React.SetStateAction<string>>
  ) => {
    if (e.key === 'Enter' || e.key === ',') {
      e.preventDefault();
      addSkill(input, setList, setInput);
    }
  };

  const removeSkill = (
    index: number,
    setList: React.Dispatch<React.SetStateAction<ExtractedSkill[]>>
  ) => setList(prev => prev.filter((_, i) => i !== index));

  const lowConfidence = (level: ConfidenceLevel | undefined) =>
    level === 'Low' || level === 'NotDetected';

  const fieldClass = (conf: ConfidenceLevel | undefined) =>
    `input ${lowConfidence(conf) ? 'border-yellow-500 focus:ring-yellow-500' : ''}`;

  return (
    <div className="fixed inset-0 bg-black/60 z-50 flex items-center justify-center p-4">
      <div className="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-lg p-6 max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between mb-5">
          <h2 className="text-lg font-semibold text-gray-100">
            {isEdit ? 'Edit Position' : 'New Position'}
          </h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-100 text-2xl leading-none">
            ×
          </button>
        </div>

        {/* Tab switcher — only in create mode */}
        {!isEdit && (
          <div className="flex gap-1 mb-5 bg-gray-800 rounded-lg p-1">
            <TabButton active={activeTab === 'manual'} onClick={() => setActiveTab('manual')}>
              Manual
            </TabButton>
            <TabButton active={activeTab === 'paste'} onClick={() => setActiveTab('paste')}>
              Paste JD
            </TabButton>
          </div>
        )}

        {/* Paste JD tab */}
        {activeTab === 'paste' && !isEdit && (
          <div className="flex flex-col gap-3 mb-5">
            <div>
              <textarea
                value={jdText}
                onChange={e => { setJdText(e.target.value); setJdError(null); }}
                className={`input min-h-[160px] resize-y w-full ${jdError ? 'border-red-500' : ''}`}
                placeholder="Paste the full job description here (100–20 000 characters)..."
              />
              <div className="flex items-center justify-between mt-1">
                <span className={`text-xs ${jdText.length < 100 ? 'text-gray-500' : 'text-gray-400'}`}>
                  {jdText.length < 100
                    ? `${jdText.length} / 100 chars`
                    : `${jdText.length.toLocaleString()} chars`}
                </span>
                {jdError && (
                  <span className="text-xs text-red-400">{jdError}</span>
                )}
              </div>
            </div>

            <button
              type="button"
              disabled={jdText.length < 100 || extractMutation.isPending}
              onClick={() => extractMutation.mutate()}
              className="btn-primary flex items-center justify-center gap-2 disabled:opacity-40"
            >
              {extractMutation.isPending ? (
                <>
                  <Spinner />
                  Extracting…
                </>
              ) : (
                'Extract with AI'
              )}
            </button>

            {extractMutation.isSuccess && (
              <p className="text-xs text-green-400">
                Extraction complete — review and edit the fields below, then click "Create Position".
              </p>
            )}
          </div>
        )}

        {/* Form — shown in manual tab always, in paste tab after extraction */}
        {(activeTab === 'manual' || extractMutation.isSuccess) && (
          <form onSubmit={handleSubmit} className="flex flex-col gap-4">

            {/* Missing info banner */}
            {missingInfo.length > 0 && (
              <div className="bg-yellow-950/60 border border-yellow-700 rounded-lg px-3 py-2 text-sm text-yellow-300">
                <span className="font-medium">This job description is missing: </span>
                {missingInfo.map(f => MISSING_LABELS[f]).join(', ')}
              </div>
            )}

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
              <Field
                label="Country"
                hint={confidence?.country === 'NotDetected' ? 'Could not detect — please select' : undefined}
              >
                <input
                  type="text"
                  value={country}
                  onChange={e => setCountry(e.target.value)}
                  className={fieldClass(confidence?.country)}
                  placeholder="e.g. Israel"
                />
              </Field>
              <Field
                label="Seniority"
                hint={confidence?.seniority === 'NotDetected' ? 'Could not detect — please select' : undefined}
              >
                <input
                  type="text"
                  value={seniorityLevel}
                  onChange={e => setSeniorityLevel(e.target.value)}
                  className={fieldClass(confidence?.seniority)}
                  placeholder="e.g. Senior"
                />
              </Field>
            </div>

            <Field label="Required Skills *">
              <SkillTagInput
                skills={requiredSkills}
                input={requiredInput}
                onInputChange={setRequiredInput}
                onKeyDown={e => handleSkillKeyDown(e, requiredInput, setRequiredSkills, setRequiredInput)}
                onRemove={i => removeSkill(i, setRequiredSkills)}
                onBlur={() => addSkill(requiredInput, setRequiredSkills, setRequiredInput)}
                lowConfidence={lowConfidence(confidence?.skills)}
                placeholder="Type a skill, press Enter or comma…"
              />
            </Field>

            <Field label="Nice to Have">
              <SkillTagInput
                skills={niceSkills}
                input={niceInput}
                onInputChange={setNiceInput}
                onKeyDown={e => handleSkillKeyDown(e, niceInput, setNiceSkills, setNiceInput)}
                onRemove={i => removeSkill(i, setNiceSkills)}
                onBlur={() => addSkill(niceInput, setNiceSkills, setNiceInput)}
                lowConfidence={false}
                placeholder="Type a skill, press Enter or comma…"
              />
            </Field>

            {/* AI extraction metadata — collapsed by default */}
            {extractionMeta && (
              <div className="border border-gray-700 rounded-lg overflow-hidden">
                <button
                  type="button"
                  onClick={() => setMetaExpanded(v => !v)}
                  className="w-full flex items-center justify-between px-3 py-2 text-sm text-gray-400 hover:text-gray-200 hover:bg-gray-800 transition-colors"
                >
                  <span>AI Extraction details</span>
                  <span>{metaExpanded ? '▲' : '▼'}</span>
                </button>
                {metaExpanded && (
                  <div className="px-3 py-2 text-xs text-gray-500 space-y-1 bg-gray-800/40">
                    <div><span className="text-gray-400">Model:</span> {extractionMeta.model}</div>
                    <div><span className="text-gray-400">Prompt version:</span> {extractionMeta.promptVersion}</div>
                    <div><span className="text-gray-400">Extracted at:</span> {new Date(extractionMeta.extractedAt).toLocaleString()}</div>
                    <div><span className="text-gray-400">Input chars:</span> {extractionMeta.inputCharCount.toLocaleString()}</div>
                  </div>
                )}
              </div>
            )}

            <div className="flex justify-end gap-3 pt-1">
              <button type="button" onClick={onClose} className="btn-secondary">
                Cancel
              </button>
              <button type="submit" disabled={submitMutation.isPending} className="btn-primary">
                {submitMutation.isPending
                  ? isEdit ? 'Saving…' : 'Creating…'
                  : isEdit ? 'Save Changes' : 'Create Position'}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}

// ─── Sub-components ────────────────────────────────────────────────────────────

function TabButton({ active, onClick, children }: { active: boolean; onClick: () => void; children: ReactNode }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`flex-1 py-1.5 text-sm font-medium rounded-md transition-colors ${
        active
          ? 'bg-gray-700 text-gray-100'
          : 'text-gray-400 hover:text-gray-200'
      }`}
    >
      {children}
    </button>
  );
}

interface SkillTagInputProps {
  skills: ExtractedSkill[];
  input: string;
  onInputChange: (v: string) => void;
  onKeyDown: (e: React.KeyboardEvent<HTMLInputElement>) => void;
  onRemove: (index: number) => void;
  onBlur: () => void;
  lowConfidence: boolean;
  placeholder: string;
}

function SkillTagInput({ skills, input, onInputChange, onKeyDown, onRemove, onBlur, lowConfidence, placeholder }: SkillTagInputProps) {
  return (
    <div className={`input min-h-[42px] flex flex-wrap gap-1.5 items-center cursor-text ${lowConfidence ? 'border-yellow-500' : ''}`}>
      {skills.map((skill, i) => (
        <SkillTag key={i} skill={skill} onRemove={() => onRemove(i)} />
      ))}
      <input
        type="text"
        value={input}
        onChange={e => onInputChange(e.target.value)}
        onKeyDown={onKeyDown}
        onBlur={onBlur}
        className="bg-transparent outline-none text-sm text-gray-200 flex-1 min-w-[120px] placeholder:text-gray-500"
        placeholder={skills.length === 0 ? placeholder : ''}
      />
    </div>
  );
}

function SkillTag({ skill, onRemove }: { skill: ExtractedSkill; onRemove: () => void }) {
  return (
    <span className="group relative flex items-center gap-1 bg-gray-700 text-gray-200 text-xs px-2 py-0.5 rounded-full">
      {skill.name}
      {skill.evidence && (
        // Tooltip with source quote on hover
        <span className="pointer-events-none absolute bottom-full left-1/2 -translate-x-1/2 mb-1.5 w-56 bg-gray-800 border border-gray-600 text-gray-300 text-xs rounded px-2 py-1.5 opacity-0 group-hover:opacity-100 transition-opacity z-10 leading-relaxed">
          "{skill.evidence}"
        </span>
      )}
      <button
        type="button"
        onClick={onRemove}
        className="text-gray-400 hover:text-gray-100 leading-none ml-0.5"
      >
        ×
      </button>
    </span>
  );
}

function Field({ label, children, hint }: { label: string; children: ReactNode; hint?: string }) {
  return (
    <div>
      <div className="flex items-center gap-1.5 mb-1">
        <label className="block text-sm font-medium text-gray-300">{label}</label>
        {hint && (
          <span className="text-xs text-yellow-400 flex items-center gap-1">
            <span>⚠</span>{hint}
          </span>
        )}
      </div>
      {children}
    </div>
  );
}

function Spinner() {
  return (
    <svg className="animate-spin h-4 w-4 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
    </svg>
  );
}
