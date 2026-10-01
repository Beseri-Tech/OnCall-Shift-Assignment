import {
  ActionIcon, Anchor, Badge, Button, Checkbox, Group, Modal, NumberInput, Paper, SegmentedControl, Select, Stack,
  Table, Text, TextInput, Tooltip,
} from '@mantine/core'
import { DatePickerInput } from '@mantine/dates'
import { modals } from '@mantine/modals'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  IconDoorExit, IconEdit, IconFileImport, IconFilterOff, IconMapPin, IconPlayerPause, IconSearch, IconTrash, IconUserCheck, IconUserPlus,
} from '@tabler/icons-react'
import { useMemo, useState } from 'react'
import { api, type AccountRole, type AdminPerson, type Clinic, type InviteResult, type OfficerStatus, type UpsertPerson } from '../../api'
import { ErrorBox, Loading } from '../../components/common'
import { formatLong, todayIso } from '../../dates'
import { notifyError, notifyOk } from '../../lib'
import { ImportModal } from './ImportModal'
import { LocationsModal } from './LocationsModal'
import { type IssuedLogin, TempPasswordsModal } from './TempPasswords'

const blank: UpsertPerson = {
  name: '', code: null, status: 'OnCall', extraShift: false, preferWeekendHoliday: false, weekendWeight: 1,
  statusReason: null, excludedUntil: null, clinicId: null, phone: null, email: null, role: 'Officer',
}

const STATUS_LABEL: Record<OfficerStatus, string> = { OnCall: 'On call', Excluded: 'Excluded', Left: 'Left' }
const STATUS_COLOR: Record<OfficerStatus, string> = { OnCall: 'green', Excluded: 'orange', Left: 'gray' }

type StatusFilter = OfficerStatus | 'All'

/** Sign-in state of an officer, for the account filter. */
type AccountFilter = 'none' | 'pending' | 'active' | 'disabled' | 'admin'
const ACCOUNT_FILTERS: { value: AccountFilter; label: string }[] = [
  { value: 'none', label: 'No account (not invited)' },
  { value: 'pending', label: 'Invited, not signed in' },
  { value: 'active', label: 'Signed in' },
  { value: 'disabled', label: 'Account disabled' },
  { value: 'admin', label: 'Admins' },
]
const accountMatches = (p: AdminPerson, f: AccountFilter) =>
  f === 'none' ? !p.email
  : f === 'pending' ? !!p.email && p.accountEnabled && p.invitePending
  : f === 'active' ? !!p.email && p.accountEnabled && !p.invitePending
  : f === 'disabled' ? !!p.email && !p.accountEnabled
  : p.role === 'Admin'

type FlagFilter = 'weekends' | 'extra' | 'weight'
const FLAG_FILTERS: { value: FlagFilter; label: string }[] = [
  { value: 'weekends', label: 'Prefers weekends' },
  { value: 'extra', label: 'Takes extra shifts' },
  { value: 'weight', label: 'Weekend weight above 1' },
]
const flagMatches = (p: AdminPerson, f: FlagFilter) =>
  f === 'weekends' ? p.preferWeekendHoliday : f === 'extra' ? p.extraShift : p.weekendWeight > 1

/** Clinic filter value for officers without a clinic. */
const NO_CLINIC = 'none'

// Sort key that puts officers without a state/district/clinic last.
const last = (s: string | null) => s ?? '￿'

