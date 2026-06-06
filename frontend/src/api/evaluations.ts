import { apiClient } from './client';
import type { Evaluation } from '../types';

export const getEvaluations = (positionId: string) =>
  apiClient.get<Evaluation[]>(`/positions/${positionId}/evaluations`).then(r => r.data);

export const screenCandidates = (positionId: string, candidateIds: string[]) =>
  apiClient
    .post<Evaluation[]>(`/positions/${positionId}/screen`, { candidateIds })
    .then(r => r.data);

export const getEvaluationsCsvUrl = (positionId: string) =>
  `${apiClient.defaults.baseURL}/positions/${positionId}/evaluations/export`;

export interface AdjustmentPayload {
  recruiterAdjustment: number;
  recruiterComment: string | null;
  adjustedBy: string | null;
}

// Recruiter override on top of AI score. Backend enforces:
//   - adjustment in [-30, +30]
//   - non-zero adjustment requires comment ≥ 10 chars
//   - adjustment=0 resets comment / adjustedBy / adjustedAt
export const updateAdjustment = (
  positionId: string,
  candidateId: string,
  payload: AdjustmentPayload
) =>
  apiClient
    .patch<Evaluation>(
      `/positions/${positionId}/candidates/${candidateId}/adjustment`,
      payload
    )
    .then(r => r.data);
