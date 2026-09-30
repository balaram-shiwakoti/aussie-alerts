import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../api/client';
import { ErrorMessage, Pagination } from '../components';
export function InboxPage() {
  const [page, setPage] = useState(1); const cache = useQueryClient();
  const query = useQuery({queryKey:['notifications', page], queryFn: () => api.notifications(page), refetchInterval: 5000});
  const mark = useMutation({mutationFn:api.markRead, onSuccess: () => cache.invalidateQueries({queryKey:['notifications']})});
  return <><h1>Your notifications</h1><p>Simulated emails stored in PostgreSQL. No email is sent. Refreshes every 5 seconds.</p>
    {query.isPending && <p>Loading inbox…</p>}<ErrorMessage error={query.error || mark.error}/>
    {query.data?.items.length === 0 && <p className="panel">Your inbox is empty. Create an alert, then add a matching fake listing.</p>}
    {query.data?.items.map(n => <article className="panel" key={n.id}><span className="badge">{n.readAt ? 'Read' : 'Unread'}</span>
      <h2>{n.subject}</h2><p>{n.body}</p><small>{new Date(n.createdAt).toLocaleString()}</small>
      {!n.readAt && <p><button disabled={mark.isPending} onClick={() => mark.mutate(n.id)}>Mark as read</button></p>}</article>)}
    {query.data && <Pagination page={page} total={query.data.total} size={20} change={setPage}/>}</>;
}
