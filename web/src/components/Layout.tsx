import { Box, Burger, Container, Divider, Drawer, Group, Menu, ScrollArea, Stack, Text, UnstyledButton } from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  IconBook, IconCalendarCheck, IconCalendarEvent, IconCalendarUser, IconChevronDown, IconKey, IconLayoutGrid, IconLogout, IconMessage2, IconShieldLock, IconUserCircle,
} from '@tabler/icons-react'
import { useEffect, useState } from 'react'
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router'
import { api } from '../api'
import { isAdmin, useMe } from '../auth'
import { FeedbackButton, FeedbackModal } from './FeedbackButton'
import { NotificationBell } from './NotificationBell'

/** Below this the header nav moves into a side drawer (Mantine's md: 62em, about 992px), so phones and tablets get the menu. */
const DRAWER_BELOW = 'md'

export function Layout() {
  const me = useMe().data
  const qc = useQueryClient()
  const navigate = useNavigate()
  const { pathname } = useLocation()
  const [drawer, { toggle, close }] = useDisclosure(false)
  const [feedback, setFeedback] = useState(false)
  const logout = useMutation({ mutationFn: api.auth.logout, onSuccess: () => { qc.clear(); navigate('/login') } })

  // Following a link closes the drawer.
  useEffect(close, [pathname, close])

  // Supervisors aren't on the rota: no leave of their own, but they can edit officers' leave.
  const links = [
    me?.personId
      ? { to: '/', label: 'My leave', icon: IconCalendarUser, end: true }
      : { to: '/leave', label: 'Leave', icon: IconCalendarUser, end: false },
    ...(me?.personId ? [{ to: '/my-oncall', label: 'My on-call', icon: IconCalendarCheck, end: false }] : []),
    { to: '/overview', label: 'Overview', icon: IconLayoutGrid, end: false },
    { to: '/rota', label: 'Rota', icon: IconCalendarEvent, end: false },
    { to: '/rules', label: 'Rules', icon: IconBook, end: false },
    ...(isAdmin(me) ? [{ to: '/admin', label: 'Admin', icon: IconShieldLock, end: false }] : []),
  ]

  return (
    <Box mih="100vh" bg="var(--app-bg)">
      <Box component="header" className="app-header">
        <Container size="xl" h="100%" px={{ base: 'xs', sm: 'md' }}>
          <Group h="100%" justify="space-between" wrap="nowrap" gap="xs">
            <Group gap={8} wrap="nowrap" miw={0}>
              <Burger opened={drawer} onClick={toggle} hiddenFrom={DRAWER_BELOW} color="white" size="sm" aria-label="Menu" />
              <Link to="/" className="brand-link">
                <div className="brand-mark"><img src="/favicon.svg" width={26} height={26} alt="" /></div>
                <div className="brand-text">
                  <Text fw={800} size="lg" c="white" lh={1.1} truncate>On-Call Rota</Text>
                  <Text size="xs" c="brand.1" lh={1.1} truncate visibleFrom="lg">Dental Officers · Perlis</Text>
                </div>
              </Link>
            </Group>
            <Group gap={4} wrap="nowrap">
              <Group gap={4} wrap="nowrap" visibleFrom={DRAWER_BELOW}>
                {links.map(({ to, label, icon: Icon, end }) => (
                  <UnstyledButton key={to} component={NavLink} to={to} end={end} className="nav-link">
                    <Icon size={18} stroke={1.8} />
                    <span>{label}</span>
                  </UnstyledButton>
                ))}
              </Group>
              <FeedbackButton onClick={() => setFeedback(true)} />
              <NotificationBell />
              <Box visibleFrom={DRAWER_BELOW}>
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
              </Box>
            </Group>
          </Group>
        </Container>
      </Box>

      <Drawer opened={drawer} onClose={close} size={300} padding={0} withCloseButton={false} hiddenFrom={DRAWER_BELOW}
        aria-label="Menu" classNames={{ body: 'drawer-body' }}>
        <div className="drawer-head">
          <Group gap={10} wrap="nowrap" miw={0}>
            <div className="brand-mark"><img src="/favicon.svg" width={26} height={26} alt="" /></div>
            <div style={{ minWidth: 0 }}>
              <Text fw={800} c="white" lh={1.1}>On-Call Rota</Text>
              <Text size="xs" c="brand.1" lh={1.2}>Dental Officers · Perlis</Text>
            </div>
          </Group>
          <Burger opened onClick={close} color="white" size="sm" aria-label="Close menu" />
        </div>
        <ScrollArea style={{ flex: 1 }}>
          <Stack gap={2} p="sm" component="nav">
            {links.map(({ to, label, icon: Icon, end }) => (
              <UnstyledButton key={to} component={NavLink} to={to} end={end} className="drawer-link">
                <Icon size={20} stroke={1.8} />
                <span>{label === 'Rules' ? 'Rules and guidelines' : label}</span>
              </UnstyledButton>
            ))}
          </Stack>
          <Divider mx="sm" />
          <Box p="sm">
            <Group gap="sm" wrap="nowrap" px="sm" py={6}>
              <IconUserCircle size={28} stroke={1.5} color="var(--brand)" style={{ flexShrink: 0 }} />
              <div style={{ minWidth: 0 }}>
                <Text size="sm" fw={600} truncate>{me?.personName ?? me?.email}</Text>
                <Text size="xs" c="dimmed" truncate>{me?.personName ? `${me.email} · ${me.role}` : me?.role}</Text>
              </div>
            </Group>
            <UnstyledButton className="drawer-link" onClick={() => { close(); setFeedback(true) }}>
              <IconMessage2 size={20} stroke={1.8} />
              <span>Send feedback</span>
            </UnstyledButton>
            <UnstyledButton component={Link} to="/account/password" className="drawer-link">
              <IconKey size={20} stroke={1.8} />
              <span>Change password</span>
            </UnstyledButton>
            <UnstyledButton className="drawer-link" onClick={() => logout.mutate()}>
              <IconLogout size={20} stroke={1.8} />
              <span>Sign out</span>
            </UnstyledButton>
          </Box>
        </ScrollArea>
      </Drawer>

      <FeedbackModal opened={feedback} onClose={() => setFeedback(false)} />

      <Container size="xl" py={{ base: 'md', sm: 'lg' }} px={{ base: 'sm', sm: 'md' }}>
        <Outlet />
      </Container>
    </Box>
  )
}
