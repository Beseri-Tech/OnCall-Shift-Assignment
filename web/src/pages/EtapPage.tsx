import {
  ActionIcon, Alert, Anchor, Badge, Button, Checkbox, Group, List, Modal, Paper, Select, SimpleGrid, Stack, Table, Text, Title, Tooltip,
} from '@mantine/core'
import { DatePickerInput, MonthPickerInput, TimeInput } from '@mantine/dates'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  IconAlertTriangle, IconArrowsExchange, IconCalendarPlus, IconCheck, IconDeviceFloppy, IconFileDownload, IconFileText, IconPlus,
  IconReceipt2, IconTrash, IconX,
} from '@tabler/icons-react'
import { useState } from 'react'
import { Link, Navigate } from 'react-router'
import { api, downloadDocument, type EtapClaim, type EtapRow, type EtapRowInput, type IsoDate, type ShiftReport } from '../api'
import { useMe } from '../auth'
import { ErrorBox, Loading, PageHeader } from '../components/common'
import { addDays, endOfMonth, formatLong, formatWeekday, todayIso } from '../dates'
import { notifyError, notifyOk, usePhone } from '../lib'

const hm = (t: string) => t.slice(0, 5)
const hours = (m: number) => `${Math.floor(m / 60)}h${m % 60 ? ` ${String(m % 60).padStart(2, '0')}m` : ''}`
const CATEGORY: Record<string, { label: string; color: string }> = {
  '2a': { label: '≥ 4 h · working day', color: 'teal' }, '2b': { label: '≥ 4 h · rest/holiday', color: 'teal' },
  '3a': { label: '< 4 h · working day', color: 'gray' }, '3b': { label: '< 4 h · rest/holiday', color: 'gray' },
}

/** What the officer edits for one row: the on-call date, start time, end time and whether it ends the next day. */
interface Draft { date: IsoDate; from: string; to: string; nextDay: boolean }
const toDraft = (r: EtapRowInput): Draft => ({ date: r.date, from: hm(r.fromTime), to: hm(r.toTime), nextDay: r.toDate > r.date })
const toInput = (d: Draft): EtapRowInput => ({
  date: d.date, fromDate: d.date, fromTime: `${d.from}:00`, toDate: d.nextDay ? addDays(d.date, 1) : d.date, toTime: `${d.to}:00`,
})

/** The officer's monthly ETAP claim: their on-call days, worked out against the rota and their cases, then the filled-in form. */
export function EtapPage() {
  const me = useMe().data
  const [month, setMonth] = useState(todayIso().slice(0, 7) + '-01')
  const ym = month.slice(0, 7)
  const claim = useQuery({ queryKey: ['etap', ym], queryFn: () => api.etap.get(ym), enabled: !!me?.personId, gcTime: 0 })
  if (!me?.personId) return <Navigate to="/" replace />

  return (
    <Stack gap="md">
      <PageHeader icon={IconReceipt2} title="ETAP claim"
        description="Your on-call claim for a month: list the days you were on call, check them against the rota and your cases, then download the filled-in ETAP form." />
      <Group>
        <MonthPickerInput value={month} onChange={m => m && setMonth(m as string)} valueFormat="MMMM YYYY" w={180} />
      </Group>
      {claim.isLoading ? <Loading /> : claim.error ? <ErrorBox error={claim.error} /> : <Claim key={ym} month={ym} claim={claim.data!} />}
      <Reports />
    </Stack>
  )
}

