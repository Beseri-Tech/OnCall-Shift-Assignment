import {
  ActionIcon, Alert, Badge, Button, Chip, Divider, Group, Paper, Progress, SegmentedControl, Select, SimpleGrid, Stack, Text,
  TextInput, Title, Tooltip,
} from '@mantine/core'
import { DatePickerInput } from '@mantine/dates'
import { modals } from '@mantine/modals'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  IconBeach, IconBriefcase, IconCalendarPlus, IconCalendarTime, IconCoins, IconDeviceFloppy, IconEraser, IconInfoCircle, IconKeyboard,
  IconLock, IconStar, IconSum, IconTrash,
} from '@tabler/icons-react'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { api, type Entries, type IsoDate, type Period } from '../api'
import { isAdmin, useMe } from '../auth'
import { ErrorBox, Legend, Loading, PageHeader, PeriodSelect, StatCard } from '../components/common'
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
  const me = useMe().data
  const admin = isAdmin(me)
  const [chosenPersonId, setPersonId] = useState<string | null>(null)
  const [chosenPeriodId, setPeriodId] = useState<string | null>(null)

  // Only admins pick someone; officers always see their own leave.
  const people = useQuery({ queryKey: ['people'], queryFn: api.people, enabled: admin })
  const periods = useQuery({ queryKey: ['periods'], queryFn: () => api.periods() })

  const visiblePeriods = useMemo(() => (periods.data ?? []).filter(p => p.status !== 'Published'), [periods.data])
  const period = pick(visiblePeriods, chosenPeriodId, defaultPeriod(visiblePeriods))
  const periodId = period?.id ?? null
  const personId = admin ? chosenPersonId ?? me?.personId ?? null : me?.personId ?? null
  const editingOther = admin && personId !== null && personId !== me?.personId

  if (people.isLoading || periods.isLoading) return <Loading />
  if (people.error) return <ErrorBox error={people.error} />
  if (periods.error) return <ErrorBox error={periods.error} />

  return (
    <Stack gap="lg">
      <PageHeader icon={IconBeach} title={editingOther || !me?.personId ? 'Leave' : 'My leave'}
        description="Mark leave and preferred on-call days on the calendar.">
          {admin && (
            <Select
              label="Officer"
              placeholder="Pick an officer"
              searchable
              w={340}
              data={(people.data ?? []).map(p => ({ value: p.id, label: `${p.name} (${p.code})${p.id === me?.personId ? ' – you' : ''}` }))}
              value={personId}
              onChange={setPersonId}
              allowDeselect={false}
            />
          )}
          {visiblePeriods.length > 1 && <PeriodSelect periods={visiblePeriods} value={periodId} onChange={setPeriodId} />}
      </PageHeader>

      {editingOther && (
        <Alert color="violet" icon={<IconInfoCircle />} title="Editing on someone's behalf">
          As an admin, the leave limits and the deadline don't apply to changes you make here.
        </Alert>
      )}

      {!period && (
        <Alert icon={<IconInfoCircle />} title="No rota period is open for leave">
          The admin hasn't opened the next period yet. Check the Rota page for published rotas.
        </Alert>
      )}

      {!personId && period && (
        <Alert icon={<IconInfoCircle />} title="Pick an officer">
          Choose an officer above to see and edit their leave for {period.name}.
        </Alert>
      )}

      {personId && period && (
        <PersonLeave key={`${personId}:${period.id}`} personId={personId} period={period} byAdmin={admin} />
      )}
    </Stack>
  )
}

function PersonLeave({ personId, period, byAdmin }: { personId: string; period: Period; byAdmin: boolean }) {
  const entries = useQuery({ queryKey: ['entries', personId, period.id], queryFn: () => api.entries(personId, period.id) })

  if (entries.isLoading) return <Loading />
  if (entries.error) return <ErrorBox error={entries.error} />
  // The editor owns the draft from here on; it starts from what was loaded.
  return <LeaveEditor personId={personId} period={period} initial={entries.data!} byAdmin={byAdmin} />
}

