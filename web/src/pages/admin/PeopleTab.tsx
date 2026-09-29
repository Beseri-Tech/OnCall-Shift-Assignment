import {
  ActionIcon, Anchor, Autocomplete, Badge, Button, Checkbox, Group, Modal, NumberInput, Paper, SegmentedControl, Select, Stack,
  Table, Text, TextInput, Textarea, Tooltip,
} from '@mantine/core'
import { DatePickerInput } from '@mantine/dates'
import { modals } from '@mantine/modals'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  IconBuildingHospital, IconDoorExit, IconDownload, IconEdit, IconPlayerPause, IconSearch, IconTrash, IconUserCheck,
  IconUserPlus, IconUsersPlus,
} from '@tabler/icons-react'
import { useMemo, useState } from 'react'
import { api, type AdminPerson, type Clinic, type OfficerStatus, type UpsertPerson } from '../../api'
import { ErrorBox, Loading } from '../../components/common'
import { formatLong, todayIso } from '../../dates'
import { notifyError, notifyOk } from '../../lib'

const blank: UpsertPerson = {
  name: '', code: null, status: 'OnCall', extraShift: false, preferWeekendHoliday: false, weekendWeight: 1,
  openingTotal: null, openingWeekday: null, openingWeekendHoliday: null,
  statusReason: null, excludedUntil: null, clinicId: null, phone: null,
}

const STATUS_LABEL: Record<OfficerStatus, string> = { OnCall: 'On call', Excluded: 'Excluded', Left: 'Left' }
const STATUS_COLOR: Record<OfficerStatus, string> = { OnCall: 'green', Excluded: 'orange', Left: 'gray' }

type StatusFilter = OfficerStatus | 'All'

// Sort key that puts officers without an area/clinic last.
const last = (s: string | null) => s ?? '￿'

