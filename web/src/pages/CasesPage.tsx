import {
  ActionIcon, Alert, Autocomplete, Badge, Button, Checkbox, FileButton, Group, Modal, Paper, SegmentedControl, Select, SimpleGrid,
  Stack, Table, Tabs, Text, TextInput, Textarea, Tooltip,
} from '@mantine/core'
import { DatePickerInput, MonthPickerInput, TimeInput } from '@mantine/dates'
import { modals } from '@mantine/modals'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconDownload, IconEdit, IconFileImport, IconLock, IconPlus, IconSearch, IconStethoscope, IconTrash } from '@tabler/icons-react'
import { useMemo, useState } from 'react'
import { api, type CaseImportResult, type CaseInput, type IsoDate, type OnCallCase } from '../api'
import { isAdmin, useMe } from '../auth'
import { ErrorBox, Loading, PageHeader } from '../components/common'
import { addDays, formatLong, todayIso } from '../dates'
import { notifyError, notifyOk, usePhone } from '../lib'

const RACES = ['MALAY', 'CHINESE', 'INDIAN', 'SIAMESE']
const REFERRALS = ['ED', 'WARD', 'OT', 'OSCC', 'FORENSIC/MORTUARY']
const DSA_STATUSES = ['CALLED AND ATTENDED', 'CALLED BUT FTA', 'CALLED BUT NO ANSWER', 'NOT BEEN CALLED']

/** A case starting before 08:00 belongs to the previous day's on-call shift. */
const defaultOnCall = (date: IsoDate, start: string) => (start && start < '08:00' ? addDays(date, -1) : date)
const hm = (t: string) => t.slice(0, 5)
const duration = (m: number) => `${Math.floor(m / 60)}h ${String(m % 60).padStart(2, '0')}m`
const minutesBetween = (start: string, finish: string) => {
  if (!/^\d\d:\d\d/.test(start) || !/^\d\d:\d\d/.test(finish)) return null
  const toMin = (t: string) => Number(t.slice(0, 2)) * 60 + Number(t.slice(3, 5))
  const m = (toMin(finish) - toMin(start) + 1440) % 1440
  return m === 0 ? 1440 : m
}

const blank = (date: IsoDate): CaseInput => ({
  date, timeStart: '', timeFinish: '', onCallDate: null, patientName: '', patientIdNo: '', gender: '', age: null, race: null,
  referralSource: null, department: null, diagnosis: null, management: null, plan: null, officerId: null, taggers: null,
  dsaName: null, dsaStatus: null, confidential: false,
})

const toInput = (c: OnCallCase): CaseInput => ({
  date: c.date, timeStart: hm(c.timeStart), timeFinish: hm(c.timeFinish), onCallDate: c.onCallDate, patientName: c.patientName,
  patientIdNo: c.patientIdNo, gender: c.gender, age: c.age, race: c.race, referralSource: c.referralSource, department: c.department,
  diagnosis: c.diagnosis, management: c.management, plan: c.plan, officerId: c.personId, taggers: c.taggers, dsaName: c.dsaName,
  dsaStatus: c.dsaStatus, confidential: c.confidential,
})

/** The on-call case log ("Buku Oncall"): replaces the Google Form. Everyone in the hospital reads it. */
export function CasesPage() {
  const [tab, setTab] = useState<string | null>('new')
  return (
    <Stack gap="md">
      <PageHeader icon={IconStethoscope} title="On-call cases"
        description="Record every patient you see on call, and browse or download the hospital's case log. Patient details stay inside the hospital." />
      <Tabs value={tab} onChange={setTab} keepMounted={false} variant="pills" radius="xl">
        <Paper className="panel" p={6} mb="md" radius="xl">
          <Tabs.List>
            <Tabs.Tab value="new" leftSection={<IconPlus size={16} />}>New case</Tabs.Tab>
            <Tabs.Tab value="log" leftSection={<IconStethoscope size={16} />}>Case log</Tabs.Tab>
          </Tabs.List>
        </Paper>
        <Tabs.Panel value="new"><Paper className="panel" p="lg"><CaseForm onSaved={() => {}} /></Paper></Tabs.Panel>
        <Tabs.Panel value="log"><CaseLog /></Tabs.Panel>
      </Tabs>
    </Stack>
  )
}