/** byAdmin: an admin editing (anyone's) leave – no limits and no deadline, like the server. */
function LeaveEditor({ personId, period, initial, byAdmin }: { personId: string; period: Period; initial: Entries; byAdmin: boolean }) {
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
    if (!r || byAdmin) return new Set<IsoDate>()
    return new Set(eachDay(period.startDate, period.endDate)
      .filter(d => !saved.leave.has(d) && (r.othersOff[d] ?? 0) >= dayCap(kindOf(d), r)))
  }, [rules.data, byAdmin, period.startDate, period.endDate, saved.leave, kindOf])
  const [mode, setMode] = useState<Mode>('leave')
  const [note, setNote] = useState<string | null>('Annual')
  const [customNote, setCustomNote] = useState('')

  const dirty = !sameDraft(draft, saved)
  const readOnly = !period.isEditable && !byAdmin
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
  const showBudget = !readOnly && !!r && !!used
  const overPoints = !byAdmin && !!used && !!was && used.points > budget && used.points > was.points
  const overWeekdays = !byAdmin && !!used && !!was && !!r && used.weekdays > r.weekdayAllowance && used.weekdays > was.weekdays
  const limitError = overPoints
    ? `Not enough points (${used!.points} needed, ${budget} available). Ask the admin for extra points.`
    : overWeekdays ? `Too many weekdays (${used!.weekdays} marked, at most ${r!.weekdayAllowance}).` : null

  return (
    <Stack gap="md" pb={dirty ? 80 : 0}>
      <SimpleGrid cols={{ base: 1, sm: 2, lg: showBudget ? 4 : 2 }} spacing="md">
        <StatCard icon={readOnly ? IconLock : IconCalendarTime} color={readOnly ? 'red' : 'green'} label={period.name}>
          <Text fw={700} mt={2}>{formatLong(period.startDate)} – {formatLong(period.endDate)}</Text>
          <Text size="sm" c={readOnly ? 'red.8' : 'green.8'} fw={600}>
            {readOnly
              ? period.status === 'Open' ? 'Deadline passed – leave is closed' : 'Locked – leave is closed'
              : period.leaveDeadline ? `Edit until ${formatLong(period.leaveDeadline)}` : 'Open for leave'}
          </Text>
        </StatCard>
        {showBudget && r && used && (
          <>
            <StatCard icon={IconCoins} color={overPoints ? 'red' : 'brand'} label="Leave points" value={`${used.points} / ${budget}`}
              hint={r.extra > 0 ? `Includes +${r.extra} extra${r.extraReason ? `: ${r.extraReason}` : ''}` : 'Weekends, public holidays, peak days'}>
              <Progress mt={6} size="sm" radius="xl" color={overPoints ? 'red' : 'brand'} value={budget ? Math.min(100, (used.points / budget) * 100) : 100} />
            </StatCard>
            <StatCard icon={IconBriefcase} color={overWeekdays ? 'red' : 'indigo'} label="Weekdays" value={`${used.weekdays} / ${r.weekdayAllowance}`}
              hint="Free, no points">
              <Progress mt={6} size="sm" radius="xl" color={overWeekdays ? 'red' : 'indigo'}
                value={r.weekdayAllowance ? Math.min(100, (used.weekdays / r.weekdayAllowance) * 100) : 100} />
            </StatCard>
          </>
        )}
        <StatCard icon={IconSum} color="orange" label="On-call shifts done" value={totals?.total ?? '–'}
          hint={totals ? `${totals.weekday} weekday · ${totals.weekendHoliday} weekend + PH` : 'No history yet – counted from the first published rota.'} />
      </SimpleGrid>

      {readOnly && (
        <Alert color="orange" icon={<IconLock />} title="Read only">
          Leave for this period can no longer be changed here. Contact the admin if something is wrong.
        </Alert>
      )}

      <Paper className="panel" p="md">
        {!readOnly && (
          <>
            <Stack gap="sm">
              <Group justify="space-between" wrap="wrap" gap="sm">
                <SegmentedControl
                  value={mode}
                  onChange={v => setMode(v as Mode)}
                  color={mode === 'leave' ? 'pink' : 'indigo'}
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
                  <Text size="sm" fw={700}>Note for new leave:</Text>
                  <Chip.Group value={note ?? ''} onChange={v => setNote(typeof v === 'string' && v ? v : null)}>
                    <Group gap={6}>
                      {[...NOTE_PRESETS, 'Other'].map(n => <Chip key={n} value={n} size="sm" color="pink">{n}</Chip>)}
                      <Chip value="" size="sm" color="pink">None</Chip>
                    </Group>
                  </Chip.Group>
                  {note === 'Other' && (
                    <TextInput size="xs" placeholder="e.g. luar negara" value={customNote} maxLength={60}
                      onChange={e => setCustomNote(e.currentTarget.value)} w={180} />
                  )}
                </Group>
              )}
              <Group gap={6} wrap="nowrap" align="flex-start">
                <IconInfoCircle size={16} color="var(--brand)" style={{ flexShrink: 0, marginTop: 2 }} />
                <Text size="xs" c="dimmed">
                  Click a day to toggle it, or press and drag across days. <b>{mode === 'leave' ? 'Leave' : 'Preferred'}</b> mode is on.
                  {showBudget && r && <> Leave on a weekend costs {r.weekendCost} point, a public holiday {r.holidayCost}, a peak day {r.peakCost}.
                    At most {r.busyDayCap} of {r.onCall} officers can be off on a weekend, public holiday or peak day, and {r.weekdayCap} on
                    other days – striped days are full. Need more? Ask the admin.</>}
                </Text>
              </Group>
            </Stack>
            <Divider my="md" />
          </>
        )}

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
        <Group gap="lg" mt="md" wrap="wrap">
          <Legend bg="var(--leave-bg)" border="var(--leave-border)" label="Leave" />
          <Legend bg="var(--pref-bg)" border="var(--pref-border)" label="Preferred on-call" />
          <Legend bg="var(--weekend-bg)" border="#cbd5e1" label="Weekend" />
          <Text size="xs" c="var(--holiday-text)" fw={700}>PH = public holiday</Text>
          <Text size="xs" c="var(--peak-text)" fw={700}>PEAK = peak day</Text>
        </Group>
      </Paper>

      <Summary draft={draft} readOnly={readOnly} onChange={setDraft} />

      {dirty && !readOnly && (
        <Paper shadow="xl" p="sm" px="md" withBorder pos="fixed" bottom={16} left="50%" radius="xl"
          style={{ transform: 'translateX(-50%)', zIndex: 200, borderColor: limitError ? 'var(--leave-border)' : 'var(--shift-border)' }}>
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
      <Paper className="panel" p="md" style={{ borderTop: '4px solid var(--leave-border)' }}>
        <Group justify="space-between" mb="xs">
          <Group gap={6}><IconBeach size={18} color="var(--leave-text)" /><Title order={5}>Leave</Title></Group>
          <Badge color="pink" variant="light">{draft.leave.size} day(s)</Badge>
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
      <Paper className="panel" p="md" style={{ borderTop: '4px solid var(--pref-border)' }}>
        <Group justify="space-between" mb="xs">
          <Group gap={6}><IconStar size={18} color="var(--pref-text)" /><Title order={5}>Preferred on-call days</Title></Group>
          <Badge color="indigo" variant="light">{draft.preferred.size} day(s)</Badge>
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