function Claim({ month, claim }: { month: string; claim: EtapClaim }) {
  const qc = useQueryClient()
  const phone = usePhone()
  const [rows, setRows] = useState<Draft[]>(() => claim.rows.map(toDraft))
  const [reporting, setReporting] = useState<EtapRow | null>(null)
  const saved = JSON.stringify(rows.map(toInput)) === JSON.stringify(claim.rows.map(r => toInput(toDraft(r))))
  const first = `${month}-01`, last = endOfMonth(first)

  const save = useMutation({
    mutationFn: () => api.etap.save(month, [...rows].sort((a, b) => a.date.localeCompare(b.date)).map(toInput)),
    onSuccess: c => { qc.setQueryData(['etap', month], c); setRows(c.rows.map(toDraft)); notifyOk('Claim saved.') },
    onError: e => notifyError(e, 'Could not save the claim'),
  })
  const set = (i: number, patch: Partial<Draft>) => setRows(rs => rs.map((r, j) => (j === i ? { ...r, ...patch } : r)))
  const computed = new Map(claim.rows.map(r => [r.date, r]))
  const free = (d: IsoDate) => !rows.some(r => r.date === d)
  const nextFree = () => { for (let d = first; d <= last; d = addDays(d, 1)) if (free(d)) return d; return first }

  return (
    <Stack gap="md">
      {!claim.profileVerified && (
        <Alert color="orange" variant="light" icon={<IconAlertTriangle size={18} />}>
          The ETAP form uses your profile, which isn't verified. <Anchor component={Link} to="/profile">Complete and verify your ETAP profile</Anchor> to download the form.
        </Alert>
      )}

      <Paper className="panel" p="lg">
        <Stack gap="sm">
          <Group justify="space-between" wrap="wrap">
            <Title order={4}>On-call days</Title>
            <Group gap="xs">
              {claim.unclaimedRotaRows.length > 0 && (
                <Button size="xs" variant="light" leftSection={<IconCalendarPlus size={14} />}
                  onClick={() => setRows(rs => [...rs, ...claim.unclaimedRotaRows.filter(r => free(r.date)).map(toDraft)].sort((a, b) => a.date.localeCompare(b.date)))}>
                  Add my rota dates ({claim.unclaimedRotaRows.length})
                </Button>
              )}
              <Button size="xs" variant="default" leftSection={<IconPlus size={14} />} disabled={rows.length >= 30}
                onClick={() => setRows(rs => [...rs, { date: nextFree(), from: '17:00', to: '08:00', nextDay: true }])}>Add a day</Button>
            </Group>
          </Group>
          <Text size="xs" c="dimmed">
            Usually 17:00 to 08:00 the next day on working days, and 08:00 to 08:00 (24 h) on weekends and public holidays: type what your punch card shows.
            Your rota dates this month: {claim.rotaDates.length ? claim.rotaDates.map(d => formatLong(d)).join(', ') : 'none'}.
          </Text>
          <Table.ScrollContainer minWidth={phone ? 760 : 900}>
            <Table verticalSpacing={6}>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>On-call date</Table.Th><Table.Th>From</Table.Th><Table.Th>To</Table.Th><Table.Th>Cases</Table.Th>
                  <Table.Th>Claimed as</Table.Th><Table.Th>Rota</Table.Th><Table.Th />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {rows.length === 0 && (
                  <Table.Tr><Table.Td colSpan={7}><Text c="dimmed" ta="center" py="md">No days yet. Add your rota dates or add a day.</Text></Table.Td></Table.Tr>
                )}
                {rows.map((r, i) => {
                  const c = saved ? computed.get(r.date) : undefined
                  return (
                    <Table.Tr key={i}>
                      <Table.Td>
                        <DatePickerInput size="xs" w={150} value={r.date} minDate={first} maxDate={last} valueFormat="ddd D MMM"
                          onChange={d => d && set(i, { date: d as string })} excludeDate={d => !free(d as string) && d !== r.date} />
                        {c?.holidayName && <Text size="xs" c="dimmed">{c.holidayName}</Text>}
                      </Table.Td>
                      <Table.Td><TimeInput size="xs" w={90} value={r.from} onChange={e => set(i, { from: e.currentTarget.value })} /></Table.Td>
                      <Table.Td>
                        <Group gap={6} wrap="nowrap">
                          <TimeInput size="xs" w={90} value={r.to} onChange={e => set(i, { to: e.currentTarget.value })} />
                          <Checkbox size="xs" label="next day" checked={r.nextDay} onChange={e => set(i, { nextDay: e.currentTarget.checked })} />
                        </Group>
                      </Table.Td>
                      <Table.Td>{c ? <Text size="sm">{c.caseCount} · {hours(c.caseMinutes)}</Text> : <Text size="xs" c="dimmed">save to see</Text>}</Table.Td>
                      <Table.Td>{c && <Badge variant="light" color={CATEGORY[c.category].color}>{c.category} · {CATEGORY[c.category].label}</Badge>}</Table.Td>
                      <Table.Td>
                        {c && (c.inRota ? <Badge variant="light" color="green" leftSection={<IconCheck size={12} />}>Yours</Badge> : c.report ? (
                          <Badge variant="light" color={c.report.status === 'Confirmed' ? 'green' : c.report.status === 'Pending' ? 'orange' : 'red'}>
                            Swap {c.report.status.toLowerCase()}
                          </Badge>
                        ) : (
                          <Group gap={4} wrap="nowrap">
                            <Tooltip label={`The rota has ${c.rotaOfficer?.name ?? 'nobody'} on call that date`}>
                              <Badge variant="light" color="orange">{c.rotaOfficer ? c.rotaOfficer.name : 'Not in rota'}</Badge>
                            </Tooltip>
                            {c.rotaOfficer && (
                              <Button size="compact-xs" variant="subtle" leftSection={<IconArrowsExchange size={12} />} onClick={() => setReporting(c)}>Report swap</Button>
                            )}
                          </Group>
                        ))}
                      </Table.Td>
                      <Table.Td>
                        <ActionIcon variant="subtle" color="red" aria-label="Remove day" onClick={() => setRows(rs => rs.filter((_, j) => j !== i))}>
                          <IconTrash size={16} />
                        </ActionIcon>
                      </Table.Td>
                    </Table.Tr>
                  )
                })}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          <Group justify="flex-end">
            <Button leftSection={<IconDeviceFloppy size={16} />} loading={save.isPending} disabled={saved && claim.saved}
              onClick={() => save.mutate()}>Save</Button>
          </Group>
        </Stack>
      </Paper>

      <SimpleGrid cols={{ base: 1, md: 2 }}>
        <Paper className="panel" p="lg">
          <Stack gap="xs">
            <Title order={4}>Section C {claim.specialist && <Badge ml={6} variant="light">Specialist rates</Badge>}</Title>
            <Table verticalSpacing={4}>
              <Table.Tbody>
                {claim.lines.map(l => (
                  <Table.Tr key={l.category}>
                    <Table.Td><Text size="sm"><b>{l.category}</b> {l.label}</Text></Table.Td>
                    <Table.Td ta="right"><Text size="sm">{l.days} × {l.rate}</Text></Table.Td>
                    <Table.Td ta="right"><Text size="sm" fw={600}>RM {l.amount.toLocaleString()}</Text></Table.Td>
                  </Table.Tr>
                ))}
                <Table.Tr>
                  <Table.Td><Text fw={700}>Jumlah tuntutan</Text></Table.Td>
                  <Table.Td ta="right"><Text fw={700}>{claim.totalDays} days</Text></Table.Td>
                  <Table.Td ta="right"><Text fw={700}>RM {claim.totalAmount.toLocaleString()}</Text></Table.Td>
                </Table.Tr>
              </Table.Tbody>
            </Table>
            <Text size="xs" c="dimmed">
              A shift counts as passive call over 4 hours when your cases on that on-call date add up to 4 hours or more
              ({claim.patients.length} patient{claim.patients.length === 1 ? '' : 's'} listed on the form). From the saved claim.
            </Text>
          </Stack>
        </Paper>

        <Paper className="panel" p="lg">
          <Stack gap="sm">
            <Title order={4}>Download</Title>
            <Button leftSection={<IconFileDownload size={16} />} component="a" href={api.etap.pdfUrl(month)}
              disabled={!claim.profileVerified || !saved || claim.rows.length === 0}>
              ETAP form (PDF)
            </Button>
            {(!saved || claim.rows.length === 0) && <Text size="xs" c="dimmed">Save your days first.</Text>}
            {claim.suratArahan ? (
              <Button variant="default" leftSection={<IconFileText size={16} />} onClick={() => downloadDocument(claim.suratArahan!.documentId).catch(notifyError)}>
                Surat arahan{claim.suratArahan.reference && ` ${claim.suratArahan.reference}`} (from {formatLong(claim.suratArahan.effectiveFrom)})
              </Button>
            ) : <Alert color="gray" variant="light" py={6}>No surat arahan uploaded yet: ask your admin.</Alert>}
            <Text size="sm" fw={600} mt="xs">Also bring yourself:</Text>
            <List size="sm" spacing={2}>
              <List.Item>A copy of your punch card (kad perakam waktu)</List.Item>
              <List.Item>Your bank statement (and IC copy, for your first claim)</List.Item>
            </List>
            <Text size="xs" c="dimmed">Sign sections C and the attachment, and get your head of department to sign section D.</Text>
          </Stack>
        </Paper>
      </SimpleGrid>

      {reporting && <ReportModal row={reporting} rotaDates={claim.rotaDates} onClose={() => setReporting(null)} />}
    </Stack>
  )
}

