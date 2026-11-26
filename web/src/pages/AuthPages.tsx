import { Alert, Anchor, Button, PasswordInput, Stack, Text, TextInput } from '@mantine/core'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { IconCircleCheck } from '@tabler/icons-react'
import { useState, type ReactNode } from 'react'
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router'
import { api } from '../api'
import { MIN_PASSWORD, safeNext, useMe } from '../auth'
import { Loading } from '../components/common'
import { notifyOk } from '../lib'
import { Wordmark } from './LandingPage'

function AuthCard({ title, subtitle, children }: { title: string; subtitle?: string; children: ReactNode }) {
  return (
    <div className="auth-split">
      <aside className="auth-brand">
        <Link to="/" className="auth-home" aria-label="Rota-OC home"><Wordmark /></Link>
        <div className="auth-brand-copy">
          <h2><span>Fair rotas.</span> <span>Fewer arguments.</span> <span>Done in minutes.</span></h2>
          <p>Automatic on-call rotas for dental officers.</p>
        </div>
        <span className="auth-brand-credit">Developed by Beseri-Tech</span>
      </aside>
      <main className="auth-form-side">
        <Stack w="100%" maw={380} gap="xl">
          <div>
            <h1>{title}</h1>
            {subtitle && <Text c="dimmed" mt={10}>{subtitle}</Text>}
          </div>
          {children}
          <Text size="xs" c="dimmed" ta="center" className="auth-form-credit">Developed by Beseri-Tech</Text>
        </Stack>
      </main>
    </div>
  )
}

export function LoginPage() {
  const qc = useQueryClient()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const me = useMe()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const login = useMutation({
    mutationFn: () => api.auth.login(email, password),
    onSuccess: data => {
      qc.clear()
      qc.setQueryData(['me'], data)
      navigate(safeNext(params.get('next')), { replace: true })
    },
  })

  if (me.isLoading) return <Loading />
  if (me.data) return <Navigate to={safeNext(params.get('next'))} replace />

  return (
    <AuthCard title="Sign in" subtitle="Enter your leave, see the rota and swap shifts.">
      <form onSubmit={e => { e.preventDefault(); login.mutate() }}>
        <Stack>
          <TextInput size="md" label="Email" type="email" autoComplete="username" required value={email}
            onChange={e => setEmail(e.currentTarget.value)} autoFocus />
          <PasswordInput size="md" label="Password" autoComplete="current-password" required value={password}
            onChange={e => setPassword(e.currentTarget.value)} error={login.error?.message} />
          <Button type="submit" size="md" loading={login.isPending} disabled={!email || !password}>Sign in</Button>
          <Anchor component={Link} to="/forgot-password" size="sm" ta="center">Forgot password?</Anchor>
        </Stack>
      </form>
    </AuthCard>
  )
}

export function ForgotPasswordPage() {
  const me = useMe()
  const [email, setEmail] = useState('')
  const send = useMutation({ mutationFn: () => api.auth.forgot(email) })
  if (me.data) return <Navigate to="/home" replace />

  return (
    <AuthCard title="Forgot password" subtitle="We'll email you a link to choose a new password.">
      {send.isSuccess ? (
        <Alert color="green" icon={<IconCircleCheck />}>
          If {email} has an account, a reset link is on its way. It works for one hour.
        </Alert>
      ) : (
        <form onSubmit={e => { e.preventDefault(); send.mutate() }}>
          <Stack>
            <TextInput label="Email" type="email" required value={email} onChange={e => setEmail(e.currentTarget.value)}
              error={send.error?.message} autoFocus />
            <Button type="submit" loading={send.isPending} disabled={!email}>Send reset link</Button>
          </Stack>
        </form>
      )}
      <Anchor component={Link} to="/login" size="sm" ta="center">Back to sign in</Anchor>
    </AuthCard>
  )
}

export function ResetPasswordPage() {
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const token = params.get('token') ?? ''
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const reset = useMutation({
    mutationFn: () => api.auth.reset(token, password),
    onSuccess: () => { notifyOk('Password changed. Sign in with your new password.'); navigate('/login', { replace: true }) },
  })

  return (
    <AuthCard title="Choose a new password">
      <form onSubmit={e => { e.preventDefault(); reset.mutate() }}>
        <NewPasswordFields password={password} confirm={confirm} onPassword={setPassword} onConfirm={setConfirm}
          error={reset.error?.message} submit="Set password" loading={reset.isPending} />
      </form>
      <Anchor component={Link} to="/forgot-password" size="sm" ta="center">Need a new link?</Anchor>
    </AuthCard>
  )
}

/** Forced after an invite or admin reset; also reachable from the user menu. */
export function ChangePasswordPage({ forced }: { forced?: boolean }) {
  const qc = useQueryClient()
  const navigate = useNavigate()
  const [current, setCurrent] = useState('')
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const change = useMutation({
    mutationFn: () => api.auth.changePassword(current, password),
    onSuccess: data => { qc.setQueryData(['me'], data); notifyOk('Password changed.'); navigate('/home', { replace: true }) },
  })
  const logout = useMutation({ mutationFn: api.auth.logout, onSuccess: () => { qc.clear(); navigate('/login') } })

  return (
    <AuthCard title={forced ? 'Welcome – choose your password' : 'Change password'}
      subtitle={forced ? 'You signed in with a temporary password. Pick your own to continue.' : undefined}>
      <form onSubmit={e => { e.preventDefault(); change.mutate() }}>
        <Stack>
          <PasswordInput label={forced ? 'Temporary password' : 'Current password'} autoComplete="current-password" required
            value={current} onChange={e => setCurrent(e.currentTarget.value)} autoFocus />
          <NewPasswordFields password={password} confirm={confirm} onPassword={setPassword} onConfirm={setConfirm}
            error={change.error?.message} submit="Save password" loading={change.isPending} disabled={!current} />
        </Stack>
      </form>
      {forced
        ? <Anchor component="button" size="sm" ta="center" onClick={() => logout.mutate()}>Sign out</Anchor>
        : <Anchor component={Link} to="/home" size="sm" ta="center">Cancel</Anchor>}
    </AuthCard>
  )
}

function NewPasswordFields({ password, confirm, onPassword, onConfirm, error, submit, loading, disabled }: {
  password: string; confirm: string; onPassword: (v: string) => void; onConfirm: (v: string) => void
  error?: string; submit: string; loading: boolean; disabled?: boolean
}) {
  const tooShort = password.length > 0 && password.length < MIN_PASSWORD
  const mismatch = confirm.length > 0 && confirm !== password
  return (
    <Stack>
      <PasswordInput label="New password" autoComplete="new-password" required value={password}
        onChange={e => onPassword(e.currentTarget.value)}
        description={`At least ${MIN_PASSWORD} characters.`} error={tooShort ? 'Too short' : undefined} />
      <PasswordInput label="Repeat new password" autoComplete="new-password" required value={confirm}
        onChange={e => onConfirm(e.currentTarget.value)} error={mismatch ? "Passwords don't match" : error} />
      <Button type="submit" loading={loading}
        disabled={disabled || password.length < MIN_PASSWORD || password !== confirm}>{submit}</Button>
    </Stack>
  )
}
