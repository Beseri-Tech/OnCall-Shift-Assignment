import { ActionIcon, Alert, Badge, Button, FileInput, Group, Paper, Select, Stack, Text, Textarea, Title, Tooltip } from '@mantine/core'
import { DatePickerInput } from '@mantine/dates'
import { modals } from '@mantine/modals'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconFileText, IconPaperclip, IconPlaneDeparture, IconPlus, IconSend, IconTrash } from '@tabler/icons-react'
import { useState } from 'react'
import { api, kindLabel, LEAVE_KINDS, leaveRequestDocumentUrl, type LeaveKind, type LeaveRequestRange, type LeaveRequestStatus } from '../api'
import { ErrorBox, Loading, PageHeader } from '../components/common'
import { diffDays, formatLong, formatRange } from '../dates'
import { notifyError, notifyOk, usePhone } from '../lib'

const MAX_BYTES = 5 * 1024 * 1024

interface Draft { dates: [string | null, string | null]; kind: LeaveKind; reason: string; file: File | null }
const blank = (): Draft => ({ dates: [null, null], kind: 'Trip', reason: '', file: null })

const STATUS_COLOR: Record<LeaveRequestStatus, string> = { Pending: 'yellow', Approved: 'teal', Rejected: 'red', Cancelled: 'gray' }

/** Officers ask for confirmed leave (trip, CR, relief duty): several date ranges, a reason and an optional document for each. */
export function LeaveRequestsPage() {
  const qc = useQueryClient()
  const phone = usePhone()
  const mine = useQuery({ queryKey: ['leaveRequests', 'mine'], queryFn: api.myLeaveRequests })
  const [drafts, setDrafts] = useState<Draft[]>([blank()])

  const send = useMutation({
    mutationFn: () => api.sendLeaveRequest(drafts.map(d => ({ startDate: d.dates[0]!, endDate: d.dates[1]!, kind: d.kind, reason: d.reason.trim(), file: d.file }))),
    onSuccess: () => {
      setDrafts([blank()])
      qc.invalidateQueries({ queryKey: ['leaveRequests'] })
      notifyOk('The admin has been told. You will get a notification when they decide.', 'Request sent')
    },
    onError: e => notifyError(e, 'Could not send the request'),
  })
  const cancel = useMutation({
    mutationFn: (id: string) => api.cancelLeaveRequest(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['leaveRequests'] }); notifyOk('Request withdrawn.') },
    onError: e => notifyError(e),
  })

  const set = (i: number, patch: Partial<Draft>) => setDrafts(drafts.map((d, j) => (j === i ? { ...d, ...patch } : d)))
  const ready = drafts.every(d => d.dates[0] && d.dates[1] && d.reason.trim())

  return (
    <Stack gap="md">
      <PageHeader icon={IconPlaneDeparture} title="Request confirmed leave"
        description="For leave the department has approved or assigned: a trip, your CR days, or relief duty at another clinic." />

      <Paper withBorder p="md">
        <Text size="sm" c="dimmed" mb="sm">
          Add each period you are away as its own date range with its reason. A document (PDF, JPG or PNG, up to 5 MB) is optional.
          Include your CR (cuti rehat) days, since you can't be on call on your official off day. The admin approves or rejects each
          range; once approved you won't be put on call on those days.
        </Text>
        <Stack gap="md">
          {drafts.map((d, i) => (
            <Paper key={i} withBorder p="sm" radius="md">
              <Stack gap="xs">
                <Group justify="space-between" wrap="nowrap">
                  <Text fw={600} size="sm">{drafts.length > 1 ? `Date range ${i + 1}` : 'Date range'}</Text>
                  {drafts.length > 1 && (
                    <Tooltip label="Remove this range">
                      <ActionIcon variant="subtle" color="red" aria-label="Remove this range"
                        onClick={() => setDrafts(drafts.filter((_, j) => j !== i))}><IconTrash size={16} /></ActionIcon>
                    </Tooltip>
                  )}
                </Group>
                <DatePickerInput type="range" label="Dates" placeholder="First and last day" required valueFormat="D MMM YYYY"
                  numberOfColumns={phone ? 1 : 2} allowSingleDateInRange value={d.dates}
                  onChange={v => set(i, { dates: v as [string | null, string | null] })} />
                <Select label="Type" required allowDeselect={false} data={LEAVE_KINDS} value={d.kind}
                  onChange={v => set(i, { kind: (v as LeaveKind) ?? 'Trip' })} />
                <Textarea label="Reason" required autosize minRows={1} maxRows={3} maxLength={200}
                  placeholder="e.g. Umrah, approved by HOD / Relief at Klinik UTC on Saturday"
                  value={d.reason} onChange={e => set(i, { reason: e.currentTarget.value })} />
                <FileInput label="Document (optional)" placeholder="PDF, JPG or PNG, up to 5 MB" clearable
                  leftSection={<IconPaperclip size={16} />} accept="application/pdf,image/jpeg,image/png"
                  value={d.file}
                  error={d.file && d.file.size > MAX_BYTES ? 'This file is over 5 MB.' : undefined}
                  onChange={f => set(i, { file: f })} />
              </Stack>
            </Paper>
          ))}
          <Group justify="space-between">
            <Button variant="subtle" leftSection={<IconPlus size={16} />} disabled={drafts.length >= 20}
              onClick={() => setDrafts([...drafts, blank()])}>Add another date range</Button>
            <Button leftSection={<IconSend size={16} />} loading={send.isPending}
              disabled={!ready || drafts.some(d => (d.file?.size ?? 0) > MAX_BYTES)} onClick={() => send.mutate()}>
              Send request
            </Button>
          </Group>
        </Stack>
      </Paper>

      <Paper withBorder p="md">
        <Title order={4} mb="xs">My requests</Title>
        {mine.isLoading ? <Loading /> : mine.error ? <ErrorBox error={mine.error} /> : (mine.data ?? []).length === 0 ? (
          <Text size="sm" c="dimmed">You haven't sent any requests yet.</Text>
        ) : (
          <Stack gap="xs">
            {mine.data!.map(r => (
              <MyRow key={r.id} r={r} onCancel={() => modals.openConfirmModal({
                title: 'Withdraw this request?',
                children: <Text size="sm">{formatRange([r.startDate, r.endDate])} will no longer be waiting for the admin.</Text>,
                labels: { confirm: 'Withdraw', cancel: 'Keep' }, confirmProps: { color: 'red' },
                onConfirm: () => cancel.mutate(r.id),
              })} />
            ))}
          </Stack>
        )}
      </Paper>
    </Stack>
  )
}

