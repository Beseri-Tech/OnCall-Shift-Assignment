import {
  ActionIcon, Alert, Autocomplete, Badge, Button, Checkbox, CopyButton, Fieldset, FileButton, Grid, Group, Input, Menu, Modal, Paper,
  SegmentedControl, Select, SimpleGrid, Stack, Table, Tabs, TagsInput, Text, TextInput, Textarea, Tooltip,
} from '@mantine/core'
import { DatePickerInput, MonthPickerInput, TimeInput } from '@mantine/dates'
import { modals } from '@mantine/modals'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  IconBrandWhatsapp, IconCheck, IconChevronDown, IconCopy, IconDownload, IconEdit, IconFileImport, IconLock, IconPlus, IconSearch,
  IconStethoscope, IconTrash,
} from '@tabler/icons-react'
import { useMemo, useState } from 'react'
import { api, type CaseImportResult, type CaseInput, type IsoDate, type OnCallCase } from '../api'
import { isAdmin, useMe } from '../auth'
import { ErrorBox, Loading, PageHeader } from '../components/common'
import { addDays, formatLong, formatMonth, todayIso } from '../dates'
import { notifyError, notifyOk, usePhone } from '../lib'

const RACES = ['MALAY', 'CHINESE', 'INDIAN', 'SIAMESE']
const REFERRALS = ['ED', 'WARD', 'OT', 'OSCC', 'FORENSIC/MORTUARY']
const DSA_STATUSES = ['CALLED AND ATTENDED', 'CALLED BUT FTA', 'CALLED BUT NO ANSWER', 'NOT BEEN CALLED']
/** The colour of each choice's filled indicator, so the selection stands out at a glance. */
const CHOICE_COLOR: Record<string, string> = {
  OMFS: 'blue', PAEDS: 'grape', GREEN: 'green', YELLOW: 'yellow', RED: 'red', MALE: 'blue', FEMALE: 'pink', TRAUMA: 'red', 'NON-TRAUMA': 'teal',
}
const choice = (value: string | null | undefined) => ({ color: (value && CHOICE_COLOR[value]) || 'teal', autoContrast: true })
const INVESTIGATIONS = ['X-RAY', 'OPG', 'PA SKULL', 'CT', 'FBC', 'RBS', 'RP', 'COAGULATION PROFILE']

/** Before 08:00 you are still on the previous day's shift. */
const currentShift = (): IsoDate => (new Date().getHours() < 8 ? addDays(todayIso(), -1) : todayIso())
const hm = (t: string) => t.slice(0, 5)
const duration = (m: number) => `${Math.floor(m / 60)}h ${String(m % 60).padStart(2, '0')}m`
const minutesBetween = (start: string, finish: string) => {
  if (!/^\d\d:\d\d/.test(start) || !/^\d\d:\d\d/.test(finish)) return null
  const toMin = (t: string) => Number(t.slice(0, 2)) * 60 + Number(t.slice(3, 5))
  const m = (toMin(finish) - toMin(start) + 1440) % 1440
  return m === 0 ? 1440 : m
}

const blank = (onCallDate: IsoDate): CaseInput => ({
  date: onCallDate, timeStart: '', timeFinish: '', onCallDate, patientName: '', patientIdNo: '', gender: '', age: null, race: null,
  referralSource: null, department: null, diagnosis: null, management: null, plan: null, officerId: null, taggers: null,
  dsaName: null, dsaStatus: null, confidential: false, spokenTo: null, seenBy: null, caseType: null, presentation: null,
  investigations: null, prescription: null, edZone: null,
})

const toInput = (c: OnCallCase): CaseInput => ({
  date: c.date, timeStart: hm(c.timeStart), timeFinish: hm(c.timeFinish), onCallDate: c.onCallDate, patientName: c.patientName,
  patientIdNo: c.patientIdNo, gender: c.gender, age: c.age, race: c.race, referralSource: c.referralSource, department: c.department,
  diagnosis: c.diagnosis, management: c.management, plan: c.plan, officerId: c.personId, taggers: c.taggers, dsaName: c.dsaName,
  dsaStatus: c.dsaStatus, confidential: c.confidential, spokenTo: c.spokenTo, seenBy: c.seenBy, caseType: c.caseType,
  presentation: c.presentation, investigations: c.investigations, prescription: c.prescription, edZone: c.edZone,
})

