import {
  ActionIcon, Alert, Badge, Button, Card, Chip, Group, Paper, SegmentedControl, Select, SimpleGrid, Stack, Text,
  TextInput, Title, Tooltip,
} from '@mantine/core'
import { DatePickerInput } from '@mantine/dates'
import { useLocalStorage } from '@mantine/hooks'
import { modals } from '@mantine/modals'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  IconBeach, IconCalendarPlus, IconDeviceFloppy, IconEraser, IconInfoCircle, IconKeyboard, IconLock, IconStar, IconTrash,
} from '@tabler/icons-react'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { api, type Entries, type IsoDate, type Period } from '../api'
import { ErrorBox, Loading, PeriodSelect } from '../components/common'
import { defaultPeriod, notifyError, notifyOk, pick } from '../lib'
import { MonthGrid } from '../components/MonthGrid'
import { diffDays, eachDay, formatLong, formatRange, monthsBetween, todayIso, toRanges } from '../dates'
import { dayCap, dayKind, usage } from '../leaveRules'

type Mode = 'leave' | 'preferred'

const NOTE_PRESETS = ['Annual', 'Overseas', 'Maternity', 'Course', 'Emergency']

interface Draft {
  leave: Map<IsoDate, string | null>
  preferred: Set<IsoDate>
}

const toDraft = (e: Entries): Draft => ({
  leave: new Map(e.leave.map(l => [l.date, l.note])),
  preferred: new Set(e.preferred),
})

const toEntries = (d: Draft): Entries => ({
  leave: [...d.leave].sort(([a], [b]) => a.localeCompare(b)).map(([date, note]) => ({ date, note })),
  preferred: [...d.preferred].sort(),
})

const sameDraft = (a: Draft, b: Draft) => JSON.stringify(toEntries(a)) === JSON.stringify(toEntries(b))

export function LeavePage() {
  const [storedPersonId, setPersonId] = useLocalStorage<string | null>({ key: 'rota.personId', defaultValue: null })
  const [chosenPeriodId, setPeriodId] = useState<string | null>(null)

  const people = useQuery({ queryKey: ['people'], queryFn: api.people })
  const periods = useQuery({ queryKey: ['periods'], queryFn: () => api.periods() })

  const visiblePeriods = useMemo(() => (periods.data ?? []).filter(p => p.status !== 'Published'), [periods.data])
  const period = pick(visiblePeriods, chosenPeriodId, defaultPeriod(visiblePeriods))
  const periodId = period?.id ?? null

  // Ignore a remembered person who was removed or deactivated.
  const personId = people.data?.some(p => p.id === storedPersonId) ? storedPersonId : null

  if (people.isLoading || periods.isLoading) return <Loading />
  if (people.error) return <ErrorBox error={people.error} />
  if (periods.error) return <ErrorBox error={periods.error} />

  return (
    <Stack gap="lg">
      <Group justify="space-between" align="flex-end" wrap="wrap">
        <div>
          <Title order={2}>My leave</Title>
          <Text c="dimmed" size="sm">Pick your name, then mark your leave and preferred on-call days on the calendar.</Text>
        </div>
        <Group align="flex-end" wrap="wrap">
          <Select
            label="Your name"
            placeholder="Search your name"
            searchable
            clearable
            w={340}
            data={(people.data ?? []).map(p => ({ value: p.id, label: `${p.name} (${p.code})` }))}
            value={personId}
            onChange={setPersonId}
            nothingFoundMessage="No match – ask the admin to add you"
          />
          {visiblePeriods.length > 1 && <PeriodSelect periods={visiblePeriods} value={periodId} onChange={setPeriodId} />}
        </Group>
      </Group>

      {!period && (
        <Alert icon={<IconInfoCircle />} color="blue" title="No rota period is open for leave">
          The admin hasn't opened the next period yet. Check the Rota page for published rotas.
        </Alert>
      )}

      {!personId && period && (
        <Alert icon={<IconInfoCircle />} color="blue" title="Who are you?">
          Choose your name above to see and edit your leave for {period.name}.
        </Alert>
      )}

      {personId && period && <PersonLeave key={`${personId}:${period.id}`} personId={personId} period={period} />}
    </Stack>
  )
}

