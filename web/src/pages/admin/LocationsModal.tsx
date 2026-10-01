import { ActionIcon, Badge, Button, Divider, Group, Modal, Paper, Select, Stack, Text, TextInput, Title } from '@mantine/core'
import { modals } from '@mantine/modals'
import { useMutation } from '@tanstack/react-query'
import { IconEdit, IconTrash } from '@tabler/icons-react'
import { useState } from 'react'
import { api, type Clinic, type State } from '../../api'
import { notifyError, notifyOk } from '../../lib'

type Level = 'state' | 'district' | 'clinic'

/** What the form at the bottom is adding or editing. parentId: the state of a district, the district of a clinic. */
interface Form { level: Level; id: string | null; name: string; parentId: string | null }

const LABEL: Record<Level, string> = { state: 'state', district: 'district', clinic: 'clinic' }

/** State -> district -> clinic. Officers are placed in a clinic. */
export function LocationsModal({ opened, states, clinics, onClose, onSaved }: {
  opened: boolean; states: State[]; clinics: Clinic[]; onClose: () => void; onSaved: () => void
}) {
  const blank = (level: Level = 'clinic'): Form => ({ level, id: null, name: '', parentId: null })
  const [form, setForm] = useState<Form>(blank())

  const save = useMutation({
    mutationFn: async () => {
      const { level, id, name, parentId } = form
      if (level === 'state') await (id ? api.admin.updateState(id, name) : api.admin.createState(name))
      else if (level === 'district') await (id ? api.admin.updateDistrict(id, name, parentId!) : api.admin.createDistrict(name, parentId!))
      else await (id ? api.admin.updateClinic(id, { name, districtId: parentId! }) : api.admin.createClinic({ name, districtId: parentId! }))
    },
    onSuccess: () => { onSaved(); notifyOk(`${form.name} saved.`); setForm(blank(form.level)) },
    onError: e => notifyError(e, `Could not save ${LABEL[form.level]}`),
  })
  const remove = useMutation({
    mutationFn: ({ level, id }: { level: Level; id: string; name: string }) =>
      level === 'state' ? api.admin.deleteState(id) : level === 'district' ? api.admin.deleteDistrict(id) : api.admin.deleteClinic(id),
    onSuccess: (_, x) => { onSaved(); notifyOk(`${x.name} removed.`) },
    onError: e => notifyError(e, 'Could not remove'),
  })

  const confirmRemove = (level: Level, id: string, name: string, detail: string) => modals.openConfirmModal({
    title: `Remove ${name}?`,
    children: <Text size="sm">{detail}</Text>,
    labels: { confirm: 'Remove', cancel: 'Cancel' },
    confirmProps: { color: 'red' },
    onConfirm: () => remove.mutate({ level, id, name }),
  })

  const districtOptions = states.map(s => ({ group: s.name, items: s.districts.map(d => ({ value: d.id, label: d.name })) }))
  const parentData = form.level === 'district' ? states.map(s => ({ value: s.id, label: s.name }))
    : form.level === 'clinic' ? districtOptions : []

  return (
    <Modal opened={opened} onClose={() => { setForm(blank()); onClose() }} title="Clinics & districts" size="lg">
      <Stack>
        <Text size="sm" c="dimmed">Officers belong to a clinic, each clinic to a district, each district to a state.</Text>
        {states.map(s => (
          <Paper key={s.id} withBorder p="sm">
            <Group justify="space-between" mb={4}>
              <Title order={5}>{s.name}</Title>
              <Buttons onEdit={() => setForm({ level: 'state', id: s.id, name: s.name, parentId: null })}
                onRemove={() => confirmRemove('state', s.id, s.name, s.districts.length
                  ? 'Remove its districts first.' : 'It has no districts.')} />
            </Group>
            {s.districts.length === 0 && <Text size="sm" c="dimmed">No districts yet.</Text>}
            <Stack gap="xs">
              {s.districts.map(d => {
                const inDistrict = clinics.filter(c => c.districtId === d.id)
                return (
                  <div key={d.id}>
                    <Group justify="space-between" wrap="nowrap">
                      <Text fw={600} size="sm">{d.name} <Text span size="xs" c="dimmed">({inDistrict.length} clinic{inDistrict.length === 1 ? '' : 's'})</Text></Text>
                      <Buttons onEdit={() => setForm({ level: 'district', id: d.id, name: d.name, parentId: s.id })}
                        onRemove={() => confirmRemove('district', d.id, d.name, inDistrict.length
                          ? 'Move or remove its clinics first.' : 'It has no clinics.')} />
                    </Group>
                    {inDistrict.map(c => (
                      <Group key={c.id} justify="space-between" wrap="nowrap" pl="md">
                        <Text size="sm">{c.name} <Badge size="xs" variant="light" color="gray">{c.people} officer{c.people === 1 ? '' : 's'}</Badge></Text>
                        <Buttons onEdit={() => setForm({ level: 'clinic', id: c.id, name: c.name, parentId: d.id })}
                          onRemove={() => confirmRemove('clinic', c.id, c.name, c.people
                            ? `${c.people} officer(s) will be left without a clinic.` : 'No officers use it.')} />
                      </Group>
                    ))}
                  </div>
                )
              })}
            </Stack>
          </Paper>
        ))}

        <Divider />
        <form onSubmit={e => { e.preventDefault(); save.mutate() }}>
          <Stack gap="xs">
            <Group justify="space-between">
              <Text fw={600} size="sm">{form.id ? `Edit ${LABEL[form.level]}` : `Add a ${LABEL[form.level]}`}</Text>
              {!form.id && (
                <Group gap={4}>
                  {(['clinic', 'district', 'state'] as const).map(l => (
                    <Button key={l} size="compact-xs" variant={form.level === l ? 'light' : 'subtle'}
                      onClick={() => setForm(blank(l))}>{LABEL[l]}</Button>
                  ))}
                </Group>
              )}
            </Group>
            <Group align="flex-end">
              {form.level !== 'state' && (
                <Select label={form.level === 'clinic' ? 'District' : 'State'} data={parentData} value={form.parentId} w={200}
                  onChange={parentId => setForm({ ...form, parentId })} searchable required />
              )}
              <TextInput label="Name" value={form.name} onChange={e => setForm({ ...form, name: e.currentTarget.value })}
                placeholder={form.level === 'clinic' ? 'KP BESERI' : form.level === 'district' ? 'Kangar' : 'Perlis'}
                maxLength={form.level === 'clinic' ? 100 : 50} style={{ flex: 1 }} />
              <Button type="submit" loading={save.isPending} disabled={!form.name.trim() || (form.level !== 'state' && !form.parentId)}>
                {form.id ? 'Save' : 'Add'}
              </Button>
              {form.id && <Button variant="default" onClick={() => setForm(blank(form.level))}>Cancel</Button>}
            </Group>
          </Stack>
        </form>
      </Stack>
    </Modal>
  )
}

function Buttons({ onEdit, onRemove }: { onEdit: () => void; onRemove: () => void }) {
  return (
    <Group gap={4} wrap="nowrap">
      <ActionIcon variant="subtle" size="sm" aria-label="Edit" onClick={onEdit}><IconEdit size={16} /></ActionIcon>
      <ActionIcon variant="subtle" size="sm" color="red" aria-label="Remove" onClick={onRemove}><IconTrash size={16} /></ActionIcon>
    </Group>
  )
}
