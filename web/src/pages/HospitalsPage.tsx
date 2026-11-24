import { ActionIcon, Badge, Button, Group, Modal, Paper, Select, Stack, Table, Tabs, Text, TextInput } from '@mantine/core'
import { modals } from '@mantine/modals'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconBuildingHospital, IconEdit, IconMapPin, IconMessage2, IconPlus, IconTrash, IconUserShield } from '@tabler/icons-react'
import { useState } from 'react'
import { api, type Facility } from '../api'
import { useSwitchFacility } from '../auth'
import { ErrorBox, Loading, PageHeader, StatusBadge } from '../components/common'
import { notifyError, notifyOk } from '../lib'
import { AccountsTab } from './admin/AccountsTab'
import { FeedbackTab } from './admin/FeedbackTab'
import { LocationsModal } from './admin/LocationsModal'

/** What a master sees before picking a hospital: every hospital, app feedback, and the master accounts. */
export function HospitalsPage() {
  const [tab, setTab] = useState<string | null>('hospitals')
  return (
    <Stack gap="md">
      <PageHeader icon={IconBuildingHospital} title="All hospitals"
        description="Each hospital runs its own rota. Open one to work in it, add hospitals, read feedback about the app and manage master accounts." />
      <Tabs value={tab} onChange={setTab} keepMounted={false} variant="pills" radius="xl">
        <Paper className="panel" p={6} mb="md" radius="xl">
          <Tabs.List className="tabs-scroll">
            <Tabs.Tab value="hospitals" leftSection={<IconBuildingHospital size={16} />}>Hospitals</Tabs.Tab>
            <Tabs.Tab value="feedback" leftSection={<IconMessage2 size={16} />}>Feedback</Tabs.Tab>
            <Tabs.Tab value="masters" leftSection={<IconUserShield size={16} />}>Master accounts</Tabs.Tab>
          </Tabs.List>
        </Paper>
        <Tabs.Panel value="hospitals"><HospitalsTab /></Tabs.Panel>
        <Tabs.Panel value="feedback"><FeedbackTab /></Tabs.Panel>
        <Tabs.Panel value="masters"><AccountsTab /></Tabs.Panel>
      </Tabs>
    </Stack>
  )
}

