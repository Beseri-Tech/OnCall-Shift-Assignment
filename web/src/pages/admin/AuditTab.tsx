import {
  Badge, Button, Code, Group, Pagination, Paper, Select, SimpleGrid, Stack, Table, Text, UnstyledButton,
} from '@mantine/core'
import { DatePickerInput } from '@mantine/dates'
import { useQuery } from '@tanstack/react-query'
import { IconChevronDown, IconChevronRight, IconFilterOff } from '@tabler/icons-react'
import { Fragment, useState } from 'react'
import { api, type AuditEntry, type AuditQuery, type IsoDate } from '../../api'
import { ErrorBox, Loading } from '../../components/common'

const PAGE_SIZE = 50

/** What each kind of logged thing is called on screen. */
const ENTITIES: Record<string, string> = {
  person: 'Officer', state: 'State', district: 'District', clinic: 'Clinic', period: 'Rota period',
  'period-day': 'Holiday / peak day', points: 'Leave points', holiday: 'Public holiday (master list)',
  'peak-day': 'Peak day (master list)', run: 'Rota', tally: 'Tally', account: 'Account', entries: 'Leave', swap: 'Swap',
}

const VERBS: Record<string, string> = {
  create: 'added', update: 'edited', delete: 'deleted', upsert: 'saved', 'bulk-add': 'added in bulk', import: 'imported',
  reorder: 'reordered', bulk: 'added in bulk', 'copy-master': 'copied from master list', open: 'unlocked', locked: 'locked',
  generate: 'generated', override: 'shift changed', review: 'sent for review', 'withdraw-review': 'withdrawn from review',
  publish: 'published', unpublish: 'unpublished', grant: 'changed', invite: 'invited', resend: 'new temporary password',
  'change-password': 'password changed', 'reset-password': 'password reset', request: 'requested', accept: 'accepted',
  decline: 'declined', cancel: 'withdrawn',
}

const COLORS: Record<string, string> = { create: 'green', delete: 'red', publish: 'brand', accept: 'teal', decline: 'orange' }

const entityLabel = (e: string) => ENTITIES[e] ?? e
function actionLabel(action: string) {
  const [entity, ...rest] = action.split('.')
  const verb = rest.join('.')
  return `${entityLabel(entity)} ${VERBS[verb] ?? verb.replace(/-/g, ' ')}`
}
const actionColor = (action: string) => COLORS[action.split('.')[1]] ?? 'gray'

/** "statusReason" / "StatusReason" -> "Status reason". */
const fieldLabel = (k: string) => {
  const words = k.replace(/([a-z0-9])([A-Z])/g, '$1 $2').toLowerCase()
  return words.charAt(0).toUpperCase() + words.slice(1)
}

function show(v: unknown): string {
  if (v === null || v === undefined || v === '') return '–'
  if (typeof v === 'boolean') return v ? 'Yes' : 'No'
  if (Array.isArray(v)) return v.length === 0 ? '–' : v.map(show).join(', ')
  if (typeof v === 'object') return JSON.stringify(v)
  return String(v)
}

const isRecord = (v: unknown): v is Record<string, unknown> => typeof v === 'object' && v !== null && !Array.isArray(v)

type Change = { field: string; before: unknown; after: unknown }

/** Fields that differ between before and after; for something added or deleted, every field it had. */
function changes(e: AuditEntry): Change[] {
  const before = isRecord(e.before) ? e.before : {}
  const after = isRecord(e.after) ? e.after : {}
  const keys = [...new Set([...Object.keys(before), ...Object.keys(after)])]
  return keys
    .filter(k => JSON.stringify(before[k] ?? null) !== JSON.stringify(after[k] ?? null))
    .map(k => ({ field: k, before: before[k], after: after[k] }))
}

function summary(e: AuditEntry): string {
  const c = changes(e)
  if (c.length === 0) return ''
  if (e.before === null || e.after === null) {
    const values = isRecord(e.after) ? e.after : isRecord(e.before) ? e.before : {}
    return show(values.name ?? values.Name ?? values.email ?? values.Email ?? '')
  }
  return c.slice(0, 3).map(x => `${fieldLabel(x.field)}: ${show(x.before)} → ${show(x.after)}`).join('; ')
    + (c.length > 3 ? ` (+${c.length - 3} more)` : '')
}

