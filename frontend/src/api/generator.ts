import { apiClient } from './client';
import type { GeneratedCandidate } from '../types';

export const generateCandidates = (positionId: string, count: number) =>
  apiClient
    .post<GeneratedCandidate[]>(`/positions/${positionId}/generate`, { count })
    .then(r => r.data);