export function PeopleTab() {
  const qc = useQueryClient()
  const people = useQuery({ queryKey: ['admin', 'people'], queryFn: api.admin.people })
  const clinics = useQuery({ queryKey: ['admin', 'clinics'], queryFn: api.admin.clinics })
  const states = useQuery({ queryKey: ['admin', 'states'], queryFn: api.admin.states })
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState<StatusFilter>('OnCall')
  const [stateId, setStateId] = useState<string | null>(null)
  const [districtId, setDistrictId] = useState<string | null>(null)
  const [clinicId, setClinicId] = useState<string | null>(null)
  const [account, setAccount] = useState<AccountFilter | null>(null)
  const [flag, setFlag] = useState<FlagFilter | null>(null)
  const filtered = !!(search.trim() || stateId || districtId || clinicId || account || flag)
  const clearFilters = () => {
    setSearch(''); setStateId(null); setDistrictId(null); setClinicId(null); setAccount(null); setFlag(null)
  }
  const [editing, setEditing] = useState<{ person: AdminPerson | null; value: UpsertPerson } | null>(null)
  const [changing, setChanging] = useState<{ person: AdminPerson; to: 'Excluded' | 'Left' } | null>(null)
  const [importOpen, setImportOpen] = useState(false)
  const [locationsOpen, setLocationsOpen] = useState(false)
  const [issued, setIssued] = useState<IssuedLogin[]>([])

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ['admin', 'people'] })
    qc.invalidateQueries({ queryKey: ['admin', 'clinics'] })
    qc.invalidateQueries({ queryKey: ['admin', 'states'] })
    qc.invalidateQueries({ queryKey: ['admin', 'accounts'] })
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

  // Cascading choices: districts of the chosen state, clinics of the chosen district (or state).
  const districtOptions = useMemo(() => (states.data ?? []).filter(s => !stateId || s.id === stateId)
    .map(s => ({ group: s.name, items: s.districts.map(d => ({ value: d.id, label: d.name })) })), [states.data, stateId])
  const clinicChoices = useMemo(() => {
    const stateOf = new Map((states.data ?? []).flatMap(s => s.districts.map(d => [d.id, s.id] as const)))
    const list = (clinics.data ?? []).filter(c => (!districtId || c.districtId === districtId)
      && (!stateId || stateOf.get(c.districtId) === stateId))
    return [{ value: NO_CLINIC, label: 'No clinic' }, ...list.map(c => ({ value: c.id, label: c.name }))]
  }, [clinics.data, states.data, stateId, districtId])

  const counts = useMemo(() => {
    const c: Record<OfficerStatus, number> = { OnCall: 0, Excluded: 0, Left: 0 }
    for (const p of people.data ?? []) c[p.status]++
    return c
  }, [people.data])

  const rows = useMemo(() => {
    const s = search.trim().toLowerCase()
    return (people.data ?? [])
      .filter(p => (status === 'All' || p.status === status)
        && (!stateId || p.stateId === stateId)
        && (!districtId || p.districtId === districtId)
        && (!clinicId || (clinicId === NO_CLINIC ? !p.clinicId : p.clinicId === clinicId))
        && (!account || accountMatches(p, account))
        && (!flag || flagMatches(p, flag))
        && (!s || p.name.toLowerCase().includes(s) || p.code.toLowerCase().includes(s) || p.email?.includes(s)
          || p.phone?.replace(/\D/g, '').includes(s.replace(/\D/g, '') || '-')))
      // Like the sheet: grouped by state, district, then clinic.
      .sort((a, b) => last(a.state).localeCompare(last(b.state))
        || last(a.district).localeCompare(last(b.district))
        || last(a.clinicName).localeCompare(last(b.clinicName))
        || a.sortOrder - b.sortOrder)
  }, [people.data, search, status, stateId, districtId, clinicId, account, flag])

  if (people.isLoading || clinics.isLoading || states.isLoading) return <Loading />
  if (people.error || clinics.error || states.error) return <ErrorBox error={people.error ?? clinics.error ?? states.error} />

  const onInvited = (name: string, email: string, invite: InviteResult) => {
    if (invite.tempPassword) setIssued([{ name, email, tempPassword: invite.tempPassword }])
    else notifyOk(`Invite emailed to ${email}.`)
  }

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
          <Button variant="default" leftSection={<IconMapPin size={16} />} onClick={() => setLocationsOpen(true)}>Clinics &amp; districts</Button>
          <Button variant="default" leftSection={<IconFileImport size={16} />} onClick={() => setImportOpen(true)}>Import CSV</Button>
          <Button leftSection={<IconUserPlus size={16} />} onClick={() => setEditing({ person: null, value: blank })}>Add officer</Button>
        </Group>
      </Group>

      <Group wrap="wrap">
        <TextInput placeholder="Name, code, email, phone" leftSection={<IconSearch size={16} />} value={search}
          onChange={e => setSearch(e.currentTarget.value)} w={260} />
        <Select placeholder="All states" clearable w={150} value={stateId}
          data={(states.data ?? []).map(st => ({ value: st.id, label: st.name }))}
          onChange={v => { setStateId(v); setDistrictId(null); setClinicId(null) }} />
        <Select placeholder="All districts" clearable searchable w={170} value={districtId} data={districtOptions}
          onChange={v => { setDistrictId(v); setClinicId(null) }} />
        <Select placeholder="All clinics" clearable searchable w={200} value={clinicId} data={clinicChoices} onChange={setClinicId} />
        <Select placeholder="Any sign-in" clearable w={220} value={account} data={ACCOUNT_FILTERS}
          onChange={v => setAccount(v as AccountFilter | null)} />
        <Select placeholder="Any preference" clearable w={200} value={flag} data={FLAG_FILTERS}
          onChange={v => setFlag(v as FlagFilter | null)} />
        {filtered && <Button variant="subtle" size="compact-sm" leftSection={<IconFilterOff size={14} />} onClick={clearFilters}>Clear filters</Button>}
        <Text size="sm" c="dimmed">{rows.length} of {people.data!.length} shown</Text>
      </Group>

      <Paper withBorder>
        <Table.ScrollContainer minWidth={900}>
          <Table striped highlightOnHover verticalSpacing={6}>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Code</Table.Th>
                <Table.Th>Name</Table.Th>
                <Table.Th>Clinic</Table.Th>
                <Table.Th>Sign-in</Table.Th>
                <Table.Th>Phone</Table.Th>
                <Table.Th>Status</Table.Th>
                <Table.Th>Flags</Table.Th>
                <Table.Th />
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {rows.length === 0 && (
                <Table.Tr><Table.Td colSpan={8}><Text c="dimmed" ta="center" py="md">No officers match.</Text></Table.Td></Table.Tr>
              )}
              {rows.map(p => (
                <Table.Tr key={p.id} opacity={p.status === 'Left' ? 0.55 : 1}>
                  <Table.Td><Badge variant="outline" color="gray">{p.code}</Badge></Table.Td>
                  <Table.Td fw={600}>{p.name}</Table.Td>
                  <Table.Td>
                    {p.clinicName
                      ? <>{p.clinicName}<Text size="xs" c="dimmed">{p.district}, {p.state}</Text></>
                      : <Text span c="dimmed">–</Text>}
                  </Table.Td>
                  <Table.Td>
                    {p.email
                      ? <>
                          <Text size="sm">{p.email}</Text>
                          <Group gap={4}>
                            {!p.accountEnabled ? <Badge size="xs" color="gray" variant="light">Disabled</Badge>
                              : p.invitePending && <Badge size="xs" color="orange" variant="light">Invited</Badge>}
                            {p.role === 'Admin' && <Badge size="xs" color="violet" variant="light">Admin</Badge>}
                          </Group>
                        </>
                      : <Text span size="sm" c="dimmed">No account</Text>}
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
                      <ActionIcon variant="subtle" aria-label="Edit" onClick={() => setEditing({ person: p, value: toUpsert(p) })}>
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

      {editing && (
        <PersonModal editing={editing} clinics={clinics.data!} onClose={() => setEditing(null)} onSaved={refresh}
          onInvited={onInvited} />
      )}
      {changing && <StatusModal {...changing} onClose={() => setChanging(null)} onSaved={refresh} />}
      {importOpen && (
        <ImportModal clinics={clinics.data!} onClose={() => setImportOpen(false)} onSaved={refresh} onIssued={setIssued} />
      )}
      <LocationsModal opened={locationsOpen} states={states.data!} clinics={clinics.data!} onClose={() => setLocationsOpen(false)}
        onSaved={refresh} />
      <TempPasswordsModal logins={issued} onClose={() => setIssued([])} />
    </Stack>
  )
}

const toUpsert = (p: AdminPerson): UpsertPerson => ({
  name: p.name, code: p.code, status: p.status, extraShift: p.extraShift, preferWeekendHoliday: p.preferWeekendHoliday,
  weekendWeight: p.weekendWeight, statusReason: p.statusReason, excludedUntil: p.excludedUntil,
  clinicId: p.clinicId, phone: p.phone, email: null, role: p.role ?? 'Officer',
})

/** Clinics grouped under "District, State". */
const clinicOptions = (clinics: Clinic[]) =>
  [...new Set(clinics.map(c => `${c.district}, ${c.state}`))].map(group => ({
    group, items: clinics.filter(c => `${c.district}, ${c.state}` === group).map(c => ({ value: c.id, label: c.name })),
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

function PersonModal({ editing, clinics, onClose, onSaved, onInvited }: {
  editing: { person: AdminPerson | null; value: UpsertPerson }; clinics: Clinic[]; onClose: () => void; onSaved: () => void
  onInvited: (name: string, email: string, invite: InviteResult) => void
}) {
  const { person } = editing
  const [v, setV] = useState(editing.value)
  const set = <K extends keyof UpsertPerson>(k: K, value: UpsertPerson[K]) => setV(s => ({ ...s, [k]: value }))
  const canInvite = !person?.email && v.status !== 'Left'
  const email = canInvite ? v.email?.trim() || null : null

  const save = useMutation({
    mutationFn: () => {
      const body = { ...v, email }
      return person ? api.admin.updatePerson(person.id, body) : api.admin.createPerson(body)
    },
    onSuccess: r => {
      onSaved()
      onClose()
      if (r.invite && email) onInvited(v.name, email.toLowerCase(), r.invite)
      else notifyOk(`${v.name} saved.`)
    },
  })

  return (
    <Modal opened onClose={onClose} title={person ? 'Edit officer' : 'Add officer'} size="lg">
      <form onSubmit={e => { e.preventDefault(); save.mutate() }}>
        <Stack>
          <Group grow align="flex-start">
            <TextInput label="Name" required value={v.name} onChange={e => set('name', e.currentTarget.value)} data-autofocus />
            <TextInput label="Code" description="Leave blank to assign the next D-number" value={v.code ?? ''}
              onChange={e => set('code', e.currentTarget.value || null)} maxLength={32} />
          </Group>
          <Group grow align="flex-start">
            <Select label="Clinic" required={!person}
              placeholder={clinics.length ? 'Pick a clinic' : 'Add clinics first (Clinics & districts)'}
              data={clinicOptions(clinics)} value={v.clinicId} onChange={id => set('clinicId', id)} clearable={!!person} searchable />
            <TextInput label="Phone" value={v.phone ?? ''} onChange={e => set('phone', e.currentTarget.value || null)} maxLength={32} />
          </Group>
          {person?.email ? (
            <TextInput label="Sign-in email" value={person.email} disabled
              description="Already invited. Change the email or role on the Accounts tab." />
          ) : canInvite && (
            <Group grow align="flex-start">
              <TextInput label="Email" type="email" value={v.email ?? ''} onChange={e => set('email', e.currentTarget.value || null)}
                description="Optional. Sends them an invite with a temporary password." maxLength={254} />
              <Stack gap={4}>
                <Text fw={500} size="sm">Account</Text>
                <SegmentedControl value={v.role} onChange={x => set('role', x as AccountRole)} disabled={!email} data={[
                  { value: 'Officer', label: 'Officer' },
                  { value: 'Admin', label: 'Admin (on call)' },
                ]} />
              </Stack>
            </Group>
          )}
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
          {save.error && <Text c="red" size="sm">{save.error.message}</Text>}
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>Cancel</Button>
            <Button type="submit" loading={save.isPending}
              disabled={!v.name.trim() || (!person && !v.clinicId) || (v.status !== 'OnCall' && !v.statusReason?.trim())}>
              {email ? (person ? 'Save and invite' : 'Add and invite') : 'Save'}
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  )
}