/** "I did a colleague's on-call date": sent to the colleague (or an admin) to confirm, then the rota is changed to match. */
function ReportModal({ row, rotaDates, onClose }: { row: EtapRow; rotaDates: IsoDate[]; onClose: () => void }) {
  const qc = useQueryClient()
  const [returnDate, setReturnDate] = useState<string | null>(null)
  const send = useMutation({
    mutationFn: () => api.etap.report(row.date, row.rotaOfficer!.personId, returnDate),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['etap'] })
      qc.invalidateQueries({ queryKey: ['etap-reports'] })
      notifyOk(`Sent to ${row.rotaOfficer!.name} to confirm.`)
      onClose()
    },
  })
  return (
    <Modal opened onClose={onClose} title="Report a swap">
      <Stack>
        <Text size="sm">
          The rota has <b>{row.rotaOfficer!.name}</b> on call on <b>{formatWeekday(row.date)}, {formatLong(row.date)}</b>. If you did it instead,
          they (or an admin) confirm it and the rota and tally are corrected.
        </Text>
        <Select label="Did they take one of your dates in return?" clearable placeholder="No, I just covered" value={returnDate}
          onChange={setReturnDate} data={rotaDates.map(d => ({ value: d, label: `${formatWeekday(d)}, ${formatLong(d)}` }))} />
        {send.error && <Text c="red" size="sm">{send.error.message}</Text>}
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>Cancel</Button>
          <Button loading={send.isPending} onClick={() => send.mutate()}>Send for confirmation</Button>
        </Group>
      </Stack>
    </Modal>
  )
}

