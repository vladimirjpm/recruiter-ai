import { apiClient } from './client';
import type { Candidate, CandidateUploadResult } from '../types';

export const getCandidates = () =>
  apiClient.get<Candidate[]>('/candidates').then(r => r.data);

export const getCandidateFileUrl = (id: string) =>
  `${apiClient.defaults.baseURL}/candidates/${id}/file`;

// Optional positionId attaches uploaded candidates to that position via the
// PositionCandidate junction. When omitted the server uploads them globally.
export const uploadCandidates = (files: File[], positionId?: string) => {
  const fd = new FormData();
  files.forEach(f => fd.append('files', f));
  const url = positionId
    ? `/candidates/upload?positionId=${encodeURIComponent(positionId)}`
    : '/candidates/upload';
  return apiClient
    .post<CandidateUploadResult[]>(url, fd, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    .then(r => r.data);
};

export interface AttachedCandidate extends Candidate {
  attachSourceContext: 'Uploaded' | 'Generated' | 'ManuallyAttached';
  attachedAt: string;
}

export interface PositionCandidatesPage {
  items: AttachedCandidate[];
  total: number;
  offset: number;
  limit: number;
}

export const getCandidatesForPosition = (positionId: string) =>
  apiClient
    .get<PositionCandidatesPage>(`/positions/${positionId}/candidates?limit=200`)
    .then(r => r.data);

export const attachCandidate = (positionId: string, candidateId: string) =>
  apiClient.post(`/positions/${positionId}/candidates/${candidateId}`).then(r => r.data);

// Removes the junction row only; candidate stays in the global pool.
// Different from deleteCandidate which removes the candidate entirely.
export const detachCandidate = (positionId: string, candidateId: string) =>
  apiClient.delete(`/positions/${positionId}/candidates/${candidateId}`);

export const deleteCandidate = (id: string) =>
  apiClient.delete(`/candidates/${id}`);

export const getCandidateResumeText = (id: string) =>
  apiClient.get<{ text: string }>(`/candidates/${id}/resume-text`).then(r => r.data.text);
