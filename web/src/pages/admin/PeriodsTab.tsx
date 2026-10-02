import {
  ActionIcon, Button, Group, Menu, Modal, NumberInput, Paper, SimpleGrid, Stack, Table, Text, TextInput, Title,
} from '@mantine/core'
import { DatePickerInput } from '@mantine/dates'
import { modals } from '@mantine/modals'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  IconCalendarPlus, IconCoins, IconDeviceFloppy, IconDots, IconDownload, IconEdit, IconLock, IconLockOpen, IconPlus, IconTrash,
} from '@tabler/icons-react'
import { useState } from 'react'
import { api, type IsoDate, type Period, type PointsRow, type UpsertPeriod } from '../../api'
import { ErrorBox, Loading, StatusBadge } from '../../components/common'
import { notifyError, notifyOk } from '../../lib'
import { formatLong, formatWeekday, todayIso } from '../../dates'

export function PeriodsTab() {
  return (
    <SimpleGrid cols={{ base: 1, lg: 2 }} spacing="lg">
      <Periods />
      <Stack gap="lg">
        <Holidays />
        <PeakDays />
      </Stack>
    </SimpleGrid>
  )
}

function Periods() {
  const qc = useQueryClient()
  const periods = useQuery({ queryKey: ['admin', 'periods'], queryFn: api.admin.periods })
  const [editing, setEditing] = useState<{ id: string | null; value: UpsertPeriod } | null>(null)
  const [pointsFor, setPointsFor] = useState<Period | null>(null)

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
                    <Menu.Item leftSection={<IconCoins size={16} />} onClick={() => setPointsFor(p)}>Leave points</Menu.Item>
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
      {pointsFor && <PointsModal period={pointsFor} onClose={() => setPointsFor(null)} />}
    </Paper>
  )
}

const toUpsert = (p: Period): UpsertPeriod =>
  ({ name: p.name, startDate: p.startDate, endDate: p.endDate, leaveDeadline: p.leaveDeadline, pointsBudget: p.pointsBudget })

/** Who used how many leave points, with per-officer top-ups for this period. */
function PointsModal({ period, onClose }: { period: Period; onClose: () => void }) {
  const rows = useQuery({ queryKey: ['admin', 'points', period.id], queryFn: () => api.admin.points(period.id) })
  return (
    <Modal opened onClose={onClose} title={`Leave points – ${period.name}`} size="xl">
      {rows.isLoading && <Loading />}
      {rows.error && <ErrorBox error={rows.error} />}
      {rows.data && (
        <Stack>
          <Text size="sm" c="dimmed">
            Everyone gets {rows.data[0]?.budget ?? 0} points (weekend 1, public holiday 2, peak day 1) and{' '}
            {rows.data[0]?.weekdayAllowance ?? 0} free weekdays. Give extra points for outstation, family matters and similar.
          </Text>
          <Table.ScrollContainer minWidth={700}>
            <Table striped verticalSpacing={4}>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Officer</Table.Th>
                  <Table.Th ta="center">Points used</Table.Th>
                  <Table.Th ta="center">Weekdays</Table.Th>
                  <Table.Th>Extra points</Table.Th>
                  <Table.Th>Reason</Table.Th>
                  <Table.Th />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {rows.data.map(r => <PointsRowEditor key={r.personId} periodId={period.id} row={r} />)}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Stack>
      )}
    </Modal>
  )
}

function PointsRowEditor({ periodId, row }: { periodId: string; row: PointsRow }) {
  const qc = useQueryClient()
  const [extra, setExtra] = useState(row.extra)
  const [reason, setReason] = useState(row.reason ?? '')
  const save = useMutation({
    mutationFn: () => api.admin.grantPoints(periodId, row.personId, extra, reason.trim() || null),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['admin', 'points', periodId] }); notifyOk(`${row.name}: extra points saved.`) },
    onError: e => notifyError(e),
  })
  const total = row.budget + row.extra
  const changed = extra !== row.extra || (reason.trim() || null) !== row.reason

  return (
    <Table.Tr>
      <Table.Td fw={600}>{row.name} <Text span size="xs" c="dimmed">{row.code}</Text></Table.Td>
      <Table.Td ta="center" c={row.pointsUsed > total ? 'red' : undefined}>{row.pointsUsed} / {total}</Table.Td>
      <Table.Td ta="center" c={row.weekdaysUsed > row.weekdayAllowance ? 'red' : undefined}>
        {row.weekdaysUsed} / {row.weekdayAllowance}
      </Table.Td>
      <Table.Td w={110}><NumberInput size="xs" min={0} max={100} value={extra} onChange={v => setExtra(Number(v) || 0)} /></Table.Td>
      <Table.Td><TextInput size="xs" placeholder="e.g. outstation" value={reason} maxLength={200}
        onChange={e => setReason(e.currentTarget.value)} /></Table.Td>
      <Table.Td w={40}>
        <ActionIcon variant="subtle" aria-label="Save extra points" disabled={!changed} loading={save.isPending} onClick={() => save.mutate()}>
          <IconDeviceFloppy size={16} />
        </ActionIcon>
      </Table.Td>
    </Table.Tr>
  )
}

