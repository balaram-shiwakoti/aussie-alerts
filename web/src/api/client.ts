import createClient from 'openapi-fetch';
import type { paths, components } from './schema';
import { getSession, setSession } from '../session';
export type Alert = components['schemas']['AlertDto'];
export type AlertInput = components['schemas']['AlertRequest'];
export type ListingInput = components['schemas']['ListingRequest'];
const client = createClient<paths>({ baseUrl: import.meta.env.VITE_API_URL ?? '' });
client.use({
  onRequest({ request }) {
    const token = getSession()?.accessToken;
    if (token) request.headers.set('Authorization', `Bearer ${token}`);
    return request;
  },
  onResponse({ response }) {
    if (response.status === 401 && getSession()) setSession(null);
    return response;
  },
});
function unwrap<T>(result: { data?: T; error?: unknown; response: Response }): T {
  if (!result.response.ok) {
    const error = result.error as { title?: string; errors?: Record<string, string[]> } | undefined;
    const messages = error?.errors ? Object.values(error.errors).flat().join(' ') : '';
    throw new Error(messages || error?.title || `Request failed (${result.response.status}).`);
  }
  return result.data as T;
}
export const api = {
  login: async (email: string, password: string) => unwrap(await client.POST('/api/auth/login', { body: { email, password } })),
  register: async (email: string, password: string) => unwrap(await client.POST('/api/auth/register', { body: { email, password } })),
  alerts: async () => unwrap(await client.GET('/api/alerts')),
  createAlert: async (body: AlertInput) => unwrap(await client.POST('/api/alerts', { body })),
  updateAlert: async (id: string, body: AlertInput) => unwrap(await client.PUT('/api/alerts/{id}', { params: { path: { id } }, body })),
  listings: async (postcode: string, page: number) => unwrap(await client.GET('/api/listings', {
    params: { query: { Postcode: postcode || undefined, Page: page, PageSize: 12 } },
  })),
  addListing: async (body: ListingInput) => unwrap(await client.POST('/api/admin/listings', { body })),
  notifications: async (page: number) => unwrap(await client.GET('/api/notifications', { params: { query: { Page: page, PageSize: 20 } } })),
  markRead: async (id: string) => unwrap(await client.PUT('/api/notifications/{id}/read', { params: { path: { id } } })),
};
