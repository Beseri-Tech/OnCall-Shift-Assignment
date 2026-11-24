import { ActionIcon, Badge, Button, Grid, Group, Modal, NavLink, Paper, ScrollArea, Select, Stack, Table, Text, TextInput } from '@mantine/core'
import { modals } from '@mantine/modals'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconBuildingHospital, IconEdit, IconMapPin, IconPlus, IconSearch, IconTrash } from '@tabler/icons-react'
import { useMemo, useState } from 'react'
import { api, type Clinic, type State } from '../../api'
import { isMaster, useMe } from '../../auth'
import { ErrorBox, Loading } from '../../components/common'
import { notifyError, notifyOk } from '../../lib'

type Level = 'state' | 'district' | 'clinic'

/** What the add/edit dialog is about. parentId: the state of a district, the district of a clinic. */
interface Form { level: Level; id: string | null; name: string; parentId: string | null }

/** Which part of the tree the clinic list shows. */
type Scope = { kind: 'all' } | { kind: 'state'; id: string } | { kind: 'district'; id: string }

const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? '' : 's'}`

/**
 * State -> district -> clinic, where officers work. Clinics are shared by every hospital; states and districts are the
 * masters' to change. Officer counts are for the current hospital.
 */
export function LocationsTab() {
  const qc = useQueryClient()
  const me = useMe().data
  const master = isMaster(me)
  const states = useQuery({ queryKey: ['admin', 'states'], queryFn: api.admin.states })
  const clinics = useQuery({ queryKey: ['admin', 'clinics'], queryFn: api.admin.clinics })
  const facilities = useQuery({ queryKey: ['facilities'], queryFn: api.facilities })
  const [picked, setPicked] = useState<Scope | null>(null)
  const [search, setSearch] = useState('')
  const [form, setForm] = useState<Form | null>(null)

  // Start on the state of the hospital being worked in: that's where its officers' clinics are.
  const homeState = facilities.data?.find(f => f.id === me?.facility?.id)?.stateId
  const scope: Scope = useMemo(() => picked ?? (homeState ? { kind: 'state', id: homeState } : { kind: 'all' }), [picked, homeState])

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ['admin', 'states'] })
    qc.invalidateQueries({ queryKey: ['admin', 'clinics'] })
    qc.invalidateQueries({ queryKey: ['admin', 'people'] })
  }
  const remove = useMutation({
    mutationFn: ({ level, id }: { level: Level; id: string; name: string }) =>
      level === 'state' ? api.admin.deleteState(id) : level === 'district' ? api.admin.deleteDistrict(id) : api.admin.deleteClinic(id),
    onSuccess: (_, x) => {
      refresh()
      notifyOk(`${x.name} removed.`)
      if (picked && picked.kind !== 'all' && picked.id === x.id) setPicked({ kind: 'all' })
    },
    onError: e => notifyError(e, 'Could not remove'),
  })
  const confirmRemove = (level: Level, id: string, name: string, detail: string) => modals.openConfirmModal({
    title: `Remove ${name}?`,
    children: <Text size="sm">{detail}</Text>,
    labels: { confirm: 'Remove', cancel: 'Cancel' },
    confirmProps: { color: 'red' },
    onConfirm: () => remove.mutate({ level, id, name }),
  })

  const rows = useMemo(() => {
    const stateOf = new Map((states.data ?? []).flatMap(s => s.districts.map(d => [d.id, s.id] as const)))
    const q = search.trim().toLowerCase()
    return (clinics.data ?? []).filter(c =>
      (scope.kind === 'all' || (scope.kind === 'state' ? stateOf.get(c.districtId) === scope.id : c.districtId === scope.id))
      && (!q || c.name.toLowerCase().includes(q) || c.district.toLowerCase().includes(q)))
  }, [clinics.data, states.data, scope, search])

  if (states.isLoading || clinics.isLoading) return <Loading />
  if (states.error || clinics.error) return <ErrorBox error={states.error ?? clinics.error} />

  const allClinics = clinics.data!
  const scopeName = scope.kind === 'all' ? 'All states'
    : scope.kind === 'state' ? states.data!.find(s => s.id === scope.id)?.name
    : states.data!.flatMap(s => s.districts).find(d => d.id === scope.id)?.name
  // A new clinic goes into the district being looked at.
  const newClinicDistrict = scope.kind === 'district' ? scope.id : null

  return (
    <Stack gap="md">
      <Group justify="space-between" wrap="wrap">
        <TextInput placeholder="Find a clinic or district" leftSection={<IconSearch size={16} />} value={search}
          onChange={e => setSearch(e.currentTarget.value)} w={280} />
        <Group>
          {master && <Button variant="default" leftSection={<IconPlus size={16} />}
            onClick={() => setForm({ level: 'state', id: null, name: '', parentId: null })}>State</Button>}
          {master && <Button variant="default" leftSection={<IconPlus size={16} />}
            onClick={() => setForm({ level: 'district', id: null, name: '', parentId: scope.kind === 'state' ? scope.id : null })}>District</Button>}
          <Button leftSection={<IconPlus size={16} />}
            onClick={() => setForm({ level: 'clinic', id: null, name: '', parentId: newClinicDistrict })}>Add clinic</Button>
        </Group>
      </Group>

      <Grid gap="md">
        <Grid.Col span={{ base: 12, md: 4 }}>
          <Paper withBorder p="xs">
            <Text size="xs" fw={700} c="dimmed" tt="uppercase" px="xs" py={4}>States &amp; districts</Text>
            <ScrollArea.Autosize mah={600}>
              <NavLink label="All states" active={scope.kind === 'all'} onClick={() => setPicked({ kind: 'all' })}
                rightSection={<Badge size="sm" variant="light" color="gray">{allClinics.length}</Badge>} />
              {states.data!.map(s => (
                <StateBranch key={s.id} state={s} clinics={allClinics} scope={scope} master={master} onPick={setPicked}
                  onEdit={(level, id, name, parentId) => setForm({ level, id, name, parentId })}
                  onRemove={confirmRemove} />
              ))}
            </ScrollArea.Autosize>
          </Paper>
        </Grid.Col>

        <Grid.Col span={{ base: 12, md: 8 }}>
          <Paper withBorder>
            <Group justify="space-between" px="md" py="sm">
              <Group gap="xs"><IconMapPin size={18} /><Text fw={700}>{scopeName}</Text></Group>
              <Text size="sm" c="dimmed">{plural(rows.length, 'clinic')}</Text>
            </Group>
            <Table.ScrollContainer minWidth={560}>
              <Table striped highlightOnHover verticalSpacing={6}>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Clinic</Table.Th>
                    <Table.Th>District</Table.Th>
                    <Table.Th ta="right">Officers here</Table.Th>
                    <Table.Th />
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {rows.map(c => (
                    <Table.Tr key={c.id}>
                      <Table.Td fw={600}>{c.name}</Table.Td>
                      <Table.Td><Text size="sm">{c.district}<Text span size="xs" c="dimmed"> · {c.state}</Text></Text></Table.Td>
                      <Table.Td ta="right">{c.people || <Text span c="dimmed">0</Text>}</Table.Td>
                      <Table.Td>
                        <Group gap={4} justify="flex-end" wrap="nowrap">
                          <ActionIcon variant="subtle" aria-label={`Edit ${c.name}`}
                            onClick={() => setForm({ level: 'clinic', id: c.id, name: c.name, parentId: c.districtId })}><IconEdit size={18} /></ActionIcon>
                          <ActionIcon variant="subtle" color="red" aria-label={`Remove ${c.name}`}
                            onClick={() => confirmRemove('clinic', c.id, c.name, c.people
                              ? `${plural(c.people, 'officer')} will be left without a clinic.` : 'No officers here use it.')}>
                            <IconTrash size={18} />
                          </ActionIcon>
                        </Group>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                  {rows.length === 0 && (
                    <Table.Tr><Table.Td colSpan={4}><Text size="sm" c="dimmed" ta="center" py="lg">
                      {search ? 'No clinic matches.' : 'No clinics here yet.'}
                    </Text></Table.Td></Table.Tr>
                  )}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          </Paper>
        </Grid.Col>
      </Grid>

      {form && <LocationModal form={form} states={states.data!} onClose={() => setForm(null)} onSaved={refresh} />}
    </Stack>
  )
}

function StateBranch({ state: s, clinics, scope, master, onPick, onEdit, onRemove }: {
  state: State; clinics: Clinic[]; scope: Scope; master: boolean; onPick: (s: Scope) => void
  onEdit: (level: Level, id: string, name: string, parentId: string | null) => void
  onRemove: (level: Level, id: string, name: string, detail: string) => void
}) {
  const count = (districtId: string) => clinics.filter(c => c.districtId === districtId).length
  const total = s.districts.reduce((n, d) => n + count(d.id), 0)
  const inState = scope.kind === 'state' ? scope.id === s.id : scope.kind === 'district' && s.districts.some(d => d.id === scope.id)
  return (
    <NavLink label={s.name} fw={600} active={scope.kind === 'state' && scope.id === s.id} defaultOpened={inState}
      onClick={() => onPick({ kind: 'state', id: s.id })} childrenOffset={16}
      rightSection={<Group gap={2} wrap="nowrap">
        <Badge size="sm" variant="light" color="gray">{total}</Badge>
        {master && <RowActions label={s.name} onEdit={() => onEdit('state', s.id, s.name, null)}
          onRemove={() => onRemove('state', s.id, s.name, s.districts.length ? 'Remove its districts first.' : 'It has no districts.')} />}
      </Group>}>
      {s.districts.length === 0 && <Text size="xs" c="dimmed" px="sm" py={4}>No districts yet.</Text>}
      {s.districts.map(d => (
        <NavLink key={d.id} label={d.name} active={scope.kind === 'district' && scope.id === d.id}
          onClick={() => onPick({ kind: 'district', id: d.id })}
          rightSection={<Group gap={2} wrap="nowrap">
            <Badge size="sm" variant="light" color="gray">{count(d.id)}</Badge>
            {master && <RowActions label={d.name} onEdit={() => onEdit('district', d.id, d.name, s.id)}
              onRemove={() => onRemove('district', d.id, d.name, count(d.id) ? 'Move or remove its clinics first.' : 'It has no clinics.')} />}
          </Group>} />
      ))}
    </NavLink>
  )
}

/** Edit/remove icons inside a nav row: they must not also select the row. */
function RowActions({ label, onEdit, onRemove }: { label: string; onEdit: () => void; onRemove: () => void }) {
  return (
    <Group gap={0} wrap="nowrap" onClick={e => e.stopPropagation()}>
      <ActionIcon component="span" variant="subtle" size="sm" aria-label={`Edit ${label}`} onClick={onEdit}><IconEdit size={15} /></ActionIcon>
      <ActionIcon component="span" variant="subtle" size="sm" color="red" aria-label={`Remove ${label}`} onClick={onRemove}><IconTrash size={15} /></ActionIcon>
    </Group>
  )
}

const LABEL: Record<Level, string> = { state: 'state', district: 'district', clinic: 'clinic' }

function LocationModal({ form: initial, states, onClose, onSaved }: {
  form: Form; states: State[]; onClose: () => void; onSaved: () => void
}) {
  const [form, setForm] = useState(initial)
  const { level, id, name, parentId } = form
  const save = useMutation({
    mutationFn: async () => {
      if (level === 'state') await (id ? api.admin.updateState(id, name) : api.admin.createState(name))
      else if (level === 'district') await (id ? api.admin.updateDistrict(id, name, parentId!) : api.admin.createDistrict(name, parentId!))
      else await (id ? api.admin.updateClinic(id, { name, districtId: parentId! }) : api.admin.createClinic({ name, districtId: parentId! }))
    },
    onSuccess: () => { onSaved(); notifyOk(`${name.trim()} saved.`); onClose() },
  })
  const parentData = level === 'district' ? states.map(s => ({ value: s.id, label: s.name }))
    : level === 'clinic' ? states.map(s => ({ group: s.name, items: s.districts.map(d => ({ value: d.id, label: d.name })) }))
    : []

  return (
    <Modal opened onClose={onClose} title={`${id ? 'Edit' : 'Add a'} ${LABEL[level]}`}>
      <form onSubmit={e => { e.preventDefault(); save.mutate() }}>
        <Stack>
          {level !== 'state' && (
            <Select label={level === 'clinic' ? 'District' : 'State'} data={parentData} value={parentId} searchable required
              leftSection={level === 'clinic' ? <IconMapPin size={16} /> : <IconBuildingHospital size={16} />}
              onChange={p => setForm({ ...form, parentId: p })} />
          )}
          <TextInput label="Name" required data-autofocus value={name} onChange={e => setForm({ ...form, name: e.currentTarget.value })}
            placeholder={level === 'clinic' ? 'Klinik Pergigian Beseri' : level === 'district' ? 'Kangar' : 'Perlis'}
            maxLength={level === 'clinic' ? 100 : 50} />
          {save.error && <Text c="red" size="sm">{save.error.message}</Text>}
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>Cancel</Button>
            <Button type="submit" loading={save.isPending} disabled={!name.trim() || (level !== 'state' && !parentId)}>Save</Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  )
}
