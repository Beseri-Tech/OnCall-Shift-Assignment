import { Alert, Anchor, Badge, Button, List, Paper, SimpleGrid, Stack, Table, Text, ThemeIcon, Title } from '@mantine/core'
import {
  IconArrowsExchange, IconBeach, IconBell, IconBook, IconDownload, IconCalendarStar, IconCalendarTime, IconListCheck, IconPlaneDeparture, IconScale, IconStar,
  IconSum, IconUserOff, type Icon,
} from '@tabler/icons-react'
import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { PageHeader } from '../components/common'

// Plain-language rules for officers. Keep in step with RotaGenerator, LeavePolicy, the Leave limits in appsettings.json
// and SwapEndpoints when those change.

function Section({ icon: I, title, children }: { icon: Icon; title: string; children: ReactNode }) {
  return (
    <Paper className="panel" p="lg">
      <Stack gap="sm">
        <Title order={3} size="h4" c="brand.9" style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <ThemeIcon size={34} radius="md" variant="light"><I size={20} stroke={1.8} /></ThemeIcon>
          {title}
        </Title>
        {children}
      </Stack>
    </Paper>
  )
}

const Items = ({ children }: { children: ReactNode }) => <List spacing={6} size="sm">{children}</List>

const POINTS = [
  { day: 'Normal weekday', cost: '0', note: 'uses the weekday allowance', color: 'gray' },
  { day: 'Saturday or Sunday', cost: '1', note: '', color: 'brand' },
  { day: 'Public holiday', cost: '2', note: 'also when it falls on a weekend', color: 'teal' },
  { day: 'Peak day', cost: '1', note: 'a busy weekday, e.g. the eve of Raya', color: 'orange' },
]