function PersonLeave({ personId, period }: { personId: string; period: Period }) {
  const entries = useQuery({ queryKey: ['entries', personId, period.id], queryFn: () => api.entries(personId, period.id) })

  if (entries.isLoading) return <Loading />
  if (entries.error) return <ErrorBox error={entries.error} />
  // The editor owns the draft from here on; it starts from what was loaded.
  return <LeaveEditor personId={personId} period={period} initial={entries.data!} />
}

function LeaveEditor({ personId, period, initial }: { personId: string; period: Period; initial: Entries }) {
  const qc = useQueryClient()
  const profile = useQuery({ queryKey: ['person', personId], queryFn: () => api.person(personId) })
  const holidays = useQuery({
    queryKey: ['holidays', period.startDate, period.endDate],
    queryFn: () => api.holidays(period.startDate, period.endDate),
  })
  const rules = useQuery({ queryKey: ['leaveRules', period.id, personId], queryFn: () => api.leaveRules(period.id, personId) })

  const [draft, setDraft] = useState<Draft>(() => toDraft(initial))
  const [saved, setSaved] = useState<Draft>(() => toDraft(initial))

  const holidayMap = useMemo(() => new Map((holidays.data ?? []).map(h => [h.date, h.name])), [holidays.data])
  const peakMap = useMemo(() => new Map((rules.data?.peaks ?? []).map(h => [h.date, h.name])), [rules.data])
  const kindOf = useCallback((d: IsoDate) => dayKind(d, holidayMap, peakMap), [holidayMap, peakMap])

  // Days where the leave cap is reached. Days you already saved stay yours (first come, first served).
  const full = useMemo(() => {
    const r = rules.data
    if (!r) return new Set<IsoDate>()
    return new Set(eachDay(period.startDate, period.endDate)
      .filter(d => !saved.leave.has(d) && (r.othersOff[d] ?? 0) >= dayCap(kindOf(d), r)))
  }, [rules.data, period.startDate, period.endDate, saved.leave, kindOf])
  const [mode, setMode] = useState<Mode>('leave')
  const [note, setNote] = useState<string | null>('Annual')
  const [customNote, setCustomNote] = useState('')

  const dirty = !sameDraft(draft, saved)
  const readOnly = !period.isEditable
  const effectiveNote = note === 'Other' ? (customNote.trim() || null) : note

  // Warn before closing the tab with unsaved changes.
  useEffect(() => {
    if (!dirty) return
    const handler = (e: BeforeUnloadEvent) => { e.preventDefault() }
    window.addEventListener('beforeunload', handler)
    return () => window.removeEventListener('beforeunload', handler)
  }, [dirty])

  const save = useMutation({
    mutationFn: () => api.saveEntries(personId, period.id, toEntries(draft)),
    onSuccess: data => {
      setDraft(toDraft(data))
      setSaved(toDraft(data))
      qc.setQueryData(['entries', personId, period.id], data)
      qc.invalidateQueries({ queryKey: ['overview', period.id] })
      qc.invalidateQueries({ queryKey: ['leaveRules', period.id] })
      notifyOk(`${data.leave.length} leave day(s) and ${data.preferred.length} preferred day(s) saved.`)
    },
    onError: e => {
      // Someone may have taken a day since this page loaded: refresh caps and "Full" days, keep the draft.
      qc.invalidateQueries({ queryKey: ['leaveRules', period.id] })
      notifyError(e, 'Could not save')
    },
  })

  /** Apply add/remove for a set of days in the current mode. */
  const paint = useCallback((dates: IsoDate[], add: boolean) => {
    setDraft(d => {
      const leave = new Map(d.leave)
      const preferred = new Set(d.preferred)
      for (const date of dates) {
        if (mode === 'leave') {
          if (add && full.has(date)) continue
          if (add) { leave.set(date, effectiveNote); preferred.delete(date) } else leave.delete(date)
        } else {
          if (add) { preferred.add(date); leave.delete(date) } else preferred.delete(date)
        }
      }
      return { leave, preferred }
    })
  }, [mode, effectiveNote, full])

  // Drag painting: the first day decides add vs remove; dragging across days (and months) continues.
  // Fast pointer moves can skip cells, so every day between the last painted day and the current one is filled.
  const dragAdd = useRef<boolean | null>(null)
  const dragLast = useRef<IsoDate | null>(null)
  useEffect(() => {
    const stop = () => { dragAdd.current = null; dragLast.current = null }
    window.addEventListener('pointerup', stop)
    window.addEventListener('pointercancel', stop)
    return () => {
      window.removeEventListener('pointerup', stop)
      window.removeEventListener('pointercancel', stop)
    }
  }, [])

  const isSelected = (date: IsoDate) => (mode === 'leave' ? draft.leave.has(date) : draft.preferred.has(date))
  const onDayDown = (date: IsoDate) => {
    dragAdd.current = !isSelected(date)
    dragLast.current = date
    paint([date], dragAdd.current)
  }
  const onDayEnter = (date: IsoDate) => {
    if (dragAdd.current === null || dragLast.current === null) return
    const [from, to] = dragLast.current < date ? [dragLast.current, date] : [date, dragLast.current]
    paint(eachDay(from, to).filter(d => d >= period.startDate && d <= period.endDate), dragAdd.current)
    dragLast.current = date
  }

  const months = monthsBetween(period.startDate, period.endDate)
  const today = todayIso()

  const totals = profile.data?.totalsKnown ? profile.data.totals : undefined

  // Same "only refuse what gets worse" rule as the server, so lowering a budget never blocks edits.
  const r = rules.data
  const used = r && usage(draft.leave.keys(), r, kindOf)
  const was = r && usage(saved.leave.keys(), r, kindOf)
  const budget = r ? r.budget + r.extra : 0
  const overPoints = !!used && !!was && used.points > budget && used.points > was.points
  const overWeekdays = !!used && !!was && !!r && used.weekdays > r.weekdayAllowance && used.weekdays > was.weekdays
  const limitError = overPoints
    ? `Not enough points (${used!.points} needed, ${budget} available). Ask the admin for extra points.`
    : overWeekdays ? `Too many weekdays (${used!.weekdays} marked, at most ${r!.weekdayAllowance}).` : null

  return (
    <Stack gap="md" pb={dirty ? 80 : 0}>
      <SimpleGrid cols={{ base: 1, md: 3 }} spacing="md">
        <Card withBorder padding="md">
          <Text size="xs" c="dimmed" fw={700} tt="uppercase">You</Text>
          <Text fw={700} size="lg" lineClamp={1}>{profile.data?.name ?? '…'}</Text>
          <Text size="sm" c="dimmed">Code {profile.data?.code}</Text>
        </Card>
        <Card withBorder padding="md">
          <Text size="xs" c="dimmed" fw={700} tt="uppercase">On-call shifts done</Text>
          <Group gap="lg" mt={4}>
            <Stat label="Total" value={totals?.total} />
            <Stat label="Weekday" value={totals?.weekday} />
            <Stat label="Weekend + PH" value={totals?.weekendHoliday} />
          </Group>
          {profile.data && !profile.data.totalsKnown && (
            <Text size="xs" c="dimmed" mt={4}>No history yet – counted from your first published rota.</Text>
          )}
        </Card>
        <Card withBorder padding="md">
          <Text size="xs" c="dimmed" fw={700} tt="uppercase">{period.name}</Text>
          <Text fw={600}>{formatLong(period.startDate)} – {formatLong(period.endDate)}</Text>
          <Text size="sm" c={readOnly ? 'red' : 'dimmed'}>
            {readOnly
              ? period.status === 'Open' ? 'Deadline passed – leave is closed' : 'Locked – leave is closed'
              : period.leaveDeadline ? `Edit until ${formatLong(period.leaveDeadline)}` : 'Open for leave'}
          </Text>
        </Card>
      </SimpleGrid>

      {readOnly && (
        <Alert color="orange" icon={<IconLock />} title="Read only">
          Leave for this period can no longer be changed here. Contact the admin if something is wrong.
        </Alert>
      )}

      {!readOnly && r && used && (
        <Paper withBorder p="md">
          <Group gap="xl" wrap="wrap" align="flex-start">
            <div>
              <Text size="xs" c="dimmed" fw={700} tt="uppercase">Leave points</Text>
              <Text fw={800} size="xl" c={overPoints ? 'red' : undefined}>{used.points} / {budget}</Text>
              {r.extra > 0 && <Text size="xs" c="dimmed">Includes +{r.extra} extra{r.extraReason ? `: ${r.extraReason}` : ''}</Text>}
            </div>
            <div>
              <Text size="xs" c="dimmed" fw={700} tt="uppercase">Weekdays</Text>
              <Text fw={800} size="xl" c={overWeekdays ? 'red' : undefined}>{used.weekdays} / {r.weekdayAllowance}</Text>
              <Text size="xs" c="dimmed">free, no points</Text>
            </div>
            <Text size="sm" c="dimmed" maw={460}>
              Leave on a weekend costs {r.weekendCost} point, a public holiday {r.holidayCost}, a peak day {r.peakCost}.
              At most {r.busyDayCap} of {r.onCall} officers can be off on a weekend, public holiday or peak day, and {r.weekdayCap} on
              other days – striped days are full. Need more? Ask the admin.
            </Text>
          </Group>
        </Paper>
      )}

      {!readOnly && (
        <Paper withBorder p="md">
          <Stack gap="sm">
            <Group justify="space-between" wrap="wrap" gap="sm">
              <SegmentedControl
                value={mode}
                onChange={v => setMode(v as Mode)}
                data={[
                  { value: 'leave', label: <Group gap={6} wrap="nowrap"><IconBeach size={16} />Leave</Group> },
                  { value: 'preferred', label: <Group gap={6} wrap="nowrap"><IconStar size={16} />Preferred on-call</Group> },
                ]}
              />
              <Group gap="xs" wrap="wrap">
                <RangeAdder period={period} onAdd={dates => paint(dates, true)} />
                <TypedDates period={period} onAdd={dates => paint(dates, true)} />
                <Tooltip label={`Remove all ${mode === 'leave' ? 'leave' : 'preferred days'} in this period`}>
                  <Button
                    variant="default"
                    leftSection={<IconEraser size={16} />}
                    onClick={() => modals.openConfirmModal({
                      title: 'Clear everything?',
                      children: <Text size="sm">Remove all your {mode === 'leave' ? 'leave' : 'preferred days'} in {period.name}? Nothing is saved until you press Save.</Text>,
                      labels: { confirm: 'Clear', cancel: 'Cancel' },
                      confirmProps: { color: 'red' },
                      onConfirm: () => paint(eachDay(period.startDate, period.endDate), false),
                    })}
                  >
                    Clear all
                  </Button>
                </Tooltip>
              </Group>
            </Group>

            {mode === 'leave' && (
              <Group gap="xs" wrap="wrap">
                <Text size="sm" fw={600}>Note for new leave:</Text>
                <Chip.Group value={note ?? ''} onChange={v => setNote(typeof v === 'string' && v ? v : null)}>
                  <Group gap={6}>
                    {[...NOTE_PRESETS, 'Other'].map(n => <Chip key={n} value={n} size="sm">{n}</Chip>)}
                    <Chip value="" size="sm">None</Chip>
                  </Group>
                </Chip.Group>
                {note === 'Other' && (
                  <TextInput size="xs" placeholder="e.g. luar negara" value={customNote} maxLength={60}
                    onChange={e => setCustomNote(e.currentTarget.value)} w={180} />
                )}
              </Group>
            )}
            <Text size="xs" c="dimmed">
              Click a day to toggle it, or press and drag across days. {mode === 'leave' ? 'Leave' : 'Preferred'} mode is on.
            </Text>
          </Stack>
        </Paper>
      )}

      <Paper withBorder p="md">
        <SimpleGrid cols={{ base: 1, sm: 2, lg: Math.min(3, months.length) }} spacing="xl">
          {months.map(m => (
            <MonthGrid
              key={m}
              month={m}
              min={period.startDate}
              max={period.endDate}
              holidays={holidayMap}
              peaks={peakMap}
              full={readOnly || mode !== 'leave' ? undefined : full}
              today={today}
              readOnly={readOnly}
              stateOf={date => ({ leave: draft.leave.has(date), note: draft.leave.get(date), preferred: draft.preferred.has(date) })}
              onDayDown={onDayDown}
              onDayEnter={onDayEnter}
            />
          ))}
        </SimpleGrid>
        <Group gap="lg" mt="md">
          <Legend color="var(--leave-bg)" border="var(--leave-border)" label="Leave" />
          <Legend color="var(--pref-bg)" border="var(--pref-border)" label="Preferred on-call" />
          <Legend color="var(--weekend-bg)" border="#cbd5e1" label="Weekend" />
          <Text size="xs" c="var(--holiday-text)" fw={700}>PH = public holiday</Text>
          <Text size="xs" c="var(--peak-text)" fw={700}>PEAK = peak day</Text>
        </Group>
      </Paper>

      <Summary draft={draft} readOnly={readOnly} onChange={setDraft} />

      {dirty && !readOnly && (
        <Paper shadow="lg" p="sm" withBorder pos="fixed" bottom={16} left="50%" style={{ transform: 'translateX(-50%)', zIndex: 200 }}>
          <Group gap="sm" wrap="nowrap">
            <Text fw={600} size="sm" c={limitError ? 'red' : undefined}>{limitError ?? 'You have unsaved changes'}</Text>
            <Button variant="default" onClick={() => setDraft(saved)}>Discard</Button>
            <Button leftSection={<IconDeviceFloppy size={18} />} loading={save.isPending} disabled={!!limitError}
              onClick={() => save.mutate()}>
              Save
            </Button>
          </Group>
        </Paper>
      )}
    </Stack>
  )
}