/** Suggest the quarter after the latest period. */
function nextPeriod(periods: Period[]): UpsertPeriod {
  const latest = [...periods].sort((a, b) => b.endDate.localeCompare(a.endDate))[0]
  const start = latest ? new Date(`${latest.endDate}T00:00:00Z`) : new Date()
  if (latest) start.setUTCDate(start.getUTCDate() + 1)
  const s = new Date(Date.UTC(start.getUTCFullYear(), start.getUTCMonth(), 1))
  const e = new Date(Date.UTC(s.getUTCFullYear(), s.getUTCMonth() + 3, 0))
  const iso = (d: Date) => d.toISOString().slice(0, 10)
  const name = `${s.toLocaleString('en-GB', { month: 'short', timeZone: 'UTC' })}–${e.toLocaleString('en-GB', { month: 'short', year: 'numeric', timeZone: 'UTC' })}`
  return { name, startDate: iso(s), endDate: iso(e), leaveDeadline: null, pointsBudget: null }
}

function PeriodModal({ editing, onClose, onSaved }: {
  editing: { id: string | null; value: UpsertPeriod }; onClose: () => void; onSaved: () => void
}) {
  const [v, setV] = useState(editing.value)
  // The picker's own range, which is half-done between the first and second click.
  // Filling the missing end with the start would make it a finished one-day range,
  // and the next click would start a new range instead of choosing the end date.
  const [range, setRange] = useState<[IsoDate | null, IsoDate | null]>([editing.value.startDate, editing.value.endDate])
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
            value={range}
            onChange={r => {
              const [a, b] = r as [IsoDate | null, IsoDate | null]
              setRange([a, b])
              if (a && b) setV({ ...v, startDate: a, endDate: b })
            }}
            error={range[0] && !range[1] ? 'Pick the end date' : undefined}
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
          <NumberInput
            label="Leave points per officer"
            description="Leave empty for automatic: 30% of the period's weekend and public holiday days."
            placeholder="Automatic"
            min={0}
            max={100}
            value={v.pointsBudget ?? ''}
            onChange={x => setV({ ...v, pointsBudget: x === '' ? null : Number(x) })}
          />
          {save.error && <Text c="red" size="sm">{save.error.message}</Text>}
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>Cancel</Button>
            <Button type="submit" loading={save.isPending} disabled={!range[0] || !range[1]}>Save</Button>
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

/** Busy weekdays (e.g. eve of Raya): leave costs a point, but they stay ordinary weekdays for the rota. */
function PeakDays() {
  const qc = useQueryClient()
  const peaks = useQuery({ queryKey: ['admin', 'peakDays'], queryFn: api.admin.peakDays })
  const [date, setDate] = useState<IsoDate | null>(null)
  const [name, setName] = useState('')

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ['admin', 'peakDays'] })
    qc.invalidateQueries({ queryKey: ['leaveRules'] })
  }
  const add = useMutation({
    mutationFn: () => api.admin.upsertPeakDay({ date: date!, name: name.trim() || 'Peak day' }),
    onSuccess: () => { refresh(); setDate(null); setName('') },
    onError: e => notifyError(e),
  })
  const remove = useMutation({
    mutationFn: (d: IsoDate) => api.admin.deletePeakDay(d),
    onSuccess: refresh,
    onError: e => notifyError(e),
  })

  if (peaks.isLoading) return <Loading />
  if (peaks.error) return <ErrorBox error={peaks.error} />

  return (
    <Paper withBorder p="md">
      <Title order={4} mb="sm">Peak days</Title>
      <Text size="sm" c="dimmed" mb="sm">
        Weekdays many people want off, like the eve of Raya. Leave on them costs a point, like a weekend.
      </Text>
      <Group align="flex-end" mb="md" wrap="wrap">
        <DatePickerInput label="Date" value={date} onChange={d => setDate(d as string | null)} valueFormat="D MMM YYYY" w={160} clearable />
        <TextInput label="Name" placeholder="e.g. Eve of Raya" value={name} onChange={e => setName(e.currentTarget.value)} w={200} />
        <Button onClick={() => add.mutate()} disabled={!date} loading={add.isPending}>Add</Button>
      </Group>
      <Table>
        <Table.Tbody>
          {peaks.data!.map(h => (
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
      {peaks.data!.length === 0 && <Text size="sm" c="dimmed" ta="center" py="md">No peak days yet.</Text>}
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
