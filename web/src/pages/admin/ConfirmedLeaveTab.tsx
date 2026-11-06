import {
  ActionIcon, Alert, Badge, Button, Group, List, Modal, Paper, SegmentedControl, Select, Stack, Text, TextInput, Textarea, Title, Tooltip,
} from '@mantine/core'
import { DatePickerInput } from '@mantine/dates'
import { modals } from '@mantine/modals'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconCheck, IconFileText, IconX, IconAlertTriangle, IconEdit, IconPlaneDeparture, IconPlus, IconTrash, IconUserCheck } from '@tabler/icons-react'
import { useState } from 'react'
import {
  api, kindLabel, LEAVE_KINDS, leaveRequestDocumentUrl, type ApprovalConflict, type LeaveKind, type LeaveRequestRange,
  type LeaveApproval, type LeaveApprovalBatchResult, type UpsertLeaveApproval,
} from '../../api'
import { ErrorBox, Loading } from '../../components/common'
import { diffDays, formatLong, formatRange, todayIso } from '../../dates'
import { notifyError, notifyOk, usePhone } from '../../lib'

type Editing = { id: string | null; value: Partial<UpsertLeaveApproval> }

/**
 * Confirmed leave: absences already approved by the department (e.g. an overseas trip). It's never refused and the
 * officer can't change it. It reduces their points and weekday allowance by the share of the period it covers, and it
 * counts toward a day being full for others.
 */
export function ConfirmedLeaveTab() {
  const qc = useQueryClient()
  const approvals = useQuery({ queryKey: ['admin', 'leaveApprovals'], queryFn: () => api.admin.leaveApprovals() })
  const [show, setShow] = useState<'upcoming' | 'all'>('upcoming')
  const [editing, setEditing] = useState<Editing | null>(null)
  const [result, setResult] = useState<LeaveApprovalBatchResult | null>(null)

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ['admin', 'leaveApprovals'] })
    qc.invalidateQueries({ queryKey: ['entries'] })
    qc.invalidateQueries({ queryKey: ['overview'] })
    qc.invalidateQueries({ queryKey: ['leaveRules'] })
  }

  const remove = useMutation({
    mutationFn: (id: string) => api.admin.deleteLeaveApproval(id),
    onSuccess: () => { refresh(); setResult(null); notifyOk('Confirmed leave removed. The officer has been told.', 'Removed') },
    onError: e => notifyError(e),
  })

  if (approvals.isLoading) return <Loading />
  if (approvals.error) return <ErrorBox error={approvals.error} />

  const today = todayIso()
  const rows = (approvals.data ?? []).filter(a => show === 'all' || a.endDate >= today)
    .sort((a, b) => a.startDate.localeCompare(b.startDate))
  // One card per officer (sorted by name) with their leave listed under the name.
  const byPerson = new Map<string, { personId: string; name: string; code: string; items: LeaveApproval[] }>()
  for (const a of rows) {
    const g = byPerson.get(a.personId) ?? { personId: a.personId, name: a.personName, code: a.personCode, items: [] }
    g.items.push(a)
    byPerson.set(a.personId, g)
  }
  const groups = [...byPerson.values()].sort((x, y) => x.name.localeCompare(y.name))

  return (
    <Stack gap="md">
      <RequestsPanel onDecided={refresh} />
      <Paper withBorder p="md">
        <Group justify="space-between" mb="xs" wrap="wrap">
          <Group gap={8}><IconPlaneDeparture size={20} /><Title order={4}>Confirmed leave</Title></Group>
          <Group gap="xs">
            <SegmentedControl size="xs" value={show} onChange={v => setShow(v as 'upcoming' | 'all')}
              data={[{ value: 'upcoming', label: 'Current and upcoming' }, { value: 'all', label: 'All' }]} />
            <Button leftSection={<IconPlus size={16} />} onClick={() => { setResult(null); setEditing({ id: null, value: {} }) }}>
              Add confirmed leave
            </Button>
          </Group>
        </Group>
        <Text size="sm" c="dimmed" mb="sm">
          For leave the department has already approved, such as an overseas trip. It's never refused, even when the officer
          is out of points or the day is full. It reduces the officer's points and weekday allowance by 33% of what its days would
          have cost as ordinary leave (weekend and peak days 1 point, public holidays 2, each weekday 1 weekday), rounded up,
          and it counts toward the daily limit for other officers. The officer can see it but can't change it. You can enter it before the rota period exists.
        </Text>

        {result && <ResultAlert result={result} onClose={() => setResult(null)} />}

        {rows.length === 0 ? (
          <Text size="sm" c="dimmed">{show === 'upcoming' ? 'No current or upcoming confirmed leave.' : 'No confirmed leave yet.'}</Text>
        ) : (
          <Stack gap="sm">
            {groups.map(g => (
              <Paper key={g.personId} withBorder p="sm" radius="md">
                <Group gap="xs" mb="xs" align="baseline">
                  <Text fw={700}>{g.name}</Text>
                  <Text size="xs" c="dimmed">{g.code}</Text>
                  <Badge variant="light" size="sm">{g.items.length} {g.items.length === 1 ? 'entry' : 'entries'}</Badge>
                </Group>
                <Stack gap="xs">
                  {g.items.map(a => (
              <Row key={a.id} a={a}
                onEdit={() => {
                  setResult(null)
                  setEditing({ id: a.id, value: { personId: a.personId, startDate: a.startDate, endDate: a.endDate, reason: a.reason, reference: a.reference, kind: a.kind } })
                }}
                onDelete={() => modals.openConfirmModal({
                  title: 'Remove confirmed leave?',
                  children: (
                    <Text size="sm">
                      {a.personName} will no longer be on leave on {formatRange([a.startDate, a.endDate])} and may be put on call on
                      those days. They'll get a notification and an email.
                    </Text>
                  ),
                  labels: { confirm: 'Remove', cancel: 'Cancel' },
                  confirmProps: { color: 'red' },
                  onConfirm: () => remove.mutate(a.id),
                })}
              />
                  ))}
                </Stack>
              </Paper>
            ))}
          </Stack>
        )}
      </Paper>

      {editing && (
        <ApprovalModal editing={editing} onClose={() => setEditing(null)}
          onSaved={r => { setEditing(null); setResult(r); refresh() }} />
      )}
    </Stack>
  )
}

