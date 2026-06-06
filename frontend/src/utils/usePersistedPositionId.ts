import { useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';

// URL ?positionId= is the source of truth (shareable links, browser back/refresh).
// localStorage stores the last-used positionId only as an initial value when the
// page loads without the query param. Setting a position writes both.
const STORAGE_KEY = 'recruiter-ai:lastPositionId';

export function usePersistedPositionId(): [string, (id: string) => void] {
  const [params, setParams] = useSearchParams();
  const positionId = params.get('positionId') ?? '';

  // On mount: if URL has no positionId but localStorage does, hydrate the URL
  // (replace: true → no extra history entry).
  useEffect(() => {
    if (positionId) return;
    const stored = typeof window !== 'undefined'
      ? window.localStorage.getItem(STORAGE_KEY)
      : null;
    if (!stored) return;
    const next = new URLSearchParams(params);
    next.set('positionId', stored);
    setParams(next, { replace: true });
    // Only on mount — subsequent changes are driven by setPositionId / user nav.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Keep localStorage in sync whenever the URL value changes.
  useEffect(() => {
    if (typeof window === 'undefined') return;
    if (positionId) window.localStorage.setItem(STORAGE_KEY, positionId);
  }, [positionId]);

  const setPositionId = (id: string) => {
    const next = new URLSearchParams(params);
    if (id) next.set('positionId', id);
    else next.delete('positionId');
    setParams(next, { replace: true });
    if (typeof window !== 'undefined') {
      if (id) window.localStorage.setItem(STORAGE_KEY, id);
      else window.localStorage.removeItem(STORAGE_KEY);
    }
  };

  return [positionId, setPositionId];
}
