import { Paper, Stack, Tabs } from '@mantine/core'
import { IconCalendarCog, IconHistory, IconShieldLock, IconSum, IconUserShield, IconUsers, IconWand } from '@tabler/icons-react'
import { PageHeader } from '../../components/common'
import { useNavigate, useParams } from 'react-router'
import { AccountsTab } from './AccountsTab'
import { AuditTab } from './AuditTab'
import { PeopleTab } from './PeopleTab'
import { PeriodsTab } from './PeriodsTab'
import { RotaTab } from './RotaTab'
import { TallyTab } from './TallyTab'

const TABS = ['rota', 'people', 'tally', 'periods', 'accounts', 'log'] as const
type Tab = (typeof TABS)[number]

/** Only reachable by admins and supervisors (see AdminOnly in main.tsx; the API checks too). */
export function AdminPage() {
  const navigate = useNavigate()
  const { '*': sub } = useParams()
  const tab: Tab = TABS.includes(sub as Tab) ? (sub as Tab) : 'rota'

  return (
    <Stack gap="md">
      <PageHeader icon={IconShieldLock} title="Admin" description="Build and publish the rota, manage officers, tallies, periods and accounts, and see who changed what." />
      <Tabs value={tab} onChange={v => navigate(`/admin/${v}`)} keepMounted={false} variant="pills" radius="xl">
        <Paper className="panel" p={6} mb="md" radius="xl">
        <Tabs.List>
          <Tabs.Tab value="rota" leftSection={<IconWand size={16} />}>Generate rota</Tabs.Tab>
          <Tabs.Tab value="people" leftSection={<IconUsers size={16} />}>Officers</Tabs.Tab>
          <Tabs.Tab value="tally" leftSection={<IconSum size={16} />}>Tally</Tabs.Tab>
          <Tabs.Tab value="periods" leftSection={<IconCalendarCog size={16} />}>Periods & holidays</Tabs.Tab>
          <Tabs.Tab value="accounts" leftSection={<IconUserShield size={16} />}>Accounts</Tabs.Tab>
          <Tabs.Tab value="log" leftSection={<IconHistory size={16} />}>Audit log</Tabs.Tab>
        </Tabs.List>
        </Paper>
        <Tabs.Panel value="rota"><RotaTab /></Tabs.Panel>
        <Tabs.Panel value="people"><PeopleTab /></Tabs.Panel>
        <Tabs.Panel value="tally"><TallyTab /></Tabs.Panel>
        <Tabs.Panel value="periods"><PeriodsTab /></Tabs.Panel>
        <Tabs.Panel value="accounts"><AccountsTab /></Tabs.Panel>
        <Tabs.Panel value="log"><AuditTab /></Tabs.Panel>
      </Tabs>
    </Stack>
  )
}
