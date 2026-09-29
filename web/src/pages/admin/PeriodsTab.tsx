import {
  ActionIcon, Button, Group, Menu, Modal, Paper, SimpleGrid, Stack, Table, Text, TextInput, Title,
} from '@mantine/core'
import { DatePickerInput } from '@mantine/dates'
import { modals } from '@mantine/modals'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  IconCalendarPlus, IconDots, IconDownload, IconEdit, IconLock, IconLockOpen, IconPlus, IconTrash,
} from '@tabler/icons-react'
import { useState } from 'react'
import { api, type IsoDate, type Period, type UpsertPeriod } from '../../api'
import { ErrorBox, Loading, StatusBadge } from '../../components/common'
import { notifyError, notifyOk } from '../../lib'
import { formatLong, formatWeekday, todayIso } from '../../dates'

export function PeriodsTab() {
  return (
    <SimpleGrid cols={{ base: 1, lg: 2 }} spacing="lg">
      <Periods />
      <Holidays />
    </SimpleGrid>
  )
}

function Periods() {
  const qc = useQueryClient()
  const periods = useQuery({ queryKey: ['admin', 'periods'], queryFn: api.admin.periods })
  const [editing, setEditing] = useState<{ id: string | null; value: UpsertPeriod } | null>(null)

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ['admin', 'periods'] })
    qc.invalidateQueries({ queryKey: ['periods'] })
  }
  const action = useMutation({
    mutationFn: async ({ kind, id }: { kind: 'lock' | 'unlock' | 'delete'; id: string }) => {
      if (kind === 'lock') await api.admin.lock(id)
      else if (kind === 'unlock') await api.admin.unlock(id)
      else await api.admin.deletePeriod(id)
    },
    onSuccess: refresh,
    onError: e => notifyError(e),
  })

  if (periods.isLoading) return <Loading />
  if (periods.error) return <ErrorBox error={periods.error} />

  return (
    <Paper withBorder p="md">
      <Group justify="space-between" mb="sm">
        <Title order={4}>Rota periods</Title>
        <Button leftSection={<IconPlus size={16} />} onClick={() => setEditing({ id: null, value: nextPeriod(periods.data!) })}>
          New period
        </Button>
      </Group>
      <Text size="sm" c="dimmed" mb="sm">
        People can enter leave while a period is <b>Open</b> (until its deadline). Lock it to freeze leave, then generate the rota.
      </Text>
      <Table highlightOnHover>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Name</Table.Th>
            <Table.Th>Dates</Table.Th>
            <Table.Th>Deadline</Table.Th>
            <Table.Th>Status</Table.Th>
            <Table.Th />
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {periods.data!.map(p => (
            <Table.Tr key={p.id}>
              <Table.Td fw={600}>{p.name}</Table.Td>
              <Table.Td>{formatLong(p.startDate)} – {formatLong(p.endDate)}</Table.Td>
              <Table.Td c={p.leaveDeadline && p.leaveDeadline < todayIso() ? 'red' : undefined}>
                {p.leaveDeadline ? formatLong(p.leaveDeadline) : '–'}
              </Table.Td>
              <Table.Td><StatusBadge status={p.status} /></Table.Td>
              <Table.Td>
                <Menu position="bottom-end" withinPortal>
                  <Menu.Target><ActionIcon variant="subtle" aria-label="Actions"><IconDots size={18} /></ActionIcon></Menu.Target>
                  <Menu.Dropdown>
                    <Menu.Item leftSection={<IconEdit size={16} />} onClick={() => setEditing({ id: p.id, value: toUpsert(p) })}>Edit</Menu.Item>
                    {p.status === 'Open' && (
                      <Menu.Item leftSection={<IconLock size={16} />} onClick={() => action.mutate({ kind: 'lock', id: p.id })}>
                        Lock leave
                      </Menu.Item>
                    )}
                    {p.status === 'Locked' && (
                      <Menu.Item leftSection={<IconLockOpen size={16} />} onClick={() => action.mutate({ kind: 'unlock', id: p.id })}>
                        Reopen for leave
                      </Menu.Item>
                    )}
                    <Menu.Item leftSection={<IconDownload size={16} />} component="a" href={api.admin.exports.leave(p.id)}>
                      Leave sheet (Excel)
                    </Menu.Item>
                    <Menu.Divider />
                    <Menu.Item color="red" leftSection={<IconTrash size={16} />} onClick={() => modals.openConfirmModal({
                      title: `Delete ${p.name}?`,
                      children: <Text size="sm">Rota drafts for this period are deleted too. People's leave is kept.</Text>,
                      labels: { confirm: 'Delete', cancel: 'Cancel' },
                      confirmProps: { color: 'red' },
                      onConfirm: () => action.mutate({ kind: 'delete', id: p.id }),
                    })}>
                      Delete
                    </Menu.Item>
                  </Menu.Dropdown>
                </Menu>
              </Table.Td>
            </Table.Tr>
          ))}
        </Table.Tbody>
      </Table>
      {periods.data!.length === 0 && <Text size="sm" c="dimmed" ta="center" py="md">No periods yet.</Text>}

      {editing && <PeriodModal editing={editing} onClose={() => setEditing(null)} onSaved={refresh} />}
    </Paper>
  )
}

