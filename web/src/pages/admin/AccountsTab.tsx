import {
  ActionIcon, Alert, Badge, Button, Code, CopyButton, Group, Modal, Paper, SegmentedControl, Select, Stack, Switch, Table, Text,
  TextInput, Tooltip,
} from '@mantine/core'
import { modals } from '@mantine/modals'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconCheck, IconCopy, IconEdit, IconMailForward, IconSearch, IconUserPlus } from '@tabler/icons-react'
import { useMemo, useState } from 'react'
import { api, type Account, type AccountRole, type AdminPerson, type InviteResult } from '../../api'
import { useMe } from '../../auth'
import { ErrorBox, Loading } from '../../components/common'
import { notifyError, notifyOk } from '../../lib'

const ROLE_LABEL: Record<AccountRole, string> = { Officer: 'Officer', Admin: 'Admin (on call)', Supervisor: 'Supervisor' }
const ROLE_HELP: Record<AccountRole, string> = {
  Officer: 'Enters their own leave, sees the overview and rota.',
  Admin: 'An on-call officer who also runs the admin pages.',
  Supervisor: 'Runs the admin pages; not on the rota.',
}

const status = (a: Account) =>
  !a.enabled ? { label: 'Disabled', color: 'gray' }
  : a.mustChangePassword ? { label: 'Invited', color: 'orange' }
  : { label: 'Active', color: 'green' }

const when = (iso: string | null) => (iso ? new Date(iso).toLocaleString('en-GB', { dateStyle: 'medium', timeStyle: 'short' }) : '–')

export function AccountsTab() {
  const qc = useQueryClient()
  const me = useMe().data
  const accounts = useQuery({ queryKey: ['admin', 'accounts'], queryFn: api.admin.accounts })
  const people = useQuery({ queryKey: ['admin', 'people'], queryFn: api.admin.people })
  const [search, setSearch] = useState('')
  const [editing, setEditing] = useState<Account | 'new' | null>(null)
  const [issued, setIssued] = useState<{ email: string; result: InviteResult } | null>(null)
  const [now] = useState(() => new Date().toISOString())

  const refresh = () => qc.invalidateQueries({ queryKey: ['admin', 'accounts'] })
  const onIssued = (email: string, result: InviteResult) => {
    refresh()
    if (result.emailSent) notifyOk(`Email sent to ${email}.`)
    else setIssued({ email, result })
  }

  const resend = useMutation({
    mutationFn: (a: Account) => api.admin.resendInvite(a.id),
    onSuccess: (r, a) => onIssued(a.email, r),
    onError: e => notifyError(e),
  })

  const rows = useMemo(() => {
    const s = search.trim().toLowerCase()
    return (accounts.data ?? []).filter(a => !s || a.email.includes(s) || a.personName?.toLowerCase().includes(s))
  }, [accounts.data, search])

  if (accounts.isLoading || people.isLoading) return <Loading />
  if (accounts.error || people.error) return <ErrorBox error={accounts.error ?? people.error} />

  const withoutAccount = people.data!.filter(p => p.status !== 'Left' && !accounts.data!.some(a => a.personId === p.id)).length

  return (
    <Stack gap="md">
      <Group justify="space-between" wrap="wrap">
        <Group>
          <TextInput placeholder="Find email or name" leftSection={<IconSearch size={16} />} value={search}
            onChange={e => setSearch(e.currentTarget.value)} w={240} />
          {withoutAccount > 0 && <Text size="sm" c="dimmed">{withoutAccount} officer(s) have no account yet</Text>}
        </Group>
        <Button leftSection={<IconUserPlus size={16} />} onClick={() => setEditing('new')}>Invite</Button>
      </Group>

      <Paper withBorder>
        <Table.ScrollContainer minWidth={820}>
          <Table striped highlightOnHover verticalSpacing={6}>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Email</Table.Th>
                <Table.Th>Officer</Table.Th>
                <Table.Th>Role</Table.Th>
                <Table.Th>Status</Table.Th>
                <Table.Th>Last sign-in</Table.Th>
                <Table.Th />
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {rows.map(a => {
                const st = status(a)
                return (
                  <Table.Tr key={a.id} opacity={a.enabled ? 1 : 0.55}>
                    <Table.Td fw={600}>{a.email}{a.id === me?.accountId && <Text span size="xs" c="dimmed"> (you)</Text>}</Table.Td>
                    <Table.Td>{a.personName ?? <Text span c="dimmed">–</Text>}</Table.Td>
                    <Table.Td><Badge variant="light" color={a.role === 'Officer' ? 'blue' : 'violet'}>{ROLE_LABEL[a.role]}</Badge></Table.Td>
                    <Table.Td>
                      <Badge color={st.color} variant="light">{st.label}</Badge>
                      {a.enabled && a.mustChangePassword && a.tempPasswordExpiresAt && (
                        <Text size="xs" c={new Date(a.tempPasswordExpiresAt).toISOString() < now ? 'red' : 'dimmed'}>
                          temporary password until {when(a.tempPasswordExpiresAt)}
                        </Text>
                      )}
                    </Table.Td>
                    <Table.Td><Text size="sm">{when(a.lastLoginAt)}</Text></Table.Td>
                    <Table.Td>
                      <Group gap={4} justify="flex-end" wrap="nowrap">
                        <Tooltip label="Send a new temporary password (re-invite or password reset)">
                          <ActionIcon variant="subtle" aria-label="Send new temporary password" disabled={!a.enabled}
                            loading={resend.isPending && resend.variables?.id === a.id}
                            onClick={() => modals.openConfirmModal({
                              title: `New temporary password for ${a.email}?`,
                              children: <Text size="sm">Their current password stops working. They'll be asked to choose a new one when they sign in.</Text>,
                              labels: { confirm: 'Send', cancel: 'Cancel' },
                              onConfirm: () => resend.mutate(a),
                            })}>
                            <IconMailForward size={18} />
                          </ActionIcon>
                        </Tooltip>
                        <ActionIcon variant="subtle" aria-label="Edit" onClick={() => setEditing(a)}><IconEdit size={18} /></ActionIcon>
                      </Group>
                    </Table.Td>
                  </Table.Tr>
                )
              })}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      </Paper>

      {editing && (
        <AccountModal account={editing === 'new' ? null : editing} people={people.data!} accounts={accounts.data!}
          isSelf={editing !== 'new' && editing.id === me?.accountId}
          onClose={() => setEditing(null)} onSaved={refresh} onIssued={onIssued} />
      )}

      <Modal opened={!!issued} onClose={() => setIssued(null)} title="Email not sent – share this password">
        {issued && (
          <Stack>
            <Alert color="orange">
              Email isn't set up on the server (or sending failed). Send these sign-in details to {issued.email} yourself, e.g. by
              WhatsApp. The password is shown only once and must be changed at first sign-in.
            </Alert>
            <Group>
              <Code fz="lg" p="sm">{issued.result.tempPassword}</Code>
              <CopyButton value={issued.result.tempPassword ?? ''}>
                {({ copied, copy }) => (
                  <Button variant="default" leftSection={copied ? <IconCheck size={16} /> : <IconCopy size={16} />} onClick={copy}>
                    {copied ? 'Copied' : 'Copy'}
                  </Button>
                )}
              </CopyButton>
            </Group>
            <Group justify="flex-end"><Button onClick={() => setIssued(null)}>Done</Button></Group>
          </Stack>
        )}
      </Modal>
    </Stack>
  )
}

