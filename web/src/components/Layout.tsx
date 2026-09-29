import { Box, Container, Group, Text, UnstyledButton } from '@mantine/core'
import { IconCalendarEvent, IconCalendarUser, IconLayoutGrid, IconShieldLock } from '@tabler/icons-react'
import { NavLink, Outlet } from 'react-router'

const links = [
  { to: '/', label: 'My leave', icon: IconCalendarUser, end: true },
  { to: '/overview', label: 'Overview', icon: IconLayoutGrid, end: false },
  { to: '/rota', label: 'Rota', icon: IconCalendarEvent, end: false },
  { to: '/admin', label: 'Admin', icon: IconShieldLock, end: false },
]

export function Layout() {
  return (
    <Box mih="100vh" bg="var(--app-bg)">
      <Box component="header" className="app-header">
        <Container size="xl" h="100%">
          <Group h="100%" justify="space-between" wrap="nowrap">
            <Group gap={10} wrap="nowrap">
              <img src="/favicon.svg" width={28} height={28} alt="" />
              <Text fw={800} size="lg" c="white">On-Call Rota</Text>
            </Group>
            <Group gap={4} wrap="nowrap">
              {links.map(({ to, label, icon: Icon, end }) => (
                <UnstyledButton key={to} component={NavLink} to={to} end={end} className="nav-link">
                  <Icon size={18} stroke={1.8} />
                  <span className="nav-label">{label}</span>
                </UnstyledButton>
              ))}
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
