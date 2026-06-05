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