/** "Choose or type" fields: the list covers the usual answers; anything else can be typed in. */
const pickOrType = 'Choose, or type if not listed'

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

/**
 * Add (no `editing`) or edit a case. The officer in charge is the officer recording it. After adding, the shift, officer and
 * DSA details stay for the next patient of the same shift.
 */
function CaseForm({ editing, onSaved, onCancel }: { editing?: OnCallCase; onSaved: () => void; onCancel?: () => void }) {
  const qc = useQueryClient()
  const [shareId, setShareId] = useState<string | null>(null)
  const me = useMe().data
  const [v, setV] = useState<CaseInput>(() => (editing ? toInput(editing) : blank(currentShift())))
  const set = <K extends keyof CaseInput>(k: K, value: CaseInput[K]) => setV(s => ({ ...s, [k]: value }))
  const shift = v.onCallDate ?? v.date
  const nextDay = v.date !== shift
  const minutes = minutesBetween(v.timeStart, v.timeFinish)
  const officer = editing ? editing.officer : me?.personName
  const canRecord = !!editing || !!me?.personId

  // A case starting before 08:00 is usually after midnight, on the day after the shift date; the officer can change it.
  const setStart = (t: string) => setV(s => ({ ...s, timeStart: t, date: t && t < '08:00' ? addDays(s.onCallDate ?? s.date, 1) : (s.onCallDate ?? s.date) }))

  const save = useMutation({
    mutationFn: async () => {
      const body = { ...v, onCallDate: shift, timeStart: `${v.timeStart}:00`, timeFinish: `${v.timeFinish}:00` }
      if (editing) await api.cases.update(editing.id, body)
      else return (await api.cases.create(body)).id
    },
    onSuccess: id => {
      qc.invalidateQueries({ queryKey: ['cases'] })
      if (editing) notifyOk('Case saved.')
      if (!editing) setV(s => ({ ...blank(shift), taggers: s.taggers, dsaName: s.dsaName, dsaStatus: s.dsaStatus }))
      if (id) modals.openConfirmModal({
        title: 'Case recorded', centered: true,
        children: <Text size="sm">Would you like to generate the WhatsApp passover for the {v.department} group? The shift, officer and DSA are kept for your next patient.</Text>,
        labels: { confirm: 'Generate passover', cancel: 'Not now' },
        confirmProps: { color: 'green', leftSection: <IconBrandWhatsapp size={16} /> },
        onConfirm: () => setShareId(id),
      })
      onSaved()
    },
    onError: e => notifyError(e, 'Could not save the case'),
  })

  const upper = (k: keyof CaseInput) => (e: { currentTarget: { value: string } }) => set(k, (e.currentTarget.value.toUpperCase() || null) as never)
  const trauma = v.caseType === 'TRAUMA'

  const fromEd = v.referralSource === 'ED'
  const ready = canRecord && v.department && v.caseType && (!fromEd || v.edZone) && v.date && minutes && v.patientName.trim() && v.patientIdNo.trim() && v.gender && v.dsaName?.trim() && v.dsaStatus

  return (
    <form onSubmit={e => { e.preventDefault(); if (ready) save.mutate() }}>
      <Stack gap="md">
        {!canRecord && (
          <Alert color="orange" variant="light">Only officers on the rota record on-call cases. You can read the case log.</Alert>
        )}
        <Fieldset legend="Shift">
          <Grid gap="md" align="flex-end">
            <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
              <DatePickerInput label="On-call date" required value={shift} valueFormat="ddd, D MMM YYYY"
                onChange={d => d && setV(s => ({ ...s, onCallDate: d as string, date: nextDay ? addDays(d as string, 1) : (d as string) }))} />
            </Grid.Col>
            <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
              <Input.Wrapper label="Patient seen on" required>
                <SegmentedControl fullWidth {...choice(null)} value={nextDay ? 'next' : 'same'} onChange={x => set('date', x === 'next' ? addDays(shift, 1) : shift)}
                  data={[{ value: 'same', label: formatLong(shift) }, { value: 'next', label: formatLong(addDays(shift, 1)) }]} />
              </Input.Wrapper>
            </Grid.Col>
            <Grid.Col span={{ base: 6, md: 3 }}>
              <TimeInput label="Time start" required value={v.timeStart} onChange={e => setStart(e.currentTarget.value)} />
            </Grid.Col>
            <Grid.Col span={{ base: 6, md: 3 }}>
              <TimeInput label="Time finish" required value={v.timeFinish} onChange={e => set('timeFinish', e.currentTarget.value)}
                rightSectionWidth={80} rightSection={minutes ? <Text size="xs" c="dimmed">{duration(minutes)}</Text> : null} />
            </Grid.Col>
          </Grid>
        </Fieldset>

        <SimpleGrid cols={{ base: 1, md: 2 }}>
          <Fieldset legend="Officer">
            <Stack gap="sm">
              <TextInput label="Officer in charge" value={officer ?? ''} readOnly variant="filled" />
              <TextInput label="Taggers" placeholder="e.g. Dr Hafiz (HO)" value={v.taggers ?? ''} maxLength={200}
                onChange={e => set('taggers', e.currentTarget.value.toUpperCase() || null)} />
            </Stack>
          </Fieldset>
          <Fieldset legend="DSA">
            <Stack gap="sm">
              <TextInput label="DSA on call" required placeholder="DSA's name" value={v.dsaName ?? ''} maxLength={200}
                onChange={e => set('dsaName', e.currentTarget.value.toUpperCase() || null)} />
              <Select label="DSA status" required placeholder="Was the DSA called?" value={v.dsaStatus} data={DSA_STATUSES}
                onChange={x => set('dsaStatus', x)} />
            </Stack>
          </Fieldset>
        </SimpleGrid>

        <Fieldset legend="Patient">
          <Grid gap="md" align="flex-end">
            <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
              <Input.Wrapper label="Department" required>
                <SegmentedControl fullWidth {...choice(v.department)} value={v.department ?? ''} onChange={x => set('department', x)} data={['OMFS', 'PAEDS']} />
              </Input.Wrapper>
            </Grid.Col>
            {!v.department && (
              <Grid.Col span={{ base: 12, sm: 6, md: 9 }}><Text size="sm" c="dimmed">Choose the department to fill in the patient.</Text></Grid.Col>
            )}
          </Grid>
          <Fieldset variant="unstyled" disabled={!v.department} mt="md">
            <Grid gap="md" align="flex-end">
              <Grid.Col span={{ base: 12, md: fromEd ? 3 : 4 }}>
                <Autocomplete label="Source of referral" placeholder={pickOrType} data={REFERRALS} value={v.referralSource ?? ''}
                  onChange={x => setV(s => ({ ...s, referralSource: x.toUpperCase() || null, edZone: x.toUpperCase() === 'ED' ? s.edZone : null }))} />
              </Grid.Col>
              {fromEd && (
                <Grid.Col span={{ base: 12, md: 3 }}>
                  <Input.Wrapper label="ED zone" required>
                    <SegmentedControl fullWidth {...choice(v.edZone)} value={v.edZone ?? ''} onChange={x => set('edZone', x as CaseInput['edZone'])} disabled={!v.department}
                      data={[{ value: 'GREEN', label: 'Green' }, { value: 'YELLOW', label: 'Yellow' }, { value: 'RED', label: 'Red' }]} />
                  </Input.Wrapper>
                </Grid.Col>
              )}
              <Grid.Col span={{ base: 12, sm: 6, md: fromEd ? 3 : 4 }}>
                <TextInput label="Spoken to (S/T)" placeholder="e.g. DR RIDWAN" value={v.spokenTo ?? ''} maxLength={200} onChange={upper('spokenTo')} />
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 6, md: fromEd ? 3 : 4 }}>
                <TextInput label="Seen by (S/B)" placeholder="e.g. DR KHONG" value={v.seenBy ?? ''} maxLength={200} onChange={upper('seenBy')} />
              </Grid.Col>
              <Grid.Col span={{ base: 12, md: 8 }}>
                <TextInput label="Patient name" required placeholder="e.g. MUHAMMAD HAZRIL BIN SYAMZUFARI" value={v.patientName} maxLength={200}
                  onChange={e => set('patientName', e.currentTarget.value.toUpperCase())} />
              </Grid.Col>
              <Grid.Col span={{ base: 12, md: 4 }}>
                <TextInput label="MyKad / KID / passport / reg no" required placeholder="e.g. 100711030571" value={v.patientIdNo}
                  maxLength={40} onChange={e => set('patientIdNo', e.currentTarget.value.toUpperCase())} />
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 6, md: 4 }}>
                <Input.Wrapper label="Gender" required>
                  <SegmentedControl fullWidth {...choice(v.gender)} value={v.gender} onChange={x => set('gender', x)} data={['MALE', 'FEMALE']} disabled={!v.department} />
                </Input.Wrapper>
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 6, md: 4 }}>
                <TextInput label="Age" placeholder="e.g. 16, 8 MONTHS" value={v.age ?? ''} maxLength={20} onChange={upper('age')} />
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 6, md: 4 }}>
                <Autocomplete label="Race" placeholder={pickOrType} data={RACES} value={v.race ?? ''}
                  onChange={x => set('race', x.toUpperCase() || null)} />
              </Grid.Col>
            </Grid>
            <Checkbox mt="md" label="Confidential patient (the ETAP form leaves out the name and registration number)"
              checked={v.confidential} onChange={e => set('confidential', e.currentTarget.checked)} />
          </Fieldset>
        </Fieldset>

        <Fieldset legend="Clinical">
          <Grid gap="md" align="flex-end">
            <Grid.Col span={{ base: 12, sm: 6, md: 4 }}>
              <Input.Wrapper label="Case type" required>
                <SegmentedControl fullWidth {...choice(v.caseType)} value={v.caseType ?? ''} onChange={x => set('caseType', x as CaseInput['caseType'])}
                  data={['TRAUMA', 'NON-TRAUMA']} />
              </Input.Wrapper>
            </Grid.Col>
            <Grid.Col span={{ base: 12, sm: 6, md: 8 }}>
              <Text size="sm" c="dimmed">
                {v.caseType ? (trauma ? 'Trauma: written as presented with (P/W).' : 'Non-trauma: written as complaint of (C/O).')
                  : 'Choose the case type to fill in the clinical details.'}
              </Text>
            </Grid.Col>
          </Grid>
          <Fieldset variant="unstyled" disabled={!v.caseType} mt="md">
            <Grid gap="md" align="flex-start">
              <Grid.Col span={{ base: 12, md: 8 }}>
                <TextInput label={trauma ? 'Presented with (P/W)' : 'Complaint of (C/O)'}
                  placeholder={trauma ? 'e.g. ALLEGED MB SKIDDED AROUND 10PM (21/11/2026)' : 'e.g. SWELLING LEFT CHEEK FOR 3 DAYS'}
                  value={v.presentation ?? ''} maxLength={500} onChange={upper('presentation')} />
              </Grid.Col>
              <Grid.Col span={{ base: 12, md: 4 }}>
                <TagsInput label="Investigations (Ix)" placeholder="e.g. OPG, PA SKULL (Enter to add)" data={INVESTIGATIONS} clearable
                  disabled={!v.caseType} value={v.investigations ? v.investigations.split(', ') : []}
                  onChange={xs => set('investigations', xs.map(x => x.trim().toUpperCase()).filter(Boolean).join(', ') || null)} />
              </Grid.Col>
              <Grid.Col span={12}>
                <Textarea label="Diagnosis (Dx)" autosize minRows={4} maxLength={500} value={v.diagnosis ?? ''} onChange={upper('diagnosis')}
                  placeholder={'e.g. OFSTI\nTRAUMA DENTAL INJURY\n- AVULSED TOOTH 11-13\n- TOOTH 21-22 COMPLICATED CROWN #\n- CLOSED RIGHT CONDYLAR #'} />
              </Grid.Col>
              <Grid.Col span={{ base: 12, md: 6 }}>
                <Textarea label="Management (Mx)" autosize minRows={3} maxLength={1000} value={v.management ?? ''} onChange={upper('management')}
                  placeholder={'e.g. T&S DONE\nARCHBAR ON MAXILLA AND MANDIBLE\nGIC 21 22'} />
              </Grid.Col>
              <Grid.Col span={{ base: 12, md: 6 }}>
                <Textarea label="Prescription (Rx)" autosize minRows={3} maxLength={500} value={v.prescription ?? ''} onChange={upper('prescription')}
                  placeholder="e.g. T. PCM, T. ARCOXIA, T. AUGMENTIN, 0.2% CHX M/W, CMC OINTMENT" />
              </Grid.Col>
              <Grid.Col span={12}>
                <Textarea label="Plan" autosize minRows={3} maxLength={1000} value={v.plan ?? ''} onChange={upper('plan')}
                  placeholder={'e.g. 1. TO DISCHARGE DENTALLY\n2. TO CONT. OTHER TEAM PLAN AND MANAGEMENT\n3. TCA 23/11/2026 @ 9.30AM AT OMFS HTF'} />
              </Grid.Col>
            </Grid>
          </Fieldset>
        </Fieldset>

        <Group justify="flex-end">
          {onCancel && <Button variant="default" onClick={onCancel}>Cancel</Button>}
          <Button type="submit" loading={save.isPending} disabled={!ready}>{editing ? 'Save' : 'Record case'}</Button>
        </Group>
      </Stack>
      {shareId && <WhatsAppModal caseId={shareId} onClose={() => setShareId(null)} />}
    </form>
  )
}

