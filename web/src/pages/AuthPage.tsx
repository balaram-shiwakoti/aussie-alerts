import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import { authSchema } from '../forms';
import { setSession } from '../session';
import { ErrorMessage } from '../components';
export function AuthPage({ register = false }: { register?: boolean }) {
  const navigate = useNavigate(); const cache = useQueryClient();
  const form = useForm<{email: string; password: string}>({ resolver: zodResolver(authSchema(register)) });
  const mutation = useMutation({ mutationFn: (v: {email: string; password: string}) =>
    register ? api.register(v.email, v.password) : api.login(v.email, v.password),
    onSuccess: data => { cache.clear(); setSession(data); navigate('/alerts'); },
  });
  return <section className="panel narrow"><h1>{register ? 'Create your account' : 'Welcome back'}</h1>
    <p>Use a fictional email for this learning project.</p>
    <form onSubmit={form.handleSubmit(v => mutation.mutate(v))} noValidate>
      <label>Email<input type="email" autoComplete="username" {...form.register('email')} /></label>
      <small role="status">{form.formState.errors.email?.message}</small>
      <label>Password<input type="password" autoComplete={register ? 'new-password' : 'current-password'} {...form.register('password')} /></label>
      {register && <small>12+ characters with uppercase, lowercase and a number.</small>}
      <small role="status">{form.formState.errors.password?.message}</small>
      <ErrorMessage error={mutation.error}/><button disabled={mutation.isPending}>{mutation.isPending ? 'Please wait…' : register ? 'Register' : 'Log in'}</button>
    </form><p><Link to={register ? '/login' : '/register'}>{register ? 'Already registered? Log in' : 'Create an account'}</Link></p>
  </section>;
}