export function RulesPage() {
  return (
    <Stack gap="lg" maw={900}>
      <PageHeader icon={IconBook} title="Rules and guidelines" description="How the on-call rota is shared out, and what you need to know.">
        <Button component="a" href="/assets/Rota-OC-user-manual.pdf" download variant="light" leftSection={<IconDownload size={18} />}>
          Download user manual (PDF)
        </Button>
      </PageHeader>

      <Alert color="yellow" variant="light" icon={<IconStar />} title="Outstation or travelling? Ask for extra points">
        <Text size="sm">
          If you'll be <b>outstation, travelling, on a course or dealing with family matters</b> and your leave points aren't
          enough, <b>ask the admin for extra points before the leave deadline.</b>
        </Text>
        <List size="sm" spacing={4} mt="xs">
          <List.Item>The admin can add extra points for you for that period, with the reason noted.</List.Item>
          <List.Item>
            Leave that's <b>officially approved</b>, such as an overseas trip, can be recorded by the admin as confirmed leave
            even when you're out of points (see below).
          </List.Item>
          <List.Item>Ask <b>early</b>, because busy days fill up first come, first served.</List.Item>
          <List.Item>
            If the app won't let you mark a day because you're out of points, ask the admin. Don't leave it unmarked, or you
            may be put on call on a day you're away.
          </List.Item>
        </List>
      </Alert>

      <Section icon={IconCalendarTime} title="How a rota period works">
        <List type="ordered" spacing={6} size="sm">
          <List.Item><b>Open for leave.</b> Mark your leave and preferred on-call days. Change them as often as you like until the leave deadline.</List.Item>
          <List.Item><b>Locked.</b> After the deadline the admin locks the period and generates the rota. You can no longer change your own entries.</List.Item>
          <List.Item><b>In review (draft).</b> Check your dates and ask other officers to swap. Draft dates can still change.</List.Item>
          <List.Item><b>Published.</b> The final rota is out. You get a notification and an email, and the shifts are added to your tally.</List.Item>
        </List>
      </Section>

      <Section icon={IconScale} title="How shifts are shared out">
        <Text size="sm">The app builds the rota automatically, following these rules in order of importance:</Text>
        <Items>
          <List.Item><b>Everyone gets the same number of shifts</b>, normally one each per rota. If the days don't divide evenly, a few officers get one extra.</List.Item>
          <List.Item><b>Nobody works two days in a row</b>, unless truly no one else is available. The app also tries to avoid shifts only 2 or 3 days apart.</List.Item>
          <List.Item>
            <b>Weekend and public holiday shifts even out over time.</b> The app looks at how many weekend/PH shifts you've
            already done. Officers who have done fewer get more this time.
          </List.Item>
          <List.Item><b>Preferred on-call days are filled first.</b></List.Item>
          <List.Item><b>No shift on a day you're on leave.</b></List.Item>
          <List.Item><b>Long leave lowers your share</b> when there aren't enough free days for a full share without back-to-back shifts.</List.Item>
        </Items>
        <Text size="sm" fw={700} mt="xs">Settings the admin can give an officer</Text>
        <Items>
          <List.Item><b>Extra shift:</b> first in line for the extra shift when days don't divide evenly.</List.Item>
          <List.Item><b>Prefers weekends/PH:</b> about twice the normal share of weekend/PH shifts, with fewer weekdays so the total stays equal.</List.Item>
          <List.Item><b>Weekend weight:</b> sets how big a share of weekend/PH shifts the officer takes (2 = about twice as many).</List.Item>
        </Items>
        <Text size="sm" c="dimmed">Speak to the admin if you'd like one of these.</Text>
      </Section>

      <Section icon={IconBeach} title="Leave">
        <Text size="sm">
          Leave means days you <b>can't be on call</b>. Mark them on <Anchor component={Link} to="/">My leave</Anchor> with a short note
          (for example "annual" or "course").
        </Text>
        <Table withTableBorder verticalSpacing={6} fz="sm">
          <Table.Thead><Table.Tr><Table.Th>Day</Table.Th><Table.Th w={90}>Points</Table.Th></Table.Tr></Table.Thead>
          <Table.Tbody>
            {POINTS.map(p => (
              <Table.Tr key={p.day}>
                <Table.Td>{p.day}{p.note && <Text span c="dimmed" size="xs"> · {p.note}</Text>}</Table.Td>
                <Table.Td><Badge color={p.color} variant="light" size="lg">{p.cost}</Badge></Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
        <Items>
          <List.Item>
            <b>Points budget:</b> about 30% of the weekend, public holiday and peak days in the period (10 such days give 3 points).
            The admin may set a different number. <b>Need more? Ask the admin.</b>
          </List.Item>
          <List.Item>
            <b>Reward points for busy-day shifts:</b> if you work a public holiday, a weekend day or a peak day in a published rota,
            you get extra leave points in the <b>next</b> rota: <b>3 for a public holiday, 2 for a weekend day, 1 for a peak day</b>.
            Only the rota right before counts. They are added automatically, show on My leave, and last for that next rota only,
            so unused points are not saved for later. In your first rota you start with none.
          </List.Item>
          <List.Item><b>Weekday allowance:</b> up to half of the normal weekdays in the period.</List.Item>
          <List.Item>
            <b>Daily limit, first come first served:</b> at most 30% of on-call officers off on a weekend, public holiday or
            peak day, and 50% on other days. Full days show <b>striped</b> on the calendar.
          </List.Item>
          <List.Item>Lowering a limit never forces you to remove leave you already have. If you end up over your points or weekday allowance, you can keep your leave or remove days, but you can’t add or swap days until you are within the limit. Removing days is always allowed.</List.Item>
          <List.Item>After the leave deadline, ask the admin, who can still edit leave for you.</List.Item>
          <List.Item>Other officers can see your leave and its note on the Overview page, so keep notes short and general.</List.Item>
        </Items>
      </Section>

      <Section icon={IconPlaneDeparture} title="Approved leave (confirmed leave)">
        <Text size="sm">
          If your leave is <b>officially approved</b>, for example an overseas trip, send the approval to the admin. The admin
          records it as <b>confirmed leave</b>.
        </Text>
        <Items>
          <List.Item>It's <b>never refused</b>, even if you're out of points or the day is full, and you won't be put on call on those days.</List.Item>
          <List.Item>
            It doesn't cost points itself, but your <b>points and weekday allowance are reduced by about a third (33%) of what
            those days would have cost as ordinary leave</b>, rounded up. Weekend and peak days cost 1 point, a public holiday 2,
            and each ordinary weekday uses 1 weekday. For example, a 9-day trip over Christmas (4 weekend days, the 25 Dec public
            holiday, 4 weekdays) would have cost 6 points, so it takes 2 points and 2 weekdays off. A trip of weekdays only takes
            no points. Any trip that includes such days takes at least 1.
          </List.Item>
          <List.Item>It still counts toward the daily limit for other officers, so a day may fill up sooner for them.</List.Item>
          <List.Item>The admin can enter it before the rota period opens. You get a notification and an email.</List.Item>
          <List.Item>It shows on My leave with its reason, but only the admin can change it. Others see only "confirmed leave".</List.Item>
          <List.Item>If you're already on call on those days, the admin moves the shifts to someone else.</List.Item>
        </Items>
      </Section>

      <Section icon={IconCalendarStar} title="Preferred on-call days">
        <Items>
          <List.Item>Switch to <b>Preferred on-call</b> on My leave to mark days you'd like to work.</List.Item>
          <List.Item>Preferred days are filled first and not moved afterwards.</List.Item>
          <List.Item>You still get the same total as everyone else. Preferred days choose <i>which</i> days, not how many.</List.Item>
          <List.Item>Not guaranteed: another officer may prefer the same day, or it would give you back-to-back shifts or more than your share.</List.Item>
          <List.Item>A day can't be both leave and preferred.</List.Item>
        </Items>
      </Section>

      <SimpleGrid cols={{ base: 1, sm: 2 }} spacing="lg">
        <Section icon={IconListCheck} title="Public holidays and peak days">
          <Items>
            <List.Item>Each rota period has its own list, set by the admin.</List.Item>
            <List.Item><b>Public holidays (PH)</b> count as weekend shifts in the rota and cost 2 points.</List.Item>
            <List.Item><b>Peak days (PEAK)</b> are busy weekdays: leave costs 1 point and the stricter daily limit applies. They count towards the points budget, and as normal weekdays in the rota.</List.Item>
          </Items>
        </Section>

        <Section icon={IconSum} title="Your tally">
          <Items>
            <List.Item>Counts every shift in published rotas, including upcoming ones, plus history the admin entered.</List.Item>
            <List.Item>Draft dates don't count until published.</List.Item>
            <List.Item>Your weekend/PH count decides your future weekend/PH share. Think it's wrong? Tell the admin.</List.Item>
            <List.Item>No history yet? You're treated as having the group average.</List.Item>
          </Items>
        </Section>
      </SimpleGrid>

      <Section icon={IconArrowsExchange} title="Swapping dates">
        <Text size="sm">While a rota is <b>in review</b>:</Text>
        <List type="ordered" spacing={6} size="sm">
          <List.Item>On <Anchor component={Link} to="/my-oncall">My on-call</Anchor>, press <b>Swap</b> next to your date.</List.Item>
          <List.Item>Pick the date you'd like instead. That officer gets your date in return.</List.Item>
          <List.Item>They get a notification and an email. Nothing changes until they accept, then both dates change straight away.</List.Item>
        </List>
        <Items>
          <List.Item>You can't swap onto a day you're on leave, or give someone a day they're on leave.</List.Item>
          <List.Item>The app warns you if a swap would give someone back-to-back days, but still allows it.</List.Item>
          <List.Item>Swaps close on the <b>swap deadline</b>, or when the rota is published. After that, ask the admin.</List.Item>
          <List.Item>Requests that no longer fit after the rota changes are closed automatically, and both officers are told.</List.Item>
          <List.Item>You can withdraw your own request before it's answered.</List.Item>
        </Items>
      </Section>

      <SimpleGrid cols={{ base: 1, sm: 2 }} spacing="lg">
        <Section icon={IconUserOff} title="Not on the rota">
          <Text size="sm">
            If you're away for a while (for example maternity leave), the admin can mark you as <b>excluded</b>. You won't get
            shifts. Tell the admin when you're back.
          </Text>
          <Text size="sm" mt="xs">
            If there's a weekday you can <b>never</b> do on call, for example because you work in another state where Sunday is a
            working day, ask the admin to <b>block</b> it. You won't be given that weekday, it shows as "Not on call" on your leave
            calendar, and it doesn't use any leave points or change your points. The block has no end date, so ask the admin to lift it when it no longer applies.
          </Text>
        </Section>

        <Section icon={IconBell} title="Notifications and feedback">
          <Items>
            <List.Item>The <b>bell</b> shows rota and swap updates.</List.Item>
            <List.Item>You get an email for swap requests, confirmed leave and when the final rota is published.</List.Item>
            <List.Item>Report a problem or share an idea with <b>Send feedback</b>: the speech-bubble icon in the header, or <b>Send feedback</b> in the side menu on a phone.</List.Item>
          </Items>
        </Section>
      </SimpleGrid>

      <Alert color="brand" variant="light" title="Quick tips">
        <List size="sm" spacing={4}>
          <List.Item>Mark leave <b>early</b>, especially weekends, public holidays and peak days.</List.Item>
          <List.Item><b>Outstation, travelling or other commitments and not enough points? Ask the admin for extra points early.</b></List.Item>
          <List.Item>Check your dates as soon as the draft is in review, so you have time to swap.</List.Item>
          <List.Item>Any question about points, limits or your tally: ask the admin.</List.Item>
        </List>
      </Alert>
    </Stack>
  )
}
