import {
  Affix, Alert, Anchor, Badge, Box, Button, Card, Group, List, Paper, SimpleGrid, Stack, Table, Text, TextInput, ThemeIcon, Title, Transition, UnstyledButton,
} from '@mantine/core'
import { useWindowScroll } from '@mantine/hooks'
import {
  IconArrowUp, IconArrowsExchange, IconBeach, IconBell, IconBook, IconBuildingHospital, IconCalendarStar, IconCalendarTime, IconDownload, IconFileCertificate,
  IconGift, IconId, IconListCheck, IconPlaneDeparture, IconReceipt2, IconScale, IconSearch, IconShieldLock, IconStar, IconStethoscope, IconSum, IconUserCircle,
  IconUserOff, IconUsers, IconWand, IconBrandWhatsapp, IconX, type Icon,
} from '@tabler/icons-react'
import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { Link } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { api, openDocument } from '../api'
import { isAdmin, isMaster, useMe } from '../auth'
import { PageHeader } from '../components/common'
import { notifyError } from '../lib'

// Plain-language rules and guidelines. Keep in step with RotaGenerator, LeavePolicy, the Leave limits in appsettings.json,
// SwapEndpoints, the case log, the ETAP claim and the admin pages when those change. The "Developed by Beseri-Tech" credit
// is in the app footer (Layout), so this page does not repeat it.

const Items = ({ children }: { children: ReactNode }) => <List spacing={6} size="sm">{children}</List>

const POINTS = [
  { day: 'Normal weekday', cost: '0', note: 'uses the weekday allowance', color: 'gray' },
  { day: 'Weekend day', cost: '1', note: 'your state\'s rest days: Saturday and Sunday, or Friday and Saturday in Kedah, Kelantan and Terengganu', color: 'brand' },
  { day: 'Public holiday', cost: '2', note: 'also when it falls on a weekend', color: 'teal' },
  { day: 'Peak day', cost: '1', note: 'a busy weekday, e.g. the eve of Raya', color: 'orange' },
]

/** The manual from private storage when one has been uploaded; otherwise the copy bundled with the app. */
function ManualButton() {
  const manual = useQuery({ queryKey: ['manual'], queryFn: () => api.documents.manual(), retry: false })
  const label = 'Open user manual (PDF)'
  if (manual.data) {
    return (
      <Button variant="light" leftSection={<IconDownload size={18} />} onClick={() => openDocument(manual.data!.id).catch(notifyError)}>
        {label}
      </Button>
    )
  }
  return (
    <Button component="a" href="/assets/Rota-OC-user-manual.pdf" download variant="light" leftSection={<IconDownload size={18} />}>
      Download user manual (PDF)
    </Button>
  )
}

interface Entry { id: string; chapter: string; title: string; blurb: string; icon: Icon; keywords: string; body: ReactNode; plain?: boolean; show?: boolean }

const CHAPTERS = [
  { id: 'rota', title: 'Rota and leave', blurb: 'How the rota is built, leave, points and swaps', icon: IconCalendarTime, color: 'brand' },
  { id: 'oncall', title: 'On-call work', blurb: 'Case log, WhatsApp passover and the ETAP claim', icon: IconStethoscope, color: 'teal' },
  { id: 'account', title: 'Your account', blurb: 'Signing in, notifications and feedback', icon: IconUserCircle, color: 'violet' },
  { id: 'admin', title: 'For admins', blurb: 'Running periods, officers, clinics, reports', icon: IconShieldLock, color: 'orange' },
]

function EntryCard({ e }: { e: Entry }) {
  if (e.plain) return <Box id={e.id} style={{ scrollMarginTop: 80 }}>{e.body}</Box>
  const I = e.icon
  return (
    <Paper id={e.id} className="panel" p="lg" style={{ scrollMarginTop: 80 }}>
      <Stack gap="sm">
        <Title order={3} size="h4" c="brand.9" style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <ThemeIcon size={34} radius="md" variant="light"><I size={20} stroke={1.8} /></ThemeIcon>
          {e.title}
        </Title>
        {e.body}
      </Stack>
    </Paper>
  )
}

