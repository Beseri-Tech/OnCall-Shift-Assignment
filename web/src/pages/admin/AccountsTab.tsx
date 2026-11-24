import {
  ActionIcon, Badge, Button, Group, Modal, Paper, SegmentedControl, Select, Stack, Switch, Table, Text, TextInput, Tooltip,
} from '@mantine/core'
import { modals } from '@mantine/modals'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconEdit, IconMailForward, IconSearch, IconUserPlus } from '@tabler/icons-react'
import { Fragment, useMemo, useState } from 'react'
import { api, type Account, type AccountRole, type AdminPerson, type InviteResult } from '../../api'
import { isMaster, useMe } from '../../auth'
import { ErrorBox, Loading } from '../../components/common'
import { notifyError, notifyOk } from '../../lib'
import { type IssuedLogin, TempPasswordsModal } from './TempPasswords'

const ROLE_LABEL: Record<AccountRole, string> = { Officer: 'Officer', Admin: 'Admin (on call)', Supervisor: 'Supervisor', Master: 'Master' }
const ROLE_HELP: Record<AccountRole, string> = {
  Officer: 'Enters their own leave, sees the overview and rota.',
  Admin: 'An on-call officer who also runs the admin pages.',
  Supervisor: 'Runs the admin pages; not on the rota.',
  Master: 'Sees and runs every hospital; not on any rota.',
}
/** Officers and on-call admins are linked to their officer record. */
const hasOfficer = (r: AccountRole) => r === 'Officer' || r === 'Admin'

const status = (a: Account) =>
  !a.enabled ? { label: 'Disabled', color: 'gray' }
  : a.mustChangePassword ? { label: 'Invited', color: 'orange' }
  : { label: 'Active', color: 'green' }

const when = (iso: string | null) => (iso ? new Date(iso).toLocaleString('en-GB', { dateStyle: 'medium', timeStyle: 'short' }) : '–')

