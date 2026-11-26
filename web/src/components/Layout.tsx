import { Box, Burger, Container, Divider, Drawer, Group, Menu, ScrollArea, Stack, Text, UnstyledButton } from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  IconBook, IconBuildingHospital, IconCalendarCheck, IconCalendarEvent, IconCalendarUser, IconChevronDown, IconHistory, IconId, IconKey, IconLayoutGrid, IconLogout, IconMessage2, IconPlaneDeparture, IconReceipt2, IconShieldLock, IconStethoscope, IconUserCircle,
} from '@tabler/icons-react'
import { Suspense, lazy, useEffect, useState } from 'react'
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router'
import { api } from '../api'
import { isAdmin, isMaster, useMe } from '../auth'
import { FacilitySwitcher } from './FacilitySwitcher'
import { Loading } from './common'
import { FeedbackButton, FeedbackModal } from './FeedbackButton'
import { APP_VERSION } from '../changelog'
import { setWeekendDays } from '../dates'
import { NotificationBell } from './NotificationBell'

const HospitalsPage = lazy(() => import('../pages/HospitalsPage').then(m => ({ default: m.HospitalsPage })))

/** Below this the header nav moves into a side drawer (Mantine's md: 62em, about 992px), so phones and tablets get the menu. */
const DRAWER_BELOW = 'md'

interface NavItem { to: string; label: string; icon: typeof IconBook; end: boolean }
/** Related pages share one header button that opens a small menu, so the bar stays on one line. */
interface NavGroup { label: string; icon: typeof IconBook; items: NavItem[] }
type Nav = NavItem | NavGroup
const isGroup = (n: Nav): n is NavGroup => 'items' in n
const matches = (n: NavItem, pathname: string) => (n.end ? pathname === n.to : pathname.startsWith(n.to))

