import { Button, Divider, Group, Indicator, Popover, ScrollArea, Stack, Text, UnstyledButton } from '@mantine/core'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconBell, IconChecks } from '@tabler/icons-react'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import { api, type AppNotification } from '../api'

const ago = (iso: string, now: number) => {
  const mins = Math.round((now - new Date(iso).getTime()) / 60_000)
  if (mins < 1) return 'just now'
  if (mins < 60) return `${mins} min ago`
  const hours = Math.round(mins / 60)
  if (hours < 24) return `${hours} h ago`
  return new Date(iso).toLocaleDateString('en-GB', { day: 'numeric', month: 'short' })
}

/** Header bell: unread count, latest notifications, click to open the related page. */
export function NotificationBell() {
  const qc = useQueryClient()
  const navigate = useNavigate()
  const [opened, setOpened] = useState(false)
  const data = useQuery({ queryKey: ['notifications'], queryFn: api.notifications, refetchInterval: 60_000 })

  const refresh = () => qc.invalidateQueries({ queryKey: ['notifications'] })
  const readAll = useMutation({ mutationFn: api.readAllNotifications, onSuccess: refresh })
  const open = useMutation({
    mutationFn: (n: AppNotification) => (n.read ? Promise.resolve() : api.readNotification(n.id)),
    onSuccess: (_, n) => {
      refresh()
      setOpened(false)
      if (n.link) navigate(n.link)
    },
  })

  const unread = data.data?.unread ?? 0
  const items = data.data?.items ?? []

  return (
    <Popover opened={opened} onChange={setOpened} position="bottom-end" width={360} shadow="md" withinPortal>
      <Popover.Target>
        <Indicator label={unread > 9 ? '9+' : unread} size={16} color="red" disabled={unread === 0} offset={4}>
          <UnstyledButton className="nav-link" aria-label={`Notifications (${unread} unread)`} onClick={() => setOpened(o => !o)}>
            <IconBell size={20} stroke={1.8} />
          </UnstyledButton>
        </Indicator>
      </Popover.Target>
      <Popover.Dropdown p={0}>
        <Group justify="space-between" px="md" py="xs">
          <Text fw={700}>Notifications</Text>
          <Button variant="subtle" size="compact-xs" leftSection={<IconChecks size={14} />} disabled={unread === 0}
            loading={readAll.isPending} onClick={() => readAll.mutate()}>Mark all read</Button>
        </Group>
        <Divider />
        {items.length === 0
          ? <Text size="sm" c="dimmed" ta="center" py="lg">Nothing yet.</Text>
          : (
            <ScrollArea.Autosize mah={420}>
              <Stack gap={0}>
                {items.map(n => (
                  <UnstyledButton key={n.id} onClick={() => open.mutate(n)} px="md" py="sm"
                    style={{ borderBottom: '1px solid var(--grid-line)', background: n.read ? undefined : 'var(--brand-soft)' }}>
                    <Group justify="space-between" gap="xs" wrap="nowrap" align="flex-start">
                      <Text size="sm" fw={n.read ? 500 : 700}>{n.title}</Text>
                      <Text size="xs" c="dimmed" style={{ whiteSpace: 'nowrap' }}>{ago(n.createdAt, data.dataUpdatedAt)}</Text>
                    </Group>
                    {n.body && <Text size="xs" c="dimmed" mt={2} style={{ whiteSpace: 'pre-line' }}>{n.body}</Text>}
                  </UnstyledButton>
                ))}
              </Stack>
            </ScrollArea.Autosize>
          )}
      </Popover.Dropdown>
    </Popover>
  )
}

