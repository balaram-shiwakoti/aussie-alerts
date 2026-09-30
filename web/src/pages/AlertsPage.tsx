import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type Alert, type AlertInput } from '../api/client';
import { alertSchema, propertyTypes, type AlertValues } from '../forms';
import { ErrorMessage, money } from '../components';

export function AlertForm({ alert, done }: { alert?: Alert; done: () => void }) {
  const cache = useQueryClient();
  const form = useForm<AlertValues>({ resolver: zodResolver(alertSchema), defaultValues: {
    suburb: alert?.suburb ?? '', postcode: alert?.postcode ?? '', minPrice: alert?.minPrice ?? 300000,
    maxPrice: alert?.maxPrice ?? 800000, minBedrooms: alert?.minBedrooms ?? 2,
    propertyType: (alert?.propertyType ?? '') as AlertValues['propertyType'], active: alert?.active ?? true,
  } });
  const mutation = useMutation({ mutationFn: (v: AlertValues) => {
    const body: AlertInput = { ...v, suburb: v.suburb || null, postcode: v.postcode || null, propertyType: v.propertyType || null };
    return alert ? api.updateAlert(alert.id, body) : api.createAlert(body);
  }, onSuccess: async () => { await cache.invalidateQueries({queryKey: ['alerts']}); done(); } });
  return <form className="panel" onSubmit={form.handleSubmit(v => mutation.mutate(v))} noValidate>
    <h2>{alert ? 'Edit alert' : 'Create alert'}</h2><p>Enter a suburb, a postcode, or both. New and edited alerts match future listings.</p>
    <div className="grid">
      <label>Suburb<input {...form.register('suburb')} placeholder="darwin"/></label>
      <label>Postcode<input {...form.register('postcode')} inputMode="numeric" placeholder="0800"/></label>
      <label>Minimum price (AUD)<input type="number" {...form.register('minPrice', { valueAsNumber: true })}/></label>
      <label>Maximum price (AUD)<input type="number" {...form.register('maxPrice', { valueAsNumber: true })}/></label>
      <label>Minimum bedrooms<input type="number" {...form.register('minBedrooms', { valueAsNumber: true })}/></label>
      <label>Property type<select {...form.register('propertyType')}><option value="">Any type</option>{propertyTypes.map(t => <option key={t}>{t}</option>)}</select></label>
    </div><label className="check"><input type="checkbox" {...form.register('active')}/>Active</label>
    {Object.entries(form.formState.errors).map(([key, e]) => <p role="alert" className="error" key={key}>{e.message}</p>)}
    <ErrorMessage error={mutation.error}/><div className="actions"><button disabled={mutation.isPending}>Save alert</button><button type="button" className="secondary" onClick={done}>Cancel</button></div>
  </form>;
}
export function AlertsPage() {
  const query = useQuery({ queryKey: ['alerts'], queryFn: api.alerts });
  const [editing, setEditing] = useState<Alert | 'new' | null>(null);
  return <><div className="heading"><div><h1>Your property alerts</h1><p>Choose what you want. Check the inbox for simulated emails.</p></div><button onClick={() => setEditing('new')}>New alert</button></div>
    {editing && <AlertForm key={editing === 'new' ? 'new' : editing.id} alert={editing === 'new' ? undefined : editing} done={() => setEditing(null)}/>}
    {query.isPending && <p>Loading alerts…</p>}<ErrorMessage error={query.error}/>
    {query.data?.length === 0 && <p className="panel">No alerts yet. Create your first alert.</p>}
    <div className="grid">{query.data?.map(a => <article className="panel" key={a.id}><span className="badge">{a.active ? 'Active' : 'Paused'}</span>
      <h2>{a.suburb || 'Any suburb'} {a.postcode}</h2><p>{money(a.minPrice)} – {money(a.maxPrice)}</p>
      <p>{a.minBedrooms}+ bedrooms · {a.propertyType || 'Any type'}</p><button className="secondary" onClick={() => setEditing(a)}>Edit</button></article>)}</div></>;
}
