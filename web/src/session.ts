import { useSyncExternalStore } from 'react';
import type { components } from './api/schema';
type Session = components['schemas']['AuthResponse'];
let session: Session | null = null;
const listeners = new Set<() => void>();
export function setSession(value: Session | null) { session = value; listeners.forEach(fn => fn()); }
export function getSession() { return session; }
export function useSession() {
  return useSyncExternalStore(fn => { listeners.add(fn); return () => { listeners.delete(fn); }; }, getSession);
}
// Intentionally in memory: refresh requires login; no token is persisted in localStorage.
