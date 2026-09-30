import {
  Alert, Badge, Button, Group, List, Modal, NumberInput, Paper, Select, Stack, Table, Text, Title, Tooltip,
} from '@mantine/core'
import { DatePickerInput } from '@mantine/dates'
import { modals } from '@mantine/modals'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  IconAlertTriangle, IconArrowsExchange, IconDownload, IconEye, IconEyeOff, IconLock, IconTrash, IconWand,
} from '@tabler/icons-react'
import { useMemo, useState } from 'react'
import { api, type Period, type RotaDay, type RunDetail } from '../../api'
import { ErrorBox, Loading, StatusBadge } from '../../components/common'
import { defaultPeriod, notifyError, notifyOk, pick } from '../../lib'
import { RotaCalendar } from '../../components/RotaCalendar'
import { formatLong, formatWeekday } from '../../dates'

export function RotaTab() {
  const qc = useQueryClient()
  const periods = useQuery({ queryKey: ['admin', 'periods'], queryFn: api.admin.periods })
  const [chosenPeriodId, setPeriodId] = useState<string | null>(null)
  const [chosenRunId, setRunId] = useState<string | null>(null)
  const [seed, setSeed] = useState<number | ''>('')

  const period = pick(periods.data, chosenPeriodId, defaultPeriod(periods.data, p => p.status !== 'Published'))
  const periodId = period?.id ?? null
  const runs = useQuery({ queryKey: ['admin', 'runs', periodId], queryFn: () => api.admin.runs(periodId!), enabled: !!periodId })
  const runId = pick(runs.data, chosenRunId, runs.data?.find(r => r.isPublished) ?? runs.data?.[0])?.id ?? null

  const refreshAll = () => {
    qc.invalidateQueries({ queryKey: ['admin'] })
    qc.invalidateQueries({ queryKey: ['periods'] })
    qc.invalidateQueries({ queryKey: ['rota'] })
  }

  const lock = useMutation({
    mutationFn: () => api.admin.lock(periodId!),
    onSuccess: () => { refreshAll(); notifyOk('Leave is locked. You can generate the rota now.', 'Locked') },
    onError: e => notifyError(e),
  })
  const generate = useMutation({
    mutationFn: () => api.admin.generate(periodId!, seed === '' ? undefined : seed),
    onSuccess: d => {
      qc.setQueryData(['admin', 'run', d.run.id], d)
      qc.invalidateQueries({ queryKey: ['admin', 'runs', periodId] })
      setRunId(d.run.id)
      notifyOk(`Draft created (seed ${d.run.seed}).`, 'Rota generated')
    },
    onError: e => notifyError(e, 'Could not generate'),
  })

  if (periods.isLoading) return <Loading />
  if (periods.error) return <ErrorBox error={periods.error} />
  if (!periods.data?.length) return <Alert title="No periods">Create a rota period on the “Periods & holidays” tab first.</Alert>

  return (
    <Stack gap="md">
      <Paper withBorder p="md">
        <Group justify="space-between" align="flex-end" wrap="wrap">
          <Group align="flex-end" wrap="wrap">
            <Select
              label="Rota period"
              data={periods.data.map(p => ({ value: p.id, label: `${p.name} · ${p.status}` }))}
              value={periodId}
              onChange={v => { setPeriodId(v); setRunId(null) }}
              allowDeselect={false}
              w={260}
            />
            {period && (
              <Stack gap={2}>
                <StatusBadge status={period.status} />
                <Text size="xs" c="dimmed">{formatLong(period.startDate)} – {formatLong(period.endDate)}</Text>
              </Stack>
            )}
          </Group>
          {period && (
            <Group align="flex-end" wrap="wrap">
              {period.status === 'Open' ? (
                <Button color="orange" leftSection={<IconLock size={16} />} loading={lock.isPending} onClick={() => modals.openConfirmModal({
                  title: `Lock leave for ${period.name}?`,
                  children: <Text size="sm">People won't be able to change their leave. You can reopen it later from “Periods & holidays”.</Text>,
                  labels: { confirm: 'Lock', cancel: 'Cancel' },
                  onConfirm: () => lock.mutate(),
                })}>
                  Lock leave to generate
                </Button>
              ) : (
                <>
                  <NumberInput label="Seed (optional)" description="Same seed = same rota" value={seed}
                    onChange={v => setSeed(v === '' ? '' : Number(v))} w={150} allowDecimal={false} />
                  <Button size="md" leftSection={<IconWand size={18} />} loading={generate.isPending} onClick={() => generate.mutate()}>
                    Generate draft
                  </Button>
                </>
              )}
            </Group>
          )}
        </Group>
      </Paper>

      {runs.data && runs.data.length > 0 && (
        <Group gap="xs" wrap="wrap">
          <Text size="sm" fw={600}>Drafts:</Text>
          {runs.data.map((r, i) => (
            <Button
              key={r.id}
              size="xs"
              variant={r.id === runId ? 'filled' : 'default'}
              color={r.isPublished ? 'green' : r.isInReview ? 'violet' : 'gray'}
              onClick={() => setRunId(r.id)}
            >
              #{runs.data.length - i} {r.isPublished ? '· published' : r.isInReview ? '· in review' : ''} ({r.unassigned} gaps, {r.consecutivePairs} back-to-back)
            </Button>
          ))}
        </Group>
      )}

      {period && runId && <RunView runId={runId} period={period} onChanged={refreshAll} onDeleted={() => setRunId(null)} />}
      {period && period.status !== 'Open' && runs.data?.length === 0 && (
        <Alert title="No drafts yet">Press “Generate draft” to create the rota for {period.name}.</Alert>
      )}
    </Stack>
  )
}

