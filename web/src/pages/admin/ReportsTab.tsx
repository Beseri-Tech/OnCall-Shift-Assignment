import { Alert, Badge, Button, Group, Paper, Select, Stack, Table, Text, Title } from '@mantine/core'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconCloudUpload, IconDownload, IconFileText } from '@tabler/icons-react'
import { api, downloadDocument } from '../../api'
import { ErrorBox, Loading } from '../../components/common'
import { notifyError, notifyOk } from '../../lib'
import { useState } from 'react'

const kb = (n: number) => (n >= 1024 * 1024 ? `${(n / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(n / 1024))} KB`)
const when = (iso: string) => new Date(iso).toLocaleString('en-GB', { dateStyle: 'medium', timeStyle: 'short' })

/**
 * Every PDF rota report saved in private storage. A report is saved automatically each time a rota is published.
 * allHospitals: the masters' view outside a hospital, with every hospital's reports and a hospital filter.
 */
export function ReportsTab({ allHospitals = false }: { allHospitals?: boolean }) {
  const qc = useQueryClient()
  const [facilityId, setFacilityId] = useState<string | null>(null)
  const facilities = useQuery({ queryKey: ['facilities'], queryFn: api.facilities, enabled: allHospitals })
  const reports = useQuery({
    queryKey: ['report-archive', facilityId],
    queryFn: () => api.admin.reportArchive(facilityId ?? undefined),
    placeholderData: p => p,
  })
  const save = useMutation({
    mutationFn: (runId: string) => api.admin.archiveReport(runId),
    onSuccess: r => { notifyOk(`A report for ${r.periodName} was saved.`, 'Report saved'); qc.invalidateQueries({ queryKey: ['report-archive'] }) },
    onError: e => notifyError(e),
  })

  if (reports.isLoading) return <Loading />
  if (reports.error) return <ErrorBox error={reports.error} />
  const data = reports.data!

  return (
    <Stack gap="md">
      <Paper className="panel" p="lg">
        <Stack gap="xs">
          <Title order={4}>Rota reports</Title>
          <Text size="sm" c="dimmed">
            The PDF report of a rota (assignments, why each officer is on each day, swaps, overrides and the audit trail) is saved to private
            storage when you publish. Each saved report is a snapshot, so you can download it later even after the rota is unpublished or replaced.
          </Text>
          {allHospitals && (
            <Select label="Hospital" placeholder="All hospitals" clearable searchable w={280} value={facilityId} onChange={setFacilityId}
              data={(facilities.data ?? []).map(f => ({ value: f.id, label: `${f.code}, ${f.name}` }))} />
          )}
          {!data.storageEnabled && (
            <Alert color="yellow" variant="light">Document storage (Cloudflare R2) isn't set up, so reports can't be saved yet.</Alert>
          )}
        </Stack>
      </Paper>

      {data.unarchived.length > 0 && (
        <Paper className="panel" p="lg">
          <Stack gap="sm">
            <Text fw={500}>Published, no saved report yet</Text>
            {data.unarchived.map(u => (
              <Group key={u.runId} justify="space-between" wrap="wrap">
                <Text size="sm">
                  {allHospitals && u.facilityCode && <Badge size="sm" variant="light" color="gray" mr={6}>{u.facilityCode}</Badge>}
                  {u.periodName}{u.publishedAt && `, published ${when(u.publishedAt)}`}
                </Text>
                {allHospitals ? <Text size="xs" c="dimmed">Open the hospital to save it</Text> : <Button size="xs" variant="default" leftSection={<IconCloudUpload size={14} />} disabled={!data.storageEnabled}
                  loading={save.isPending && save.variables === u.runId} onClick={() => save.mutate(u.runId)}>Save report now</Button>}
              </Group>
            ))}
          </Stack>
        </Paper>
      )}

      <Paper className="panel" p="lg">
        {data.reports.length === 0 ? (
          <Alert icon={<IconFileText size={18} />} color="gray" variant="light">No reports saved yet. One is saved each time you publish a rota.</Alert>
        ) : (
          <Table.ScrollContainer minWidth={560}>
            <Table verticalSpacing="sm" style={{ fontVariantNumeric: 'tabular-nums' }}>
              <Table.Thead>
                <Table.Tr>{allHospitals && <Table.Th fw={500} c="var(--text-2)">Hospital</Table.Th>}<Table.Th fw={500} c="var(--text-2)">Rota</Table.Th><Table.Th fw={500} c="var(--text-2)">Saved</Table.Th><Table.Th fw={500} c="var(--text-2)">By</Table.Th><Table.Th ta="right" fw={500} c="var(--text-2)">Size</Table.Th><Table.Th /></Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {data.reports.map(r => (
                  <Table.Tr key={r.documentId}>
                    {allHospitals && <Table.Td>{r.facilityCode ? <Badge variant="light" color="gray" size="sm">{r.facilityCode}</Badge> : '-'}</Table.Td>}
                    <Table.Td><Text fw={500} size="sm">{r.periodName}</Text><Text size="xs" c="dimmed">{r.runIsPublished ? 'Currently published' : 'Not published now'}</Text></Table.Td>
                    <Table.Td><Text size="sm">{when(r.createdAt)}</Text></Table.Td>
                    <Table.Td><Text size="sm">{r.createdBy ?? '-'}</Text></Table.Td>
                    <Table.Td ta="right"><Text size="sm">{kb(r.sizeBytes)}</Text></Table.Td>
                    <Table.Td>
                      <Button size="xs" variant="default" leftSection={<IconDownload size={14} />}
                        onClick={() => downloadDocument(r.documentId).catch(notifyError)}>Download</Button>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        )}
      </Paper>
    </Stack>
  )
}