export function RulesPage() {
  const me = useMe().data
  const admin = isAdmin(me)
  const master = isMaster(me)
  const [query, setQuery] = useState('')
  const [active, setActive] = useState('')
  const [scroll, scrollTo] = useWindowScroll()

  const entries: Entry[] = useMemo(() => [
    {
      id: 'period', chapter: 'rota', icon: IconCalendarTime, title: 'How a rota period works', blurb: 'Open, locked, in review, published',
      keywords: 'stages deadline lock draft publish review period',
      body: (
        <List type="ordered" spacing={6} size="sm">
          <List.Item><b>Open for leave.</b> Mark your leave and preferred on-call days. Change them as often as you like until the leave deadline.</List.Item>
          <List.Item><b>Locked.</b> After the deadline the admin locks the period and generates the rota. You can no longer change your own entries.</List.Item>
          <List.Item><b>In review (draft).</b> Check your dates and ask other officers to swap. Draft dates can still change.</List.Item>
          <List.Item><b>Published.</b> The final rota is out. You get a notification and an email, and the shifts are added to your tally.</List.Item>
        </List>
      ),
    },
    {
      id: 'sharing', chapter: 'rota', icon: IconScale, title: 'How shifts are shared out', blurb: 'Equal shifts, no back-to-back, fair weekends',
      keywords: 'fair equal back-to-back weekend extra shift weight prefer generator',
      body: (
        <>
          <Text size="sm">The app builds the rota automatically, following these rules in order of importance:</Text>
          <Items>
            <List.Item><b>Everyone gets the same number of shifts</b>, normally one each per rota. If the days don't divide evenly, a few officers get one extra.</List.Item>
            <List.Item><b>Nobody works two days in a row</b>, unless truly no one else is available. The app also tries to avoid shifts only 2 or 3 days apart.</List.Item>
            <List.Item>
              <b>Weekend and public holiday shifts even out over time.</b> The app looks at how many weekend/PH shifts you've
              already done. Officers who have done fewer get more this time. Your state decides which days are the weekend.
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
        </>
      ),
    },
    {
      id: 'leave', chapter: 'rota', icon: IconBeach, title: 'Leave and points', blurb: 'Marking leave, the points budget and daily limits',
      keywords: 'leave points budget allowance weekday limit full striped badges CL EX RW over',
      body: (
        <>
          <Text size="sm">
            Leave means days you <b>can't be on call</b>. Mark them on <Anchor inherit component={Link} to="/">My leave</Anchor> with a short note
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
            <List.Item><b>Weekday allowance:</b> up to half of the normal weekdays in the period.</List.Item>
            <List.Item>
              <b>On My leave, small badges show where your points come from:</b> <b>CL</b> is points or weekdays taken off for
              confirmed leave, <b>EX</b> is extra points from the admin and <b>RW</b> is reward points. Hover or tap a badge for the detail.
            </List.Item>
            <List.Item>
              <b>Daily limit, first come first served:</b> at most 30% of on-call officers off on a weekend, public holiday or
              peak day, and 50% on other days. Officers on confirmed leave that day are taken out first: the limit is worked out on the officers available. Full days show <b>striped</b> on the calendar.
            </List.Item>
            <List.Item>Lowering a limit never forces you to remove leave you already have. If you end up over your points or weekday allowance, you can keep your leave or remove days, but you can’t add or swap days until you are within the limit. Removing days is always allowed.</List.Item>
            <List.Item>After the leave deadline, ask the admin, who can still edit leave for you.</List.Item>
            <List.Item>Other officers can see your leave and its note on the Overview page, so keep notes short and general.</List.Item>
          </Items>
        </>
      ),
    },
    {
      id: 'rewards', chapter: 'rota', icon: IconGift, title: 'Reward points', blurb: 'Work a busy day, earn leave points next rota', plain: true,
      keywords: 'reward points RW public holiday weekend peak next rota',
      body: (
        <Alert color="teal" variant="light" radius="md" icon={<IconGift />} title="Reward points: work a busy day, earn leave points"
          styles={{ root: { borderWidth: 2, borderStyle: 'solid', borderColor: 'var(--mantine-color-teal-5)' }, title: { fontSize: 'var(--mantine-font-size-lg)', fontWeight: 700 } }}>
          <Text size="sm">
            Work a busy day in a published rota and you get <b>extra leave points in the next rota</b>:
          </Text>
          <Group gap="sm" my="sm">
            <Badge size="xl" color="teal" variant="filled">Public holiday +3</Badge>
            <Badge size="xl" color="brand" variant="filled">Weekend day +2</Badge>
            <Badge size="xl" color="orange" variant="filled">Peak day +1</Badge>
          </Group>
          <List size="sm" spacing={4}>
            <List.Item>They are added automatically and show on My leave.</List.Item>
            <List.Item><b>Next rota only.</b> Unused points are not saved for later.</List.Item>
          </List>
        </Alert>
      ),
    },
    {
      id: 'confirmed', chapter: 'rota', icon: IconPlaneDeparture, title: 'Approved leave (confirmed leave)', blurb: 'Trips, CR days and relief duty',
      keywords: 'confirmed leave request trip CR cuti rehat relief duty approval document 33%',
      body: (
        <>
          <Text size="sm">
            If your leave is <b>officially approved</b> or assigned to you, such as an overseas trip, your CR days or relief duty at
            another clinic, ask for it on the <Anchor inherit component={Link} to="/leave-requests">Request leave</Anchor> page. The admin
            records it as <b>confirmed leave</b>.
          </Text>
          <Items>
            <List.Item>
              <b>How to ask:</b> add each period you are away as its own <b>date range</b>, pick the <b>type</b> (Trip, CR, Relief duty
              or Other) and give a short <b>reason</b>. You can attach a <b>document</b> (PDF, JPG, PNG or DOCX, up to 20 MB) to each
              range, but it's optional. Send it once for as many ranges as you need.
            </List.Item>
            <List.Item>
              The admin <b>approves or rejects each range</b>. You get a notification and an email either way. A rejection always
              comes with the reason, and you can still mark your own leave or send a new request. Until it's decided you can
              <b> withdraw</b> a range. Nothing changes on the rota until it's approved.
            </List.Item>
            <List.Item>You can still send an approval to the admin instead, and they can enter the confirmed leave for you.</List.Item>
            <List.Item>
              <b>Relief duty</b> (working at another clinic) keeps you off call on those days but <b>takes no points or weekdays</b>.
              Trip, CR and Other are reduced as described below.
            </List.Item>
            <List.Item>It's <b>never refused</b>, even if you're out of points or the day is full, and you won't be put on call on those days.</List.Item>
            <List.Item>
              <b>Include your CR (cuti rehat) days</b>, either inside the trip's dates or as their own range with type CR. You can't be on
              call on your official off day, so the admin needs your CR days recorded together with the trip.
            </List.Item>
            <List.Item>
              It doesn't cost points itself, but your <b>points and weekday allowance are reduced by about a third (33%) of what
              those days would have cost as ordinary leave</b>, rounded up. Weekend and peak days cost 1 point, a public holiday 2,
              and each ordinary weekday uses 1 weekday. For example, a 9-day trip over Christmas (4 weekend days, the 25 Dec public
              holiday, 4 weekdays) would have cost 6 points, so it takes 2 points and 2 weekdays off. A trip of weekdays only takes
              no points. Any trip that includes such days takes at least 1. Days you can never be on call (a blocked weekday or a
              clinic event day) inside the trip aren't counted.
            </List.Item>
            <List.Item>It still counts toward the daily limit for other officers, so a day may fill up sooner for them.</List.Item>
            <List.Item>It can be entered before the rota period opens.</List.Item>
            <List.Item>It shows on My leave with its reason, but only the admin can change it. Others see only "confirmed leave".</List.Item>
            <List.Item>If you're already on call on those days, the admin moves the shifts to someone else.</List.Item>
          </Items>
        </>
      ),
    },
    {
      id: 'preferred', chapter: 'rota', icon: IconCalendarStar, title: 'Preferred on-call days', blurb: 'Choose days you would like to work',
      keywords: 'preferred prefer on-call days choose',
      body: (
        <Items>
          <List.Item>Switch to <b>Preferred on-call</b> on My leave to mark days you'd like to work.</List.Item>
          <List.Item>Preferred days are filled first and not moved afterwards.</List.Item>
          <List.Item>You still get the same total as everyone else. Preferred days choose <i>which</i> days, not how many.</List.Item>
          <List.Item>Not guaranteed: another officer may prefer the same day, or it would give you back-to-back shifts or more than your share.</List.Item>
          <List.Item>A day can't be both leave and preferred.</List.Item>
        </Items>
      ),
    },
    {
      id: 'holidays', chapter: 'rota', icon: IconListCheck, title: 'Public holidays and peak days', blurb: 'What counts as a busy day',
      keywords: 'public holiday PH peak day busy',
      body: (
        <Items>
          <List.Item>Each rota period has its own list, taken from your state's holidays and set by the admin.</List.Item>
          <List.Item><b>Public holidays (PH)</b> count as weekend shifts in the rota and cost 2 points.</List.Item>
          <List.Item><b>Peak days (PEAK)</b> are busy weekdays: leave costs 1 point and the stricter daily limit applies. They count towards the points budget, and as normal weekdays in the rota.</List.Item>
          <List.Item>Which days are the <b>weekend</b> follows your state's rest days. Changing a state's weekend only affects rotas generated afterwards; published rotas and tallies stay as they are.</List.Item>
        </Items>
      ),
    },
    {
      id: 'tally', chapter: 'rota', icon: IconSum, title: 'Your tally', blurb: 'How your shifts are counted',
      keywords: 'tally count shifts history weekend',
      body: (
        <Items>
          <List.Item>Counts every shift in published rotas, including upcoming ones, plus history the admin entered.</List.Item>
          <List.Item>Draft dates don't count until published.</List.Item>
          <List.Item>Your weekend/PH count decides your future weekend/PH share. Think it's wrong? Tell the admin.</List.Item>
          <List.Item>No history yet? You're treated as having the group average.</List.Item>
        </Items>
      ),
    },
    {
      id: 'swaps', chapter: 'rota', icon: IconArrowsExchange, title: 'Swapping dates', blurb: 'Swap with a colleague while the rota is in review',
      keywords: 'swap exchange review deadline',
      body: (
        <>
          <Text size="sm">While a rota is <b>in review</b>:</Text>
          <List type="ordered" spacing={6} size="sm">
            <List.Item>On <Anchor inherit component={Link} to="/my-oncall">My on-call</Anchor>, press <b>Swap</b> next to your date.</List.Item>
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
        </>
      ),
    },
    {
      id: 'notrota', chapter: 'rota', icon: IconUserOff, title: 'Not on the rota', blurb: 'Excluded, blocked weekdays and clinic events',
      keywords: 'excluded left out blocked weekday clinic event no duty maternity',
      body: (
        <>
          <Text size="sm">
            If you're away for a while (for example maternity leave), the admin can mark you as <b>excluded</b>. You won't get
            shifts. Tell the admin when you're back. The admin can also leave you out of <b>one particular rota</b>: you then get
            no shift in it and aren't asked for leave.
          </Text>
          <Text size="sm">
            If there's a weekday you can <b>never</b> do on call, for example because you work in another state where Sunday is a
            working day, ask the admin to <b>block</b> it. You won't be given that weekday, it shows as "No duty" on your leave
            calendar, and it doesn't use any leave points or change your points. The block has no end date, so ask the admin to lift it when it no longer applies.
          </Text>
          <Text size="sm">
            If your <b>clinic has an event</b>, the admin can block that date for everyone in the clinic. Nobody from the clinic is put
            on call, you can't mark leave on it, it shows as "No duty" and it costs no points.
          </Text>
        </>
      ),
    },

    {
      id: 'newcase', chapter: 'oncall', icon: IconStethoscope, title: 'Recording a new case', blurb: 'The new case form, replacing the Google Form',
      keywords: 'new case record patient on call DSA taggers confidential trauma non-trauma ED zone Ix Dx Mx Rx plan form',
      body: (
        <>
          <Text size="sm">
            Record every patient you see on call on <Anchor inherit component={Link} to="/cases">Cases</Anchor> (under <b>On call</b>), on the <b>New case</b> tab,
            instead of the Google Form. Only officers on the rota record cases.
          </Text>
          <Items>
            <List.Item><b>Pick the on-call date</b> (the shift) and whether you saw the patient that day or after midnight. A start time before 08:00 is taken as the day after, and you can change it.</List.Item>
            <List.Item><b>Department and case type come first</b> and are required: the patient and clinical details open once they are chosen. Department is what the ETAP form shows under Catatan. For an ED referral, choose the zone (green, yellow or red).</List.Item>
            <List.Item><b>Trauma</b> cases are written as <i>presented with</i> (P/W), <b>non-trauma</b> cases as <i>complaint of</i> (C/O). Add investigations (Ix), diagnosis (Dx), management (Mx), prescription (Rx) and plan. Each field shows an example.</List.Item>
            <List.Item><b>You are the officer in charge.</b> Type taggers separately, and always name the <b>DSA on call</b> with their status.</List.Item>
            <List.Item>The start and finish times set how long the patient took. The ETAP claim uses it (see below).</List.Item>
            <List.Item>Tick <b>Confidential</b> to leave the patient's name and registration number off the ETAP form.</List.Item>
            <List.Item>After a case, the shift, taggers and DSA stay filled in for your next patient, and the app offers the WhatsApp passover.</List.Item>
            <List.Item><b>Patient details stay inside the hospital.</b> Other hospitals never see them, the browser doesn't keep them, and every view and download is recorded in the audit log (without the patient's details).</List.Item>
          </Items>
        </>
      ),
    },
    {
      id: 'caselog', chapter: 'oncall', icon: IconListCheck, title: 'The case log', blurb: 'Browse, search, edit and download the hospital\'s cases',
      keywords: 'case log search filter officer excel month year edit remove import google sheet',
      body: (
        <Items>
          <List.Item>The <b>Case log</b> tab on Cases lists the hospital's cases by month. Everyone in the hospital can read it.</List.Item>
          <List.Item>Search by patient, IC or diagnosis, and filter by officer.</List.Item>
          <List.Item><b>Excel</b> downloads a month, or the whole year.</List.Item>
          <List.Item>The green WhatsApp icon opens the passover (for your own cases), the pencil edits and the bin removes. You can edit or remove your own cases; admins can edit any.</List.Item>
          <List.Item>Admins can <b>import the old Google Sheet</b> (download it as CSV). Cases already in the log are skipped.</List.Item>
          <List.Item>Patient details are confidential: only people of this hospital see them, and every view and download is recorded.</List.Item>
        </Items>
      ),
    },
    {
      id: 'passover', chapter: 'oncall', icon: IconBrandWhatsapp, title: 'WhatsApp passover', blurb: 'The seniors\' template, ready to copy',
      keywords: 'whatsapp passover template group copy referral S/T S/B',
      body: (
        <Items>
          <List.Item>After you record a case, the app asks whether to generate the <b>WhatsApp passover</b> in the seniors' template (referral, S/T, S/B, patient, P/W or C/O, Ix ticked, Dx, Mx, plan, Rx).</List.Item>
          <List.Item>Press <b>Copy text</b> and paste it into the department group. <b>Check it before sending</b>: it contains patient details.</List.Item>
          <List.Item>The <b>WhatsApp</b> button on a row in the case log opens it again for your own cases.</List.Item>
          <List.Item>An ED referral reads like "N/C from ED GREEN ZONE", from the zone you chose.</List.Item>
          <List.Item>S/T (spoken to), S/B (seen by), Ix and Rx are in the case log and Excel, but not on the ETAP form.</List.Item>
        </Items>
      ),
    },
    {
      id: 'etap', chapter: 'oncall', icon: IconReceipt2, title: 'ETAP claim', blurb: 'Your monthly on-call claim, filled in for you',
      keywords: 'ETAP claim form passive call 4 hours rest day public holiday surat arahan punch card bank',
      body: (
        <>
          <Text size="sm">
            <Anchor inherit component={Link} to="/etap">ETAP claim</Anchor> (under <b>On call</b>) builds your claim for a month and fills in the official form.
            You need a <b>verified ETAP profile</b> first (next section).
          </Text>
          <List type="ordered" spacing={6} size="sm">
            <List.Item>Pick the month. Add your on-call dates with one button (it says which rota it adds from), or add a day and type it yourself.</List.Item>
            <List.Item>Set the times to match your <b>punch card</b>: usually 17:00 to 08:00 the next day on working days, and 08:00 to 08:00 (24 h) on weekends and public holidays.</List.Item>
            <List.Item><b>Save</b>. Each day is priced as passive call <b>under or over 4 hours</b>, on a <b>working day</b> or a <b>rest day / public holiday</b>.</List.Item>
            <List.Item>Download the <b>ETAP form (PDF)</b> and the latest <b>surat arahan</b> uploaded by the admin.</List.Item>
          </List>
          <Items>
            <List.Item>A day counts as <b>over 4 hours</b> when your recorded cases for that on-call date add up to 4 hours or more. Those patients fill the form's table (confidential ones without name and registration number), and a patient can take up to three rows.</List.Item>
            <List.Item>The download ends with a <b>patient-details attachment</b> for every day over 4 hours, with a "Disahkan oleh" signature space.</List.Item>
            <List.Item>A day you claim that the published rota gives to someone else is marked, so you can check it before submitting.</List.Item>
            <List.Item>The weekend follows your state's rest days (for example Friday and Saturday).</List.Item>
            <List.Item><b>Bring yourself:</b> a copy of your punch card, your bank statement (and an IC copy for your first claim). Sign section C and the attachment, and get your head of department to sign section D.</List.Item>
          </Items>
        </>
      ),
    },
    {
      id: 'profile', chapter: 'oncall', icon: IconId, title: 'My ETAP profile', blurb: 'Your details for the claim form, verified once',
      keywords: 'profile IC jawatan gred address salary bank verify specialist pakar',
      body: (
        <Items>
          <List.Item>Open <Anchor inherit component={Link} to="/profile">My ETAP profile</Anchor> from the account menu (top right; the side menu on a phone).</List.Item>
          <List.Item>Fill in your IC, jawatan, gred, place of work, address, distance, salary number, phones and bank. Tick specialist (pakar) and give the gazette date if it applies, because specialist rates then apply.</List.Item>
          <List.Item><b>Only you can see it.</b> Admins and masters can't read it, and its values never go into the audit log.</List.Item>
          <List.Item>When everything is right, <b>verify it with your password</b>. Any later change, including to your phone or email, needs a new verification, and the ETAP form needs a verified profile.</List.Item>
        </Items>
      ),
    },

    {
      id: 'signin', chapter: 'account', icon: IconUserCircle, title: 'Signing in and your account', blurb: 'Password, hospital, version',
      keywords: 'sign in password forgot temporary hospital account changelog version email',
      body: (
        <Items>
          <List.Item>Your sign-in is part of your officer record. The admin adds you with your email and invites you: you get a <b>temporary password</b> that expires and must be changed the first time you sign in.</List.Item>
          <List.Item>Forgot your password? Use <b>Forgot password?</b> on the sign-in page and follow the emailed link, or ask the admin for a new temporary password.</List.Item>
          <List.Item>Change your password any time from the account menu (top right).</List.Item>
          <List.Item>The app runs more than one hospital. Each hospital has its own officers, rota, leave, cases and reports, and you only see your own hospital's data.</List.Item>
          <List.Item>The footer and the account menu show the app <b>version</b>. Open the <Anchor inherit component={Link} to="/changelog">Changelog</Anchor> to see what changed in every release.</List.Item>
        </Items>
      ),
    },
    {
      id: 'notify', chapter: 'account', icon: IconBell, title: 'Notifications and feedback', blurb: 'The bell, emails and Send feedback',
      keywords: 'notifications bell email feedback problem idea',
      body: (
        <Items>
          <List.Item>The <b>bell</b> shows rota, swap and leave updates.</List.Item>
          <List.Item>You get an email for swap requests, confirmed leave and when the final rota is published.</List.Item>
          <List.Item>Report a problem or share an idea with <b>Send feedback</b>: the speech-bubble icon in the header, or <b>Send feedback</b> in the side menu on a phone. You can choose whether your hospital's admins or the app team read it.</List.Item>
        </Items>
      ),
    },

    {
      id: 'admin-rota', chapter: 'admin', icon: IconWand, title: 'Running a rota period', blurb: 'Lock, generate, review, publish',
      keywords: 'admin generate lock draft publish review report generator notes seed unpublish',
      show: admin,
      body: (
        <>
          <Text size="sm">On <Anchor inherit component={Link} to="/admin">Admin → Generate rota</Anchor>, for each period:</Text>
          <List type="ordered" spacing={6} size="sm">
            <List.Item><b>Lock leave</b> after the deadline, then <b>Generate draft</b> (a seed gives the same rota again). You can keep several drafts and compare them.</List.Item>
            <List.Item>Read the badges (unassigned, back-to-back, manual). The <b>notes from the generator</b> are folded into a one-line summary so the rota stays in view: press <b>Show all</b> to read them.</List.Item>
            <List.Item>Click a day to <b>change who is on call</b>. You get a warning if it clashes with leave.</List.Item>
            <List.Item><b>Send for review</b> with an optional swap deadline. Officers swap among themselves; <b>Back to draft</b> pulls it back.</List.Item>
            <List.Item><b>Publish.</b> Everyone is told, open swaps close and the shifts go into the tally. A <b>PDF report</b> is saved to private storage automatically.</List.Item>
          </List>
          <Text size="sm" c="dimmed">Unpublish reverses publishing. Timetable (Excel) and Report (PDF) are on a published rota.</Text>
        </>
      ),
    },
    {
      id: 'admin-people', chapter: 'admin', icon: IconUsers, title: 'Officers, roles and clinics', blurb: 'One record per person, clinics and districts',
      keywords: 'admin officers accounts roles supervisor invite import csv clinics districts email needed blocked weekday',
      show: admin,
      body: (
        <Items>
          <List.Item><b>Officers tab:</b> an officer and their sign-in are one record. Add someone with their email to invite them straight away; change email, role and <b>Can sign in</b> on the same form. Each row can send a new temporary password, and the list filters by role and sign-in.</List.Item>
          <List.Item>Roles: <b>Officer</b>, <b>Admin</b> (an officer who also runs the admin pages) and <b>Supervisor</b> (admin pages only, never on the rota, so no on-call, excluded or left status: switch off Can sign in when one leaves).</List.Item>
          <List.Item>Officers without an email from before the update stay on the rota and show <b>Email needed</b>: edit each one to add it.</List.Item>
          <List.Item>Status: <b>On call</b>, <b>Excluded</b> (temporarily out) or <b>Left</b> (hidden, history kept). Also set blocked weekdays, extra shift and weekend preference here.</List.Item>
          <List.Item><b>CSV import</b> needs an Email on every row and accepts the Supervisor role.</List.Item>
          <List.Item><b>Clinics tab:</b> pick a state or district, search, and add, rename or remove clinics. Clinic event days block a date for the whole clinic.</List.Item>
        </Items>
      ),
    },
    {
      id: 'admin-periods', chapter: 'admin', icon: IconCalendarTime, title: 'Periods, leave and tally', blurb: 'Deadlines, budgets, confirmed leave',
      keywords: 'admin periods holidays peak days points budget extra points leave out confirmed leave requests tally',
      show: admin,
      body: (
        <Items>
          <List.Item><b>Periods & holidays:</b> create a period (dates, leave deadline, points budget), edit its public holidays and peak days, give officers <b>extra points</b> with a reason, leave an officer out of the period, lock or reopen it. Holidays are kept per state.</List.Item>
          <List.Item><b>Confirmed leave:</b> approve or reject officers' requests range by range (a rejection needs a reason), or add confirmed leave yourself. Remember the 33% deduction and the officer's CR days.</List.Item>
          <List.Item><b>Tally:</b> shows each officer's weekday and weekend/PH shifts; edit it to enter history from before the app. Excel exports it.</List.Item>
          <List.Item>You can edit any officer's leave after the deadline from the Confirmed leave and leave pages.</List.Item>
        </Items>
      ),
    },
    {
      id: 'admin-docs', chapter: 'admin', icon: IconFileCertificate, title: 'Reports, surat arahan and feedback', blurb: 'Saved reports and documents',
      keywords: 'admin reports pdf surat arahan feedback manual storage R2',
      show: admin,
      body: (
        <Items>
          <List.Item><b>Reports:</b> every time you publish, a PDF report (assignments, why each officer is on each day, swaps, overrides, totals and the audit trail) is saved. Download earlier ones here, or press <b>Save report now</b> for a rota published before reports existed.</List.Item>
          <List.Item><b>Surat arahan:</b> upload the latest letter. Officers download it with their ETAP claim. Masters set each state's weekly rest days.</List.Item>
          <List.Item><b>Feedback:</b> read, filter and mark resolved what officers send.</List.Item>
          <List.Item>Admins and supervisors cannot read officers' ETAP profiles.</List.Item>
        </Items>
      ),
    },
    {
      id: 'admin-masters', chapter: 'admin', icon: IconBuildingHospital, title: 'Masters: all hospitals', blurb: 'Hospitals, settings, manual and audit log',
      keywords: 'master hospitals states districts email switch manual audit log reports',
      show: master,
      body: (
        <Items>
          <List.Item>A <b>Master</b> looks after the whole app: add hospitals, states and districts, and switch between hospitals from the header.</List.Item>
          <List.Item><b>All hospitals → Settings</b> switches email off or on for the whole app. While it is off, invites show the temporary password instead of emailing it.</List.Item>
          <List.Item>The <b>user manual</b> is the same for every hospital, so masters upload it from All hospitals → Manual. The <b>audit log</b> and every hospital's <b>rota reports</b> are also there, with a Hospital filter.</List.Item>
        </Items>
      ),
    },
  ].filter(e => e.show !== false), [admin, master])

  const q = query.trim().toLowerCase()
  const shown = useMemo(() => (q ? entries.filter(e => `${e.title} ${e.blurb} ${e.keywords}`.toLowerCase().includes(q)) : entries), [entries, q])
  const chapters = CHAPTERS.filter(c => shown.some(e => e.chapter === c.id))

  // Highlight the section being read in the side directory.
  useEffect(() => {
    const els = shown.map(e => document.getElementById(e.id)).filter((x): x is HTMLElement => !!x)
    if (!els.length) return
    const obs = new IntersectionObserver(es => {
      const hit = es.filter(x => x.isIntersecting).sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top)[0]
      if (hit) setActive(hit.target.id)
    }, { rootMargin: '-80px 0px -65% 0px' })
    els.forEach(x => obs.observe(x))
    return () => obs.disconnect()
  }, [shown])

  const go = (id: string) => {
    setQuery('')
    // Wait a tick so a filtered-out section is back in the page before scrolling.
    setTimeout(() => document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' }), 30)
  }

  return (
    <Stack gap="lg" maw={1180} mx="auto" w="100%">
      <PageHeader icon={IconBook} title="Rules and guidelines" description="How the on-call rota is shared out, how to record cases and claim ETAP, and what you need to know.">
        <ManualButton />
      </PageHeader>

      <Alert color="yellow" variant="light" icon={<IconStar />} title="Outstation or travelling? Ask for extra points">
        <Text size="sm">
          If you'll be <b>outstation, travelling, on a course or dealing with family matters</b> and your leave points aren't
          enough, <b>ask the admin for extra points before the leave deadline.</b>
        </Text>
        <List size="sm" spacing={4} mt="xs">
          <List.Item>The admin can add extra points for you for that period, with the reason noted.</List.Item>
          <List.Item>
            Leave that's <b>officially approved</b>, such as an overseas trip, goes in as confirmed leave: send it on <b>Request leave</b>{' '}
            (see <Anchor component="button" type="button" fz="sm" onClick={() => go('confirmed')}>Approved leave</Anchor>), even when you're out of points. <b>Include your CR (cuti rehat) days</b> when you send it.
          </List.Item>
          <List.Item>Ask <b>early</b>, because busy days fill up first come, first served.</List.Item>
          <List.Item>
            If the app won't let you mark a day because you're out of points, ask the admin. Don't leave it unmarked, or you
            may be put on call on a day you're away.
          </List.Item>
        </List>
      </Alert>

      <Paper className="panel" p="lg">
        <Stack gap="md">
          <Group justify="space-between" align="flex-end" wrap="wrap">
            <Title order={3} size="h4" c="brand.9">Directory</Title>
            <TextInput placeholder="Search the rules, e.g. swap, ETAP, points" leftSection={<IconSearch size={16} />} w={{ base: '100%', sm: 340 }}
              aria-label="Search the rules" value={query} onChange={e => setQuery(e.currentTarget.value)}
              rightSection={query ? <UnstyledButton aria-label="Clear search" onClick={() => setQuery('')}><IconX size={16} /></UnstyledButton> : null} />
          </Group>
          {chapters.length === 0 ? (
            <Text c="dimmed" size="sm">Nothing matches "{query}". Try another word, or ask the admin.</Text>
          ) : (
            <SimpleGrid cols={{ base: 1, sm: 2, lg: chapters.length > 3 ? 4 : chapters.length }} spacing="md">
              {chapters.map(c => (
                <Card key={c.id} withBorder radius="md" p="md" className="rules-chapter">
                  <Group gap="sm" wrap="nowrap" mb="xs">
                    <ThemeIcon size={38} radius="md" variant="light" color={c.color}><c.icon size={22} stroke={1.8} /></ThemeIcon>
                    <div>
                      <Text fw={700} lh={1.2}>{c.title}</Text>
                      <Text size="xs" c="dimmed" lh={1.3}>{c.blurb}</Text>
                    </div>
                  </Group>
                  <Stack gap={2}>
                    {shown.filter(e => e.chapter === c.id).map(e => (
                      <UnstyledButton key={e.id} className="rules-link" onClick={() => go(e.id)}>
                        <Group gap={8} wrap="nowrap"><e.icon size={15} stroke={1.8} /><Text size="sm">{e.title}</Text></Group>
                      </UnstyledButton>
                    ))}
                  </Stack>
                </Card>
              ))}
            </SimpleGrid>
          )}
        </Stack>
      </Paper>

      <Group align="flex-start" gap="lg" wrap="nowrap">
        <Box visibleFrom="md" w={230} style={{ position: 'sticky', top: 76, flexShrink: 0, maxHeight: 'calc(100vh - 96px)', overflowY: 'auto' }}>
          <Stack gap="md">
            {chapters.map(c => (
              <div key={c.id}>
                <Text size="xs" fw={700} tt="uppercase" c="dimmed" mb={4}>{c.title}</Text>
                <Stack gap={0}>
                  {shown.filter(e => e.chapter === c.id).map(e => (
                    <UnstyledButton key={e.id} className={`rules-side${active === e.id ? ' active' : ''}`} onClick={() => go(e.id)}>
                      <Text size="sm">{e.title}</Text>
                    </UnstyledButton>
                  ))}
                </Stack>
              </div>
            ))}
          </Stack>
        </Box>

        <Stack gap="lg" style={{ flex: 1, minWidth: 0 }}>
          {chapters.map(c => (
            <Stack key={c.id} gap="md">
              <Group gap="sm" mt="sm">
                <ThemeIcon size={30} radius="xl" color={c.color}><c.icon size={18} stroke={1.8} /></ThemeIcon>
                <Title order={2} size="h3" c="brand.9">{c.title}</Title>
              </Group>
              {shown.filter(e => e.chapter === c.id).map(e => <EntryCard key={e.id} e={e} />)}
            </Stack>
          ))}

          {!q && (
            <Alert color="brand" variant="light" title="Quick tips">
              <List size="sm" spacing={4}>
                <List.Item>Mark leave <b>early</b>, especially weekends, public holidays and peak days.</List.Item>
                <List.Item><b>Outstation, travelling or other commitments and not enough points? Ask the admin for extra points early.</b></List.Item>
                <List.Item>Check your dates as soon as the draft is in review, so you have time to swap.</List.Item>
                <List.Item>Record your cases the same night, while the details are fresh, and keep your ETAP profile verified.</List.Item>
                <List.Item>Any question about points, limits or your tally: ask the admin.</List.Item>
              </List>
            </Alert>
          )}
        </Stack>
      </Group>

      <Affix position={{ bottom: 20, right: 20 }}>
        <Transition transition="slide-up" mounted={scroll.y > 600}>
          {styles => (
            <Button style={styles} radius="xl" leftSection={<IconArrowUp size={16} />} onClick={() => scrollTo({ y: 0 })}>Directory</Button>
          )}
        </Transition>
      </Affix>
    </Stack>
  )
}