function RunView({ runId, period, onChanged, onDeleted }: {
  runId: string; period: Period; onChanged: () => void; onDeleted: () => void
}) {
  const qc = useQueryClient()
  const detail = useQuery({ queryKey: ['admin', 'run', runId], queryFn: () => api.admin.run(runId) })
  const [editing, setEditing] = useState<RotaDay | null>(null)

  const [reviewOpen, setReviewOpen] = useState(false)
  const act = useMutation({
    mutationFn: (kind: 'publish' | 'unpublish' | 'delete' | 'withdraw') =>
      kind === 'publish' ? api.admin.publish(runId) : kind === 'unpublish' ? api.admin.unpublish(runId)
        : kind === 'withdraw' ? api.admin.withdrawReview(runId) : api.admin.deleteRun(runId),
    onSuccess: (_, kind) => {
      qc.invalidateQueries({ queryKey: ['admin', 'run', runId] })
      onChanged()
      if (kind === 'delete') onDeleted()
      notifyOk(kind === 'publish' ? 'Published: officers are notified and shift totals are updated.'
        : kind === 'unpublish' ? 'Rota hidden; totals rolled back.'
        : kind === 'withdraw' ? 'Back to draft: officers no longer see it and open swap requests are closed.' : 'Draft deleted.')
    },
    onError: e => notifyError(e),
  })

  if (detail.isLoading) return <Loading />
  if (detail.error) return <ErrorBox error={detail.error} />
  const d = detail.data!
  const { run } = d

  return (
    <Stack gap="md">
      <Paper withBorder p="md">
        <Group justify="space-between" wrap="wrap">
          <Group gap="xs" wrap="wrap">
            <Badge size="lg" color={run.isPublished ? 'green' : run.isInReview ? 'violet' : 'gray'}>
              {run.isPublished ? 'Published' : run.isInReview ? 'In review' : 'Draft'}
            </Badge>
            <Badge size="lg" color={run.unassigned ? 'red' : 'green'} variant="light">{run.unassigned} unassigned</Badge>
            <Badge size="lg" color={run.consecutivePairs ? 'orange' : 'green'} variant="light">{run.consecutivePairs} back-to-back</Badge>
            <Badge size="lg" color="yellow" variant="light">{run.manualChanges} manual</Badge>
            <Text size="xs" c="dimmed">seed {run.seed} · {new Date(run.createdAt).toLocaleString()}</Text>
          </Group>
          <Group gap="xs">
            <Button variant="default" leftSection={<IconDownload size={16} />} component="a" href={api.admin.exports.timetable(run.id)}>
              Timetable (Excel)
            </Button>
            {run.isPublished ? (
              <Button color="orange" variant="light" leftSection={<IconEyeOff size={16} />} loading={act.isPending}
                onClick={() => act.mutate('unpublish')}>Unpublish</Button>
            ) : (
              <>
                {run.isInReview ? (
                  <Button color="gray" variant="light" loading={act.isPending} onClick={() => act.mutate('withdraw')}>Back to draft</Button>
                ) : (
                  <>
                    <Button color="red" variant="subtle" leftSection={<IconTrash size={16} />} onClick={() => act.mutate('delete')}>Delete draft</Button>
                    <Button color="violet" variant="light" leftSection={<IconArrowsExchange size={16} />} onClick={() => setReviewOpen(true)}>
                      Send for review
                    </Button>
                  </>
                )}
                <Button color="green" leftSection={<IconEye size={16} />} loading={act.isPending} onClick={() => modals.openConfirmModal({
                  title: `Publish this rota for ${period.name}?`,
                  children: (
                    <Text size="sm">
                      Officers are notified, everyone sees it on the Rota page and these shifts count towards each person's totals
                      {run.isInReview ? '. Open swap requests are closed' : ''}
                      {run.unassigned ? `. Note: ${run.unassigned} day(s) are still unassigned.` : '.'}
                    </Text>
                  ),
                  labels: { confirm: 'Publish', cancel: 'Cancel' },
                  confirmProps: { color: 'green' },
                  onConfirm: () => act.mutate('publish'),
                })}>Publish</Button>
              </>
            )}
          </Group>
        </Group>
        {run.warnings.length > 0 && (
          <Alert mt="md" color={run.unassigned || run.consecutivePairs ? 'orange' : 'brand'} icon={<IconAlertTriangle />} title="Notes from the generator">
            <List size="sm" spacing={2}>{run.warnings.map((w, i) => <List.Item key={i}>{w}</List.Item>)}</List>
          </Alert>
        )}
      </Paper>

      <Stack gap="md">
        <Paper withBorder p="md">
          <Group justify="space-between" mb="sm">
            <Title order={5}>Calendar</Title>
            <Text size="xs" c="dimmed">
              {run.isPublished ? 'Unpublish to make changes.'
                : run.isInReview ? 'Click a day to change it – the officers involved are notified.' : 'Click a day to change who is on call.'}
            </Text>
          </Group>
          <RotaCalendar days={d.days} onDayClick={run.isPublished ? undefined : setEditing} />
        </Paper>
        <Stats detail={d} />
      </Stack>

      {reviewOpen && <ReviewModal runId={runId} period={period} onClose={() => setReviewOpen(false)} onDone={() => {
        qc.invalidateQueries({ queryKey: ['admin', 'run', runId] })
        onChanged()
      }} />}

      {editing && (
        <OverrideModal day={editing} detail={d} onClose={() => setEditing(null)}
          onSaved={() => qc.invalidateQueries({ queryKey: ['admin', 'run', runId] })} />
      )}
    </Stack>
  )
}

