import { Button, Center, Group, Paper, PasswordInput, Stack, Tabs, Text, Title } from '@mantine/core'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconCalendarCog, IconFileImport, IconLock, IconLogout, IconUsers, IconWand } from '@tabler/icons-react'
import { useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { ApiError, api } from '../../api'
import { ErrorBox, Loading } from '../../components/common'
import { ImportTab } from './ImportTab'
import { PeopleTab } from './PeopleTab'
import { PeriodsTab } from './PeriodsTab'
import { RotaTab } from './RotaTab'

const TABS = ['rota', 'people', 'periods', 'import'] as const
type Tab = (typeof TABS)[number]

export function AdminPage() {
  const qc = useQueryClient()
  const me = useQuery({
    queryKey: ['admin', 'me'],
    queryFn: api.admin.me,
    retry: (_, e) => !(e instanceof ApiError && e.status === 401),
  })

  const logout = useMutation({
    mutationFn: api.admin.logout,
    onSuccess: () => qc.removeQueries({ queryKey: ['admin'] }),
  })

  if (me.isLoading) return <Loading />
  if (me.error instanceof ApiError && me.error.status === 401) return <Login />
  if (me.error) return <ErrorBox error={me.error} />

  return (
    <Stack gap="md">
      <Group justify="space-between">
        <Title order={2}>Admin</Title>
        <Button variant="default" leftSection={<IconLogout size={16} />} onClick={() => logout.mutate()}>Log out</Button>
      </Group>
      <AdminTabs />
    </Stack>
  )
}

function AdminTabs() {
  const navigate = useNavigate()
  const { '*': sub } = useParams()
  const tab: Tab = TABS.includes(sub as Tab) ? (sub as Tab) : 'rota'

  return (
    <Tabs value={tab} onChange={v => navigate(`/admin/${v}`)} keepMounted={false}>
      <Tabs.List mb="md">
        <Tabs.Tab value="rota" leftSection={<IconWand size={16} />}>Generate rota</Tabs.Tab>
        <Tabs.Tab value="people" leftSection={<IconUsers size={16} />}>People</Tabs.Tab>
        <Tabs.Tab value="periods" leftSection={<IconCalendarCog size={16} />}>Periods & holidays</Tabs.Tab>
        <Tabs.Tab value="import" leftSection={<IconFileImport size={16} />}>Import</Tabs.Tab>
      </Tabs.List>
      <Tabs.Panel value="rota"><RotaTab /></Tabs.Panel>
      <Tabs.Panel value="people"><PeopleTab /></Tabs.Panel>
      <Tabs.Panel value="periods"><PeriodsTab /></Tabs.Panel>
      <Tabs.Panel value="import"><ImportTab /></Tabs.Panel>
    </Tabs>
  )
}

function Login() {
  const qc = useQueryClient()
  const [password, setPassword] = useState('')
  const login = useMutation({
    mutationFn: () => api.admin.login(password),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin'] }),
  })

  return (
    <Center py={60}>
      <Paper withBorder shadow="sm" p="xl" w={380}>
        <form onSubmit={e => { e.preventDefault(); login.mutate() }}>
          <Stack>
            <Group gap="xs"><IconLock /><Title order={3}>Admin login</Title></Group>
            <Text size="sm" c="dimmed">Manage people, rota periods and generate the rota.</Text>
            <PasswordInput
              label="Password"
              value={password}
              onChange={e => setPassword(e.currentTarget.value)}
              error={login.error?.message}
              autoFocus
            />
            <Button type="submit" loading={login.isPending} disabled={!password}>Log in</Button>
          </Stack>
        </form>
      </Paper>
    </Center>
  )
}
