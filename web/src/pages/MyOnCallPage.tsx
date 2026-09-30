import {
  Alert, Badge, Button, Card, Group, Modal, Paper, SegmentedControl, Select, SimpleGrid, Stack, Table, Text, Textarea, Title,
} from '@mantine/core'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  IconArrowsExchange, IconBriefcase, IconCalendarCheck, IconCheck, IconInfoCircle, IconStethoscope, IconSum, IconSun, IconX,
} from '@tabler/icons-react'
import { useState } from 'react'
import { Navigate } from 'react-router'
import { api, type Period, type RotaDay, type Shift, type Swap, type SwapStatus } from '../api'
import { useMe } from '../auth'
import { ErrorBox, Loading, PageHeader, StatCard } from '../components/common'
import { RotaCalendar } from '../components/RotaCalendar'
import { diffDays, formatLong, formatWeekday, todayIso } from '../dates'
import { notifyError, notifyOk } from '../lib'

type Show = 'upcoming' | 'past' | 'all'

const inDays = (n: number) => (n === 0 ? 'Today' : n === 1 ? 'Tomorrow' : n > 0 ? `In ${n} days` : n === -1 ? 'Yesterday' : `${-n} days ago`)

/** Swaps are open while the rota is in review and before its swap deadline (if any). */
const swapsOpen = (s: Shift, periods: Map<string, Period>, today: string) => {
  const deadline = periods.get(s.periodId)?.swapDeadline
  return s.inReview && s.date >= today && (!deadline || today <= deadline)
}

