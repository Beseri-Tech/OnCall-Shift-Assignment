import {
  ActionIcon, Badge, Button, Checkbox, Group, Modal, NumberInput, Paper, Stack, Switch, Table, Text, TextInput, Textarea,
  Tooltip,
} from '@mantine/core'
import { modals } from '@mantine/modals'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconDownload, IconEdit, IconSearch, IconTrash, IconUserPlus, IconUsersPlus } from '@tabler/icons-react'
import { useMemo, useState } from 'react'
import { api, type AdminPerson, type UpsertPerson } from '../../api'
import { ErrorBox, Loading } from '../../components/common'
import { notifyError, notifyOk } from '../../lib'

const blank: UpsertPerson = {
  name: '', code: null, active: true, extraShift: false, preferWeekendHoliday: false, weekendWeight: 1,
  openingTotal: null, openingWeekday: null, openingWeekendHoliday: null,
}

export function PeopleTab() {
  const qc = useQueryClient()
  const people = useQuery({ queryKey: ['admin', 'people'], queryFn: api.admin.people })
  const [search, setSearch] = useState('')
  const [showInactive, setShowInactive] = useState(false)
  const [editing, setEditing] = useState<{ id: string | null; value: UpsertPerson } | null>(null)
  const [bulkOpen, setBulkOpen] = useState(false)

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ['admin', 'people'] })
    qc.invalidateQueries({ queryKey: ['people'] })
  }

  const remove = useMutation({
    mutationFn: (id: string) => api.admin.deletePerson(id),
    onSuccess: () => { refresh(); notifyOk('Person removed.') },
    onError: e => notifyError(e, 'Could not remove'),
  })

  const rows = useMemo(() => {
    const s = search.trim().toLowerCase()
    return (people.data ?? []).filter(p =>
      (showInactive || p.active) && (!s || p.name.toLowerCase().includes(s) || p.code.toLowerCase().includes(s)))
  }, [people.data, search, showInactive])

  if (people.isLoading) return <Loading />
  if (people.error) return <ErrorBox error={people.error} />

  const active = people.data!.filter(p => p.active).length

  return (
    <Stack gap="md">
      <Group justify="space-between" wrap="wrap">
        <Group wrap="wrap">
          <TextInput placeholder="Find name or code" leftSection={<IconSearch size={16} />} value={search}
            onChange={e => setSearch(e.currentTarget.value)} w={240} />
          <Switch label="Show inactive" checked={showInactive} onChange={e => setShowInactive(e.currentTarget.checked)} />
          <Text size="sm" c="dimmed">{active} active</Text>
        </Group>
        <Group>
          <Button variant="default" leftSection={<IconDownload size={16} />} component="a" href={api.admin.exports.totals()}>
            Totals (Excel)
          </Button>
          <Button variant="default" leftSection={<IconUsersPlus size={16} />} onClick={() => setBulkOpen(true)}>Add many</Button>
          <Button leftSection={<IconUserPlus size={16} />} onClick={() => setEditing({ id: null, value: blank })}>Add person</Button>
        </Group>
      </Group>

      <Paper withBorder>
        <Table.ScrollContainer minWidth={900}>
          <Table striped highlightOnHover verticalSpacing={6}>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Code</Table.Th>
                <Table.Th>Name</Table.Th>
                <Table.Th>Flags</Table.Th>
                <Table.Th ta="center">Opening<br /><Text span size="xs" c="dimmed">total / wkday / wkend+PH</Text></Table.Th>
                <Table.Th ta="center">Done so far<br /><Text span size="xs" c="dimmed">total / wkday / wkend+PH</Text></Table.Th>
                <Table.Th />
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {rows.map(p => (
                <Table.Tr key={p.id} opacity={p.active ? 1 : 0.55}>
                  <Table.Td><Badge variant="outline" color="gray">{p.code}</Badge></Table.Td>
                  <Table.Td fw={600}>{p.name}</Table.Td>
                  <Table.Td>
                    <Group gap={4}>
                      {!p.active && <Badge color="gray">Inactive</Badge>}
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
                      <ActionIcon variant="subtle" aria-label="Edit" onClick={() => setEditing({ id: p.id, value: toUpsert(p) })}>
                        <IconEdit size={18} />
                      </ActionIcon>
                      <ActionIcon variant="subtle" color="red" aria-label="Delete" onClick={() => modals.openConfirmModal({
                        title: `Remove ${p.name}?`,
                        children: <Text size="sm">This also removes their leave. People with shifts in a rota can't be removed – deactivate them instead.</Text>,
                        labels: { confirm: 'Remove', cancel: 'Cancel' },
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

      {editing && <PersonModal editing={editing} onClose={() => setEditing(null)} onSaved={refresh} />}
      <BulkAddModal opened={bulkOpen} onClose={() => setBulkOpen(false)} onSaved={refresh} />
    </Stack>
  )
}

const toUpsert = (p: AdminPerson): UpsertPerson => ({
  name: p.name, code: p.code, active: p.active, extraShift: p.extraShift, preferWeekendHoliday: p.preferWeekendHoliday,
  weekendWeight: p.weekendWeight, openingTotal: p.openingTotal, openingWeekday: p.openingWeekday,
  openingWeekendHoliday: p.openingWeekendHoliday,
})

const num = (v: string | number) => (v === '' ? null : Number(v))

function PersonModal({ editing, onClose, onSaved }: {
  editing: { id: string | null; value: UpsertPerson }; onClose: () => void; onSaved: () => void
}) {
  const [v, setV] = useState(editing.value)
  const set = <K extends keyof UpsertPerson>(k: K, value: UpsertPerson[K]) => setV(s => ({ ...s, [k]: value }))

  const save = useMutation({
    mutationFn: () => (editing.id ? api.admin.updatePerson(editing.id, v) : api.admin.createPerson(v).then(() => undefined)),
    onSuccess: () => { onSaved(); onClose(); notifyOk(`${v.name} saved.`) },
  })

  return (
    <Modal opened onClose={onClose} title={editing.id ? 'Edit person' : 'Add person'} size="lg">
      <form onSubmit={e => { e.preventDefault(); save.mutate() }}>
        <Stack>
          <Group grow align="flex-start">
            <TextInput label="Name" required value={v.name} onChange={e => set('name', e.currentTarget.value)} data-autofocus />
            <TextInput label="Code" description="Leave blank to assign the next D-number" value={v.code ?? ''}
              onChange={e => set('code', e.currentTarget.value || null)} maxLength={32} />
          </Group>
          <Group>
            <Checkbox label="Active" checked={v.active} onChange={e => set('active', e.currentTarget.checked)} />
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
            <Button type="submit" loading={save.isPending} disabled={!v.name.trim()}>Save</Button>
          </Group>
        </Stack>
      </form>
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
    <Modal opened={opened} onClose={onClose} title="Add many people" size="lg">
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
