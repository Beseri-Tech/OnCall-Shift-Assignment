import { Badge, Button, Card, Code, Group, Pagination, SegmentedControl, Select, Stack, Text } from '@mantine/core'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconArrowBackUp, IconCheck } from '@tabler/icons-react'
import { useState } from 'react'
import { api, type Feedback, type FeedbackCategory, type FeedbackQuery } from '../../api'
import { ErrorBox, Loading } from '../../components/common'
import { notifyError } from '../../lib'

const PAGE_SIZE = 25

const CATEGORY: Record<FeedbackCategory, { label: string; color: string }> = {
  Bug: { label: 'Problem', color: 'red' },
  Suggestion: { label: 'Idea', color: 'violet' },
  Other: { label: 'Other', color: 'gray' },
}

type Status = 'open' | 'resolved' | 'all'

const whenFmt = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })

function Item({ f }: { f: Feedback }) {
  const qc = useQueryClient()
  const toggle = useMutation({
    mutationFn: () => (f.resolvedAt ? api.admin.reopenFeedback(f.id) : api.admin.resolveFeedback(f.id)),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'feedback'] }),
    onError: e => notifyError(e),
  })
  const c = CATEGORY[f.category]

  return (
    <Card withBorder padding="md" opacity={f.resolvedAt ? 0.7 : 1}>
      <Group justify="space-between" align="flex-start" wrap="nowrap" gap="md">
        <Stack gap={6} style={{ minWidth: 0, flex: 1 }}>
          <Group gap="xs" wrap="wrap">
            <Badge variant="light" color={c.color}>{c.label}</Badge>
            {f.resolvedAt && <Badge variant="light" color="teal">Resolved</Badge>}
            <Text size="sm" fw={600}>{f.senderName ?? f.senderEmail ?? 'Deleted account'}</Text>
            {f.senderName && <Text size="xs" c="dimmed">{f.senderEmail}</Text>}
          </Group>
          <Text size="sm" style={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{f.message}</Text>
          <Text size="xs" c="dimmed">
            {whenFmt.format(new Date(f.createdAt))}
            {f.page && <> · on <Code fz="xs">{f.page}</Code></>}
            {f.resolvedAt && ` · resolved ${whenFmt.format(new Date(f.resolvedAt))}${f.resolvedByEmail ? ` by ${f.resolvedByEmail}` : ''}`}
          </Text>
        </Stack>
        <Button size="xs" variant={f.resolvedAt ? 'subtle' : 'light'} loading={toggle.isPending} onClick={() => toggle.mutate()}
          leftSection={f.resolvedAt ? <IconArrowBackUp size={14} /> : <IconCheck size={14} />}>
          {f.resolvedAt ? 'Reopen' : 'Mark resolved'}
        </Button>
      </Group>
    </Card>
  )
}

/** Feedback users sent from the header button. Admins and supervisors only (the API checks too). */
export function FeedbackTab() {
  const [status, setStatus] = useState<Status>('open')
  const [category, setCategory] = useState<FeedbackCategory | null>(null)
  const [page, setPage] = useState(1)

  const query: FeedbackQuery = {
    status: status === 'all' ? undefined : status, category: category ?? undefined, page, pageSize: PAGE_SIZE,
  }
  const list = useQuery({ queryKey: ['admin', 'feedback', query], queryFn: () => api.admin.feedback(query), placeholderData: p => p })

  if (list.error) return <ErrorBox error={list.error} />

  const open = list.data?.open ?? 0
  const pages = Math.max(1, Math.ceil((list.data?.total ?? 0) / PAGE_SIZE))

  return (
    <Stack gap="md">
      <Group wrap="wrap" align="flex-end">
        <SegmentedControl value={status} onChange={v => { setStatus(v as Status); setPage(1) }} data={[
          { value: 'open', label: `Open${open ? ` (${open})` : ''}` },
          { value: 'resolved', label: 'Resolved' },
          { value: 'all', label: 'All' },
        ]} />
        <Select placeholder="Any kind" clearable w={180} value={category}
          onChange={v => { setCategory(v as FeedbackCategory | null); setPage(1) }}
          data={(Object.keys(CATEGORY) as FeedbackCategory[]).map(k => ({ value: k, label: CATEGORY[k].label }))} />
      </Group>

      <Text size="sm" c="dimmed">
        What officers and admins sent with the feedback button in the header, newest first. Mark an item resolved once it's dealt with.
      </Text>

      {list.isLoading ? <Loading /> : (
        <Stack gap="sm" opacity={list.isPlaceholderData ? 0.6 : 1}>
          {list.data?.items.length === 0 && (
            <Text c="dimmed" ta="center" py="lg">{status === 'open' ? 'No open feedback. All caught up.' : 'No feedback matches.'}</Text>
          )}
          {list.data?.items.map(f => <Item key={f.id} f={f} />)}
        </Stack>
      )}

      {pages > 1 && <Group justify="center"><Pagination total={pages} value={page} onChange={setPage} /></Group>}
    </Stack>
  )
}