const whenFmt = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })

function Details({ e }: { e: AuditEntry }) {
  const c = changes(e)
  const detail = isRecord(e.detail) ? Object.entries(e.detail) : []
  return (
    <Stack gap="sm" p="sm">
      {c.length > 0 && (
        <Table withTableBorder withColumnBorders fz="sm" verticalSpacing={4}>
          <Table.Thead>
            <Table.Tr><Table.Th w={180}>Field</Table.Th><Table.Th>Before</Table.Th><Table.Th>After</Table.Th></Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {c.map(x => (
              <Table.Tr key={x.field}>
                <Table.Td fw={600}>{fieldLabel(x.field)}</Table.Td>
                <Table.Td c={e.before === null ? 'dimmed' : 'red.8'}>{e.before === null ? '(new)' : show(x.before)}</Table.Td>
                <Table.Td c={e.after === null ? 'dimmed' : 'green.8'}>{e.after === null ? '(deleted)' : show(x.after)}</Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      )}
      {detail.length > 0 && (
        <SimpleGrid cols={{ base: 1, sm: 2 }} spacing={4}>
          {detail.map(([k, v]) => (
            <Text key={k} size="sm"><Text span fw={600}>{fieldLabel(k)}:</Text> {show(v)}</Text>
          ))}
        </SimpleGrid>
      )}
      {c.length === 0 && detail.length === 0 && <Text size="sm" c="dimmed">No further details were recorded.</Text>}
      <Text size="xs" c="dimmed">
        {e.entityId && <>Id <Code fz="xs">{e.entityId}</Code> · </>}
        {e.ip ?? 'unknown address'}{e.userAgent && ` · ${e.userAgent}`}
      </Text>
    </Stack>
  )
}

/** Who changed what and when. Admins and supervisors only (the API checks too). */
export function AuditTab() {
  const [range, setRange] = useState<[IsoDate | null, IsoDate | null]>([null, null])
  const [accountId, setAccountId] = useState<string | null>(null)
  const [entity, setEntity] = useState<string | null>(null)
  const [action, setAction] = useState<string | null>(null)
  const [personId, setPersonId] = useState<string | null>(null)
  const [page, setPage] = useState(1)
  const [open, setOpen] = useState<number | null>(null)

  const query: AuditQuery = {
    from: range[0] ?? undefined, to: range[1] ?? range[0] ?? undefined, accountId: accountId ?? undefined,
    entity: entity ?? undefined, action: action ?? undefined, personId: personId ?? undefined, page, pageSize: PAGE_SIZE,
  }
  const log = useQuery({ queryKey: ['admin', 'audit', query], queryFn: () => api.admin.audit(query), placeholderData: p => p })
  const filters = useQuery({ queryKey: ['admin', 'audit', 'filters'], queryFn: api.admin.auditFilters })
  const people = useQuery({ queryKey: ['admin', 'people'], queryFn: api.admin.people })

  // Any filter change goes back to the first page.
  const set = <T,>(setter: (v: T) => void) => (v: T) => { setter(v); setPage(1); setOpen(null) }
  const filtered = range[0] || accountId || entity || action || personId
  const clear = () => { setRange([null, null]); setAccountId(null); setEntity(null); setAction(null); setPersonId(null); setPage(1) }

  if (log.error) return <ErrorBox error={log.error} />

  const actions = (filters.data?.actions ?? []).filter(a => !entity || a.startsWith(`${entity}.`))
  const pages = Math.max(1, Math.ceil((log.data?.total ?? 0) / PAGE_SIZE))

  return (
    <Stack gap="md">
      <Group wrap="wrap" align="flex-end">
        <DatePickerInput type="range" allowSingleDateInRange label="Dates" placeholder="Any time" clearable w={240}
          valueFormat="D MMM YYYY" value={range}
          onChange={set((r: unknown) => setRange(r as [IsoDate | null, IsoDate | null]))} />
        <Select label="Who" placeholder="Anyone" clearable searchable w={220} value={accountId} onChange={set(setAccountId)}
          data={(filters.data?.actors ?? []).map(a => ({ value: a.accountId, label: a.name ? `${a.name} (${a.email})` : a.email }))} />
        <Select label="What" placeholder="Everything" clearable w={220} value={entity}
          onChange={set((v: string | null) => { setEntity(v); setAction(null) })}
          data={(filters.data?.entities ?? []).map(e => ({ value: e, label: entityLabel(e) }))} />
        <Select label="Action" placeholder="Any action" clearable searchable w={240} value={action} onChange={set(setAction)}
          data={actions.map(a => ({ value: a, label: actionLabel(a) }))} />
        <Select label="About officer" placeholder="Any officer" clearable searchable w={220} value={personId} onChange={set(setPersonId)}
          data={(people.data ?? []).map(p => ({ value: p.id, label: `${p.name} (${p.code})` }))} />
        {filtered && <Button variant="subtle" leftSection={<IconFilterOff size={16} />} onClick={clear}>Clear</Button>}
      </Group>

      <Text size="sm" c="dimmed">
        Every change made in the app, newest first. Click a row to see the values before and after.
        {log.data && ` ${log.data.total} change${log.data.total === 1 ? '' : 's'}.`}
      </Text>

      {log.isLoading ? <Loading /> : (
        <Paper withBorder>
          <Table.ScrollContainer minWidth={820}>
            <Table highlightOnHover verticalSpacing={6} opacity={log.isPlaceholderData ? 0.6 : 1}>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th w={28} />
                  <Table.Th w={170}>When</Table.Th>
                  <Table.Th w={200}>Who</Table.Th>
                  <Table.Th w={230}>Action</Table.Th>
                  <Table.Th>Change</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {log.data?.items.length === 0 && (
                  <Table.Tr><Table.Td colSpan={5}><Text c="dimmed" ta="center" py="md">No changes match.</Text></Table.Td></Table.Tr>
                )}
                {log.data?.items.map(e => {
                  const isOpen = open === e.id
                  const Chevron = isOpen ? IconChevronDown : IconChevronRight
                  return (
                    <Fragment key={e.id}>
                      <Table.Tr style={{ cursor: 'pointer' }} onClick={() => setOpen(isOpen ? null : e.id)}>
                        <Table.Td>
                          <UnstyledButton aria-label={isOpen ? 'Hide details' : 'Show details'} aria-expanded={isOpen}>
                            <Chevron size={16} />
                          </UnstyledButton>
                        </Table.Td>
                        <Table.Td><Text size="sm">{whenFmt.format(new Date(e.at))}</Text></Table.Td>
                        <Table.Td>
                          <Text size="sm" fw={600}>{e.actorName ?? e.actorEmail ?? 'Not signed in'}</Text>
                          {e.actorName && <Text size="xs" c="dimmed">{e.actorEmail}</Text>}
                        </Table.Td>
                        <Table.Td><Badge variant="light" color={actionColor(e.action)} tt="none" h="auto" py={2}
                          styles={{ label: { whiteSpace: 'normal' } }}>{actionLabel(e.action)}</Badge></Table.Td>
                        <Table.Td>
                          {e.personName && <Text size="sm" fw={600}>{e.personName}</Text>}
                          {summary(e) !== e.personName && <Text size="sm" c="dimmed" lineClamp={2}>{summary(e)}</Text>}
                        </Table.Td>
                      </Table.Tr>
                      {isOpen && (
                        <Table.Tr>
                          <Table.Td colSpan={5} bg="gray.0"><Details e={e} /></Table.Td>
                        </Table.Tr>
                      )}
                    </Fragment>
                  )
                })}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Paper>
      )}

      {pages > 1 && <Group justify="center"><Pagination total={pages} value={page} onChange={p => { setPage(p); setOpen(null) }} /></Group>}
    </Stack>
  )
}