/** Add (no `editing`) or edit a case. After adding, the shift details stay for the next patient of the same shift. */
function CaseForm({ editing, onSaved, onCancel }: { editing?: OnCallCase; onSaved: () => void; onCancel?: () => void }) {
  const qc = useQueryClient()
  const [v, setV] = useState<CaseInput>(() => (editing ? toInput(editing) : blank(todayIso())))
  const set = <K extends keyof CaseInput>(k: K, value: CaseInput[K]) => setV(s => ({ ...s, [k]: value }))
  const officers = useQuery({ queryKey: ['people'], queryFn: api.people })
  const onCallDate = v.onCallDate ?? defaultOnCall(v.date, v.timeStart)
  const rota = useQuery({ queryKey: ['cases', 'on-call-officer', onCallDate], queryFn: () => api.cases.onCallOfficer(onCallDate) })
  const minutes = minutesBetween(v.timeStart, v.timeFinish)

  // The officer in charge follows the rota until someone picks one.
  const [officerPicked, setOfficerPicked] = useState(!!editing)
  const officerId = officerPicked ? v.officerId : rota.data?.personId ?? null

  const save = useMutation({
    mutationFn: async () => {
      const body = { ...v, officerId, onCallDate, timeStart: `${v.timeStart}:00`, timeFinish: `${v.timeFinish}:00` }
      if (editing) await api.cases.update(editing.id, body)
      else await api.cases.create(body)
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['cases'] })
      notifyOk(editing ? 'Case saved.' : 'Case recorded. The shift details are kept for your next patient.')
      if (!editing) {
        setV(s => ({ ...blank(s.date), date: s.date, onCallDate: s.onCallDate, officerId: s.officerId, taggers: s.taggers,
          dsaName: s.dsaName, dsaStatus: s.dsaStatus }))
      }
      onSaved()
    },
    onError: e => notifyError(e, 'Could not save the case'),
  })

  const ready = v.date && minutes && v.patientName.trim() && v.patientIdNo.trim() && v.gender && officerId
    && v.dsaName?.trim() && v.dsaStatus

  return (
    <form onSubmit={e => { e.preventDefault(); if (ready) save.mutate() }}>
      <Stack>
        <Text fw={700}>Shift</Text>
        <SimpleGrid cols={{ base: 1, sm: 3 }}>
          <DatePickerInput label="Date" required value={v.date} valueFormat="D MMM YYYY"
            onChange={d => setV(s => ({ ...s, date: (d as string) ?? todayIso(), onCallDate: null }))} />
          <TimeInput label="Time start" required value={v.timeStart} onChange={e => setV(s => ({ ...s, timeStart: e.currentTarget.value, onCallDate: null }))} />
          <TimeInput label="Time finish" required value={v.timeFinish} onChange={e => set('timeFinish', e.currentTarget.value)}
            description={minutes ? `${duration(minutes)}${v.timeFinish <= v.timeStart ? ' (next day)' : ''}` : undefined} />
        </SimpleGrid>
        <Group gap="xs">
          <Text size="sm">Counts for the on-call shift of</Text>
          <SegmentedControl size="xs" value={onCallDate} onChange={x => set('onCallDate', x)}
            data={[{ value: addDays(v.date, -1), label: formatLong(addDays(v.date, -1)) }, { value: v.date, label: formatLong(v.date) }]} />
        </Group>
        <SimpleGrid cols={{ base: 1, sm: 2 }}>
          <Select label="Officer in charge" required searchable value={officerId}
            data={(officers.data ?? []).map(p => ({ value: p.id, label: p.name }))}
            onChange={x => { setOfficerPicked(true); set('officerId', x) }}
            description={!officerPicked && rota.data ? 'From the rota' : !rota.data ? 'Nobody on the rota that day: pick the officer' : undefined} />
          <TextInput label="Taggers" placeholder="e.g. Dr Hafiz (HO)" value={v.taggers ?? ''} maxLength={200}
            onChange={e => set('taggers', e.currentTarget.value || null)} />
        </SimpleGrid>
        <SimpleGrid cols={{ base: 1, sm: 2 }}>
          <TextInput label="DSA on call" required placeholder="DSA's name" value={v.dsaName ?? ''} maxLength={200}
            onChange={e => set('dsaName', e.currentTarget.value.toUpperCase() || null)} />
          <Select label="DSA" required value={v.dsaStatus} data={DSA_STATUSES} onChange={x => set('dsaStatus', x)} />
        </SimpleGrid>

        <Text fw={700} mt="sm">Patient</Text>
        <SimpleGrid cols={{ base: 1, sm: 2 }}>
          <TextInput label="Patient name" required value={v.patientName} maxLength={200}
            onChange={e => set('patientName', e.currentTarget.value.toUpperCase())} />
          <TextInput label="MyKad / KID / passport / reg no" required description="MyKad without dashes" value={v.patientIdNo}
            maxLength={40} onChange={e => set('patientIdNo', e.currentTarget.value.toUpperCase())} />
        </SimpleGrid>
        <SimpleGrid cols={{ base: 1, sm: 4 }}>
          <Stack gap={4}>
            <Text size="sm" fw={500}>Gender <Text span c="red">*</Text></Text>
            <SegmentedControl value={v.gender} onChange={x => set('gender', x)} data={['MALE', 'FEMALE']} />
          </Stack>
          <TextInput label="Age" value={v.age ?? ''} maxLength={20} onChange={e => set('age', e.currentTarget.value || null)} />
          <Autocomplete label="Race" data={RACES} value={v.race ?? ''} description="Or type another"
            onChange={x => set('race', x.toUpperCase() || null)} />
          <Select label="Department" data={['OMFS', 'PAEDS']} clearable value={v.department} onChange={x => set('department', x)} />
        </SimpleGrid>
        <Autocomplete label="Source of referral" data={REFERRALS} value={v.referralSource ?? ''} description="Or type another"
          onChange={x => set('referralSource', x.toUpperCase() || null)} maw={360} />
        <Textarea label="Diagnosis" autosize minRows={1} maxLength={500} value={v.diagnosis ?? ''}
          onChange={e => set('diagnosis', e.currentTarget.value || null)} />
        <SimpleGrid cols={{ base: 1, sm: 2 }}>
          <Textarea label="Management" autosize minRows={2} maxLength={1000} value={v.management ?? ''}
            onChange={e => set('management', e.currentTarget.value || null)} />
          <Textarea label="Plan" autosize minRows={2} maxLength={1000} value={v.plan ?? ''}
            onChange={e => set('plan', e.currentTarget.value || null)} />
        </SimpleGrid>
        <Checkbox label="Confidential patient (leave the name and registration number out of the ETAP form)"
          checked={v.confidential} onChange={e => set('confidential', e.currentTarget.checked)} />
        <Group justify="flex-end">
          {onCancel && <Button variant="default" onClick={onCancel}>Cancel</Button>}
          <Button type="submit" loading={save.isPending} disabled={!ready}>{editing ? 'Save' : 'Record case'}</Button>
        </Group>
      </Stack>
    </form>
  )
}