function Stat({ label, value }: { label: string; value: number | undefined }) {
  return (
    <div>
      <Text fw={800} size="xl" lh={1.1}>{value ?? '–'}</Text>
      <Text size="xs" c="dimmed">{label}</Text>
    </div>
  )
}

function Legend({ color, border, label }: { color: string; border: string; label: string }) {
  return (
    <Group gap={6}>
      <span className="legend-swatch" style={{ background: color, borderColor: border }} />
      <Text size="xs">{label}</Text>
    </Group>
  )
}

function RangeAdder({ period, onAdd }: { period: Period; onAdd: (dates: IsoDate[]) => void }) {
  const [range, setRange] = useState<[string | null, string | null]>([null, null])
  return (
    <Group gap={6} wrap="nowrap">
      <DatePickerInput
        type="range"
        placeholder="Pick a date range"
        value={range}
        onChange={v => setRange(v as [string | null, string | null])}
        minDate={period.startDate}
        maxDate={period.endDate}
        defaultDate={period.startDate}
        valueFormat="D MMM"
        numberOfColumns={2}
        w={190}
        clearable
      />
      <Button
        variant="light"
        leftSection={<IconCalendarPlus size={16} />}
        disabled={!range[0] || !range[1]}
        onClick={() => {
          onAdd(eachDay(range[0]!, range[1]!))
          setRange([null, null])
        }}
      >
        Add
      </Button>
    </Group>
  )
}