function AccountModal({ account, people, accounts, isSelf, onClose, onSaved, onIssued }: {
  account: Account | null; people: AdminPerson[]; accounts: Account[]; isSelf: boolean
  onClose: () => void; onSaved: () => void; onIssued: (email: string, r: InviteResult) => void
}) {
  const [email, setEmail] = useState(account?.email ?? '')
  const [role, setRole] = useState<AccountRole>(account?.role ?? 'Officer')
  const [personId, setPersonId] = useState<string | null>(account?.personId ?? null)
  const [enabled, setEnabled] = useState(account?.enabled ?? true)

  // Officers who are still around and don't have (another) account.
  const options = people
    .filter(p => p.status !== 'Left' && !accounts.some(a => a.personId === p.id && a.id !== account?.id))
    .map(p => ({ value: p.id, label: `${p.name} (${p.code})` }))

  const save = useMutation({
    mutationFn: async () => {
      const pid = role === 'Supervisor' ? null : personId
      if (account) await api.admin.updateAccount(account.id, { email, role, personId: pid, enabled })
      else return api.admin.invite(email, role, pid)
    },
    onSuccess: r => {
      onClose()
      if (r) onIssued(email.trim().toLowerCase(), r)
      else { onSaved(); notifyOk('Account saved.') }
    },
  })

  return (
    <Modal opened onClose={onClose} title={account ? 'Edit account' : 'Invite someone'} size="lg">
      <form onSubmit={e => { e.preventDefault(); save.mutate() }}>
        <Stack>
          <TextInput label="Email" type="email" required value={email} onChange={e => setEmail(e.currentTarget.value)} data-autofocus
            description={account ? undefined : 'They get an email with a temporary password (valid 7 days).'} />
          <Stack gap={4}>
            <Text fw={500} size="sm">Role</Text>
            <SegmentedControl value={role} onChange={v => setRole(v as AccountRole)} disabled={isSelf}
              data={(['Officer', 'Admin', 'Supervisor'] as const).map(r => ({ value: r, label: ROLE_LABEL[r] }))} />
            <Text size="xs" c="dimmed">{ROLE_HELP[role]}</Text>
          </Stack>
          {role !== 'Supervisor' && (
            <Select label="Officer" required searchable placeholder="Pick the officer" data={options} value={personId}
              onChange={setPersonId} nothingFoundMessage="No officer without an account – add them on the Officers tab first" />
          )}
          {account && (
            <Switch label="Can sign in" checked={enabled} disabled={isSelf} onChange={e => setEnabled(e.currentTarget.checked)}
              description={isSelf ? "You can't disable yourself or remove your own admin rights." : 'Disabling signs them out immediately.'} />
          )}
          {save.error && <Text c="red" size="sm">{save.error.message}</Text>}
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>Cancel</Button>
            <Button type="submit" loading={save.isPending} disabled={!email.trim() || (role !== 'Supervisor' && !personId)}>
              {account ? 'Save' : 'Send invite'}
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  )
}
