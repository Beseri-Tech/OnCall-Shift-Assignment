import { ActionIcon, Alert, Button, FileButton, Group, Paper, Stack, Table, Text, TextInput, Title } from '@mantine/core'
import { DatePickerInput } from '@mantine/dates'
import { modals } from '@mantine/modals'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconDownload, IconTrash, IconUpload } from '@tabler/icons-react'
import { useState } from 'react'
import { api, downloadDocument, uploadDocument } from '../../api'
import { ErrorBox, Loading } from '../../components/common'
import { formatLong, todayIso } from '../../dates'
import { notifyError, notifyOk } from '../../lib'

/** The hospital's surat arahan: officers' ETAP claims for a month use the latest one in effect by the end of that month. */
export function SuratArahanTab() {
  const qc = useQueryClient()
  const letters = useQuery({ queryKey: ['admin', 'surat-arahan'], queryFn: api.admin.suratArahan })
  const [file, setFile] = useState<File | null>(null)
  const [from, setFrom] = useState<string>(todayIso().slice(0, 7) + '-01')
  const [reference, setReference] = useState('')

  const add = useMutation({
    mutationFn: async () => {
      const doc = await uploadDocument(file!, 'SuratArahan')
      await api.admin.addSuratArahan(doc.id, from, reference.trim() || null)
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['admin', 'surat-arahan'] })
      qc.invalidateQueries({ queryKey: ['etap'] })
      setFile(null); setReference('')
      notifyOk('Surat arahan uploaded.')
    },
    onError: e => notifyError(e, 'Could not upload'),
  })
  const remove = useMutation({
    mutationFn: (id: string) => api.admin.deleteSuratArahan(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['admin', 'surat-arahan'] }); notifyOk('Removed.') },
    onError: e => notifyError(e),
  })

  if (letters.isLoading) return <Loading />
  if (letters.error) return <ErrorBox error={letters.error} />

  return (
    <Stack gap="md">
      <Paper className="panel" p="lg">
        <Stack gap="sm">
          <Title order={4}>Upload a surat arahan</Title>
          <Text size="sm" c="dimmed">
            Officers download it with their ETAP form. A new letter applies from its date; until you upload another, the latest one is used.
          </Text>
          <Group align="flex-end" wrap="wrap">
            <FileButton accept="application/pdf,image/png,image/jpeg" onChange={setFile}>
              {props => <Button {...props} variant="default" leftSection={<IconUpload size={16} />}>{file ? file.name : 'Choose file'}</Button>}
            </FileButton>
            <DatePickerInput label="In effect from" value={from} onChange={d => d && setFrom(d as string)} valueFormat="D MMM YYYY" w={170} />
            <TextInput label="Reference (optional)" placeholder="e.g. HTF/ETAP/1/2026" value={reference} maxLength={100}
              onChange={e => setReference(e.currentTarget.value)} w={220} />
            <Button loading={add.isPending} disabled={!file} onClick={() => add.mutate()}>Upload</Button>
          </Group>
        </Stack>
      </Paper>
      <Paper className="panel">
        {letters.data!.length === 0 ? <Alert color="gray" variant="light">No surat arahan yet.</Alert> : (
          <Table verticalSpacing={6} style={{ fontVariantNumeric: 'tabular-nums' }}>
            <Table.Thead><Table.Tr><Table.Th fw={500} c="var(--text-2)">In effect from</Table.Th><Table.Th fw={500} c="var(--text-2)">Reference</Table.Th><Table.Th fw={500} c="var(--text-2)">File</Table.Th><Table.Th /></Table.Tr></Table.Thead>
            <Table.Tbody>
              {letters.data!.map((l, i) => (
                <Table.Tr key={l.id}>
                  <Table.Td><Text size="sm" fw={i === 0 ? 500 : 400}>{formatLong(l.effectiveFrom)}{i === 0 && ' (latest)'}</Text></Table.Td>
                  <Table.Td><Text size="sm">{l.reference ?? '–'}</Text></Table.Td>
                  <Table.Td><Text size="sm">{l.fileName}</Text></Table.Td>
                  <Table.Td>
                    <Group gap={4} justify="flex-end">
                      <ActionIcon variant="subtle" aria-label="Download" onClick={() => downloadDocument(l.documentId).catch(notifyError)}><IconDownload size={18} /></ActionIcon>
                      <ActionIcon variant="subtle" color="gray" aria-label="Remove" onClick={() => modals.openConfirmModal({
                        title: 'Remove this surat arahan?', children: <Text size="sm">Claims will use the previous letter instead.</Text>,
                        labels: { confirm: 'Remove', cancel: 'Cancel' }, confirmProps: { color: 'red' }, onConfirm: () => remove.mutate(l.id),
                      })}><IconTrash size={18} /></ActionIcon>
                    </Group>
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        )}
      </Paper>
    </Stack>
  )
}