export function AccountsTab() {
  const qc = useQueryClient()
  const me = useMe().data
  const accounts = useQuery({ queryKey: ['admin', 'accounts'], queryFn: api.admin.accounts })
  // A master with no hospital picked manages masters only: there are no officers to link.
  const inHospital = !!me?.facility
  const people = useQuery({ queryKey: ['admin', 'people'], queryFn: api.admin.people, enabled: inHospital })
  const [search, setSearch] = useState('')
  const [roleFilter, setRoleFilter] = useState<string | null>(null)
  const [statusFilter, setStatusFilter] = useState<string | null>(null)
  // Outside a hospital the list holds every hospital's accounts: start on the masters, pick a hospital to see its own.
  const [hospital, setHospital] = useState<string | null>('masters')
  const facilities = useQuery({ queryKey: ['facilities'], queryFn: api.facilities, enabled: !inHospital })
  const [editing, setEditing] = useState<Account | 'new' | null>(null)
  const [issued, setIssued] = useState<IssuedLogin[]>([])
  const [now] = useState(() => new Date().toISOString())

  const refresh = () => qc.invalidateQueries({ queryKey: ['admin', 'accounts'] })
  const onIssued = (email: string, result: InviteResult) => {
    refresh()
    if (result.emailSent) notifyOk(`Email sent to ${email}.`)
    else if (result.tempPassword) setIssued([{ email, tempPassword: result.tempPassword }])
  }

  const resend = useMutation({
    mutationFn: (a: Account) => api.admin.resendInvite(a.id),
    onSuccess: (r, a) => onIssued(a.email, r),
    onError: e => notifyError(e),
  })

  const rows = useMemo(() => {
    const s = search.trim().toLowerCase()
    return (accounts.data ?? []).filter(a =>
      (!s || a.email.includes(s) || a.personName?.toLowerCase().includes(s))
      && (!roleFilter || a.role === roleFilter)
      && (!statusFilter || status(a).label === statusFilter)
      && (inHospital || !hospital || (hospital === 'masters' ? a.facilityId === null : a.facilityId === hospital)))
  }, [accounts.data, search, roleFilter, statusFilter, hospital, inHospital])

  // Outside a hospital, accounts are grouped by hospital (masters first).
  const groups = useMemo(() => {
    if (inHospital) return [{ key: 'all', label: null as string | null, items: rows }]
    const byKey = new Map<string, Account[]>()
    for (const a of rows) byKey.set(a.facilityCode ?? '', [...(byKey.get(a.facilityCode ?? '') ?? []), a])
    return [...byKey.entries()].sort(([a], [b]) => (a === '' ? -1 : b === '' ? 1 : a.localeCompare(b)))
      .map(([key, items]) => ({ key, label: key === '' ? 'Masters' : key, items }))
  }, [rows, inHospital])

  if (accounts.isLoading || people.isLoading) return <Loading />
  if (accounts.error || people.error) return <ErrorBox error={accounts.error ?? people.error} />

  const roles: AccountRole[] = !inHospital ? ['Master']
    : isMaster(me) ? ['Officer', 'Admin', 'Supervisor', 'Master'] : ['Officer', 'Admin', 'Supervisor']
  const withoutAccount = (people.data ?? []).filter(p => p.status !== 'Left' && !accounts.data!.some(a => a.personId === p.id)).length

  return (
    <Stack gap="md">
      <Group justify="space-between" wrap="wrap">
        <Group>
          <TextInput placeholder="Find email or name" leftSection={<IconSearch size={16} />} value={search}
            onChange={e => setSearch(e.currentTarget.value)} w={240} />
          {!inHospital && (
            <Select placeholder="All hospitals" clearable w={220} value={hospital} onChange={setHospital} searchable
              data={[{ value: 'masters', label: 'Masters' },
                ...(facilities.data ?? []).map(f => ({ value: f.id, label: `${f.code} · ${f.name}` }))]} />
          )}
          <Select placeholder="Any role" clearable w={160} value={roleFilter} onChange={setRoleFilter}
            data={roles.concat(inHospital || hospital === 'masters' ? [] : ['Officer', 'Admin', 'Supervisor'] as AccountRole[])
              .filter((r, i, all) => all.indexOf(r) === i).map(r => ({ value: r, label: ROLE_LABEL[r] }))} />
          <Select placeholder="Any status" clearable w={140} value={statusFilter} onChange={setStatusFilter}
            data={['Active', 'Invited', 'Disabled']} />
          <Text size="sm" c="dimmed">{rows.length} of {accounts.data!.length}</Text>
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
              {groups.map(g => (
                <Fragment key={g.key}>
                {g.label && (
                  <Table.Tr bg="var(--mantine-color-gray-1)">
                    <Table.Td colSpan={6}><Text fw={700} size="sm">{g.label} <Text span size="xs" c="dimmed">· {g.items.length}</Text></Text></Table.Td>
                  </Table.Tr>
                )}
              {g.items.map(a => {
                const st = status(a)
                return (
                  <Table.Tr key={a.id} opacity={a.enabled ? 1 : 0.55}>
                    <Table.Td fw={600}>{a.email}{a.id === me?.accountId && <Text span size="xs" c="dimmed"> (you)</Text>}</Table.Td>
                    <Table.Td>{a.personName ?? <Text span c="dimmed">–</Text>}</Table.Td>
                    <Table.Td><Badge variant="light" color={a.role === 'Officer' ? 'brand' : 'violet'}>{ROLE_LABEL[a.role]}</Badge></Table.Td>
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
                </Fragment>
              ))}
              {rows.length === 0 && (
                <Table.Tr><Table.Td colSpan={6}><Text size="sm" c="dimmed" ta="center" py="lg">No accounts match.</Text></Table.Td></Table.Tr>
              )}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      </Paper>

      {editing && (
        <AccountModal account={editing === 'new' ? null : editing} people={people.data ?? []} accounts={accounts.data!} roles={roles}
          isSelf={editing !== 'new' && editing.id === me?.accountId}
          onClose={() => setEditing(null)} onSaved={refresh} onIssued={onIssued} />
      )}

      <TempPasswordsModal logins={issued} onClose={() => setIssued([])} />
    </Stack>
  )
}

function AccountModal({ account, people, accounts, roles, isSelf, onClose, onSaved, onIssued }: {
  account: Account | null; people: AdminPerson[]; accounts: Account[]; roles: AccountRole[]; isSelf: boolean
  onClose: () => void; onSaved: () => void; onIssued: (email: string, r: InviteResult) => void
}) {
  const [email, setEmail] = useState(account?.email ?? '')
  const [role, setRole] = useState<AccountRole>(account?.role ?? roles[0])
  const [personId, setPersonId] = useState<string | null>(account?.personId ?? null)
  const [enabled, setEnabled] = useState(account?.enabled ?? true)

  // Officers who are still around and don't have (another) account.
  const options = people
    .filter(p => p.status !== 'Left' && !accounts.some(a => a.personId === p.id && a.id !== account?.id))
    .map(p => ({ value: p.id, label: `${p.name} (${p.code})` }))

  const save = useMutation({
    mutationFn: async () => {
      const pid = hasOfficer(role) ? personId : null
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
              data={(roles.includes(role) ? roles : [...roles, role]).map(r => ({ value: r, label: ROLE_LABEL[r] }))} />
            <Text size="xs" c="dimmed">{ROLE_HELP[role]}</Text>
          </Stack>
          {hasOfficer(role) && (
            <Select label="Officer" required searchable placeholder="Pick the officer" data={options} value={personId}
              onChange={setPersonId} nothingFoundMessage="No officer without an account – add them (with their email) on the Officers tab" />
          )}
          {account && (
            <Switch label="Can sign in" checked={enabled} disabled={isSelf} onChange={e => setEnabled(e.currentTarget.checked)}
              description={isSelf ? "You can't disable yourself or remove your own admin rights." : 'Disabling signs them out immediately.'} />
          )}
          {save.error && <Text c="red" size="sm">{save.error.message}</Text>}
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>Cancel</Button>
            <Button type="submit" loading={save.isPending} disabled={!email.trim() || (hasOfficer(role) && !personId)}>
              {account ? 'Save' : 'Send invite'}
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  )
}