function Row({ a, onEdit, onDelete }: { a: LeaveApproval; onEdit: () => void; onDelete: () => void }) {
  const days = diffDays(a.startDate, a.endDate) + 1
  return (
    <Paper withBorder p="sm" radius="md" style={a.conflicts.length ? { borderColor: 'var(--mantine-color-red-4)' } : undefined}>
      <Group justify="space-between" align="flex-start" wrap="nowrap" gap="sm">
        <Group gap="xl" align="flex-start" wrap="wrap" style={{ flex: 1, minWidth: 0, rowGap: 6 }}>
          <Stack gap={0} w={150}>
            <Text size="sm" fw={600}>{formatRange([a.startDate, a.endDate])}</Text>
            <Text size="xs" c="dimmed">{days} day{days === 1 ? '' : 's'}</Text>
          </Stack>
          <Stack gap={0} style={{ flex: 1, minWidth: 200 }}>
            <Group gap={6}><Badge variant="light" size="sm">{kindLabel(a.kind)}</Badge><Text size="sm">{a.reason}</Text></Group>
            <Text size="xs" c="dimmed">
              {a.reference ? `Ref. ${a.reference} · ` : ''}Entered by {a.createdBy ?? 'unknown'}, {formatLong(a.createdAt.slice(0, 10))}
            </Text>
          </Stack>
        </Group>
        <Group gap={4} wrap="nowrap">
          <Tooltip label="Edit"><ActionIcon variant="subtle" aria-label="Edit" onClick={onEdit}><IconEdit size={16} /></ActionIcon></Tooltip>
          <Tooltip label="Remove"><ActionIcon variant="subtle" color="red" aria-label="Remove" onClick={onDelete}><IconTrash size={16} /></ActionIcon></Tooltip>
        </Group>
      </Group>
      {a.conflicts.length > 0 && (
        <Stack gap="xs" mt="sm" p="sm" bg="var(--mantine-color-red-0)" style={{ borderRadius: 8 }}>
          <Group gap={6} wrap="nowrap" align="flex-start">
            <IconAlertTriangle size={18} color="var(--mantine-color-red-7)" style={{ flexShrink: 0 }} />
            <Text size="sm" fw={600}>
              {a.personName} is on call on {a.conflicts.length} day{a.conflicts.length === 1 ? '' : 's'} of this leave. Give
              {a.conflicts.length === 1 ? ' that shift' : ' those shifts'} to someone else:
            </Text>
          </Group>
          {a.conflicts.map(c => <ConflictRow key={`${c.runId}:${c.date}`} c={c} />)}
        </Stack>
      )}
    </Paper>
  )
}