function CaseLog() {
  const qc = useQueryClient()
  const me = useMe().data
  const phone = usePhone()
  const [month, setMonth] = useState<string>(todayIso().slice(0, 7) + '-01')
  const [personId, setPersonId] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const [editing, setEditing] = useState<OnCallCase | null>(null)
  const [importing, setImporting] = useState(false)
  const ym = month.slice(0, 7)
  const cases = useQuery({ queryKey: ['cases', ym, personId], queryFn: () => api.cases.list(ym, personId ?? undefined) })
  const officers = useQuery({ queryKey: ['people'], queryFn: api.people })

  const remove = useMutation({
    mutationFn: (id: string) => api.cases.remove(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['cases'] }); notifyOk('Case removed.') },
    onError: e => notifyError(e),
  })

  const rows = useMemo(() => {
    const s = search.trim().toUpperCase()
    return (cases.data ?? []).filter(c => !s || [c.patientName, c.patientIdNo, c.diagnosis, c.officer, c.dsaName]
      .some(x => x?.toUpperCase().includes(s)))
  }, [cases.data, search])
  const total = rows.reduce((n, c) => n + c.minutes, 0)

  return (
    <Stack gap="md">
      <Group justify="space-between" wrap="wrap">
        <Group>
          <MonthPickerInput value={month} onChange={m => m && setMonth(m as string)} valueFormat="MMMM YYYY" w={170} />
          <Select placeholder="All officers" clearable searchable w={220} value={personId} onChange={setPersonId}
            data={(officers.data ?? []).map(p => ({ value: p.id, label: p.name }))} />
          <TextInput placeholder="Patient, IC, diagnosis…" leftSection={<IconSearch size={16} />} value={search}
            onChange={e => setSearch(e.currentTarget.value)} w={220} />
        </Group>
        <Group>
          {isAdmin(me) && (
            <Button variant="default" leftSection={<IconFileImport size={16} />} onClick={() => setImporting(true)}>Import Google Sheet</Button>
          )}
          <Button variant="default" leftSection={<IconDownload size={16} />} component="a"
            href={api.cases.exportUrl(ym, personId ?? undefined)}>Excel</Button>
        </Group>
      </Group>
      <Alert variant="light" color="gray" icon={<IconLock size={18} />} py={6}>
        Patient details are confidential. Only people of this hospital can see them, and every view and download is recorded.
      </Alert>

      {cases.isLoading ? <Loading /> : cases.error ? <ErrorBox error={cases.error} /> : (
        <Paper withBorder>
          <Group justify="space-between" px="md" py="xs">
            <Text size="sm" c="dimmed">{rows.length} case{rows.length === 1 ? '' : 's'} · {duration(total)} in total</Text>
          </Group>
          <Table.ScrollContainer minWidth={phone ? 700 : 980}>
            <Table striped highlightOnHover verticalSpacing={6}>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>On call</Table.Th><Table.Th>Time</Table.Th><Table.Th>Patient</Table.Th><Table.Th>Diagnosis</Table.Th>
                  <Table.Th>Officer</Table.Th><Table.Th>DSA</Table.Th><Table.Th />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {rows.length === 0 && (
                  <Table.Tr><Table.Td colSpan={7}><Text c="dimmed" ta="center" py="md">No cases this month.</Text></Table.Td></Table.Tr>
                )}
                {rows.map(c => (
                  <Table.Tr key={c.id}>
                    <Table.Td><Text size="sm" fw={600}>{formatLong(c.onCallDate)}</Text>
                      {c.date !== c.onCallDate && <Text size="xs" c="dimmed">seen {formatLong(c.date)}</Text>}</Table.Td>
                    <Table.Td><Text size="sm">{hm(c.timeStart)}–{hm(c.timeFinish)}</Text><Text size="xs" c="dimmed">{duration(c.minutes)}</Text></Table.Td>
                    <Table.Td>
                      <Text size="sm" fw={600}>{c.patientName} {c.confidential && <Badge size="xs" color="red" variant="light">Confidential</Badge>}</Text>
                      <Text size="xs" c="dimmed">{c.patientIdNo} · {c.gender}{c.age && `, ${c.age}`}{c.department && ` · ${c.department}`}</Text>
                    </Table.Td>
                    <Table.Td><Text size="sm" lineClamp={2}>{c.diagnosis ?? '–'}</Text></Table.Td>
                    <Table.Td><Text size="sm">{c.officer}</Text>{c.taggers && <Text size="xs" c="dimmed">{c.taggers}</Text>}</Table.Td>
                    <Table.Td><Text size="sm">{c.dsaName ?? '–'}</Text>{c.dsaStatus && <Text size="xs" c="dimmed">{c.dsaStatus}</Text>}</Table.Td>
                    <Table.Td>
                      {c.canEdit && (
                        <Group gap={4} wrap="nowrap" justify="flex-end">
                          <Tooltip label="Edit"><ActionIcon variant="subtle" aria-label="Edit" onClick={() => setEditing(c)}><IconEdit size={18} /></ActionIcon></Tooltip>
                          <Tooltip label="Remove">
                            <ActionIcon variant="subtle" color="red" aria-label="Remove" onClick={() => modals.openConfirmModal({
                              title: 'Remove this case?', children: <Text size="sm">It disappears from the log and the ETAP form.</Text>,
                              labels: { confirm: 'Remove', cancel: 'Cancel' }, confirmProps: { color: 'red' }, onConfirm: () => remove.mutate(c.id),
                            })}><IconTrash size={18} /></ActionIcon>
                          </Tooltip>
                        </Group>
                      )}
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Paper>
      )}

      {editing && (
        <Modal opened onClose={() => setEditing(null)} title="Edit case" size="xl" fullScreen={phone}>
          <CaseForm editing={editing} onSaved={() => setEditing(null)} onCancel={() => setEditing(null)} />
        </Modal>
      )}
      {importing && <CaseImportModal onClose={() => setImporting(false)} />}
    </Stack>
  )
}

/** Admins bring the existing Google Sheet in: File → Download → CSV on the response sheet. */
function CaseImportModal({ onClose }: { onClose: () => void }) {
  const qc = useQueryClient()
  const [csv, setCsv] = useState<string | null>(null)
  const [fallback, setFallback] = useState<string | null>(null)
  const [result, setResult] = useState<CaseImportResult | null>(null)
  const officers = useQuery({ queryKey: ['people'], queryFn: api.people })
  const check = useMutation({
    mutationFn: ({ text, commit }: { text: string; commit: boolean }) => api.cases.import(text, commit, fallback),
    onSuccess: r => {
      setResult(r)
      if (r.committed) {
        qc.invalidateQueries({ queryKey: ['cases'] })
        notifyOk(`${r.added} case(s) imported${r.duplicates ? `, ${r.duplicates} already in the log skipped` : ''}.`)
        onClose()
      }
    },
    onError: e => notifyError(e, 'Could not read the file'),
  })
  const problems = result?.rows.filter(r => r.errors.length > 0) ?? []
  const ready = !!result && result.fileErrors.length === 0 && result.rows.length > 0 && problems.length === 0

  return (
    <Modal opened onClose={onClose} title="Import the Google Sheet" size="xl">
      <Stack>
        <Text size="sm">
          Open the form's response sheet, choose <b>File → Download → Comma-separated values</b>, and pick that file here. Each row is
          given to the officer named in its "officer & taggers" column; pick an officer for rows that name nobody on the rota. Cases
          already in the log are skipped. Nothing is added unless every row is valid.
        </Text>
        <Group>
          <FileButton accept=".csv,text/csv" onChange={async f => {
            if (!f) return
            const text = await f.text()
            setCsv(text)
            check.mutate({ text, commit: false })
          }}>
            {props => <Button {...props} leftSection={<IconFileImport size={16} />} loading={check.isPending && !result}>Choose CSV file</Button>}
          </FileButton>
          <Select placeholder="Officer for unmatched rows" clearable searchable w={260} value={fallback}
            data={(officers.data ?? []).map(p => ({ value: p.id, label: p.name }))}
            onChange={x => { setFallback(x); setResult(null) }} />
        </Group>
        {result && (
          <>
            {result.fileErrors.map(e => <Alert key={e} color="red" variant="light">{e}</Alert>)}
            <Text size="sm">
              {result.rows.length} row(s){result.duplicates > 0 && `, ${result.duplicates} already in the log`}
              {problems.length > 0 ? `, ${problems.length} with problems:` : '.'}
            </Text>
            {problems.length > 0 && (
              <Paper withBorder mah={300} style={{ overflow: 'auto' }}>
                <Table verticalSpacing={4}>
                  <Table.Tbody>
                    {problems.map(r => r.errors.map((e, i) => (
                      <Table.Tr key={`${r.line}-${i}`}>
                        <Table.Td><Text size="sm">Row {r.line}{e.letter && `, column ${e.letter}`}</Text></Table.Td>
                        <Table.Td><Text size="sm">{e.message}</Text></Table.Td>
                      </Table.Tr>
                    )))}
                  </Table.Tbody>
                </Table>
              </Paper>
            )}
          </>
        )}
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>Cancel</Button>
          {csv && !result && <Button variant="light" loading={check.isPending} onClick={() => check.mutate({ text: csv, commit: false })}>Check again</Button>}
          <Button disabled={!ready} loading={check.isPending && !!result} onClick={() => csv && check.mutate({ text: csv, commit: true })}>
            Import {result ? result.rows.length - result.duplicates : ''} case(s)
          </Button>
        </Group>
      </Stack>
    </Modal>
  )
}