const toUpsert = (p: Period): UpsertPeriod =>
  ({ name: p.name, startDate: p.startDate, endDate: p.endDate, leaveDeadline: p.leaveDeadline })

/** Suggest the quarter after the latest period. */
function nextPeriod(periods: Period[]): UpsertPeriod {
  const latest = [...periods].sort((a, b) => b.endDate.localeCompare(a.endDate))[0]
  const start = latest ? new Date(`${latest.endDate}T00:00:00Z`) : new Date()
  if (latest) start.setUTCDate(start.getUTCDate() + 1)
  const s = new Date(Date.UTC(start.getUTCFullYear(), start.getUTCMonth(), 1))
  const e = new Date(Date.UTC(s.getUTCFullYear(), s.getUTCMonth() + 3, 0))
  const iso = (d: Date) => d.toISOString().slice(0, 10)
  const name = `${s.toLocaleString('en-GB', { month: 'short', timeZone: 'UTC' })}–${e.toLocaleString('en-GB', { month: 'short', year: 'numeric', timeZone: 'UTC' })}`
  return { name, startDate: iso(s), endDate: iso(e), leaveDeadline: null }
}

function PeriodModal({ editing, onClose, onSaved }: {
  editing: { id: string | null; value: UpsertPeriod }; onClose: () => void; onSaved: () => void
}) {
  const [v, setV] = useState(editing.value)
  const save = useMutation({
    mutationFn: () => (editing.id ? api.admin.updatePeriod(editing.id, v) : api.admin.createPeriod(v)),
    onSuccess: () => { onSaved(); onClose(); notifyOk(`${v.name} saved.`) },
  })

  return (
    <Modal opened onClose={onClose} title={editing.id ? 'Edit period' : 'New rota period'}>
      <form onSubmit={e => { e.preventDefault(); save.mutate() }}>
        <Stack>
          <TextInput label="Name" required value={v.name} onChange={e => setV({ ...v, name: e.currentTarget.value })} />
          <DatePickerInput
            type="range"
            label="Dates"
            required
            value={[v.startDate, v.endDate]}
            onChange={r => {
              const [a, b] = r as [string | null, string | null]
              setV({ ...v, startDate: a ?? v.startDate, endDate: b ?? a ?? v.endDate })
            }}
            valueFormat="D MMM YYYY"
            numberOfColumns={2}
          />
          <DatePickerInput
            label="Leave deadline"
            description="Last day people can edit their leave. Leave empty to keep it open until you lock it."
            value={v.leaveDeadline}
            onChange={d => setV({ ...v, leaveDeadline: (d as string | null) ?? null })}
            valueFormat="D MMM YYYY"
            clearable
          />
          {save.error && <Text c="red" size="sm">{save.error.message}</Text>}
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>Cancel</Button>
            <Button type="submit" loading={save.isPending}>Save</Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  )
}