/** One shift on confirmed leave, with who could take it (free that day and the days around it, fewest shifts first). */
function ConflictRow({ c }: { c: ApprovalConflict }) {
  const qc = useQueryClient()
  const [to, setTo] = useState<string | null>(c.candidates[0]?.personId ?? null)
  const give = useMutation({
    mutationFn: () => api.admin.override(c.runId, c.date, to),
    onSuccess: res => {
      qc.invalidateQueries({ queryKey: ['admin', 'leaveApprovals'] })
      qc.invalidateQueries({ queryKey: ['admin', 'run', c.runId] })
      notifyOk(res.warnings.length ? res.warnings.join(' ') : `${formatLong(c.date)} is now ${res.day.personName}'s.`, 'Shift moved')
    },
    onError: e => notifyError(e, 'Could not move the shift'),
  })

  const options = c.candidates.map(p => ({ value: p.personId, label: `${p.name} (${p.shifts} shift${p.shifts === 1 ? '' : 's'} this rota)` }))
  return (
    <Group gap="sm" wrap="wrap">
      <Text size="sm" fw={600} miw={150}>{formatLong(c.date)}</Text>
      <Badge variant="light" color={c.isPublished ? 'brand' : 'yellow'}>{c.periodName} · {c.isPublished ? 'published' : 'in review'}</Badge>
      {c.isPublished ? (
        <Text size="sm" c="dimmed">
          Unpublish the rota on the Generate rota tab to change this day.
          {c.candidates.length > 0 && <> Free that day: {c.candidates.slice(0, 4).map(p => p.name).join(', ')}.</>}
        </Text>
      ) : c.candidates.length === 0 ? (
        <Text size="sm" c="dimmed">No one is free without a back-to-back shift. Change it on the Generate rota tab.</Text>
      ) : (
        <>
          <Select size="xs" w={300} maw="100%" data={options} value={to} onChange={setTo} allowDeselect={false} aria-label="Give the shift to" />
          <Button size="xs" leftSection={<IconUserCheck size={14} />} loading={give.isPending} disabled={!to} onClick={() => give.mutate()}>
            Give shift
          </Button>
        </>
      )}
    </Group>
  )
}

function ResultAlert({ result, onClose }: { result: LeaveApprovalBatchResult; onClose: () => void }) {
  const first = result.approvals[0]
  return (
    <Alert mb="md" withCloseButton onClose={onClose} color={result.warnings.length ? 'yellow' : 'teal'}
      icon={result.warnings.length ? <IconAlertTriangle /> : <IconPlaneDeparture />}
      title={`Saved: ${first.personName}, ${result.approvals.map(a => formatRange([a.startDate, a.endDate])).join('; ')}`}>
      <Text size="sm">The officer has been told by one notification and email.</Text>
      {result.warnings.length > 0 && (
        <List size="sm" mt={6} spacing={4}>
          {result.warnings.map(w => <List.Item key={w}>{w}</List.Item>)}
        </List>
      )}
    </Alert>
  )
}

