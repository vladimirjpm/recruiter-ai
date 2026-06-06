export interface PositionSummary {
  id: string;
  title: string;
  country: string | null;
  seniorityLevel: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface Position {
  id: string;
  title: string;
  description: string;
  country: string | null;
  seniorityLevel: string | null;
  requiredSkills: string[];
  niceToHaveSkills: string[];
  createdAt: string;
  updatedAt: string | null;
}

export interface UpsertPositionPayload {
  title: string;
  description: string;
  country: string | null;
  seniorityLevel: string | null;
  requiredSkills: string[];
  niceToHaveSkills: string[];
}

export interface Candidate {
  id: string;
  name: string;
  email: string | null;
  fileName: string;
  storagePath: string | null;
  language: string;
  source: string;
  uploadedAt: string;
}

export interface CandidateUploadResult {
  id: string;
  fileName: string;
}

export interface Evaluation {
  id: string;
  candidateId: string;
  candidateName: string;
  candidateFileName: string;
  score: number;
  matchLevel: string;
  reasoning: string;
  strengths: string[];
  weaknesses: string[];
  matchedSkills: string[];
  missingSkills: string[];
  redFlags: string[];
  interviewQuestions: string[];
  aiModel: string;
  inputTokens: number | null;
  outputTokens: number | null;
  estimatedCost: number | null;
  createdAt: string;
  isStale: boolean;
}

export type ConfidenceLevel = 'High' | 'Low' | 'NotDetected';

export type MissingInfoField =
  | 'Country'
  | 'Seniority'
  | 'Salary'
  | 'WorkingArrangement'
  | 'ContractType'
  | 'TeamSize';

export interface ExtractedSkill {
  name: string;
  evidence: string;
}

export interface ExtractionResult {
  title: string;
  description: string;
  country: string | null;
  seniorityLevel: string | null;
  requiredSkills: ExtractedSkill[];
  niceToHaveSkills: ExtractedSkill[];
  confidence: {
    country: ConfidenceLevel;
    seniority: ConfidenceLevel;
    skills: ConfidenceLevel;
  };
  missingInformation: MissingInfoField[];
  metadata: {
    model: string;
    promptVersion: string;
    extractedAt: string;
    inputCharCount: number;
  };
}

export interface GeneratedCandidate {
  id: string;
  name: string;
  email: string | null;
  fileName: string;
  language: string;
  expectedFitLevel: string;
  expectedScoreMin: number;
  expectedScoreMax: number;
  batchId: string;
  generatedAt: string;
}