/** The signed-in officer's shifts (published, and in review), swap requests and tally. */
export function MyOnCallPage() {
  const qc = useQueryClient()
  const personId = useMe().data?.personId
  const [show, setShow] = useState<Show>('upcoming')
  const [swapping, setSwapping] = useState<Shift | null>(null)
  const profile = useQuery({ queryKey: ['person', personId], queryFn: () => api.person(personId!), enabled: !!personId })
  const shifts = useQuery({ queryKey: ['shifts', personId], queryFn: () => api.shifts(personId!), enabled: !!personId })
  const swaps = useQuery({ queryKey: ['swaps'], queryFn: api.swaps, enabled: !!personId })
  const periods = useQuery({ queryKey: ['periods'], queryFn: () => api.periods() })
  // Public holidays so the calendar can mark them on days you're not on call.
  const holidays = useQuery({ queryKey: ['holidays', 'all'], queryFn: () => api.holidays() })

  const refresh = () => {
    for (const key of ['swaps', 'shifts', 'rota', 'notifications']) qc.invalidateQueries({ queryKey: [key] })
  }
  const respond = useMutation({
    mutationFn: ({ swap, action }: { swap: Swap; action: 'accept' | 'decline' | 'cancel' }) =>
      action === 'accept' ? api.acceptSwap(swap.id) : action === 'decline' ? api.declineSwap(swap.id) : api.cancelSwap(swap.id),
    onSuccess: (_, { action }) => {
      refresh()
      notifyOk(action === 'accept' ? 'Swap done – your dates are updated.' : action === 'decline' ? 'Request declined.' : 'Request withdrawn.')
    },
    onError: e => { refresh(); notifyError(e, 'Could not update the request') },
  })

  if (!personId) return <Navigate to="/overview" replace />   // supervisors aren't on the rota
  if (profile.isLoading || shifts.isLoading) return <Loading />
  if (profile.error || shifts.error) return <ErrorBox error={profile.error ?? shifts.error} />

  const today = todayIso()
  const all = shifts.data ?? []
  const next = all.find(s => s.date >= today)
  const list = show === 'all' ? all
    : show === 'upcoming' ? all.filter(s => s.date >= today)
    : all.filter(s => s.date < today).reverse()
  const totals = profile.data?.totals
  const holidayMap = new Map((holidays.data ?? []).map(h => [h.date, h.name]))
  const periodMap = new Map((periods.data ?? []).map(p => [p.id, p]))
  const inReview = [...new Set(all.filter(s => s.inReview).map(s => s.periodId))].map(id => periodMap.get(id)).filter(p => !!p)

  const pending = (swaps.data ?? []).filter(s => s.status === 'Pending')
  const incoming = pending.filter(s => s.toPersonId === personId)
  const outgoing = pending.filter(s => s.fromPersonId === personId)
  const recent = (swaps.data ?? []).filter(s => s.status !== 'Pending').slice(0, 5)

  const swapPanel = (incoming.length > 0 || outgoing.length > 0 || recent.length > 0) && (
    <Paper className="panel" p="md">
      <Group justify="space-between" mb="sm">
        <Group gap="xs"><IconArrowsExchange size={20} color="var(--mantine-color-violet-6)" /><Title order={4}>Swap requests</Title></Group>
        {incoming.length > 0 && <Badge color="violet">{incoming.length} waiting for you</Badge>}
      </Group>
      <Stack gap="sm">
        {incoming.map(s => (
          <SwapRow key={s.id} swap={s} text={<><b>{s.fromName}</b> asks for your <b>{formatWeekday(s.toDate)}</b> and gives you <b>{formatWeekday(s.fromDate)}</b></>}>
            <Button size="xs" color="green" leftSection={<IconCheck size={14} />} loading={respond.isPending && respond.variables?.swap.id === s.id}
              onClick={() => respond.mutate({ swap: s, action: 'accept' })}>Accept</Button>
            <Button size="xs" variant="default" leftSection={<IconX size={14} />} disabled={respond.isPending}
              onClick={() => respond.mutate({ swap: s, action: 'decline' })}>Decline</Button>
          </SwapRow>
        ))}
        {outgoing.map(s => (
          <SwapRow key={s.id} swap={s} text={<>You asked <b>{s.toName}</b> for their <b>{formatWeekday(s.toDate)}</b> in exchange for your <b>{formatWeekday(s.fromDate)}</b> – waiting</>}>
            <Button size="xs" variant="default" disabled={respond.isPending} onClick={() => respond.mutate({ swap: s, action: 'cancel' })}>Withdraw</Button>
          </SwapRow>
        ))}
        {recent.length > 0 && (
          <>
            <Text size="xs" c="dimmed" fw={700} tt="uppercase" mt={4}>Recent</Text>
            {recent.map(s => (
              <Group key={s.id} gap="xs" wrap="nowrap">
                <StatusBadge status={s.status} />
                <Text size="sm">
                  {s.fromPersonId === personId ? `You → ${s.toName}` : `${s.fromName} → you`}: {formatWeekday(s.fromDate)} ↔ {formatWeekday(s.toDate)}
                </Text>
              </Group>
            ))}
          </>
        )}
      </Stack>
    </Paper>
  )
  const nextIn = next ? diffDays(today, next.date) : 0

  return (
    <Stack gap="lg">
      <PageHeader icon={IconStethoscope} title="My on-call" description="Your on-call dates, swap requests and tally." />

      {inReview.map(p => (
        <Alert key={p.id} color="violet" icon={<IconArrowsExchange />} title={`${p.name} rota is in review`}>
          These dates are a draft and can still change. To trade a date, press <b>Swap</b> next to it and pick another officer's
          date – if they accept, both dates change straight away.
          {p.swapDeadline ? <> Swaps close after <b>{formatLong(p.swapDeadline)}</b>.</> : ' Swaps close when the rota is published.'}
        </Alert>
      ))}

      {/* Requests waiting for an answer come first; otherwise the panel sits below the stats. */}
      {incoming.length > 0 && swapPanel}

      <SimpleGrid cols={{ base: 1, sm: 2, lg: 4 }} spacing="md">
        <Card className="hero-card" padding="lg">
          <Group gap="xs" mb={6}>
            <IconCalendarCheck size={18} />
            <Text size="xs" fw={700} tt="uppercase" opacity={0.85}>Next shift</Text>
          </Group>
          {next ? (
            <>
              <Text fw={800} fz={24} lh={1.2}>{formatWeekday(next.date)}</Text>
              <Group gap={6} mt={8}>
                {nextIn <= 2
                  ? <Badge color="orange" variant="filled">{inDays(nextIn)}</Badge>
                  : <Badge color="white" c="brand.8" variant="filled">{inDays(nextIn)}</Badge>}
                {next.inReview && <Badge color="violet" variant="filled">Draft</Badge>}
              </Group>
            </>
          ) : <Text mt={4} opacity={0.85}>None yet</Text>}
        </Card>
        <StatCard icon={IconSum} label="Total shifts" value={totals?.total ?? '–'} />
        <StatCard icon={IconBriefcase} color="indigo" label="Weekday" value={totals?.weekday ?? '–'} />
        <StatCard icon={IconSun} color="orange" label="Weekend + PH" value={totals?.weekendHoliday ?? '–'} />
      </SimpleGrid>
      <Text size="xs" c="dimmed" mt={-10}>
        {profile.data?.totalsKnown
          ? 'The tally counts every shift in published rotas, including upcoming ones, plus any history the admin entered. Draft dates don\'t count until published.'
          : 'No history recorded yet – the tally starts from your first published rota.'}
      </Text>

      {incoming.length === 0 && swapPanel}

      <Paper className="panel" p="md">
        <Group justify="space-between" wrap="wrap" mb="md">
          <Title order={4}>Shifts</Title>
          <SegmentedControl value={show} onChange={v => setShow(v as Show)} data={[
            { value: 'upcoming', label: `Upcoming (${all.filter(s => s.date >= today).length})` },
            { value: 'past', label: `Past (${all.filter(s => s.date < today).length})` },
            { value: 'all', label: 'All' },
          ]} />
        </Group>

        {list.length === 0 ? (
          <Alert icon={<IconInfoCircle />}>
            {show === 'past' ? 'No past shifts yet.' : 'No upcoming shifts. They appear here once the admin shares a rota.'}
          </Alert>
        ) : (
          <Stack gap="xl">
            <RotaCalendar days={toDays(list)} highlightPersonId="me" fillMonths holidays={holidayMap} />
            <Table highlightOnHover verticalSpacing="sm">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Date</Table.Th>
                  <Table.Th>Type</Table.Th>
                  <Table.Th>Period</Table.Th>
                  <Table.Th ta="right">When</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {list.map(s => (
                  <Table.Tr key={s.date} bg={s.date === next?.date ? 'brand.0' : undefined}>
                    <Table.Td fw={700}>{formatLong(s.date)}<Text span c="dimmed" size="sm" fw={400}> · {formatWeekday(s.date).split(' ')[0]}</Text></Table.Td>
                    <Table.Td>
                      <Group gap={4}>
                        <TypeBadge shift={s} />
                        {s.inReview && <Badge color="violet" variant="outline" size="sm">Draft</Badge>}
                      </Group>
                    </Table.Td>
                    <Table.Td><Text size="sm">{s.periodName}</Text></Table.Td>
                    <Table.Td ta="right">
                      {swapsOpen(s, periodMap, today)
                        ? <Button size="compact-xs" variant="light" color="violet" leftSection={<IconArrowsExchange size={14} />}
                            onClick={() => setSwapping(s)}>Swap</Button>
                        : <Text size="sm" c="dimmed">{inDays(diffDays(today, s.date))}</Text>}
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Stack>
        )}
      </Paper>

      {swapping && <SwapModal shift={swapping} personId={personId} onClose={() => setSwapping(null)} onSent={refresh} />}
    </Stack>
  )
}

/** Pick another officer's date in the same draft rota to offer this date for. */
function SwapModal({ shift, personId, onClose, onSent }: { shift: Shift; personId: string; onClose: () => void; onSent: () => void }) {
  const rota = useQuery({ queryKey: ['rota', shift.periodId], queryFn: () => api.rota(shift.periodId) })
  const [toDate, setToDate] = useState<string | null>(null)
  const [note, setNote] = useState('')
  const send = useMutation({
    mutationFn: () => api.requestSwap(shift.runId, shift.date, toDate!, note.trim() || null),
    onSuccess: s => { onSent(); onClose(); notifyOk(`Request sent to ${s.toName}. You'll be notified when they answer.`) },
  })

  const today = todayIso()
  const options = (rota.data?.days ?? [])
    .filter(d => d.personId && d.personId !== personId && d.date >= today)
    .map(d => ({
      value: d.date,
      label: `${formatWeekday(d.date)}${d.holidayName ? ` (${d.holidayName})` : d.isWeekendHoliday ? ' (weekend)' : ''} – ${d.personName}`,
    }))

  return (
    <Modal opened onClose={onClose} title={`Swap ${formatWeekday(shift.date)}`} size="lg">
      <Stack>
        <Text size="sm" c="dimmed">
          Pick the date you'd like instead. That officer gets your {formatWeekday(shift.date)}. Nothing changes until they accept.
        </Text>
        {rota.error && <ErrorBox error={rota.error} />}
        <Select label="Their date" placeholder={rota.isLoading ? 'Loading…' : 'Pick a date and officer'} searchable data={options}
          value={toDate} onChange={setToDate} maxDropdownHeight={300} nothingFoundMessage="No match" />
        <Textarea label="Message (optional)" placeholder="e.g. family event that weekend" maxLength={200} autosize minRows={2}
          value={note} onChange={e => setNote(e.currentTarget.value)} />
        {send.error && <Text c="red" size="sm">{send.error.message}</Text>}
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>Cancel</Button>
          <Button color="violet" leftSection={<IconArrowsExchange size={16} />} disabled={!toDate} loading={send.isPending}
            onClick={() => send.mutate()}>Send request</Button>
        </Group>
      </Stack>
    </Modal>
  )
}

function SwapRow({ swap, text, children }: { swap: Swap; text: React.ReactNode; children: React.ReactNode }) {
  return (
    <Paper withBorder p="sm" radius="md" bg="violet.0" style={{ borderColor: 'var(--mantine-color-violet-2)' }}>
      <Group justify="space-between" wrap="wrap" gap="xs">
        <div>
          <Text size="sm">{text}</Text>
          <Text size="xs" c="dimmed">{swap.periodName}{swap.note ? ` · "${swap.note}"` : ''}</Text>
          {swap.warnings.map(w => <Text key={w} size="xs" c="orange.8">⚠ {w}</Text>)}
        </div>
        <Group gap="xs">{children}</Group>
      </Group>
    </Paper>
  )
}

const SWAP_COLOR: Record<SwapStatus, string> = { Pending: 'violet', Accepted: 'green', Declined: 'red', Cancelled: 'gray', Expired: 'gray' }

function StatusBadge({ status }: { status: SwapStatus }) {
  return <Badge size="sm" variant="light" color={SWAP_COLOR[status]}>{status}</Badge>
}

function TypeBadge({ shift }: { shift: Shift }) {
  if (shift.holidayName) return <Badge color="green" variant="light">{shift.holidayName}</Badge>
  return shift.isWeekendHoliday
    ? <Badge color="orange" variant="light">Weekend</Badge>
    : <Badge color="gray" variant="light">Weekday</Badge>
}

/** Days for the calendar: your shifts only (fillMonths draws the other dates empty). */
const toDays = (shifts: Shift[]): RotaDay[] => [...shifts]
  .sort((a, b) => a.date.localeCompare(b.date))
  .map(s => ({
    date: s.date, isWeekendHoliday: s.isWeekendHoliday, holidayName: s.holidayName, personId: 'me',
    personName: s.inReview ? 'On call (draft)' : 'On call', isManual: false,
  }))
