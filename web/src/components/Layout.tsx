import { AppShell, Box, Burger, Container, Divider, Group, Menu, ScrollArea, Text, Tooltip, UnstyledButton } from '@mantine/core'
import { useDisclosure, useLocalStorage, useMediaQuery, useWindowEvent } from '@mantine/hooks'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  IconBook, IconBuildingHospital, IconCalendarCheck, IconCalendarEvent, IconCalendarUser, IconChevronDown, IconHistory, IconId, IconKey, IconLayoutGrid, IconLayoutSidebarLeftCollapse, IconLayoutSidebarLeftExpand, IconLogout, IconMessage2, IconPlaneDeparture, IconReceipt2, IconStethoscope, IconUserCircle,
} from '@tabler/icons-react'
import { Suspense, lazy, useEffect, useState } from 'react'
import { Link, Outlet, useLocation, useNavigate } from 'react-router'
import { api } from '../api'
import { isAdmin, isMaster, useMe } from '../auth'
import { ADMIN_SECTIONS, adminSectionOf } from '../pages/admin/adminSections'
import { FacilitySwitcher } from './FacilitySwitcher'
import { Loading } from './common'
import { FeedbackButton, FeedbackModal } from './FeedbackButton'
import { APP_VERSION } from '../changelog'
import { setWeekendDays } from '../dates'
import { NotificationBell } from './NotificationBell'

const HospitalsPage = lazy(() => import('../pages/HospitalsPage').then(m => ({ default: m.HospitalsPage })))

/** At and above this (Mantine's md: 62em, about 992px) the nav is a side bar; below it, a drawer opened by the burger. */
const SIDE_BELOW = 'md'
const SIDE_QUERY = '(min-width: 62em)'
const NAV_WIDTH = 248
const RAIL_WIDTH = 64

interface NavItem { to: string; label: string; icon: typeof IconBook; end?: boolean; active?: boolean }
interface NavGroup { heading?: string; items: NavItem[] }