export function Layout() {
  const me = useMe().data
  setWeekendDays(me?.facility?.weekendDays)   // before any page below reads isWeekend
  const qc = useQueryClient()
  const navigate = useNavigate()
  const { pathname } = useLocation()
  const [drawer, { toggle, close }] = useDisclosure(false)
  const [feedback, setFeedback] = useState(false)
  const logout = useMutation({ mutationFn: api.auth.logout, onSuccess: () => { qc.clear(); navigate('/login') } })

  // Following a link closes the drawer.
  useEffect(close, [pathname, close])

  // A master works in one hospital at a time; until they pick one they get the hospitals page.
  const master = isMaster(me)
  const allHospitals = master && !me?.facility && !['/changelog', '/account'].some(p => pathname.startsWith(p))
  const subtitle = `Dental Officers · ${me?.facility?.name ?? 'All hospitals'}`

  // Officers get two short menus (their leave; their on call). Supervisors aren't on the rota: no leave or on call of their
  // own, so a plain Leave page (everyone's leave) and the case log.
  const nav: Nav[] = allHospitals ? [{ to: '/hospitals', label: 'Hospitals', icon: IconBuildingHospital, end: true }] : [
    me?.personId
      ? { label: 'Leave', icon: IconCalendarUser, items: [
          { to: '/my-leave', label: 'My leave', icon: IconCalendarUser, end: true },
          { to: '/leave-requests', label: 'Request leave', icon: IconPlaneDeparture, end: false },
        ] }
      : { to: '/leave', label: 'Leave', icon: IconCalendarUser, end: false },
    me?.personId
      ? { label: 'On call', icon: IconCalendarCheck, items: [
          { to: '/my-oncall', label: 'My on-call', icon: IconCalendarCheck, end: false },
          { to: '/cases', label: 'Cases', icon: IconStethoscope, end: false },
          { to: '/etap', label: 'ETAP Claim', icon: IconReceipt2, end: false },
        ] }
      : { to: '/cases', label: 'Cases', icon: IconStethoscope, end: false },
    { to: '/overview', label: 'Overview', icon: IconLayoutGrid, end: false },
    { to: '/rota', label: 'Rota', icon: IconCalendarEvent, end: false },
    { to: '/help', label: 'Help', icon: IconBook, end: false },
    ...(isAdmin(me) ? [{ to: '/admin', label: 'Admin', icon: IconShieldLock, end: false }] : []),
  ]

  return (
    <Box mih="100vh" bg="var(--app-bg)" style={{ display: 'flex', flexDirection: 'column' }}>
      <Box component="header" className="app-header">
        <Container size="xl" h="100%" px={{ base: 'xs', sm: 'md' }}>
          <Group h="100%" justify="space-between" wrap="nowrap" gap="xs">
            <Group gap={8} wrap="nowrap" miw={0}>
              <Burger opened={drawer} onClick={toggle} hiddenFrom={DRAWER_BELOW} color="white" size="sm" aria-label="Menu" />
              <Link to="/home" className="brand-link">
                <div className="brand-mark"><img src="/favicon.svg" width={26} height={26} alt="" /></div>
                <div className="brand-text">
                  <Text fw={800} size="lg" c="white" lh={1.1} truncate>Rota-OC</Text>
                  <Text size="xs" c="brand.1" lh={1.1} truncate visibleFrom="lg">{subtitle}</Text>
                </div>
              </Link>
            </Group>
            <Group gap={4} wrap="nowrap">
              <Group gap={4} wrap="nowrap" visibleFrom={DRAWER_BELOW}>
                {nav.map(n => isGroup(n) ? (
                  <Menu key={n.label} trigger="click-hover" openDelay={60} closeDelay={150} position="bottom-start" withinPortal>
                    <Menu.Target>
                      <UnstyledButton className={`nav-link${n.items.some(i => matches(i, pathname)) ? ' active' : ''}`}>
                        <n.icon size={18} stroke={1.8} />
                        <span>{n.label}</span>
                        <IconChevronDown size={14} />
                      </UnstyledButton>
                    </Menu.Target>
                    <Menu.Dropdown>
                      {n.items.map(({ to, label, icon: Icon }) => (
                        <Menu.Item key={to} component={Link} to={to} leftSection={<Icon size={16} />}
                          fw={n.items.find(i => matches(i, pathname))?.to === to ? 700 : undefined}>{label}</Menu.Item>
                      ))}
                    </Menu.Dropdown>
                  </Menu>
                ) : (
                  <UnstyledButton key={n.to} component={NavLink} to={n.to} end={n.end} className="nav-link">
                    <n.icon size={18} stroke={1.8} />
                    <span>{n.label}</span>
                  </UnstyledButton>
                ))}
              </Group>
              {master && <Box visibleFrom={DRAWER_BELOW}><FacilitySwitcher /></Box>}
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
                    {me?.personId && <Menu.Item leftSection={<IconId size={16} />} component={Link} to="/profile">My ETAP profile</Menu.Item>}
                    <Menu.Item leftSection={<IconKey size={16} />} component={Link} to="/account/password">Change password</Menu.Item>
                    <Menu.Item leftSection={<IconHistory size={16} />} component={Link} to="/changelog">Changelog · v{APP_VERSION}</Menu.Item>
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
              <Text fw={800} c="white" lh={1.1}>Rota-OC</Text>
              <Text size="xs" c="brand.1" lh={1.2}>{subtitle}</Text>
            </div>
          </Group>
          <Burger opened onClick={close} color="white" size="sm" aria-label="Close menu" />
        </div>
        <ScrollArea style={{ flex: 1 }}>
          {master && <Box px="sm" pt="sm"><FacilitySwitcher w="100%" /></Box>}
          <Stack gap={2} p="sm" component="nav">
            {nav.flatMap(n => isGroup(n) ? [{ heading: n.label }, ...n.items] : [n]).map(n => 'heading' in n ? (
              <Text key={n.heading} size="xs" fw={700} c="dimmed" tt="uppercase" px="sm" pt="sm">{n.heading}</Text>
            ) : (({ to, label, icon: Icon, end }: NavItem) => (
              <UnstyledButton key={to} component={NavLink} to={to} end={end} className="drawer-link">
                <Icon size={20} stroke={1.8} />
                <span>{label}</span>
              </UnstyledButton>
            ))(n))}
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
            {me?.personId && (
              <UnstyledButton component={Link} to="/profile" className="drawer-link">
                <IconId size={20} stroke={1.8} />
                <span>My ETAP profile</span>
              </UnstyledButton>
            )}
            <UnstyledButton component={Link} to="/account/password" className="drawer-link">
              <IconKey size={20} stroke={1.8} />
              <span>Change password</span>
            </UnstyledButton>
            <UnstyledButton component={Link} to="/changelog" className="drawer-link">
              <IconHistory size={20} stroke={1.8} />
              <span>Changelog · v{APP_VERSION}</span>
            </UnstyledButton>
            <UnstyledButton className="drawer-link" onClick={() => logout.mutate()}>
              <IconLogout size={20} stroke={1.8} />
              <span>Sign out</span>
            </UnstyledButton>
          </Box>
        </ScrollArea>
      </Drawer>

      <FeedbackModal opened={feedback} onClose={() => setFeedback(false)} />

      <Container size="xl" w="100%" py={{ base: 'md', sm: 'lg' }} px={{ base: 'sm', sm: 'md' }} style={{ flex: 1 }}>
        {allHospitals ? <Suspense fallback={<Loading />}><HospitalsPage /></Suspense> : <Outlet />}
      </Container>
      <Container size="xl" w="100%" pb="md" px={{ base: 'sm', sm: 'md' }}>
        <Text component={Link} to="/changelog" size="xs" c="dimmed" ta="center" display="block" td="none">
          Rota-OC v{APP_VERSION} · What's new
        </Text>
        <Text size="xs" c="dimmed" ta="center" mt={4}>Developed by Beseri-Tech</Text>
      </Container>
    </Box>
  )
}
