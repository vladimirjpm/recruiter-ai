import { apiClient } from './client';
import type { Position, PositionSummary, UpsertPositionPayload, ExtractionResult } from '../types';

export const getPositions = () =>
  apiClient.get<PositionSummary[]>('/positions').then(r => r.data);

export const getPosition = (id: string) =>
  apiClient.get<Position>(`/positions/${id}`).then(r => r.data);

export const createPosition = (payload: UpsertPositionPayload) =>
  apiClient.post<Position>('/positions', payload).then(r => r.data);

export const updatePosition = (id: string, payload: UpsertPositionPayload) =>
  apiClient.put<Position>(`/positions/${id}`, payload).then(r => r.data);

export const deletePosition = (id: string) =>
  apiClient.delete(`/positions/${id}`).then(r => r.data);

export const extractPosition = (jobDescriptionText: string) =>
  apiClient.post<ExtractionResult>('/positions/extract', { jobDescriptionText }).then(r => r.data);

export const findMatchingCandidates = (positionId: string) =>
  apiClient.post<import('../types').CandidateMatch[]>(`/positions/${positionId}/find-matches`).then(r => r.data);
