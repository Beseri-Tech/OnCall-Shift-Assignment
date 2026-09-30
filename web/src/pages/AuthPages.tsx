import { Alert, Anchor, Box, Button, Center, Group, Paper, PasswordInput, Stack, Text, TextInput, ThemeIcon, Title } from '@mantine/core'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { IconCircleCheck, IconLock } from '@tabler/icons-react'
import { useState, type ReactNode } from 'react'
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router'
import { api } from '../api'
import { MIN_PASSWORD, useMe } from '../auth'
import { Loading } from '../components/common'
import { notifyOk } from '../lib'

function AuthCard({ title, subtitle, children }: { title: string; subtitle?: string; children: ReactNode }) {
  return (
    <Box mih="100vh" className="auth-bg">
      <Center py={60} px="md">
        <Stack w="100%" maw={420} gap="lg">
          <Group gap="sm" justify="center" wrap="nowrap">
            <div className="brand-mark"><img src="/favicon.svg" width={26} height={26} alt="" /></div>
            <div>
              <Text fw={800} size="xl" c="white" lh={1.1}>On-Call Rota</Text>
              <Text size="sm" c="brand.1" lh={1.2}>Dental Officers · Perlis</Text>
            </div>
          </Group>
        <Paper shadow="xl" p="xl" radius="lg">
          <Stack>
            <div>
              <Group gap="sm" wrap="nowrap" align="center">
                <ThemeIcon size={36} radius="md" variant="light"><IconLock size={20} /></ThemeIcon>
                <Title order={3} c="brand.9">{title}</Title>
              </Group>
              {subtitle && <Text size="sm" c="dimmed" mt={8}>{subtitle}</Text>}
            </div>
            {children}
          </Stack>
        </Paper>
        </Stack>
      </Center>
    </Box>
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
      navigate(params.get('next') ?? '/', { replace: true })
    },
  })

  if (me.isLoading) return <Loading />
  if (me.data) return <Navigate to="/" replace />

  return (
    <AuthCard title="Sign in" subtitle="On-Call Rota – enter your leave and see the rota.">
      <form onSubmit={e => { e.preventDefault(); login.mutate() }}>
        <Stack>
          <TextInput label="Email" type="email" autoComplete="username" required value={email}
            onChange={e => setEmail(e.currentTarget.value)} autoFocus />
          <PasswordInput label="Password" autoComplete="current-password" required value={password}
            onChange={e => setPassword(e.currentTarget.value)} error={login.error?.message} />
          <Button type="submit" loading={login.isPending} disabled={!email || !password}>Sign in</Button>
          <Anchor component={Link} to="/forgot-password" size="sm" ta="center">Forgot password?</Anchor>
        </Stack>
      </form>
    </AuthCard>
  )
}

export function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const send = useMutation({ mutationFn: () => api.auth.forgot(email) })

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
    onSuccess: data => { qc.setQueryData(['me'], data); notifyOk('Password changed.'); navigate('/', { replace: true }) },
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
        : <Anchor component={Link} to="/" size="sm" ta="center">Cancel</Anchor>}
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