function Holidays() {
  const qc = useQueryClient()
  const from = `${Number(todayIso().slice(0, 4)) - 1}-01-01`
  const holidays = useQuery({ queryKey: ['holidays', from, 'all'], queryFn: () => api.holidays(from) })
  const [date, setDate] = useState<IsoDate | null>(null)
  const [name, setName] = useState('')
  const [bulkOpen, setBulkOpen] = useState(false)

  const refresh = () => qc.invalidateQueries({ queryKey: ['holidays'] })
  const add = useMutation({
    mutationFn: () => api.admin.upsertHoliday({ date: date!, name: name.trim() || 'Public holiday' }),
    onSuccess: () => { refresh(); setDate(null); setName('') },
    onError: e => notifyError(e),
  })
  const remove = useMutation({
    mutationFn: (d: IsoDate) => api.admin.deleteHoliday(d),
    onSuccess: refresh,
    onError: e => notifyError(e),
  })

  if (holidays.isLoading) return <Loading />
  if (holidays.error) return <ErrorBox error={holidays.error} />

  return (
    <Paper withBorder p="md">
      <Group justify="space-between" mb="sm">
        <Title order={4}>Public holidays</Title>
        <Button variant="default" leftSection={<IconCalendarPlus size={16} />} onClick={() => setBulkOpen(true)}>Add several</Button>
      </Group>
      <Text size="sm" c="dimmed" mb="sm">Public holidays count as weekend shifts when balancing the rota.</Text>
      <Group align="flex-end" mb="md" wrap="wrap">
        <DatePickerInput label="Date" value={date} onChange={d => setDate(d as string | null)} valueFormat="D MMM YYYY" w={160} clearable />
        <TextInput label="Name" placeholder="e.g. Deepavali" value={name} onChange={e => setName(e.currentTarget.value)} w={200} />
        <Button onClick={() => add.mutate()} disabled={!date} loading={add.isPending}>Add</Button>
      </Group>
      <Table>
        <Table.Tbody>
          {holidays.data!.map(h => (
            <Table.Tr key={h.date}>
              <Table.Td w={170}>{formatWeekday(h.date)} {h.date.slice(0, 4)}</Table.Td>
              <Table.Td>{h.name}</Table.Td>
              <Table.Td w={40}>
                <ActionIcon variant="subtle" color="red" aria-label="Delete" onClick={() => remove.mutate(h.date)}>
                  <IconTrash size={16} />
                </ActionIcon>
              </Table.Td>
            </Table.Tr>
          ))}
        </Table.Tbody>
      </Table>
      {holidays.data!.length === 0 && <Text size="sm" c="dimmed" ta="center" py="md">No holidays yet.</Text>}
      <BulkHolidays opened={bulkOpen} onClose={() => setBulkOpen(false)} onSaved={refresh} />
    </Paper>
  )
}

function BulkHolidays({ opened, onClose, onSaved }: { opened: boolean; onClose: () => void; onSaved: () => void }) {
  const [text, setText] = useState('')
  const [name, setName] = useState('')
  const add = useMutation({
    mutationFn: () => api.admin.bulkHolidays(text, todayIso(), name),
    onSuccess: r => { onSaved(); onClose(); setText(''); notifyOk(`${r.added} holiday(s) added.`) },
  })
  return (
    <Modal opened={opened} onClose={onClose} title="Add several holidays">
      <Stack>
        <TextInput label="Dates" description="Day/month, e.g. 20/10, 25/12, 1/1/2027" value={text} onChange={e => setText(e.currentTarget.value)} />
        <TextInput label="Name (optional)" value={name} onChange={e => setName(e.currentTarget.value)} />
        {add.error && <Text c="red" size="sm">{add.error.message}</Text>}
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>Cancel</Button>
          <Button onClick={() => add.mutate()} disabled={!text.trim()} loading={add.isPending}>Add</Button>
        </Group>
      </Stack>
    </Modal>
  )
}
