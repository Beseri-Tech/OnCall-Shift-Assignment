import { Badge, Group, Paper, Stack, Text, Title } from '@mantine/core'
import dayjs from 'dayjs'
import { PageHeader } from '../components/common'
import { APP_COMMIT, APP_VERSION, CHANGELOG, type Change } from '../changelog'

const KIND: Record<Change['kind'], { label: string; color: string }> = {
  new: { label: 'New', color: 'gray' },
  improved: { label: 'Improved', color: 'gray' },
  fixed: { label: 'Fixed', color: 'gray' },
}

export function ChangelogPage() {
  return (
    <Stack gap="md" maw={820}>
      <PageHeader title="Changelog" description={`You are on version ${APP_VERSION} (build ${APP_COMMIT}).`} />
      {CHANGELOG.map(r => (
        <Paper key={r.version} className="panel" p="lg">
          <Stack gap="xs">
            <Group gap="sm" align="baseline">
              <Title order={3} size="h4">{r.version}</Title>
              {r.version === APP_VERSION && <Badge size="sm" variant="light" color="brand" tt="none">Current</Badge>}
              <Text size="sm" c="dimmed">{dayjs(r.date).format('D MMM YYYY')}</Text>
            </Group>
            <Text fw={500}>{r.title}</Text>
            <Stack gap={6}>
              {r.changes.map((c, i) => (
                <Group key={i} gap="xs" wrap="nowrap" align="flex-start">
                  <Badge size="xs" w={72} fw={500} tt="none" variant="light" color={KIND[c.kind].color} mt={3} style={{ flexShrink: 0 }}>{KIND[c.kind].label}</Badge>
                  <Text size="sm">{c.text}</Text>
                </Group>
              ))}
            </Stack>
          </Stack>
        </Paper>
      ))}
    </Stack>
  )
}
