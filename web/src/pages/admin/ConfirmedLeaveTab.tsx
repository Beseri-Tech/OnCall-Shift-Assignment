import {
  ActionIcon, Alert, Badge, Button, Group, List, Modal, Paper, Select, Stack, Table, Text, TextInput, Textarea, Title, Tooltip,
} from '@mantine/core'
import { DatePickerInput } from '@mantine/dates'
import { modals } from '@mantine/modals'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconCheck, IconFileText, IconX, IconAlertTriangle, IconEdit, IconPlaneDeparture, IconPlus, IconTrash, IconUserCheck } from '@tabler/icons-react'
import { Fragment, useState } from 'react'
import {
  api, kindLabel, LEAVE_KINDS, openDocument, type ApprovalConflict, type LeaveKind, type LeaveRequestRange,
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
  const people = useQuery({ queryKey: ['admin', 'people'], queryFn: api.admin.people })
  const [f, setF] = useState<Filters>(NO_FILTERS)
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
  const clinicOf = new Map((people.data ?? []).map(p => [p.id, { id: p.clinicId, name: p.clinicName }]))
  const all = approvals.data ?? []
  const rows = all.filter(a => matches(a, f, today, clinicOf.get(a.personId)?.id ?? null))
    .sort((a, b) => a.startDate.localeCompare(b.startDate) || a.personName.localeCompare(b.personName))
  const officerOptions = [...new Map(all.map(a => [a.personId, `${a.personName} (${a.personCode})`])).entries()]
    .map(([value, label]) => ({ value, label })).sort((x, y) => x.label.localeCompare(y.label))
  const clinicOptions = [...new Map((people.data ?? []).filter(p => p.clinicId && all.some(a => a.personId === p.id))
    .map(p => [p.clinicId!, p.clinicName ?? ''])).entries()].map(([value, label]) => ({ value, label })).sort((x, y) => x.label.localeCompare(y.label))
  // One block per officer (by name), their leave in date order.
  const byPerson = new Map<string, { personId: string; name: string; code: string; items: LeaveApproval[] }>()
  for (const a of rows) {
    const g = byPerson.get(a.personId) ?? { personId: a.personId, name: a.personName, code: a.personCode, items: [] }
    g.items.push(a)
    byPerson.set(a.personId, g)
  }
  const groups = [...byPerson.values()].sort((x, y) => x.name.localeCompare(y.name))
  const filtered = JSON.stringify(f) !== JSON.stringify(NO_FILTERS)

  return (
    <Stack gap="md">
      <RequestsPanel onDecided={refresh} />
      <Paper withBorder p="md">
        <Group justify="space-between" mb="xs" wrap="wrap">
          <Group gap={8}><IconPlaneDeparture size={20} /><Title order={4}>Confirmed leave</Title></Group>
          <Group gap="xs">
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

        <Group gap="xs" mb="sm" align="flex-end" wrap="wrap">
          <Select size="xs" label="Officer" placeholder="All" clearable searchable data={officerOptions} w={200}
            value={f.personId} onChange={v => setF({ ...f, personId: v })} />
          <Select size="xs" label="Clinic" placeholder="All" clearable searchable data={clinicOptions} w={200}
            value={f.clinicId} onChange={v => setF({ ...f, clinicId: v })} />
          <Select size="xs" label="Type" placeholder="All" clearable data={LEAVE_KINDS} w={150}
            value={f.kind} onChange={v => setF({ ...f, kind: v as LeaveKind | null })} />
          <Select size="xs" label="Status" allowDeselect={false} w={170} value={f.when}
            data={[{ value: 'current', label: 'Current and upcoming' }, { value: 'now', label: 'Away now' }, { value: 'past', label: 'Past' }, { value: 'all', label: 'All' }]}
            onChange={v => setF({ ...f, when: (v as Filters['when']) ?? 'current' })} />
          <DatePickerInput size="xs" type="range" label="Dates overlapping" placeholder="Any dates" clearable valueFormat="D MMM YYYY"
            w={220} allowSingleDateInRange value={f.range} onChange={v => setF({ ...f, range: v as [string | null, string | null] })} />
          {filtered && <Button size="xs" variant="subtle" onClick={() => setF(NO_FILTERS)}>Clear filters</Button>}
        </Group>
        <Text size="xs" c="dimmed" mb={6}>
          {rows.length} {rows.length === 1 ? 'entry' : 'entries'} · {new Set(rows.map(r => r.personId)).size} officer(s)
        </Text>

        {rows.length === 0 ? (
          <Text size="sm" c="dimmed">{all.length === 0 ? 'No confirmed leave yet.' : 'Nothing matches these filters.'}</Text>
        ) : (
          <Table.ScrollContainer minWidth={760}>
            <Table highlightOnHover verticalSpacing={6} stickyHeader>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Dates</Table.Th><Table.Th>Days</Table.Th><Table.Th>Type</Table.Th><Table.Th>Reason</Table.Th>
                  <Table.Th>Document</Table.Th><Table.Th>Status</Table.Th><Table.Th />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {groups.map(g => (
                  <Fragment key={g.personId}>
                    <Table.Tr bg="var(--mantine-color-gray-1)">
                      <Table.Td colSpan={7}>
                        <Group gap="xs" align="baseline">
                          <Text fw={700}>{g.name}</Text>
                          <Text size="xs" c="dimmed">{g.code}{clinicOf.get(g.personId)?.name ? ` · ${clinicOf.get(g.personId)!.name}` : ''}</Text>
                          <Badge variant="light" size="sm">{g.items.length} {g.items.length === 1 ? 'entry' : 'entries'}</Badge>
                          <Text size="xs" c="dimmed">{g.items.reduce((n, a) => n + diffDays(a.startDate, a.endDate) + 1, 0)} days</Text>
                        </Group>
                      </Table.Td>
                    </Table.Tr>
                    {g.items.map(a => (
                      <LeaveRow key={a.id} a={a} today={today}
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
                  </Fragment>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        )}
      </Paper>

      {editing && (
        <ApprovalModal editing={editing} onClose={() => setEditing(null)}
          onSaved={r => { setEditing(null); setResult(r); refresh() }} />
      )}
    </Stack>
  )
}

interface Filters {
  personId: string | null; clinicId: string | null; kind: LeaveKind | null
  when: 'current' | 'now' | 'past' | 'all'; range: [string | null, string | null]
}
const NO_FILTERS: Filters = { personId: null, clinicId: null, kind: null, when: 'current', range: [null, null] }

function matches(a: LeaveApproval, f: Filters, today: string, clinicId: string | null) {
  if (f.personId && a.personId !== f.personId) return false
  if (f.clinicId && clinicId !== f.clinicId) return false
  if (f.kind && a.kind !== f.kind) return false
  if (f.when === 'current' && a.endDate < today) return false
  if (f.when === 'now' && !(a.startDate <= today && a.endDate >= today)) return false
  if (f.when === 'past' && a.endDate >= today) return false
  const [from, to] = f.range
  if (from && a.endDate < from) return false
  if (to && a.startDate > to) return false
  return true
}

function LeaveRow({ a, today, onEdit, onDelete }: {
  a: LeaveApproval; today: string; onEdit: () => void; onDelete: () => void
}) {
  const days = diffDays(a.startDate, a.endDate) + 1
  const status = a.endDate < today ? { label: 'Past', color: 'gray' } : a.startDate <= today ? { label: 'Away now', color: 'teal' } : { label: 'Upcoming', color: 'blue' }
  return (
    <>
      <Table.Tr style={a.conflicts.length ? { background: 'var(--mantine-color-red-0)' } : undefined}>
        <Table.Td style={{ whiteSpace: 'nowrap' }}><Text size="sm" fw={600}>{formatRange([a.startDate, a.endDate])}</Text></Table.Td>
        <Table.Td>{days}</Table.Td>
        <Table.Td><Badge variant="light" size="sm">{kindLabel(a.kind)}</Badge></Table.Td>
        <Table.Td maw={280}>
          <Text size="sm">{a.reason}</Text>
          <Text size="xs" c="dimmed">{a.reference ? `Ref. ${a.reference} · ` : ''}Entered by {a.createdBy ?? 'unknown'}, {formatLong(a.createdAt.slice(0, 10))}</Text>
        </Table.Td>
        <Table.Td>
          {a.documentId ? (
            <Tooltip label={a.documentName ?? 'Open document'}>
              <Button size="compact-xs" variant="light" leftSection={<IconFileText size={14} />}
                onClick={() => openDocument(a.documentId!).catch(e => notifyError(e))}>View</Button>
            </Tooltip>
          ) : <Text size="xs" c="dimmed">–</Text>}
        </Table.Td>
        <Table.Td>
          <Badge color={status.color} size="sm" variant="light">{status.label}</Badge>
          {a.conflicts.length > 0 && <Badge color="red" size="sm" ml={4} leftSection={<IconAlertTriangle size={10} />}>On call</Badge>}
        </Table.Td>
        <Table.Td>
          <Group gap={4} wrap="nowrap" justify="flex-end">
            <Tooltip label="Edit"><ActionIcon variant="subtle" aria-label="Edit" onClick={onEdit}><IconEdit size={16} /></ActionIcon></Tooltip>
            <Tooltip label="Remove"><ActionIcon variant="subtle" color="red" aria-label="Remove" onClick={onDelete}><IconTrash size={16} /></ActionIcon></Tooltip>
          </Group>
        </Table.Td>
      </Table.Tr>
      {a.conflicts.length > 0 && (
        <Table.Tr style={{ background: 'var(--mantine-color-red-0)' }}>
          <Table.Td colSpan={7}>
            <Stack gap="xs" pb="xs">
              <Group gap={6} wrap="nowrap" align="flex-start">
                <IconAlertTriangle size={18} color="var(--mantine-color-red-7)" style={{ flexShrink: 0 }} />
                <Text size="sm" fw={600}>
                  {a.personName} is on call on {a.conflicts.length} day{a.conflicts.length === 1 ? '' : 's'} of this leave. Give
                  {a.conflicts.length === 1 ? ' that shift' : ' those shifts'} to someone else:
                </Text>
              </Group>
              {a.conflicts.map(c => <ConflictRow key={`${c.runId}:${c.date}`} c={c} />)}
            </Stack>
          </Table.Td>
        </Table.Tr>
      )}
    </>
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
          {r.documentId && r.documentName && (
            <Text size="xs" c="blue" style={{ cursor: 'pointer' }} onClick={() => openDocument(r.documentId!).catch(e => notifyError(e))}>
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
