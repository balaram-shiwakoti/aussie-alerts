import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../api/client';
import { listingSchema, propertyTypes, type ListingValues } from '../forms';
import { useSession } from '../session';
import { ErrorMessage, Pagination, money } from '../components';

function AddListing() {
  const cache = useQueryClient();
  const form = useForm<ListingValues>({ resolver: zodResolver(listingSchema), defaultValues: {
    title: 'Demo Darwin unit — fictional', suburb: 'darwin', postcode: '0800', price: 500000, bedrooms: 2, propertyType: 'unit',
  } });
  const mutation = useMutation({ mutationFn: api.addListing,
    onSuccess: () => cache.invalidateQueries({queryKey: ['listings']}) });
  return <details className="panel"><summary>Admin: add a fake listing</summary>
    <form onSubmit={form.handleSubmit(v => mutation.mutate(v))} noValidate><div className="grid">
      <label>Title<input {...form.register('title')}/></label><label>Suburb<input {...form.register('suburb')}/></label>
      <label>Postcode<input {...form.register('postcode')}/></label><label>Price (AUD)<input type="number" {...form.register('price', {valueAsNumber:true})}/></label>
      <label>Bedrooms<input type="number" {...form.register('bedrooms', {valueAsNumber:true})}/></label>
      <label>Type<select {...form.register('propertyType')}>{propertyTypes.map(t => <option key={t}>{t}</option>)}</select></label>
    </div>{Object.entries(form.formState.errors).map(([key, e]) => <p role="alert" key={key}>{e.message}</p>)}
      <ErrorMessage error={mutation.error}/>{mutation.isSuccess && <p role="status">Fake listing added. Matching runs every 15 seconds.</p>}
      <button disabled={mutation.isPending}>Add fake listing</button>
    </form></details>;
}
export function ListingsPage() {
  const [postcode, setPostcode] = useState(''); const [draft, setDraft] = useState(''); const [page, setPage] = useState(1);
  const session = useSession();
  const query = useQuery({queryKey:['listings', postcode, page], queryFn: () => api.listings(postcode, page)});
  return <><h1>Browse fake listings</h1><p>Every listing is fictional. Prices are in Australian dollars.</p>
    {session?.role === 'Admin' && <AddListing/>}
    <form className="search" onSubmit={e => {e.preventDefault(); setPostcode(draft); setPage(1);}}><label>Filter by postcode<input value={draft} onChange={e=>setDraft(e.target.value)} pattern="[0-9]{4}" placeholder="0800"/></label><button>Search</button></form>
    {query.isPending && <p>Loading listings…</p>}<ErrorMessage error={query.error}/>
    {query.data?.items.length === 0 && <p className="panel">No listings found. Ask the demo admin to add a fake listing.</p>}
    <div className="grid">{query.data?.items.map(l => <article className="panel" key={l.id}><span className="badge">Fictional listing</span><h2>{l.title}</h2>
      <p className="price">{money(l.price)}</p><p>{l.suburb} · {l.postcode}</p><p>{l.bedrooms} bedrooms · {l.propertyType}</p></article>)}</div>
    {query.data && <Pagination page={page} total={query.data.total} size={12} change={setPage}/>}</>;
}
