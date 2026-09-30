import { Box, Container, Group, Menu, Text, UnstyledButton } from '@mantine/core'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  IconCalendarCheck, IconCalendarEvent, IconCalendarUser, IconChevronDown, IconKey, IconLayoutGrid, IconLogout, IconShieldLock, IconUserCircle,
} from '@tabler/icons-react'
import { Link, NavLink, Outlet, useNavigate } from 'react-router'
import { api } from '../api'
import { isAdmin, useMe } from '../auth'
import { NotificationBell } from './NotificationBell'

export function Layout() {
  const me = useMe().data
  const qc = useQueryClient()
  const navigate = useNavigate()
  const logout = useMutation({ mutationFn: api.auth.logout, onSuccess: () => { qc.clear(); navigate('/login') } })

  // Supervisors aren't on the rota: no leave of their own, but they can edit officers' leave.
  const links = [
    me?.personId
      ? { to: '/', label: 'My leave', icon: IconCalendarUser, end: true }
      : { to: '/leave', label: 'Leave', icon: IconCalendarUser, end: false },
    ...(me?.personId ? [{ to: '/my-oncall', label: 'My on-call', icon: IconCalendarCheck, end: false }] : []),
    { to: '/overview', label: 'Overview', icon: IconLayoutGrid, end: false },
    { to: '/rota', label: 'Rota', icon: IconCalendarEvent, end: false },
    ...(isAdmin(me) ? [{ to: '/admin', label: 'Admin', icon: IconShieldLock, end: false }] : []),
  ]

  return (
    <Box mih="100vh" bg="var(--app-bg)">
      <Box component="header" className="app-header">
        <Container size="xl" h="100%">
          <Group h="100%" justify="space-between" wrap="nowrap">
            <Group gap={10} wrap="nowrap">
              <img src="/favicon.svg" width={28} height={28} alt="" />
              <Text fw={800} size="lg" c="white" visibleFrom="xs">On-Call Rota</Text>
            </Group>
            <Group gap={4} wrap="nowrap">
              {links.map(({ to, label, icon: Icon, end }) => (
                <UnstyledButton key={to} component={NavLink} to={to} end={end} className="nav-link">
                  <Icon size={18} stroke={1.8} />
                  <span className="nav-label">{label}</span>
                </UnstyledButton>
              ))}
              <NotificationBell />
              <Menu position="bottom-end" withinPortal>
                <Menu.Target>
                  <UnstyledButton className="nav-link" aria-label="Account">
                    <IconUserCircle size={20} stroke={1.8} />
                    <IconChevronDown size={14} />
                  </UnstyledButton>
                </Menu.Target>
                <Menu.Dropdown>
                  <Menu.Label>
                    <Text size="sm" fw={600} c="dark">{me?.personName ?? me?.email}</Text>
                    <Text size="xs" c="dimmed">{me?.email} · {me?.role}</Text>
                  </Menu.Label>
                  <Menu.Item leftSection={<IconKey size={16} />} component={Link} to="/account/password">Change password</Menu.Item>
                  <Menu.Item leftSection={<IconLogout size={16} />} onClick={() => logout.mutate()}>Sign out</Menu.Item>
                </Menu.Dropdown>
              </Menu>
            </Group>
          </Group>
        </Container>
      </Box>
      <Container size="xl" py="lg">
        <Outlet />
      </Container>
    </Box>
  )
}
