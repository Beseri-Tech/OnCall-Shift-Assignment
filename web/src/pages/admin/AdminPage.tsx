import { Stack, Tabs } from '@mantine/core'
import { PageHeader } from '../../components/common'
import { useParams } from 'react-router'
import { ADMIN_SECTIONS, adminSectionOf } from './adminSections'
import { ConfirmedLeaveTab } from './ConfirmedLeaveTab'
import { FeedbackTab } from './FeedbackTab'
import { LocationsTab } from './LocationsTab'
import { PeopleTab } from './PeopleTab'
import { PeriodsTab } from './PeriodsTab'
import { ReportsTab } from './ReportsTab'
import { SuratArahanTab } from './SuratArahanTab'
import { RotaTab } from './RotaTab'
import { TallyTab } from './TallyTab'

/** Only reachable by admins and supervisors (see AdminOnly in main.tsx; the API checks too). The section list is the side nav (Layout). */
export function AdminPage() {
  const { '*': sub } = useParams()
  const tab = adminSectionOf(sub)

  return (
    <Stack gap="md">
      {/* The side nav lists the sections; the title says which one this is. */}
      <PageHeader title={ADMIN_SECTIONS.find(s => s.value === tab)!.label} />
      <Tabs value={tab} keepMounted={false} variant="default">
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
