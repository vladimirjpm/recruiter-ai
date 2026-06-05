import { apiClient } from './client';
import type { Candidate, CandidateUploadResult } from '../types';

export const getCandidates = () =>
  apiClient.get<Candidate[]>('/candidates').then(r => r.data);

export const getCandidateFileUrl = (id: string) =>
  `${apiClient.defaults.baseURL}/candidates/${id}/file`;

export const uploadCandidates = (files: File[]) => {
  const fd = new FormData();
  files.forEach(f => fd.append('files', f));
  return apiClient
    .post<CandidateUploadResult[]>('/candidates/upload', fd, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    .then(r => r.data);
};

export const deleteCandidate = (id: string) =>
  apiClient.delete(`/candidates/${id}`);