function HospitalsTab() {
  const qc = useQueryClient()
  const facilities = useQuery({ queryKey: ['facilities'], queryFn: api.facilities })
  const states = useQuery({ queryKey: ['admin', 'states'], queryFn: api.admin.states })
  const clinics = useQuery({ queryKey: ['admin', 'clinics'], queryFn: api.admin.clinics })
  const [editing, setEditing] = useState<Facility | 'new' | null>(null)
  const [locations, setLocations] = useState(false)
  const switchTo = useSwitchFacility()

  const refresh = () => { qc.invalidateQueries({ queryKey: ['facilities'] }); qc.invalidateQueries({ queryKey: ['admin'] }) }
  const remove = useMutation({
    mutationFn: (f: Facility) => api.admin.deleteFacility(f.id),
    onSuccess: (_, f) => { refresh(); notifyOk(`${f.name} removed.`) },
    onError: e => notifyError(e, 'Could not remove'),
  })

  if (facilities.isLoading || states.isLoading || clinics.isLoading) return <Loading />
  if (facilities.error || states.error || clinics.error) return <ErrorBox error={facilities.error ?? states.error ?? clinics.error} />

  return (
    <Stack gap="md">
      <Group justify="flex-end">
        <Button variant="default" leftSection={<IconMapPin size={16} />} onClick={() => setLocations(true)}>States, districts & clinics</Button>
        <Button leftSection={<IconPlus size={16} />} onClick={() => setEditing('new')}>Add hospital</Button>
      </Group>
      <Paper withBorder>
        <Table.ScrollContainer minWidth={760}>
          <Table striped highlightOnHover verticalSpacing={6}>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Hospital</Table.Th>
                <Table.Th>District</Table.Th>
                <Table.Th ta="right">Officers</Table.Th>
                <Table.Th ta="right">Accounts</Table.Th>
                <Table.Th>Latest period</Table.Th>
                <Table.Th />
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {facilities.data!.map(f => (
                <Table.Tr key={f.id}>
                  <Table.Td><Text fw={600} size="sm">{f.name}</Text><Badge size="xs" variant="light">{f.code}</Badge></Table.Td>
                  <Table.Td><Text size="sm">{f.district}, {f.state}</Text></Table.Td>
                  <Table.Td ta="right">{f.officers}</Table.Td>
                  <Table.Td ta="right">{f.accounts}</Table.Td>
                  <Table.Td>
                    {f.currentPeriod
                      ? <Group gap={6}><Text size="sm">{f.currentPeriod}</Text>{f.currentPeriodStatus && <StatusBadge status={f.currentPeriodStatus} />}</Group>
                      : <Text size="sm" c="dimmed">–</Text>}
                  </Table.Td>
                  <Table.Td>
                    <Group gap={4} justify="flex-end" wrap="nowrap">
                      <Button size="compact-sm" loading={switchTo.isPending && switchTo.variables === f.id} onClick={() => switchTo.mutate(f.id)}>Open</Button>
                      <ActionIcon variant="subtle" aria-label="Edit" onClick={() => setEditing(f)}><IconEdit size={18} /></ActionIcon>
                      <ActionIcon variant="subtle" color="red" aria-label="Remove" onClick={() => modals.openConfirmModal({
                        title: `Remove ${f.name}?`,
                        children: <Text size="sm">Only a hospital with no officers, periods or accounts can be removed.</Text>,
                        labels: { confirm: 'Remove', cancel: 'Cancel' },
                        confirmProps: { color: 'red' },
                        onConfirm: () => remove.mutate(f),
                      })}><IconTrash size={18} /></ActionIcon>
                    </Group>
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      </Paper>

      {editing && (
        <HospitalModal hospital={editing === 'new' ? null : editing}
          districts={states.data!.map(s => ({ group: s.name, items: s.districts.map(d => ({ value: d.id, label: d.name })) }))}
          onClose={() => setEditing(null)} onSaved={refresh} />
      )}
      <LocationsModal opened={locations} states={states.data!} clinics={clinics.data!} onClose={() => setLocations(false)} onSaved={refresh} />
    </Stack>
  )
}

function HospitalModal({ hospital, districts, onClose, onSaved }: {
  hospital: Facility | null; districts: { group: string; items: { value: string; label: string }[] }[]
  onClose: () => void; onSaved: () => void
}) {
  const [name, setName] = useState(hospital?.name ?? '')
  const [code, setCode] = useState(hospital?.code ?? '')
  const [districtId, setDistrictId] = useState<string | null>(hospital?.districtId ?? null)
  const save = useMutation({
    mutationFn: async () => {
      const f = { name, code, districtId: districtId! }
      if (hospital) await api.admin.updateFacility(hospital.id, f)
      else await api.admin.createFacility(f)
    },
    onSuccess: () => { onSaved(); notifyOk(`${name} saved.`); onClose() },
  })

  return (
    <Modal opened onClose={onClose} title={hospital ? 'Edit hospital' : 'Add hospital'}>
      <form onSubmit={e => { e.preventDefault(); save.mutate() }}>
        <Stack>
          <TextInput label="Name" required value={name} onChange={e => setName(e.currentTarget.value)} placeholder="Hospital Sultanah Bahiyah"
            maxLength={100} data-autofocus />
          <TextInput label="Short code" required value={code} onChange={e => setCode(e.currentTarget.value.toUpperCase())} placeholder="HSB"
            maxLength={16} description="Shown in the header and on the feedback form." />
          <Select label="District" required searchable data={districts} value={districtId} onChange={setDistrictId}
            description="Its state's public holidays apply. Add states and districts with “States, districts & clinics”." />
          {save.error && <Text c="red" size="sm">{save.error.message}</Text>}
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>Cancel</Button>
            <Button type="submit" loading={save.isPending} disabled={!name.trim() || !code.trim() || !districtId}>Save</Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  )
}