function ApprovalModal({ editing, onClose, onSaved }: {
  editing: Editing; onClose: () => void; onSaved: (r: LeaveApprovalBatchResult) => void
}) {
  const phone = usePhone()
  const people = useQuery({ queryKey: ['admin', 'people'], queryFn: api.admin.people })
  const [v, setV] = useState(editing.value)
  // New leave can have several date ranges (saved together, one email); editing changes a single range.
  const [ranges, setRanges] = useState<[string | null, string | null][]>([[null, null]])
  const save = useMutation({
    mutationFn: async (): Promise<LeaveApprovalBatchResult> => {
      const reason = v.reason?.trim() ?? '', reference = v.reference?.trim() || null
      if (editing.id) {
        const body: UpsertLeaveApproval = { personId: v.personId!, startDate: v.startDate!, endDate: v.endDate!, reason, reference, kind: v.kind }
        const r = await api.admin.updateLeaveApproval(editing.id, body)
        return { approvals: [r.approval], warnings: r.warnings }
      }
      return api.admin.createLeaveApprovals({
        personId: v.personId!, reason, reference, kind: v.kind,
        ranges: ranges.filter(([s, e]) => s && e).map(([s, e]) => ({ startDate: s!, endDate: e! })),
      })
    },
    onSuccess: onSaved,
    onError: e => notifyError(e, 'Could not save'),
  })

  const datesReady = editing.id
    ? !!v.startDate && !!v.endDate
    : ranges.every(([s, e]) => s && e)
  const ready = !!v.personId && datesReady && !!v.reason?.trim()
  const officers = (people.data ?? []).filter(p => p.status !== 'Left' || p.id === v.personId)
    .map(p => ({ value: p.id, label: `${p.name} (${p.code})${p.status === 'Excluded' ? ' – excluded' : ''}` }))

  return (
    <Modal opened onClose={onClose} title={editing.id ? 'Edit confirmed leave' : 'Add confirmed leave'} size="md" fullScreen={phone}>
      <Stack gap="sm">
        <Select label="Officer" placeholder="Pick an officer" searchable required data={officers} value={v.personId ?? null}
          disabled={!!editing.id} onChange={id => setV({ ...v, personId: id ?? undefined })} />
        {editing.id ? (
          <DatePickerInput type="range" label="Dates" placeholder="First and last day away" required valueFormat="D MMM YYYY"
            numberOfColumns={phone ? 1 : 2} allowSingleDateInRange
            value={[v.startDate ?? null, v.endDate ?? null]}
            onChange={r => { const [s, e] = r as [string | null, string | null]; setV({ ...v, startDate: s ?? undefined, endDate: e ?? undefined }) }} />
        ) : (
          <Stack gap="xs">
            {ranges.map((r, i) => (
              <Group key={i} gap="xs" align="flex-end" wrap="nowrap">
                <DatePickerInput type="range" style={{ flex: 1 }} label={ranges.length > 1 ? `Dates ${i + 1}` : 'Dates'}
                  placeholder="First and last day away" required valueFormat="D MMM YYYY"
                  numberOfColumns={phone ? 1 : 2} allowSingleDateInRange value={r}
                  onChange={val => setRanges(ranges.map((x, j) => (j === i ? (val as [string | null, string | null]) : x)))} />
                {ranges.length > 1 && (
                  <Tooltip label="Remove this range">
                    <ActionIcon variant="subtle" color="red" mb={4} aria-label="Remove this range"
                      onClick={() => setRanges(ranges.filter((_, j) => j !== i))}><IconTrash size={16} /></ActionIcon>
                  </Tooltip>
                )}
              </Group>
            ))}
            <Button variant="subtle" size="xs" leftSection={<IconPlus size={14} />} style={{ alignSelf: 'flex-start' }}
              onClick={() => setRanges([...ranges, [null, null]])}>
              Add another range
            </Button>
          </Stack>
        )}
        <Select label="Type" description="Relief duty keeps the officer off call without taking points" allowDeselect={false}
          data={LEAVE_KINDS} value={v.kind ?? 'Trip'} onChange={k => setV({ ...v, kind: (k as LeaveKind) ?? 'Trip' })} />
        <TextInput label="Reason" required placeholder="e.g. Overseas trip, approved by HOD" maxLength={200}
          value={v.reason ?? ''} onChange={e => setV({ ...v, reason: e.currentTarget.value })} />
        <TextInput label="Approval reference" description="Optional, e.g. letter or memo number" maxLength={100}
          value={v.reference ?? ''} onChange={e => setV({ ...v, reference: e.currentTarget.value })} />
        <Text size="xs" c="dimmed">
          Any leave the officer already marked on these days becomes confirmed, and preferred on-call days on them are removed.
          The officer gets one notification and email, even with several ranges.
        </Text>
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>Cancel</Button>
          <Button disabled={!ready} loading={save.isPending} onClick={() => save.mutate()}>Save</Button>
        </Group>
      </Stack>
    </Modal>
  )
}