/** Swap reports waiting for the user, and their own. */
function Reports() {
  const qc = useQueryClient()
  const reports = useQuery({ queryKey: ['etap-reports'], queryFn: api.etap.reports })
  const act = useMutation({
    mutationFn: ({ r, action }: { r: ShiftReport; action: 'confirm' | 'reject' | 'cancel' }) => api.etap[action](r.id),
    onSuccess: (_, { action }) => {
      qc.invalidateQueries({ queryKey: ['etap'] })
      qc.invalidateQueries({ queryKey: ['etap-reports'] })
      notifyOk(action === 'confirm' ? 'Confirmed: the rota is updated.' : action === 'reject' ? 'Not confirmed.' : 'Withdrawn.')
    },
    onError: e => notifyError(e),
  })
  const list = reports.data ?? []
  if (list.length === 0) return null
  return (
    <Paper className="panel" p="lg">
      <Stack gap="xs">
        <Title order={4}>Swap reports</Title>
        <Table verticalSpacing={6}>
          <Table.Tbody>
            {list.map(r => (
              <Table.Tr key={r.id}>
                <Table.Td>
                  <Text size="sm"><b>{r.reporter}</b> was on call on <b>{formatLong(r.date)}</b> instead of <b>{r.colleague}</b>
                    {r.returnDate && <>, who took {formatLong(r.returnDate)} in return</>}.</Text>
                </Table.Td>
                <Table.Td><Badge variant="light" color={r.status === 'Confirmed' ? 'green' : r.status === 'Pending' ? 'orange' : 'gray'}>{r.status}</Badge></Table.Td>
                <Table.Td>
                  <Group gap={4} justify="flex-end" wrap="nowrap">
                    {r.canDecide && <>
                      <Button size="compact-sm" leftSection={<IconCheck size={14} />} onClick={() => act.mutate({ r, action: 'confirm' })}>Confirm</Button>
                      <Button size="compact-sm" variant="default" leftSection={<IconX size={14} />} onClick={() => act.mutate({ r, action: 'reject' })}>Reject</Button>
                    </>}
                    {!r.canDecide && r.status === 'Pending' && (
                      <Button size="compact-sm" variant="subtle" color="gray" onClick={() => act.mutate({ r, action: 'cancel' })}>Withdraw</Button>
                    )}
                  </Group>
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      </Stack>
    </Paper>
  )
}
