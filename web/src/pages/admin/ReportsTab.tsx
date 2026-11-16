import { Alert, Button, Group, Paper, Stack, Table, Text, Title } from '@mantine/core'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconCloudUpload, IconDownload, IconFileText } from '@tabler/icons-react'
import { api, downloadDocument } from '../../api'
import { ErrorBox, Loading } from '../../components/common'
import { notifyError, notifyOk } from '../../lib'

const kb = (n: number) => (n >= 1024 * 1024 ? `${(n / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(n / 1024))} KB`)
const when = (iso: string) => new Date(iso).toLocaleString('en-GB', { dateStyle: 'medium', timeStyle: 'short' })

/** Every PDF rota report saved in private storage. A report is saved automatically each time a rota is published. */
export function ReportsTab() {
  const qc = useQueryClient()
  const reports = useQuery({ queryKey: ['report-archive'], queryFn: () => api.admin.reportArchive() })
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
          {!data.storageEnabled && (
            <Alert color="orange" variant="light">Document storage (Cloudflare R2) isn't set up, so reports can't be saved yet.</Alert>
          )}
        </Stack>
      </Paper>

      {data.unarchived.length > 0 && (
        <Paper className="panel" p="lg">
          <Stack gap="sm">
            <Text fw={600}>Published, no saved report yet</Text>
            {data.unarchived.map(u => (
              <Group key={u.runId} justify="space-between" wrap="wrap">
                <Text size="sm">{u.periodName}{u.publishedAt && ` · published ${when(u.publishedAt)}`}</Text>
                <Button size="xs" variant="light" leftSection={<IconCloudUpload size={14} />} disabled={!data.storageEnabled}
                  loading={save.isPending && save.variables === u.runId} onClick={() => save.mutate(u.runId)}>Save report now</Button>
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
            <Table verticalSpacing="sm">
              <Table.Thead>
                <Table.Tr><Table.Th>Rota</Table.Th><Table.Th>Saved</Table.Th><Table.Th>By</Table.Th><Table.Th>Size</Table.Th><Table.Th /></Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {data.reports.map(r => (
                  <Table.Tr key={r.documentId}>
                    <Table.Td><Text fw={600} size="sm">{r.periodName}</Text><Text size="xs" c="dimmed">{r.runIsPublished ? 'Currently published' : 'Not published now'}</Text></Table.Td>
                    <Table.Td><Text size="sm">{when(r.createdAt)}</Text></Table.Td>
                    <Table.Td><Text size="sm">{r.createdBy ?? '-'}</Text></Table.Td>
                    <Table.Td><Text size="sm">{kb(r.sizeBytes)}</Text></Table.Td>
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
