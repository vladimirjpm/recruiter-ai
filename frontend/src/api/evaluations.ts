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