export function PeopleTab() {
  const qc = useQueryClient()
  const people = useQuery({ queryKey: ['admin', 'people'], queryFn: api.admin.people })
  const clinics = useQuery({ queryKey: ['admin', 'clinics'], queryFn: api.admin.clinics })
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState<StatusFilter>('OnCall')
  const [area, setArea] = useState('All')
  const [clinicId, setClinicId] = useState<string | null>(null)
  const [editing, setEditing] = useState<{ id: string | null; value: UpsertPerson } | null>(null)
  const [changing, setChanging] = useState<{ person: AdminPerson; to: 'Excluded' | 'Left' } | null>(null)
  const [bulkOpen, setBulkOpen] = useState(false)
  const [clinicsOpen, setClinicsOpen] = useState(false)

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ['admin', 'people'] })
    qc.invalidateQueries({ queryKey: ['admin', 'clinics'] })
    qc.invalidateQueries({ queryKey: ['people'] })
  }

  const remove = useMutation({
    mutationFn: (id: string) => api.admin.deletePerson(id),
    onSuccess: () => { refresh(); notifyOk('Officer removed.') },
    onError: e => notifyError(e, 'Could not remove'),
  })

  const backOnCall = useMutation({
    mutationFn: (p: AdminPerson) => api.admin.updatePerson(p.id, { ...toUpsert(p), status: 'OnCall' }),
    onSuccess: (_, p) => { refresh(); notifyOk(`${p.name} is back on call.`) },
    onError: e => notifyError(e),
  })

  const areas = useMemo(() => [...new Set((clinics.data ?? []).map(c => c.area))], [clinics.data])

  const counts = useMemo(() => {
    const c: Record<OfficerStatus, number> = { OnCall: 0, Excluded: 0, Left: 0 }
    for (const p of people.data ?? []) c[p.status]++
    return c
  }, [people.data])

  const rows = useMemo(() => {
    const s = search.trim().toLowerCase()
    return (people.data ?? [])
      .filter(p => (status === 'All' || p.status === status) && (area === 'All' || p.area === area)
        && (!clinicId || p.clinicId === clinicId)
        && (!s || p.name.toLowerCase().includes(s) || p.code.toLowerCase().includes(s)))
      // Like the sheet: grouped by area, then clinic.
      .sort((a, b) => last(a.area).localeCompare(last(b.area))
        || last(a.clinicName).localeCompare(last(b.clinicName))
        || a.sortOrder - b.sortOrder)
  }, [people.data, search, status, area, clinicId])

  if (people.isLoading || clinics.isLoading) return <Loading />
  if (people.error || clinics.error) return <ErrorBox error={people.error ?? clinics.error} />

  const today = todayIso()

  return (
    <Stack gap="md">
      <Group justify="space-between" wrap="wrap">
        <SegmentedControl value={status} onChange={v => setStatus(v as StatusFilter)} data={[
          { value: 'OnCall', label: `On call (${counts.OnCall})` },
          { value: 'Excluded', label: `Excluded (${counts.Excluded})` },
          { value: 'Left', label: `Left (${counts.Left})` },
          { value: 'All', label: 'All' },
        ]} />
        <Group>
          <Button variant="default" leftSection={<IconDownload size={16} />} component="a" href={api.admin.exports.totals()}>
            Totals (Excel)
          </Button>
          <Button variant="default" leftSection={<IconBuildingHospital size={16} />} onClick={() => setClinicsOpen(true)}>Clinics</Button>
          <Button variant="default" leftSection={<IconUsersPlus size={16} />} onClick={() => setBulkOpen(true)}>Add many</Button>
          <Button leftSection={<IconUserPlus size={16} />} onClick={() => setEditing({ id: null, value: blank })}>Add officer</Button>
        </Group>
      </Group>

      <Group wrap="wrap">
        <TextInput placeholder="Find name or code" leftSection={<IconSearch size={16} />} value={search}
          onChange={e => setSearch(e.currentTarget.value)} w={240} />
        {areas.length > 0 && (
          <SegmentedControl value={area} onChange={v => { setArea(v); setClinicId(null) }}
            data={['All', ...areas].map(a => ({ value: a, label: a === 'All' ? 'All areas' : a }))} />
        )}
        <Select placeholder="All clinics" clearable w={220} value={clinicId} onChange={setClinicId}
          data={(clinics.data ?? []).filter(c => area === 'All' || c.area === area).map(c => ({ value: c.id, label: c.name }))} />
        <Text size="sm" c="dimmed">{rows.length} shown</Text>
      </Group>

      <Paper withBorder>
        <Table.ScrollContainer minWidth={1100}>
          <Table striped highlightOnHover verticalSpacing={6}>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Code</Table.Th>
                <Table.Th>Name</Table.Th>
                <Table.Th>Clinic</Table.Th>
                <Table.Th>Phone</Table.Th>
                <Table.Th>Status</Table.Th>
                <Table.Th>Flags</Table.Th>
                <Table.Th ta="center">Opening<br /><Text span size="xs" c="dimmed">total / wkday / wkend+PH</Text></Table.Th>
                <Table.Th ta="center">Done so far<br /><Text span size="xs" c="dimmed">total / wkday / wkend+PH</Text></Table.Th>
                <Table.Th />
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {rows.length === 0 && (
                <Table.Tr><Table.Td colSpan={9}><Text c="dimmed" ta="center" py="md">No officers match.</Text></Table.Td></Table.Tr>
              )}
              {rows.map(p => (
                <Table.Tr key={p.id} opacity={p.status === 'Left' ? 0.55 : 1}>
                  <Table.Td><Badge variant="outline" color="gray">{p.code}</Badge></Table.Td>
                  <Table.Td fw={600}>{p.name}</Table.Td>
                  <Table.Td>
                    {p.clinicName
                      ? <>{p.clinicName}<Text size="xs" c="dimmed">{p.area}</Text></>
                      : <Text span c="dimmed">–</Text>}
                  </Table.Td>
                  <Table.Td>
                    {p.phone ? <Anchor href={`tel:${p.phone}`} size="sm">{p.phone}</Anchor> : <Text span c="dimmed">–</Text>}
                  </Table.Td>
                  <Table.Td>
                    <Badge color={STATUS_COLOR[p.status]} variant="light">{STATUS_LABEL[p.status]}</Badge>
                    {p.statusReason && <Text size="xs">{p.statusReason}</Text>}
                    {p.excludedUntil && (
                      <Text size="xs" c={p.excludedUntil <= today ? 'red' : 'dimmed'} fw={p.excludedUntil <= today ? 600 : undefined}>
                        {p.excludedUntil <= today ? 'Due back ' : 'Until '}{formatLong(p.excludedUntil)}
                      </Text>
                    )}
                  </Table.Td>
                  <Table.Td>
                    <Group gap={4}>
                      {p.preferWeekendHoliday && <Badge color="green" variant="light">Prefers weekends</Badge>}
                      {p.extraShift && <Badge color="violet" variant="light">Extra</Badge>}
                      {p.weekendWeight > 1 && <Badge variant="light">Weight {p.weekendWeight}</Badge>}
                    </Group>
                  </Table.Td>
                  <Table.Td ta="center" c="dimmed">
                    {[p.openingTotal, p.openingWeekday, p.openingWeekendHoliday].map(v => v ?? '–').join(' / ')}
                  </Table.Td>
                  <Table.Td ta="center">
                    <Tooltip label={p.totalsKnown ? 'Opening numbers + published rotas' : 'Unknown – the group average is used for weekend balancing'}>
                      <Text span fw={600} c={p.totalsKnown ? undefined : 'dimmed'}>
                        {p.totals.total} / {p.totals.weekday} / {p.totals.weekendHoliday}
                      </Text>
                    </Tooltip>
                  </Table.Td>
                  <Table.Td>
                    <Group gap={4} justify="flex-end" wrap="nowrap">
                      {p.status === 'OnCall' ? (
                        <>
                          <Tooltip label="Exclude for a while (e.g. maternity leave)">
                            <ActionIcon variant="subtle" color="orange" aria-label="Exclude"
                              onClick={() => setChanging({ person: p, to: 'Excluded' })}>
                              <IconPlayerPause size={18} />
                            </ActionIcon>
                          </Tooltip>
                          <Tooltip label="Mark as left (moved out / resigned)">
                            <ActionIcon variant="subtle" color="gray" aria-label="Mark as left"
                              onClick={() => setChanging({ person: p, to: 'Left' })}>
                              <IconDoorExit size={18} />
                            </ActionIcon>
                          </Tooltip>
                        </>
                      ) : (
                        <Tooltip label="Put back on call">
                          <ActionIcon variant="subtle" color="green" aria-label="Put back on call"
                            loading={backOnCall.isPending && backOnCall.variables?.id === p.id}
                            onClick={() => backOnCall.mutate(p)}>
                            <IconUserCheck size={18} />
                          </ActionIcon>
                        </Tooltip>
                      )}
                      <ActionIcon variant="subtle" aria-label="Edit" onClick={() => setEditing({ id: p.id, value: toUpsert(p) })}>
                        <IconEdit size={18} />
                      </ActionIcon>
                      <ActionIcon variant="subtle" color="red" aria-label="Delete" onClick={() => modals.openConfirmModal({
                        title: `Delete ${p.name}?`,
                        children: <Text size="sm">Only for mistakes – this also removes their leave. If they moved out, use "Mark as left" so their history is kept.</Text>,
                        labels: { confirm: 'Delete', cancel: 'Cancel' },
                        confirmProps: { color: 'red' },
                        onConfirm: () => remove.mutate(p.id),
                      })}>
                        <IconTrash size={18} />
                      </ActionIcon>
                    </Group>
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      </Paper>

      {editing && <PersonModal editing={editing} clinics={clinics.data!} onClose={() => setEditing(null)} onSaved={refresh} />}
      {changing && <StatusModal {...changing} onClose={() => setChanging(null)} onSaved={refresh} />}
      <BulkAddModal opened={bulkOpen} onClose={() => setBulkOpen(false)} onSaved={refresh} />
      <ClinicsModal opened={clinicsOpen} clinics={clinics.data!} onClose={() => setClinicsOpen(false)} onSaved={refresh} />
    </Stack>
  )
}

const toUpsert = (p: AdminPerson): UpsertPerson => ({
  name: p.name, code: p.code, status: p.status, extraShift: p.extraShift, preferWeekendHoliday: p.preferWeekendHoliday,
  weekendWeight: p.weekendWeight, openingTotal: p.openingTotal, openingWeekday: p.openingWeekday,
  openingWeekendHoliday: p.openingWeekendHoliday, statusReason: p.statusReason, excludedUntil: p.excludedUntil,
  clinicId: p.clinicId, phone: p.phone,
})

const num = (v: string | number) => (v === '' ? null : Number(v))

const clinicOptions = (clinics: Clinic[]) =>
  [...new Set(clinics.map(c => c.area))].map(area => ({
    group: area, items: clinics.filter(c => c.area === area).map(c => ({ value: c.id, label: c.name })),
  }))

/** Reason + (for Excluded) expected return date; shared by the edit form and the quick status modal. */
function StatusFields({ status, reason, until, onReason, onUntil }: {
  status: OfficerStatus; reason: string | null; until: string | null
  onReason: (v: string | null) => void; onUntil: (v: string | null) => void
}) {
  if (status === 'OnCall') return null
  return (
    <Group grow align="flex-start">
      <TextInput label="Reason" required data-autofocus value={reason ?? ''} onChange={e => onReason(e.currentTarget.value || null)}
        placeholder={status === 'Excluded' ? 'e.g. CUTI BERSALIN, PREGNANT' : 'e.g. PINDAH, RESIGN'} maxLength={200} />
      {status === 'Excluded' && (
        <DatePickerInput label="Expected back" description="Optional – a reminder only, not applied automatically"
          value={until} onChange={d => onUntil((d as string | null) ?? null)} valueFormat="D MMM YYYY" clearable />
      )}
    </Group>
  )
}

function StatusModal({ person, to, onClose, onSaved }: {
  person: AdminPerson; to: 'Excluded' | 'Left'; onClose: () => void; onSaved: () => void
}) {
  const [reason, setReason] = useState<string | null>(null)
  const [until, setUntil] = useState<string | null>(null)
  const save = useMutation({
    mutationFn: () => api.admin.updatePerson(person.id, { ...toUpsert(person), status: to, statusReason: reason, excludedUntil: until }),
    onSuccess: () => { onSaved(); onClose(); notifyOk(`${person.name} ${to === 'Left' ? 'marked as left' : 'excluded from the rota'}.`) },
  })

  return (
    <Modal opened onClose={onClose} title={`${to === 'Left' ? 'Mark as left' : 'Exclude'}: ${person.name}`} size="lg">
      <form onSubmit={e => { e.preventDefault(); save.mutate() }}>
        <Stack>
          <Text size="sm" c="dimmed">
            {to === 'Left'
              ? 'They leave the rota and the leave page. Past shifts stay in history and totals.'
              : 'They stay in the list but are not put on the rota until you put them back on call.'}
          </Text>
          <StatusFields status={to} reason={reason} until={until} onReason={setReason} onUntil={setUntil} />
          {save.error && <Text c="red" size="sm">{save.error.message}</Text>}
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>Cancel</Button>
            <Button type="submit" color={to === 'Left' ? 'gray' : 'orange'} loading={save.isPending} disabled={!reason?.trim()}>
              {to === 'Left' ? 'Mark as left' : 'Exclude'}
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  )
}

function PersonModal({ editing, clinics, onClose, onSaved }: {
  editing: { id: string | null; value: UpsertPerson }; clinics: Clinic[]; onClose: () => void; onSaved: () => void
}) {
  const [v, setV] = useState(editing.value)
  const set = <K extends keyof UpsertPerson>(k: K, value: UpsertPerson[K]) => setV(s => ({ ...s, [k]: value }))

  const save = useMutation({
    mutationFn: () => (editing.id ? api.admin.updatePerson(editing.id, v) : api.admin.createPerson(v).then(() => undefined)),
    onSuccess: () => { onSaved(); onClose(); notifyOk(`${v.name} saved.`) },
  })

  return (
    <Modal opened onClose={onClose} title={editing.id ? 'Edit officer' : 'Add officer'} size="lg">
      <form onSubmit={e => { e.preventDefault(); save.mutate() }}>
        <Stack>
          <Group grow align="flex-start">
            <TextInput label="Name" required value={v.name} onChange={e => set('name', e.currentTarget.value)} data-autofocus />
            <TextInput label="Code" description="Leave blank to assign the next D-number" value={v.code ?? ''}
              onChange={e => set('code', e.currentTarget.value || null)} maxLength={32} />
          </Group>
          <Group grow align="flex-start">
            <Select label="Clinic" placeholder={clinics.length ? 'Pick a clinic' : 'Add clinics first (Clinics button)'}
              data={clinicOptions(clinics)} value={v.clinicId} onChange={id => set('clinicId', id)} clearable searchable />
            <TextInput label="Phone" value={v.phone ?? ''} onChange={e => set('phone', e.currentTarget.value || null)} maxLength={32} />
          </Group>
          <Stack gap={4}>
            <Text fw={500} size="sm">Status</Text>
            <SegmentedControl value={v.status} onChange={x => set('status', x as OfficerStatus)} data={[
              { value: 'OnCall', label: 'On call' },
              { value: 'Excluded', label: 'Excluded (temporary)' },
              { value: 'Left', label: 'Left' },
            ]} />
          </Stack>
          <StatusFields status={v.status} reason={v.statusReason} until={v.excludedUntil}
            onReason={x => set('statusReason', x)} onUntil={x => set('excludedUntil', x)} />
          <Group>
            <Checkbox label="Prefers weekends / public holidays" checked={v.preferWeekendHoliday}
              onChange={e => set('preferWeekendHoliday', e.currentTarget.checked)} />
            <Checkbox label="Takes extra shifts" checked={v.extraShift} onChange={e => set('extraShift', e.currentTarget.checked)} />
          </Group>
          <NumberInput label="Weekend weight" description="Higher = larger share of weekend/PH shifts (1–5)" min={1} max={5}
            value={v.weekendWeight} onChange={x => set('weekendWeight', Number(x) || 1)} w={200} />
          <Text fw={600} size="sm" mt="xs">Opening numbers (shifts done before this app)</Text>
          <Text size="xs" c="dimmed" mt={-10}>Leave blank if unknown – the group average is used until their first published rota.</Text>
          <Group grow>
            <NumberInput label="Total" min={0} value={v.openingTotal ?? ''} onChange={x => set('openingTotal', num(x))} />
            <NumberInput label="Weekday" min={0} value={v.openingWeekday ?? ''} onChange={x => set('openingWeekday', num(x))} />
            <NumberInput label="Weekend + PH" min={0} value={v.openingWeekendHoliday ?? ''}
              onChange={x => set('openingWeekendHoliday', num(x))} />
          </Group>
          {save.error && <Text c="red" size="sm">{save.error.message}</Text>}
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>Cancel</Button>
            <Button type="submit" loading={save.isPending}
              disabled={!v.name.trim() || (v.status !== 'OnCall' && !v.statusReason?.trim())}>Save</Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  )
}

function ClinicsModal({ opened, clinics, onClose, onSaved }: {
  opened: boolean; clinics: Clinic[]; onClose: () => void; onSaved: () => void
}) {
  const [form, setForm] = useState<{ id: string | null; name: string; area: string }>({ id: null, name: '', area: '' })
  const reset = () => setForm({ id: null, name: '', area: '' })

  const save = useMutation({
    mutationFn: () => (form.id ? api.admin.updateClinic(form.id, form) : api.admin.createClinic(form).then(() => undefined)),
    onSuccess: () => { onSaved(); notifyOk(`${form.name} saved.`); reset() },
    onError: e => notifyError(e, 'Could not save clinic'),
  })
  const remove = useMutation({
    mutationFn: (id: string) => api.admin.deleteClinic(id),
    onSuccess: () => { onSaved(); notifyOk('Clinic removed.') },
    onError: e => notifyError(e, 'Could not remove clinic'),
  })

  return (
    <Modal opened={opened} onClose={() => { reset(); onClose() }} title="Clinics" size="lg">
      <Stack>
        <Table striped verticalSpacing={4}>
          <Table.Thead>
            <Table.Tr><Table.Th>Clinic</Table.Th><Table.Th>Area</Table.Th><Table.Th ta="center">Officers</Table.Th><Table.Th /></Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {clinics.length === 0 && (
              <Table.Tr><Table.Td colSpan={4}><Text c="dimmed" ta="center" size="sm">No clinics yet.</Text></Table.Td></Table.Tr>
            )}
            {clinics.map(c => (
              <Table.Tr key={c.id}>
                <Table.Td fw={600}>{c.name}</Table.Td>
                <Table.Td>{c.area}</Table.Td>
                <Table.Td ta="center">{c.people}</Table.Td>
                <Table.Td>
                  <Group gap={4} justify="flex-end" wrap="nowrap">
                    <ActionIcon variant="subtle" aria-label="Edit" onClick={() => setForm({ id: c.id, name: c.name, area: c.area })}>
                      <IconEdit size={18} />
                    </ActionIcon>
                    <ActionIcon variant="subtle" color="red" aria-label="Delete" onClick={() => modals.openConfirmModal({
                      title: `Remove ${c.name}?`,
                      children: <Text size="sm">{c.people ? `${c.people} officer(s) will be left without a clinic.` : 'No officers use it.'}</Text>,
                      labels: { confirm: 'Remove', cancel: 'Cancel' },
                      confirmProps: { color: 'red' },
                      onConfirm: () => remove.mutate(c.id),
                    })}>
                      <IconTrash size={18} />
                    </ActionIcon>
                  </Group>
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
        <form onSubmit={e => { e.preventDefault(); save.mutate() }}>
          <Group align="flex-end">
            <TextInput label={form.id ? 'Edit clinic' : 'New clinic'} placeholder="KP BESERI" value={form.name}
              onChange={e => setForm({ ...form, name: e.currentTarget.value })} maxLength={100} style={{ flex: 1 }} />
            <Autocomplete label="Area" placeholder="Kangar" value={form.area} onChange={area => setForm({ ...form, area })}
              data={[...new Set(clinics.map(c => c.area))]} maxLength={50} w={160} />
            <Button type="submit" loading={save.isPending} disabled={!form.name.trim() || !form.area.trim()}>
              {form.id ? 'Save' : 'Add'}
            </Button>
            {form.id && <Button variant="default" onClick={reset}>Cancel</Button>}
          </Group>
        </form>
      </Stack>
    </Modal>
  )
}

function BulkAddModal({ opened, onClose, onSaved }: { opened: boolean; onClose: () => void; onSaved: () => void }) {
  const [names, setNames] = useState('')
  const add = useMutation({
    mutationFn: () => api.admin.bulkAdd(names),
    onSuccess: r => {
      onSaved()
      onClose()
      setNames('')
      notifyOk(`${r.added.length} added${r.skipped.length ? `, ${r.skipped.length} already in the list` : ''}.`)
    },
    onError: e => notifyError(e),
  })
  const count = names.split('\n').filter(n => n.trim()).length

  return (
    <Modal opened={opened} onClose={onClose} title="Add many officers" size="lg">
      <Stack>
        <Textarea label="Names, one per line" autosize minRows={8} maxRows={18} value={names}
          onChange={e => setNames(e.currentTarget.value)} placeholder={'Dr Ahmad bin Ali\nDr Siti binti Abu'} />
        <Group justify="space-between">
          <Text size="sm" c="dimmed">{count} name(s). Existing names are skipped; codes are assigned automatically.</Text>
          <Group>
            <Button variant="default" onClick={onClose}>Cancel</Button>
            <Button onClick={() => add.mutate()} loading={add.isPending} disabled={!count}>Add</Button>
          </Group>
        </Group>
      </Stack>
    </Modal>
  )
}
