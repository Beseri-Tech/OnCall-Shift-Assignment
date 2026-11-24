import { ActionIcon, Badge, Button, Group, Modal, Paper, Stack, Switch, Table, Text, TextInput, Tooltip } from '@mantine/core'
import { modals } from '@mantine/modals'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconEdit, IconMailForward, IconUserPlus } from '@tabler/icons-react'
import { useState } from 'react'
import { api, type Account, type InviteResult } from '../../api'
import { useMe } from '../../auth'
import { ErrorBox, Loading } from '../../components/common'
import { notifyError, notifyOk } from '../../lib'
import { type IssuedLogin, TempPasswordsModal } from './TempPasswords'

const when = (iso: string | null) => (iso ? new Date(iso).toLocaleString('en-GB', { dateStyle: 'medium', timeStyle: 'short' }) : '–')

/**
 * The master accounts. Masters belong to no hospital; everyone else is added on a hospital's Officers tab, where an
 * officer and their sign-in are one record.
 */
export function MastersTab() {
  const qc = useQueryClient()
  const me = useMe().data
  const masters = useQuery({ queryKey: ['admin', 'masters'], queryFn: api.admin.masters })
  const [editing, setEditing] = useState<Account | 'new' | null>(null)
  const [issued, setIssued] = useState<IssuedLogin[]>([])

  const onIssued = (email: string, r: InviteResult) => {
    qc.invalidateQueries({ queryKey: ['admin', 'masters'] })
    if (r.tempPassword) setIssued([{ email, tempPassword: r.tempPassword }])
    else notifyOk(`Email sent to ${email}.`)
  }
  const resend = useMutation({
    mutationFn: (a: Account) => api.admin.resendInvite(a.id),
    onSuccess: (r, a) => onIssued(a.email, r),
    onError: e => notifyError(e),
  })

  if (masters.isLoading) return <Loading />
  if (masters.error) return <ErrorBox error={masters.error} />

  return (
    <Stack gap="md">
      <Group justify="space-between">
        <Text size="sm" c="dimmed">Masters see and run every hospital. Hospital admins, supervisors and officers are added on each hospital’s Officers tab.</Text>
        <Button leftSection={<IconUserPlus size={16} />} onClick={() => setEditing('new')}>Add master</Button>
      </Group>
      <Paper withBorder>
        <Table.ScrollContainer minWidth={640}>
          <Table striped highlightOnHover verticalSpacing={6}>
            <Table.Thead>
              <Table.Tr><Table.Th>Name</Table.Th><Table.Th>Email</Table.Th><Table.Th>Status</Table.Th><Table.Th>Last sign-in</Table.Th><Table.Th /></Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {masters.data!.map(a => (
                <Table.Tr key={a.id} opacity={a.enabled ? 1 : 0.55}>
                  <Table.Td fw={600}>{a.personName}{a.id === me?.accountId && <Text span size="xs" c="dimmed"> (you)</Text>}</Table.Td>
                  <Table.Td>{a.email}</Table.Td>
                  <Table.Td>
                    <Badge variant="light" color={!a.enabled ? 'gray' : a.mustChangePassword ? 'orange' : 'green'}>
                      {!a.enabled ? 'Disabled' : a.mustChangePassword ? 'Invited' : 'Active'}
                    </Badge>
                  </Table.Td>
                  <Table.Td><Text size="sm">{when(a.lastLoginAt)}</Text></Table.Td>
                  <Table.Td>
                    <Group gap={4} justify="flex-end" wrap="nowrap">
                      <Tooltip label="Send a new temporary password">
                        <ActionIcon variant="subtle" aria-label="Send new temporary password" disabled={!a.enabled}
                          loading={resend.isPending && resend.variables?.id === a.id}
                          onClick={() => modals.openConfirmModal({
                            title: `New temporary password for ${a.email}?`,
                            children: <Text size="sm">Their current password stops working. They’ll choose a new one when they sign in.</Text>,
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
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      </Paper>
      {editing && (
        <MasterModal master={editing === 'new' ? null : editing} isSelf={editing !== 'new' && editing.id === me?.accountId}
          onClose={() => setEditing(null)} onIssued={onIssued}
          onSaved={() => { qc.invalidateQueries({ queryKey: ['admin', 'masters'] }); notifyOk('Saved.') }} />
      )}
      <TempPasswordsModal logins={issued} onClose={() => setIssued([])} />
    </Stack>
  )
}

function MasterModal({ master, isSelf, onClose, onSaved, onIssued }: {
  master: Account | null; isSelf: boolean; onClose: () => void; onSaved: () => void; onIssued: (email: string, r: InviteResult) => void
}) {
  const [name, setName] = useState(master?.personName ?? '')
  const [email, setEmail] = useState(master?.email ?? '')
  const [enabled, setEnabled] = useState(master?.enabled ?? true)
  const save = useMutation({
    mutationFn: async () => {
      if (master) await api.admin.updateMaster(master.id, { name, email, enabled })
      else return api.admin.addMaster({ name, email })
    },
    onSuccess: r => { onClose(); if (r) onIssued(email.trim().toLowerCase(), r); else onSaved() },
  })
  return (
    <Modal opened onClose={onClose} title={master ? 'Edit master' : 'Add master'}>
      <form onSubmit={e => { e.preventDefault(); save.mutate() }}>
        <Stack>
          <TextInput label="Name" required value={name} onChange={e => setName(e.currentTarget.value)} data-autofocus />
          <TextInput label="Email" type="email" required value={email} onChange={e => setEmail(e.currentTarget.value)}
            description={master ? undefined : 'They get an email with a temporary password (valid 7 days).'} />
          {master && (
            <Switch label="Can sign in" checked={enabled} disabled={isSelf} onChange={e => setEnabled(e.currentTarget.checked)}
              description={isSelf ? "You can't disable yourself." : 'Switching it off signs them out straight away.'} />
          )}
          {save.error && <Text c="red" size="sm">{save.error.message}</Text>}
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>Cancel</Button>
            <Button type="submit" loading={save.isPending} disabled={!name.trim() || !email.trim()}>{master ? 'Save' : 'Send invite'}</Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  )
}
