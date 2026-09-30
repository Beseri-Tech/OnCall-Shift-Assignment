import {
  Badge, Button, Group, NumberInput, Paper, SegmentedControl, Select, Stack, Table, Text, TextInput, Tooltip, UnstyledButton,
} from '@mantine/core'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconArrowsSort, IconDeviceFloppy, IconDownload, IconEdit, IconSearch, IconX } from '@tabler/icons-react'
import { useMemo, useState } from 'react'
import { api, type OfficerStatus, type TallyRow } from '../../api'
import { ErrorBox, Loading } from '../../components/common'
import { notifyError, notifyOk } from '../../lib'

type Sort = 'name' | 'total' | 'weekendHoliday'
type Edit = { weekday: number; weekendHoliday: number }

function SortHead({ by, label, sort, onSort }: { by: Sort; label: string; sort: Sort; onSort: (s: Sort) => void }) {
  return (
    <UnstyledButton onClick={() => onSort(by)} fw={700} fz="sm" c={sort === by ? 'blue' : undefined}>
      <Group gap={4} wrap="nowrap" justify="center" style={{ whiteSpace: 'nowrap' }}>{label}<IconArrowsSort size={14} /></Group>
    </UnstyledButton>
  )
}

/** Shifts done per officer. Published rotas add to it automatically; the admin can correct any number. */
export function TallyTab() {
  const qc = useQueryClient()
  const tally = useQuery({ queryKey: ['admin', 'tally'], queryFn: api.admin.tally })
  const [status, setStatus] = useState<OfficerStatus | 'All'>('OnCall')
  const [area, setArea] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const [sort, setSort] = useState<Sort>('name')
  const [edits, setEdits] = useState<Map<string, Edit> | null>(null)   // null = not editing

  const save = useMutation({
    mutationFn: () => api.admin.saveTally([...edits!].map(([personId, e]) => ({ personId, ...e }))),
    onSuccess: rows => {
      qc.setQueryData(['admin', 'tally'], rows)
      notifyOk(`${edits!.size} officer(s) updated.`)
      setEdits(null)
    },
    onError: e => notifyError(e, 'Could not save'),
  })

  const areas = useMemo(() => [...new Set((tally.data ?? []).map(r => r.area).filter((a): a is string => !!a))].sort(), [tally.data])

  const rows = useMemo(() => {
    const s = search.trim().toLowerCase()
    const list = (tally.data ?? []).filter(r => (status === 'All' || r.status === status) && (!area || r.area === area)
      && (!s || r.name.toLowerCase().includes(s) || r.code.toLowerCase().includes(s)))
    return sort === 'name' ? list : [...list].sort((a, b) => b[sort] - a[sort] || a.name.localeCompare(b.name))
  }, [tally.data, status, area, search, sort])

  if (tally.isLoading) return <Loading />
  if (tally.error) return <ErrorBox error={tally.error} />

  const editing = edits !== null
  const valueOf = (r: TallyRow): Edit => edits?.get(r.personId) ?? { weekday: r.weekday, weekendHoliday: r.weekendHoliday }
  const change = (r: TallyRow, patch: Partial<Edit>) => setEdits(m => {
    const next = new Map(m)
    const v = { ...valueOf(r), ...patch }
    if (v.weekday === r.weekday && v.weekendHoliday === r.weekendHoliday) next.delete(r.personId)
    else next.set(r.personId, v)
    return next
  })
  const sums = rows.map(valueOf).reduce((t, v) => ({ weekday: t.weekday + v.weekday, weekend: t.weekend + v.weekendHoliday }), { weekday: 0, weekend: 0 })

  return (
    <Stack gap="md">
      <Group justify="space-between" wrap="wrap">
        <Group wrap="wrap">
          <SegmentedControl value={status} onChange={v => setStatus(v as OfficerStatus | 'All')} data={[
            { value: 'OnCall', label: 'On call' }, { value: 'Excluded', label: 'Excluded' },
            { value: 'Left', label: 'Left' }, { value: 'All', label: 'All' },
          ]} />
          {areas.length > 0 && <Select placeholder="All areas" clearable w={160} data={areas} value={area} onChange={setArea} />}
          <TextInput placeholder="Find name or code" leftSection={<IconSearch size={16} />} value={search}
            onChange={e => setSearch(e.currentTarget.value)} w={220} />
        </Group>
        <Group>
          {editing ? (
            <>
              <Text size="sm" c="dimmed">{edits.size} changed</Text>
              <Button variant="default" leftSection={<IconX size={16} />} onClick={() => setEdits(null)}>Cancel</Button>
              <Button leftSection={<IconDeviceFloppy size={16} />} disabled={edits.size === 0} loading={save.isPending}
                onClick={() => save.mutate()}>Save changes</Button>
            </>
          ) : (
            <>
              <Button variant="default" leftSection={<IconDownload size={16} />} component="a" href={api.admin.exports.tally()}>
                Excel
              </Button>
              <Button leftSection={<IconEdit size={16} />} onClick={() => setEdits(new Map())}>Edit tally</Button>
            </>
          )}
        </Group>
      </Group>

      <Text size="sm" c="dimmed">
        Shifts done so far. Publishing a rota adds its shifts automatically; edit a number to correct it or to enter history from
        before the app. Officers marked "not set" count as the group average for weekend balancing.
      </Text>

      <Paper withBorder>
        <Table.ScrollContainer minWidth={760}>
          <Table striped highlightOnHover verticalSpacing={6}>
            <Table.Thead>
              <Table.Tr>
                <Table.Th><SortHead by="name" label="Officer" sort={sort} onSort={setSort} /></Table.Th>
                <Table.Th>Clinic</Table.Th>
                <Table.Th ta="center" w={130}>Weekday</Table.Th>
                <Table.Th ta="center" w={160}><SortHead by="weekendHoliday" label="Weekend / PH" sort={sort} onSort={setSort} /></Table.Th>
                <Table.Th ta="center" w={110}><SortHead by="total" label="Total" sort={sort} onSort={setSort} /></Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {rows.length === 0 && (
                <Table.Tr><Table.Td colSpan={5}><Text c="dimmed" ta="center" py="md">No officers match.</Text></Table.Td></Table.Tr>
              )}
              {rows.map(r => {
                const v = valueOf(r)
                const changed = edits?.has(r.personId)
                return (
                  <Table.Tr key={r.personId} bg={changed ? 'yellow.0' : undefined} opacity={r.status === 'Left' ? 0.6 : 1}>
                    <Table.Td>
                      <Text fw={600} size="sm">{r.name}</Text>
                      <Group gap={6}>
                        <Text size="xs" c="dimmed">{r.code}</Text>
                        {r.status !== 'OnCall' && <Badge size="xs" color="gray" variant="light">{r.status}</Badge>}
                        {!r.known && !changed && <Badge size="xs" color="orange" variant="light">not set</Badge>}
                      </Group>
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm">{r.clinicName ?? '–'}</Text>
                      {r.area && <Text size="xs" c="dimmed">{r.area}</Text>}
                    </Table.Td>
                    {editing ? (
                      <>
                        <Table.Td><NumberInput size="xs" min={0} value={v.weekday} onChange={x => change(r, { weekday: Number(x) || 0 })} /></Table.Td>
                        <Table.Td><NumberInput size="xs" min={0} value={v.weekendHoliday} onChange={x => change(r, { weekendHoliday: Number(x) || 0 })} /></Table.Td>
                      </>
                    ) : (
                      <>
                        <Table.Td ta="center">{r.weekday}</Table.Td>
                        <Table.Td ta="center">{r.weekendHoliday}</Table.Td>
                      </>
                    )}
                    <Table.Td ta="center">
                      <Tooltip label={`${r.publishedWeekday + r.publishedWeekendHoliday} from published rotas, ${r.total - r.publishedWeekday - r.publishedWeekendHoliday} entered by admin`}>
                        <Text span fw={800}>{v.weekday + v.weekendHoliday}</Text>
                      </Tooltip>
                    </Table.Td>
                  </Table.Tr>
                )
              })}
            </Table.Tbody>
            <Table.Tfoot>
              <Table.Tr>
                <Table.Th colSpan={2}>{rows.length} officers</Table.Th>
                <Table.Th ta="center">{sums.weekday}</Table.Th>
                <Table.Th ta="center">{sums.weekend}</Table.Th>
                <Table.Th ta="center">{sums.weekday + sums.weekend}</Table.Th>
              </Table.Tr>
            </Table.Tfoot>
          </Table>
        </Table.ScrollContainer>
      </Paper>
    </Stack>
  )
}