export function Layout() {
  const me = useMe().data
  setWeekendDays(me?.facility?.weekendDays)   // before any page below reads isWeekend
  const qc = useQueryClient()
  const navigate = useNavigate()
  const { pathname } = useLocation()
  const [drawer, { toggle, close }] = useDisclosure(false)
  const [collapsed, setCollapsed] = useLocalStorage({ key: 'rota-oc-nav-collapsed', defaultValue: false })
  const desktop = useMediaQuery(SIDE_QUERY)
  const rail = collapsed && !!desktop   // icons only: only on the side bar, never in the drawer
  const [feedback, setFeedback] = useState(false)
  const logout = useMutation({ mutationFn: api.auth.logout, onSuccess: () => { qc.clear(); navigate('/login') } })

  // Following a link closes the drawer.
  useEffect(close, [pathname, close])
  // Escape closes the phone menu wherever focus is.
  useWindowEvent('keydown', e => { if (e.key === 'Escape' && drawer) close() })

  // A master works in one hospital at a time; until they pick one they get the hospitals page.
  const master = isMaster(me)
  const allHospitals = master && !me?.facility && !['/changelog', '/account'].some(p => pathname.startsWith(p))
  const subtitle = `Dental Officers, ${me?.facility?.name ?? 'All hospitals'}`

  // Officers get their leave and on-call pages. Supervisors aren't on the rota: no leave or on call of their own, so a plain
  // Leave page (everyone's leave) and the case log. Admins also get the admin sections.
  const adminTab = adminSectionOf(pathname.split('/')[2])
  const groups: NavGroup[] = allHospitals
    ? [{ items: [{ to: '/hospitals', label: 'Hospitals', icon: IconBuildingHospital, end: true }] }]
    : [
        { heading: 'Leave', items: me?.personId
          ? [
              { to: '/my-leave', label: 'My leave', icon: IconCalendarUser, end: true },
              { to: '/leave-requests', label: 'Request leave', icon: IconPlaneDeparture },
            ]
          : [{ to: '/leave', label: 'Leave', icon: IconCalendarUser }] },
        { heading: 'On call', items: me?.personId
          ? [
              { to: '/my-oncall', label: 'My on-call', icon: IconCalendarCheck },
              { to: '/cases', label: 'Cases', icon: IconStethoscope },
              { to: '/etap', label: 'ETAP Claim', icon: IconReceipt2 },
            ]
          : [{ to: '/cases', label: 'Cases', icon: IconStethoscope }] },
        { heading: 'Rota', items: [
          { to: '/overview', label: 'Overview', icon: IconLayoutGrid },
          { to: '/rota', label: 'Rota', icon: IconCalendarEvent },
        ] },
        ...(isAdmin(me) ? [{ heading: 'Admin', items: ADMIN_SECTIONS.map(s => ({
          to: `/admin/${s.value}`, label: s.label, icon: s.icon, active: pathname.startsWith('/admin') && adminTab === s.value,
        })) }] : []),
        { items: [{ to: '/help', label: 'Help', icon: IconBook }] },
      ]

  const isActive = (n: NavItem) => n.active ?? (n.end ? pathname === n.to : pathname.startsWith(n.to))

  const navLink = (n: NavItem) => {
    const active = isActive(n)
    const link = (
      <Link key={n.to} to={n.to} className={`side-link${active ? ' active' : ''}`} aria-current={active ? 'page' : undefined}
        aria-label={rail ? n.label : undefined}>
        <n.icon size={18} stroke={1.75} />
        <span className="side-label">{n.label}</span>
      </Link>
    )
    return rail ? <Tooltip key={n.to} label={n.label} position="right" withArrow>{link}</Tooltip> : link
  }

  const phoneLink = (label: string, Icon: typeof IconBook, props: object) => (
    <UnstyledButton className="side-link" {...props}>
      <Icon size={18} stroke={1.75} />
      <span className="side-label">{label}</span>
    </UnstyledButton>
  )

  return (
    <AppShell
      header={{ height: 56 }}
      navbar={{ width: collapsed ? RAIL_WIDTH : NAV_WIDTH, breakpoint: SIDE_BELOW, collapsed: { mobile: !drawer } }}
      padding={0}
      bg="var(--app-bg)"
    >
      <AppShell.Header className="app-header">
        <Group h="100%" justify="space-between" wrap="nowrap" gap="xs" px={{ base: 'xs', sm: 'md' }}>
          <Group gap={8} wrap="nowrap" miw={0}>
            <Burger opened={drawer} onClick={toggle} hiddenFrom={SIDE_BELOW} color="white" size="sm" aria-label="Menu" />
            <Link to="/home" className="brand-link">
              <img src="/favicon.svg" width={28} height={28} alt="" className="brand-mark" />
              <div className="brand-text">
                <Text fw={600} size="lg" c="white" lh={1.1} truncate>Rota-OC</Text>
                <Text size="xs" c="rgb(255 255 255 / 0.7)" lh={1.2} truncate visibleFrom="lg">{subtitle}</Text>
              </div>
            </Link>
          </Group>
          <Group gap={4} wrap="nowrap">
            {master && <Box visibleFrom={SIDE_BELOW}><FacilitySwitcher /></Box>}
            <FeedbackButton onClick={() => setFeedback(true)} />
            <NotificationBell />
            <Box visibleFrom={SIDE_BELOW}>
              <Menu position="bottom-end" withinPortal>
                <Menu.Target>
                  <UnstyledButton className="nav-link" aria-label="Account">
                    <IconUserCircle size={20} stroke={1.75} />
                    <IconChevronDown size={14} />
                  </UnstyledButton>
                </Menu.Target>
                <Menu.Dropdown>
                  <Menu.Label>
                    <Text size="sm" fw={600} c="var(--ink)">{me?.personName ?? me?.email}</Text>
                    <Text size="xs" c="dimmed">{me?.email}</Text>
                    <Text size="xs" c="dimmed">{me?.role}</Text>
                  </Menu.Label>
                  {me?.personId && <Menu.Item leftSection={<IconId size={16} />} component={Link} to="/profile">My ETAP profile</Menu.Item>}
                  <Menu.Item leftSection={<IconKey size={16} />} component={Link} to="/account/password">Change password</Menu.Item>
                  <Menu.Item leftSection={<IconHistory size={16} />} component={Link} to="/changelog">Changelog, v{APP_VERSION}</Menu.Item>
                  <Menu.Item leftSection={<IconLogout size={16} />} onClick={() => logout.mutate()}>Sign out</Menu.Item>
                </Menu.Dropdown>
              </Menu>
            </Box>
          </Group>
        </Group>
      </AppShell.Header>

      <AppShell.Navbar className={`side-nav${rail ? ' rail' : ''}`} aria-label="Menu" component="nav">
        <ScrollArea style={{ flex: 1 }} type="auto">
          <div className="side-list">
            {master && <Box hiddenFrom={SIDE_BELOW} pb="xs"><FacilitySwitcher w="100%" /></Box>}
            {groups.map((g, i) => (
              <div key={g.heading ?? i} className="side-group">
                {g.heading && <Text className="side-heading">{g.heading}</Text>}
                {i > 0 && <Divider className="side-rule" />}
                {g.items.map(navLink)}
              </div>
            ))}
            <Box hiddenFrom={SIDE_BELOW}>
              <Divider my="xs" />
              <Group gap="sm" wrap="nowrap" px="sm" py={6}>
                <IconUserCircle size={28} stroke={1.5} color="var(--text-3)" style={{ flexShrink: 0 }} />
                <div style={{ minWidth: 0 }}>
                  <Text size="sm" fw={600} truncate>{me?.personName ?? me?.email}</Text>
                  <Text size="xs" c="dimmed" truncate>{me?.personName ? me.email : me?.role}</Text>
                  {me?.personName && <Text size="xs" c="dimmed" truncate>{me.role}</Text>}
                </div>
              </Group>
              {phoneLink('Send feedback', IconMessage2, { onClick: () => { close(); setFeedback(true) } })}
              {me?.personId && phoneLink('My ETAP profile', IconId, { component: Link, to: '/profile' })}
              {phoneLink('Change password', IconKey, { component: Link, to: '/account/password' })}
              {phoneLink(`Changelog, v${APP_VERSION}`, IconHistory, { component: Link, to: '/changelog' })}
              {phoneLink('Sign out', IconLogout, { onClick: () => logout.mutate() })}
            </Box>
          </div>
        </ScrollArea>
        <Box visibleFrom={SIDE_BELOW} className="side-foot">
          <Tooltip label="Expand menu" position="right" withArrow disabled={!rail}>
            <UnstyledButton className="side-link" onClick={() => setCollapsed(c => !c)} aria-expanded={!collapsed}
              aria-label={collapsed ? 'Expand menu' : 'Collapse menu'}>
              {collapsed ? <IconLayoutSidebarLeftExpand size={18} stroke={1.75} /> : <IconLayoutSidebarLeftCollapse size={18} stroke={1.75} />}
              <span className="side-label">Collapse menu</span>
            </UnstyledButton>
          </Tooltip>
        </Box>
      </AppShell.Navbar>

      <FeedbackModal opened={feedback} onClose={() => setFeedback(false)} />

      <AppShell.Main className="app-main" style={{ display: 'flex', flexDirection: 'column' }}>
        <Container size="xl" w="100%" py={{ base: 'md', sm: 'lg' }} px={{ base: 'sm', sm: 'md' }} style={{ flex: 1 }}>
          {allHospitals ? <Suspense fallback={<Loading />}><HospitalsPage /></Suspense> : <Outlet />}
        </Container>
        <Container size="xl" w="100%" pb="md" px={{ base: 'sm', sm: 'md' }}>
          <Text component={Link} to="/changelog" size="xs" c="dimmed" ta="center" display="block" td="none">
            Rota-OC {APP_VERSION}, what's new
          </Text>
          <Text size="xs" c="dimmed" ta="center" mt={4}>Developed by Beseri-Tech</Text>
        </Container>
      </AppShell.Main>
    </AppShell>
  )
}
