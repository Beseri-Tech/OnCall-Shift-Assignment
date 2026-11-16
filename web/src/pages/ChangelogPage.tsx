import { IconHistory } from '@tabler/icons-react'
import { Badge, Group, Paper, Stack, Text, Title } from '@mantine/core'
import dayjs from 'dayjs'
import { PageHeader } from '../components/common'
import { APP_COMMIT, APP_VERSION, CHANGELOG, type Change } from '../changelog'

const KIND: Record<Change['kind'], { label: string; color: string }> = {
  new: { label: 'New', color: 'teal' },
  improved: { label: 'Improved', color: 'blue' },
  fixed: { label: 'Fixed', color: 'orange' },
}

export function ChangelogPage() {
  return (
    <Stack gap="md" maw={820}>
      <PageHeader icon={IconHistory} title="Changelog" description={`You are on version ${APP_VERSION} (build ${APP_COMMIT}).`} />
      {CHANGELOG.map(r => (
        <Paper key={r.version} className="panel" p="lg">
          <Stack gap="xs">
            <Group gap="sm" align="baseline">
              <Title order={3} size="h4" c="brand.9">{r.version}</Title>
              {r.version === APP_VERSION && <Badge size="sm">Current</Badge>}
              <Text size="sm" c="dimmed">{dayjs(r.date).format('D MMM YYYY')}</Text>
            </Group>
            <Text fw={600}>{r.title}</Text>
            <Stack gap={6}>
              {r.changes.map((c, i) => (
                <Group key={i} gap="xs" wrap="nowrap" align="flex-start">
                  <Badge size="xs" w={72} variant="light" color={KIND[c.kind].color} mt={3} style={{ flexShrink: 0 }}>{KIND[c.kind].label}</Badge>
                  <Text size="sm">{c.text}</Text>
                </Group>
              ))}
            </Stack>
          </Stack>
        </Paper>
      ))}
      <Text size="xs" c="dimmed" ta="center">Developed by Beseri-Tech</Text>
    </Stack>
  )
}
