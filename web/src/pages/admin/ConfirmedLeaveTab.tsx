import {
  ActionIcon, Alert, Badge, Button, Group, List, Modal, Paper, SegmentedControl, Select, Stack, Text, TextInput, Title, Tooltip,
} from '@mantine/core'
import { DatePickerInput } from '@mantine/dates'
import { modals } from '@mantine/modals'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconAlertTriangle, IconEdit, IconPlaneDeparture, IconPlus, IconTrash, IconUserCheck } from '@tabler/icons-react'
import { useState } from 'react'
import { api, type ApprovalConflict, type LeaveApproval, type LeaveApprovalResult, type UpsertLeaveApproval } from '../../api'
import { ErrorBox, Loading } from '../../components/common'
import { diffDays, formatLong, formatRange, todayIso } from '../../dates'
import { notifyError, notifyOk } from '../../lib'

type Editing = { id: string | null; value: Partial<UpsertLeaveApproval> }

/**
 * Confirmed leave: absences already approved by the department (e.g. an overseas trip). It costs no points, isn't
 * refused when a day is full and the officer can't change it, but it still counts toward a day being full for others.
 */
export function ConfirmedLeaveTab() {
  const qc = useQueryClient()
  const approvals = useQuery({ queryKey: ['admin', 'leaveApprovals'], queryFn: () => api.admin.leaveApprovals() })
  const [show, setShow] = useState<'upcoming' | 'all'>('upcoming')
  const [editing, setEditing] = useState<Editing | null>(null)
  const [result, setResult] = useState<LeaveApprovalResult | null>(null)

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

  return (
    <Stack gap="md">
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
          For leave the department has already approved, such as an overseas trip. It doesn't use the officer's points or
          weekday allowance and is never refused because a day is full. It still counts toward the daily limit for other
          officers. The officer can see it but can't change it. You can enter it before the rota period exists.
        </Text>

        {result && <ResultAlert result={result} onClose={() => setResult(null)} />}

        {rows.length === 0 ? (
          <Text size="sm" c="dimmed">{show === 'upcoming' ? 'No current or upcoming confirmed leave.' : 'No confirmed leave yet.'}</Text>
        ) : (
          <Stack gap="sm">
            {rows.map(a => (
              <Row key={a.id} a={a}
                onEdit={() => {
                  setResult(null)
                  setEditing({ id: a.id, value: { personId: a.personId, startDate: a.startDate, endDate: a.endDate, reason: a.reason, reference: a.reference } })
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
            <Text fw={700} size="sm">{a.personName}</Text>
            <Text size="xs" c="dimmed">{a.personCode}</Text>
          </Stack>
          <Stack gap={0} w={150}>
            <Text size="sm" fw={600}>{formatRange([a.startDate, a.endDate])}</Text>
            <Text size="xs" c="dimmed">{days} day{days === 1 ? '' : 's'}</Text>
          </Stack>
          <Stack gap={0} style={{ flex: 1, minWidth: 200 }}>
            <Text size="sm">{a.reason}</Text>
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

function ResultAlert({ result, onClose }: { result: LeaveApprovalResult; onClose: () => void }) {
  const a = result.approval
  return (
    <Alert mb="md" withCloseButton onClose={onClose} color={result.warnings.length ? 'yellow' : 'teal'}
      icon={result.warnings.length ? <IconAlertTriangle /> : <IconPlaneDeparture />}
      title={`Saved: ${a.personName}, ${formatRange([a.startDate, a.endDate])}`}>
      <Text size="sm">The officer has been told by notification and email.</Text>
      {result.warnings.length > 0 && (
        <List size="sm" mt={6} spacing={4}>
          {result.warnings.map(w => <List.Item key={w}>{w}</List.Item>)}
        </List>
      )}
    </Alert>
  )
}

function ApprovalModal({ editing, onClose, onSaved }: {
  editing: Editing; onClose: () => void; onSaved: (r: LeaveApprovalResult) => void
}) {
  const people = useQuery({ queryKey: ['admin', 'people'], queryFn: api.admin.people })
  const [v, setV] = useState(editing.value)
  const save = useMutation({
    mutationFn: () => {
      const body: UpsertLeaveApproval = {
        personId: v.personId!, startDate: v.startDate!, endDate: v.endDate!, reason: v.reason?.trim() ?? '',
        reference: v.reference?.trim() || null,
      }
      return editing.id ? api.admin.updateLeaveApproval(editing.id, body) : api.admin.createLeaveApproval(body)
    },
    onSuccess: onSaved,
    onError: e => notifyError(e, 'Could not save'),
  })

  const ready = !!v.personId && !!v.startDate && !!v.endDate && !!v.reason?.trim()
  const officers = (people.data ?? []).filter(p => p.status !== 'Left' || p.id === v.personId)
    .map(p => ({ value: p.id, label: `${p.name} (${p.code})${p.status === 'Excluded' ? ' – excluded' : ''}` }))

  return (
    <Modal opened onClose={onClose} title={editing.id ? 'Edit confirmed leave' : 'Add confirmed leave'} size="md">
      <Stack gap="sm">
        <Select label="Officer" placeholder="Pick an officer" searchable required data={officers} value={v.personId ?? null}
          disabled={!!editing.id} onChange={id => setV({ ...v, personId: id ?? undefined })} />
        <DatePickerInput type="range" label="Dates" placeholder="First and last day away" required valueFormat="D MMM YYYY"
          numberOfColumns={2} allowSingleDateInRange
          value={[v.startDate ?? null, v.endDate ?? null]}
          onChange={r => { const [s, e] = r as [string | null, string | null]; setV({ ...v, startDate: s ?? undefined, endDate: e ?? undefined }) }} />
        <TextInput label="Reason" required placeholder="e.g. Overseas trip, approved by HOD" maxLength={200}
          value={v.reason ?? ''} onChange={e => setV({ ...v, reason: e.currentTarget.value })} />
        <TextInput label="Approval reference" description="Optional, e.g. letter or memo number" maxLength={100}
          value={v.reference ?? ''} onChange={e => setV({ ...v, reference: e.currentTarget.value })} />
        <Text size="xs" c="dimmed">
          Any leave the officer already marked on these days becomes confirmed, and preferred on-call days on them are removed.
          The officer gets a notification and an email.
        </Text>
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>Cancel</Button>
          <Button disabled={!ready} loading={save.isPending} onClick={() => save.mutate()}>Save</Button>
        </Group>
      </Stack>
    </Modal>
  )
}
