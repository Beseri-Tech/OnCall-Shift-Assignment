import { ActionIcon, Button, Group, Modal, Select, Stack, Table, Text, TextInput } from '@mantine/core'
import { DatePickerInput } from '@mantine/dates'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconTrash } from '@tabler/icons-react'
import { useState } from 'react'
import { api, type Clinic, type IsoDate } from '../../api'
import { ErrorBox, Loading } from '../../components/common'
import { formatLong } from '../../dates'
import { notifyError, notifyOk } from '../../lib'

/**
 * Dates nobody from a clinic can be on call (e.g. a clinic event). The generator treats them as unavailable for every officer
 * of that clinic; they cost no leave points and officers can't mark leave on them.
 */
export function ClinicEventsModal({ opened, clinics, onClose }: { opened: boolean; clinics: Clinic[]; onClose: () => void }) {
  const qc = useQueryClient()
  const days = useQuery({ queryKey: ['clinic-blocked-days'], queryFn: api.admin.clinicBlockedDays, enabled: opened })
  const [clinicId, setClinicId] = useState<string | null>(null)
  const [dates, setDates] = useState<IsoDate[]>([])
  const [reason, setReason] = useState('')

  const refresh = () => qc.invalidateQueries({ queryKey: ['clinic-blocked-days'] })

  const add = useMutation({
    mutationFn: () => api.admin.blockClinicDays(clinicId!, dates, reason.trim() || null),
    onSuccess: () => { refresh(); setDates([]); setReason(''); notifyOk('Days blocked. Generate the rota again to apply them.') },
    onError: e => notifyError(e, 'Could not block the days'),
  })
  const remove = useMutation({
    mutationFn: (d: { clinicId: string; date: IsoDate }) => api.admin.unblockClinicDay(d.clinicId, d.date),
    onSuccess: () => { refresh(); notifyOk('Day unblocked.') },
    onError: e => notifyError(e, 'Could not unblock the day'),
  })

  return (
    <Modal opened={opened} onClose={onClose} title="Clinic event days" size="lg">
      <Stack>
        <Text size="sm" c="dimmed">
          Block a date for everyone in a clinic, e.g. when the clinic has an event. Nobody from that clinic is put on call that day,
          officers can't mark leave on it, and it costs no leave points. A rota that is already generated is not changed until you
          generate it again.
        </Text>
        <Group align="flex-end" wrap="wrap">
          <Select label="Clinic" placeholder="Pick a clinic" searchable w={220} value={clinicId} onChange={setClinicId}
            data={clinics.map(c => ({ value: c.id, label: c.name }))} />
          <DatePickerInput type="multiple" label="Dates" placeholder="Pick one or more" value={dates}
            onChange={v => setDates(v as IsoDate[])} valueFormat="D MMM YYYY" w={220} clearable />
          <TextInput label="Reason" placeholder="e.g. Clinic annual dinner" maxLength={200} value={reason}
            onChange={e => setReason(e.currentTarget.value)} style={{ flex: 1, minWidth: 180 }} />
          <Button onClick={() => add.mutate()} disabled={!clinicId || dates.length === 0} loading={add.isPending}>Block</Button>
        </Group>

        {days.error ? <ErrorBox error={days.error} /> : !days.data ? <Loading /> : days.data.length === 0 ? (
          <Text size="sm" c="dimmed">No upcoming blocked days.</Text>
        ) : (
          <Table>
            <Table.Thead><Table.Tr><Table.Th>Date</Table.Th><Table.Th>Clinic</Table.Th><Table.Th>Reason</Table.Th><Table.Th /></Table.Tr></Table.Thead>
            <Table.Tbody>
              {days.data.map(d => (
                <Table.Tr key={`${d.clinicId}-${d.date}`}>
                  <Table.Td>{formatLong(d.date)}</Table.Td>
                  <Table.Td>{d.clinicName}</Table.Td>
                  <Table.Td>{d.reason ?? '–'}</Table.Td>
                  <Table.Td>
                    <ActionIcon variant="subtle" color="red" aria-label={`Unblock ${formatLong(d.date)} for ${d.clinicName}`}
                      onClick={() => remove.mutate({ clinicId: d.clinicId, date: d.date })}><IconTrash size={16} /></ActionIcon>
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        )}
      </Stack>
    </Modal>
  )
}
