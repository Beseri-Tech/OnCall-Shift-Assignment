import { Paper, Stack, Tabs } from '@mantine/core'
import {
  IconCalendarCog, IconFileCertificate, IconMapPin, IconFileText, IconMessage2, IconPlaneDeparture, IconShieldLock, IconSum, IconUsers, IconWand,
} from '@tabler/icons-react'
import { PageHeader } from '../../components/common'
import { useEffect } from 'react'
import { useNavigate, useParams } from 'react-router'
import { ConfirmedLeaveTab } from './ConfirmedLeaveTab'
import { FeedbackTab } from './FeedbackTab'
import { LocationsTab } from './LocationsTab'
import { PeopleTab } from './PeopleTab'
import { PeriodsTab } from './PeriodsTab'
import { ReportsTab } from './ReportsTab'
import { SuratArahanTab } from './SuratArahanTab'
import { RotaTab } from './RotaTab'
import { TallyTab } from './TallyTab'

const TABS = ['rota', 'people', 'locations', 'leave', 'tally', 'periods', 'feedback', 'reports', 'surat-arahan'] as const
type Tab = (typeof TABS)[number]

/** Only reachable by admins and supervisors (see AdminOnly in main.tsx; the API checks too). */
export function AdminPage() {
  const navigate = useNavigate()
  const { '*': sub } = useParams()
  const tab: Tab = TABS.includes(sub as Tab) ? (sub as Tab) : 'rota'

  // On phones the tab row scrolls: keep the open tab in view (e.g. after following a link to a tab).
  useEffect(() => {
    document.querySelector('.tabs-scroll [data-active]')?.scrollIntoView({ block: 'nearest', inline: 'nearest' })
  }, [tab])

  return (
    <Stack gap="md">
      <PageHeader icon={IconShieldLock} title="Admin" description="Build and publish the rota, manage officers (and their sign-in), clinics, confirmed leave, tallies and periods, read feedback and download reports." />
      <Tabs value={tab} onChange={v => navigate(`/admin/${v}`)} keepMounted={false} variant="pills" radius="xl">
        <Paper className="panel" p={6} mb="md" radius="xl">
        {/* One row; on phones it scrolls sideways instead of wrapping into a tall block. */}
        <Tabs.List className="tabs-scroll">
          <Tabs.Tab value="rota" leftSection={<IconWand size={16} />}>Generate rota</Tabs.Tab>
          <Tabs.Tab value="people" leftSection={<IconUsers size={16} />}>Officers</Tabs.Tab>
          <Tabs.Tab value="locations" leftSection={<IconMapPin size={16} />}>Clinics</Tabs.Tab>
          <Tabs.Tab value="leave" leftSection={<IconPlaneDeparture size={16} />}>Confirmed leave</Tabs.Tab>
          <Tabs.Tab value="tally" leftSection={<IconSum size={16} />}>Tally</Tabs.Tab>
          <Tabs.Tab value="periods" leftSection={<IconCalendarCog size={16} />}>Periods & holidays</Tabs.Tab>
          <Tabs.Tab value="feedback" leftSection={<IconMessage2 size={16} />}>Feedback</Tabs.Tab>
          <Tabs.Tab value="reports" leftSection={<IconFileText size={16} />}>Reports</Tabs.Tab>
          <Tabs.Tab value="surat-arahan" leftSection={<IconFileCertificate size={16} />}>Surat arahan</Tabs.Tab>
        </Tabs.List>
        </Paper>
        <Tabs.Panel value="rota"><RotaTab /></Tabs.Panel>
        <Tabs.Panel value="people"><PeopleTab /></Tabs.Panel>
        <Tabs.Panel value="locations"><LocationsTab /></Tabs.Panel>
        <Tabs.Panel value="leave"><ConfirmedLeaveTab /></Tabs.Panel>
        <Tabs.Panel value="tally"><TallyTab /></Tabs.Panel>
        <Tabs.Panel value="periods"><PeriodsTab /></Tabs.Panel>
        <Tabs.Panel value="feedback"><FeedbackTab /></Tabs.Panel>
        <Tabs.Panel value="reports"><ReportsTab /></Tabs.Panel>
        <Tabs.Panel value="surat-arahan"><SuratArahanTab /></Tabs.Panel>
      </Tabs>
    </Stack>
  )
}