function TypedDates({ period, onAdd }: { period: Period; onAdd: (dates: IsoDate[]) => void }) {
  const [text, setText] = useState('')
  const [error, setError] = useState<string | null>(null)
  const parse = useMutation({
    mutationFn: () => api.parse(text, period.startDate),
    onSuccess: res => {
      if (res.error) return setError(res.error)
      const inside = res.dates.filter(d => d >= period.startDate && d <= period.endDate)
      if (inside.length < res.dates.length) return setError(`Some dates are outside ${period.name}.`)
      onAdd(inside)
      setText('')
      setError(null)
    },
    onError: e => setError(e instanceof Error ? e.message : String(e)),
  })

  return (
    <Group gap={6} wrap="nowrap" align="flex-start">
      <TextInput
        placeholder="Type dates: 3/10-5/10, 12/10"
        leftSection={<IconKeyboard size={16} />}
        value={text}
        error={error}
        onChange={e => { setText(e.currentTarget.value); setError(null) }}
        onKeyDown={e => { if (e.key === 'Enter' && text.trim()) parse.mutate() }}
        w={250}
      />
      <Button variant="light" disabled={!text.trim()} loading={parse.isPending} onClick={() => parse.mutate()}>Add</Button>
    </Group>
  )
}

/** Leave grouped into consecutive ranges with the same note; notes can be edited per range. */
function Summary({ draft, readOnly, onChange }: { draft: Draft; readOnly: boolean; onChange: (d: Draft) => void }) {
  const leaveRanges = useMemo(() => {
    const out: { from: IsoDate; to: IsoDate; note: string | null }[] = []
    for (const [date, note] of [...draft.leave].sort(([a], [b]) => a.localeCompare(b))) {
      const last = out[out.length - 1]
      if (last && diffDays(last.to, date) === 1 && last.note === note) last.to = date
      else out.push({ from: date, to: date, note })
    }
    return out
  }, [draft.leave])

  const preferredRanges = toRanges(draft.preferred)

  const setNoteFor = (from: IsoDate, to: IsoDate, note: string | null) => {
    const leave = new Map(draft.leave)
    for (const d of eachDay(from, to)) leave.set(d, note)
    onChange({ ...draft, leave })
  }
  const removeLeave = (from: IsoDate, to: IsoDate) => {
    const leave = new Map(draft.leave)
    for (const d of eachDay(from, to)) leave.delete(d)
    onChange({ ...draft, leave })
  }
  const removePreferred = (from: IsoDate, to: IsoDate) => {
    const preferred = new Set(draft.preferred)
    for (const d of eachDay(from, to)) preferred.delete(d)
    onChange({ ...draft, preferred })
  }

  return (
    <SimpleGrid cols={{ base: 1, md: 2 }} spacing="md">
      <Paper withBorder p="md">
        <Group justify="space-between" mb="xs">
          <Title order={5}>Leave</Title>
          <Badge color="red" variant="light">{draft.leave.size} day(s)</Badge>
        </Group>
        {leaveRanges.length === 0 && <Text size="sm" c="dimmed">No leave marked.</Text>}
        <Stack gap={6}>
          {leaveRanges.map(r => (
            <Group key={r.from} justify="space-between" wrap="nowrap" gap="xs">
              <Text size="sm" fw={600} miw={130}>{formatRange([r.from, r.to])}</Text>
              <Select
                size="xs"
                placeholder="No note"
                data={[...new Set([...NOTE_PRESETS, ...(r.note ? [r.note] : [])])]}
                value={r.note}
                onChange={v => setNoteFor(r.from, r.to, v)}
                clearable
                searchable
                disabled={readOnly}
                w={170}
              />
              {!readOnly && (
                <ActionIcon variant="subtle" color="red" aria-label="Remove" onClick={() => removeLeave(r.from, r.to)}>
                  <IconTrash size={16} />
                </ActionIcon>
              )}
            </Group>
          ))}
        </Stack>
      </Paper>
      <Paper withBorder p="md">
        <Group justify="space-between" mb="xs">
          <Title order={5}>Preferred on-call days</Title>
          <Badge variant="light">{draft.preferred.size} day(s)</Badge>
        </Group>
        {preferredRanges.length === 0 && (
          <Text size="sm" c="dimmed">None. Switch to “Preferred on-call” mode to mark days you'd like to work.</Text>
        )}
        <Stack gap={6}>
          {preferredRanges.map(r => (
            <Group key={r[0]} justify="space-between" wrap="nowrap">
              <Text size="sm" fw={600}>{formatRange(r)}</Text>
              {!readOnly && (
                <ActionIcon variant="subtle" color="red" aria-label="Remove" onClick={() => removePreferred(r[0], r[1])}>
                  <IconTrash size={16} />
                </ActionIcon>
              )}
            </Group>
          ))}
        </Stack>
      </Paper>
    </SimpleGrid>
  )
}
