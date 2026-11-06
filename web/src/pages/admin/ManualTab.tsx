import { Alert, Button, FileButton, Group, Paper, Stack, Text } from '@mantine/core'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconBook, IconTrash, IconUpload } from '@tabler/icons-react'
import { api, openDocument, uploadDocument } from '../../api'
import { ErrorBox, Loading } from '../../components/common'
import { notifyError, notifyOk } from '../../lib'

const mb = (n: number) => `${(n / 1024 / 1024).toFixed(1)} MB`

/** Upload the user manual (a PDF). Officers get it from the Rules page; a new upload replaces the old one. */
export function ManualTab() {
  const qc = useQueryClient()
  const manual = useQuery({ queryKey: ['manual'], queryFn: () => api.documents.manual() })
  const refresh = () => qc.invalidateQueries({ queryKey: ['manual'] })

  const upload = useMutation({
    mutationFn: (file: File) => uploadDocument(file, 'Manual'),
    onSuccess: () => { notifyOk('The manual is now available to everyone.', 'Manual uploaded'); refresh() },
    onError: e => notifyError(e),
  })
  const remove = useMutation({
    mutationFn: (id: string) => api.documents.remove(id),
    onSuccess: () => { notifyOk('The manual was removed.', 'Removed'); refresh() },
    onError: e => notifyError(e),
  })

  if (manual.isLoading) return <Loading />
  if (manual.error) return <ErrorBox error={manual.error} />
  const m = manual.data

  return (
    <Paper className="panel" p="lg">
      <Stack gap="md">
        <Text size="sm" c="dimmed">
          The user manual (PDF) is kept in private storage and shown to officers on the Rules page. Uploading a new file replaces the current one.
        </Text>
        {m ? (
          <Alert icon={<IconBook size={18} />} color="teal" variant="light" title={m.fileName}>
            {mb(m.sizeBytes)}{m.uploadedAt && ` · uploaded ${new Date(m.uploadedAt).toLocaleString('en-GB')}`}
          </Alert>
        ) : (
          <Alert color="gray" variant="light">No manual uploaded yet. Officers see the built-in copy until you add one.</Alert>
        )}
        <Group>
          <FileButton accept="application/pdf" onChange={f => f && upload.mutate(f)}>
            {props => <Button {...props} loading={upload.isPending} leftSection={<IconUpload size={16} />}>{m ? 'Replace manual' : 'Upload manual'}</Button>}
          </FileButton>
          {m && <Button variant="light" onClick={() => openDocument(m.id).catch(notifyError)}>Open</Button>}
          {m && <Button variant="subtle" color="red" loading={remove.isPending} leftSection={<IconTrash size={16} />}
            onClick={() => { if (confirm('Remove the manual?')) remove.mutate(m.id) }}>Remove</Button>}
        </Group>
      </Stack>
    </Paper>
  )
}