/** Requests officers sent: approve (creates the confirmed leave) or reject each date range. */
function RequestsPanel({ onDecided }: { onDecided: () => void }) {
  const qc = useQueryClient()
  const list = useQuery({ queryKey: ['admin', 'leaveRequests'], queryFn: () => api.admin.leaveRequests() })
  const [rejecting, setRejecting] = useState<LeaveRequestRange | null>(null)
  const [note, setNote] = useState('')
  const [warnings, setWarnings] = useState<string[]>([])
  const [showAll, setShowAll] = useState(false)

  const done = (w: string[]) => {
    setWarnings(w); setRejecting(null); setNote('')
    qc.invalidateQueries({ queryKey: ['admin', 'leaveRequests'] })
    onDecided()
  }
  const approve = useMutation({
    mutationFn: (id: string) => api.admin.approveLeaveRequest(id),
    onSuccess: r => { done(r.warnings); notifyOk('Added to confirmed leave. The officer has been told.', 'Approved') },
    onError: e => notifyError(e, 'Could not approve'),
  })
  const reject = useMutation({
    mutationFn: () => api.admin.rejectLeaveRequest(rejecting!.id, note.trim()),
    onSuccess: () => { done([]); notifyOk('The officer has been told.', 'Rejected') },
    onError: e => notifyError(e, 'Could not reject'),
  })

  if (list.isLoading) return null
  if (list.error) return <ErrorBox error={list.error} />
  const all = list.data ?? []
  const pending = all.filter(r => r.status === 'Pending')
  const decided = all.filter(r => r.status !== 'Pending')

  return (
    <Paper withBorder p="md">
      <Group justify="space-between" mb="xs">
        <Group gap={8}><IconUserCheck size={20} /><Title order={4}>Leave requests</Title>
          {pending.length > 0 && <Badge color="yellow">{pending.length} waiting</Badge>}</Group>
        {decided.length > 0 && <Button size="xs" variant="subtle" onClick={() => setShowAll(!showAll)}>{showAll ? 'Hide decided' : `Show decided (${decided.length})`}</Button>}
      </Group>
      {warnings.length > 0 && (
        <Alert color="yellow" icon={<IconAlertTriangle />} mb="sm" withCloseButton onClose={() => setWarnings([])}>
          <List size="sm" spacing={4}>{warnings.map(w => <List.Item key={w}>{w}</List.Item>)}</List>
        </Alert>
      )}
      {pending.length === 0 ? <Text size="sm" c="dimmed">No requests waiting.</Text> : (
        <Stack gap="xs">
          {pending.map(r => (
            <RequestRow key={r.id} r={r}>
              <Button size="xs" color="teal" leftSection={<IconCheck size={14} />} loading={approve.isPending && approve.variables === r.id}
                onClick={() => approve.mutate(r.id)}>Approve</Button>
              <Button size="xs" color="red" variant="light" leftSection={<IconX size={14} />} onClick={() => { setNote(''); setRejecting(r) }}>Reject</Button>
            </RequestRow>
          ))}
        </Stack>
      )}
      {showAll && decided.length > 0 && (
        <Stack gap="xs" mt="md">
          {decided.map(r => <RequestRow key={r.id} r={r} />)}
        </Stack>
      )}
      <Modal opened={!!rejecting} onClose={() => setRejecting(null)} title="Reject this request">
        <Stack gap="sm">
          <Text size="sm">{rejecting?.personName}, {rejecting && formatRange([rejecting.startDate, rejecting.endDate])}</Text>
          <Textarea label="Reason for the officer" required maxLength={200} autosize minRows={2} value={note} onChange={e => setNote(e.currentTarget.value)} />
          <Group justify="flex-end">
            <Button variant="default" onClick={() => setRejecting(null)}>Cancel</Button>
            <Button color="red" disabled={!note.trim()} loading={reject.isPending} onClick={() => reject.mutate()}>Reject</Button>
          </Group>
        </Stack>
      </Modal>
    </Paper>
  )
}

function RequestRow({ r, children }: { r: LeaveRequestRange; children?: React.ReactNode }) {
  const days = diffDays(r.startDate, r.endDate) + 1
  const color = { Pending: 'yellow', Approved: 'teal', Rejected: 'red', Cancelled: 'gray' }[r.status]
  return (
    <Paper withBorder p="sm" radius="md">
      <Group justify="space-between" align="flex-start" gap="sm">
        <Stack gap={2} style={{ flex: 1, minWidth: 200 }}>
          <Group gap="xs">
            <Text size="sm" fw={700}>{r.personName}</Text>
            <Text size="sm" fw={600}>{formatRange([r.startDate, r.endDate])}</Text>
            <Text size="xs" c="dimmed">{days} day{days === 1 ? '' : 's'}</Text>
            <Badge variant="light" size="sm">{kindLabel(r.kind)}</Badge>
            {r.status !== 'Pending' && <Badge color={color} size="sm">{r.status}</Badge>}
          </Group>
          <Text size="sm">{r.reason}</Text>
          {r.documentName && (
            <Text size="xs" component="a" href={leaveRequestDocumentUrl(r.id)} target="_blank" rel="noopener noreferrer">
              <IconFileText size={12} style={{ verticalAlign: -1 }} /> {r.documentName}
            </Text>
          )}
          <Text size="xs" c="dimmed">
            Sent {formatLong(r.submittedAt.slice(0, 10))}
            {r.decidedAt && <> · {r.status.toLowerCase()} {formatLong(r.decidedAt.slice(0, 10))}{r.decidedBy ? ` by ${r.decidedBy}` : ''}{r.decisionNote ? `: ${r.decisionNote}` : ''}</>}
          </Text>
          {r.overlaps.map(o => <Text key={o} size="xs" c="red">{o} Approving will fail until you change that one.</Text>)}
        </Stack>
        {children && <Group gap="xs">{children}</Group>}
      </Group>
    </Paper>
  )
}