function Stats({ detail }: { detail: RunDetail }) {
  const [sort, setSort] = useState<'name' | 'total' | 'weekend' | 'prior'>('name')
  const rows = useMemo(() => {
    const s = [...detail.stats]
    const key = {
      name: (a: typeof s[0]) => a.name,
      total: (a: typeof s[0]) => -a.total,
      weekend: (a: typeof s[0]) => -a.weekendHoliday,
      prior: (a: typeof s[0]) => (a.priorWeekendHoliday ?? -1) + a.weekendHoliday,
    }[sort]
    return s.sort((a, b) => {
      const x = key(a), y = key(b)
      return typeof x === 'string' ? x.localeCompare(y as string) : (x as number) - (y as number)
    })
  }, [detail.stats, sort])

  const th = (k: typeof sort, label: string) => (
    <Table.Th style={{ cursor: 'pointer' }} onClick={() => setSort(k)} c={sort === k ? 'brand' : undefined}>{label}</Table.Th>
  )

  return (
    <Paper withBorder p="md">
      <Title order={5} mb="sm">Per person</Title>
      <Table.ScrollContainer minWidth={520} mah={640}>
        <Table stickyHeader striped verticalSpacing={3} fz="sm">
          <Table.Thead>
            <Table.Tr>
              {th('name', 'Name')}
              {th('total', 'Shifts')}
              <Table.Th>Wkday</Table.Th>
              {th('weekend', 'Wkend+PH')}
              {th('prior', 'Prior wkend → after')}
              <Table.Th>Leave</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {rows.map(s => (
              <Table.Tr key={s.personId}>
                <Table.Td><Text size="sm" fw={600} lineClamp={1}>{s.name}</Text></Table.Td>
                <Table.Td>{s.total}</Table.Td>
                <Table.Td>{s.weekday}</Table.Td>
                <Table.Td>{s.weekendHoliday}</Table.Td>
                <Table.Td>
                  {s.priorWeekendHoliday === null
                    ? <Tooltip label="No history – the group average was used"><Text span c="dimmed">? → {s.weekendHoliday}</Text></Tooltip>
                    : `${s.priorWeekendHoliday} → ${s.priorWeekendHoliday + s.weekendHoliday}`}
                </Table.Td>
                <Table.Td c="dimmed">{s.leaveDays}</Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      </Table.ScrollContainer>
    </Paper>
  )
}

function OverrideModal({ day, detail, onClose, onSaved }: {
  day: RotaDay; detail: RunDetail; onClose: () => void; onSaved: () => void
}) {
  const [personId, setPersonId] = useState<string | null>(day.personId)
  const [warnings, setWarnings] = useState<string[]>([])

  const counts = new Map(detail.stats.map(s => [s.personId, s]))
  const options = detail.stats.map(s => ({
    value: s.personId,
    label: `${s.name} — ${s.total} shift(s), ${s.weekendHoliday} wkend`,
  }))

  const save = useMutation({
    mutationFn: () => api.admin.override(detail.run.id, day.date, personId),
    onSuccess: r => {
      onSaved()
      if (r.warnings.length) setWarnings(r.warnings)
      else { onClose(); notifyOk(`${formatWeekday(day.date)}: ${r.day.personName ?? 'unassigned'}.`) }
    },
    onError: e => notifyError(e),
  })

  const current = day.personId ? counts.get(day.personId) : undefined

  return (
    <Modal opened onClose={onClose} title={`${formatWeekday(day.date)}${day.holidayName ? ` · ${day.holidayName}` : ''}`}>
      <Stack>
        <Text size="sm">Currently: <b>{current?.name ?? 'Unassigned'}</b>{day.isManual ? ' (changed manually)' : ''}</Text>
        <Select label="On call" searchable clearable placeholder="Unassigned" data={options} value={personId}
          onChange={v => { setPersonId(v); setWarnings([]) }} />
        {warnings.length > 0 && (
          <Alert color="orange" icon={<IconAlertTriangle />} title="Saved, but check this">
            <List size="sm">{warnings.map((w, i) => <List.Item key={i}>{w}</List.Item>)}</List>
          </Alert>
        )}
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>{warnings.length ? 'Close' : 'Cancel'}</Button>
          {!warnings.length && (
            <Button onClick={() => save.mutate()} loading={save.isPending} disabled={personId === day.personId}>Save</Button>
          )}
        </Group>
      </Stack>
    </Modal>
  )
}

/** Show the draft to officers so they can check their dates and swap, before publishing. */
function ReviewModal({ runId, period, onClose, onDone }: { runId: string; period: Period; onClose: () => void; onDone: () => void }) {
  const [deadline, setDeadline] = useState<string | null>(null)
  const send = useMutation({
    mutationFn: () => api.admin.sendForReview(runId, deadline),
    onSuccess: () => { onDone(); onClose(); notifyOk('Sent for review: officers are notified and can request swaps.') },
  })
  return (
    <Modal opened onClose={onClose} title={`Send ${period.name} for review`}>
      <Stack>
        <Text size="sm">
          Officers see this draft on My on-call and can ask each other to swap dates; an accepted swap updates the rota
          immediately. You can still change days yourself. Nothing counts until you publish.
        </Text>
        <DatePickerInput label="Swap deadline (optional)" description="Last day officers can request or accept swaps. Empty = until you publish."
          value={deadline} onChange={d => setDeadline((d as string | null) ?? null)} valueFormat="D MMM YYYY" clearable />
        {send.error && <Text c="red" size="sm">{send.error.message}</Text>}
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>Cancel</Button>
          <Button color="violet" loading={send.isPending} onClick={() => send.mutate()}>Send for review</Button>
        </Group>
      </Stack>
    </Modal>
  )
}