/** The case as the WhatsApp passover message (seniors' template), ready to copy into the department group. */
function WhatsAppModal({ caseId, onClose }: { caseId: string; onClose: () => void }) {
  const text = useQuery({ queryKey: ['case-whatsapp', caseId], queryFn: () => api.cases.whatsapp(caseId), gcTime: 0 })
  return (
    <Modal opened onClose={onClose} title="WhatsApp passover" size="lg">
      {text.isLoading ? <Loading /> : text.error ? <ErrorBox error={text.error} /> : (
        <Stack>
          <Text size="sm" c="dimmed">Copy and paste it into the department group. Check it before sending: it contains patient details.</Text>
          <Textarea readOnly autosize minRows={8} maxRows={20} value={text.data?.text ?? ''} />
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>Close</Button>
            <CopyButton value={text.data?.text ?? ''}>
              {({ copied, copy }) => (
                <Button color={copied ? 'teal' : 'green'} leftSection={copied ? <IconCheck size={16} /> : <IconCopy size={16} />} onClick={copy}>
                  {copied ? 'Copied' : 'Copy text'}
                </Button>
              )}
            </CopyButton>
          </Group>
        </Stack>
      )}
    </Modal>
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
  const [shareId, setShareId] = useState<string | null>(null)
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
          <Menu position="bottom-end" withinPortal>
            <Menu.Target>
              <Button variant="default" leftSection={<IconDownload size={16} />} rightSection={<IconChevronDown size={14} />}>Excel</Button>
            </Menu.Target>
            <Menu.Dropdown>
              <Menu.Label>{personId ? 'This officer’s cases' : 'All cases'}</Menu.Label>
              <Menu.Item component="a" href={api.cases.exportUrl(ym, personId ?? undefined)}>{formatMonth(month)}</Menu.Item>
              <Menu.Item component="a" href={api.cases.exportUrl(ym, personId ?? undefined, Number(month.slice(0, 4)))}>
                Whole of {month.slice(0, 4)}
              </Menu.Item>
            </Menu.Dropdown>
          </Menu>
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
                      <Group gap={4} wrap="nowrap" justify="flex-end">
                        {c.canShare && (
                          <Tooltip label="WhatsApp passover">
                            <ActionIcon variant="subtle" color="green" aria-label="WhatsApp passover" onClick={() => setShareId(c.id)}>
                              <IconBrandWhatsapp size={18} />
                            </ActionIcon>
                          </Tooltip>
                        )}
                        {c.canEdit && (<>
                          <Tooltip label="Edit"><ActionIcon variant="subtle" aria-label="Edit" onClick={() => setEditing(c)}><IconEdit size={18} /></ActionIcon></Tooltip>
                          <Tooltip label="Remove">
                            <ActionIcon variant="subtle" color="red" aria-label="Remove" onClick={() => modals.openConfirmModal({
                              title: 'Remove this case?', children: <Text size="sm">It disappears from the log and the ETAP form.</Text>,
                              labels: { confirm: 'Remove', cancel: 'Cancel' }, confirmProps: { color: 'red' }, onConfirm: () => remove.mutate(c.id),
                            })}><IconTrash size={18} /></ActionIcon>
                          </Tooltip>
                        </>)}
                      </Group>
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
      {shareId && <WhatsAppModal caseId={shareId} onClose={() => setShareId(null)} />}
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