function MyRow({ r, onCancel }: { r: LeaveRequestRange; onCancel: () => void }) {
  const days = diffDays(r.startDate, r.endDate) + 1
  return (
    <Paper withBorder p="sm" radius="md">
      <Group justify="space-between" align="flex-start" wrap="nowrap" gap="sm">
        <Stack gap={2} style={{ minWidth: 0 }}>
          <Group gap="xs">
            <Text size="sm" fw={600}>{formatRange([r.startDate, r.endDate])}</Text>
            <Text size="xs" c="dimmed">{days} day{days === 1 ? '' : 's'}</Text>
            <Badge variant="light" size="sm">{kindLabel(r.kind)}</Badge>
            <Badge color={STATUS_COLOR[r.status]} size="sm">{r.status}</Badge>
          </Group>
          <Text size="sm">{r.reason}</Text>
          {r.documentName && (
            <Text size="xs" component="a" href={leaveRequestDocumentUrl(r.id)} target="_blank" rel="noopener noreferrer">
              <IconFileText size={12} style={{ verticalAlign: -1 }} /> {r.documentName}
            </Text>
          )}
          <Text size="xs" c="dimmed">
            Sent {formatLong(r.submittedAt.slice(0, 10))}
            {r.decidedAt && r.status !== 'Cancelled' && <> · {r.status.toLowerCase()} {formatLong(r.decidedAt.slice(0, 10))}{r.decidedBy ? ` by ${r.decidedBy}` : ''}</>}
          </Text>
          {r.decisionNote && <Alert color={r.status === 'Rejected' ? 'red' : 'gray'} p="xs" mt={4}><Text size="sm">{r.decisionNote}</Text></Alert>}
        </Stack>
        {r.status === 'Pending' && <Button size="xs" variant="default" onClick={onCancel}>Withdraw</Button>}
      </Group>
    </Paper>
  )
}
