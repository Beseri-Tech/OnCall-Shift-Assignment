import { Alert, Group, Paper, Select, SimpleGrid, Stack, Table, Text, TextInput, Title } from '@mantine/core'
import { useQuery } from '@tanstack/react-query'
import { IconInfoCircle, IconSearch, IconUserCheck } from '@tabler/icons-react'
import { useMemo, useState } from 'react'
import { api } from '../api'
import { useMe } from '../auth'
import { ErrorBox, Legend, Loading, PageHeader } from '../components/common'
import { defaultPeriod, pick } from '../lib'
import { RotaCalendar } from '../components/RotaCalendar'
import { formatLong, formatWeekday } from '../dates'

export function RotaPage() {
  const [chosenId, setPeriodId] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const myId = useMe().data?.personId ?? null

  // Published rotas, plus drafts shown for review (marked as such).
  const allPeriods = useQuery({ queryKey: ['periods'], queryFn: () => api.periods() })
  const periods = { ...allPeriods, data: allPeriods.data?.filter(p => p.status === 'Published' || p.status === 'Review') }
  const periodId = pick(periods.data, chosenId, defaultPeriod(periods.data, () => true))?.id ?? null

  const rota = useQuery({ queryKey: ['rota', periodId], queryFn: () => api.rota(periodId!), enabled: !!periodId })

  const perPerson = useMemo(() => {
    const map = new Map<string, { name: string; dates: string[]; weekend: number }>()
    for (const d of rota.data?.days ?? []) {
      if (!d.personId) continue
      const e = map.get(d.personId) ?? { name: d.personName ?? '', dates: [], weekend: 0 }
      e.dates.push(d.date)
      if (d.isWeekendHoliday) e.weekend++
      map.set(d.personId, e)
    }
    const s = search.trim().toLowerCase()
    return [...map.entries()]
      .filter(([, e]) => !s || e.name.toLowerCase().includes(s))
      .sort(([a, x], [b, y]) => (a === myId ? -1 : b === myId ? 1 : x.name.localeCompare(y.name)))
  }, [rota.data, search, myId])

  if (periods.isLoading) return <Loading />
  if (periods.error) return <ErrorBox error={periods.error} />
  if (!periods.data?.length)
    return <Alert icon={<IconInfoCircle />} title="No published rota yet">The rota appears here once the admin publishes it.</Alert>

  const mine = rota.data?.days.filter(d => d.personId === myId) ?? []

  return (
    <Stack gap="md">
      <PageHeader title="On-call rota" description="Who is on call each day. Your shifts are highlighted.">
        <Select
          label="Period"
          data={periods.data.map(p => ({ value: p.id, label: p.status === 'Review' ? `${p.name} (in review)` : p.name }))}
          value={periodId}
          onChange={setPeriodId}
          allowDeselect={false}
          w={240}
        />
      </PageHeader>

      {rota.isLoading && <Loading />}
      {rota.error && <ErrorBox error={rota.error} />}

      {rota.data?.inReview && (
        <Alert color="gray" icon={<IconInfoCircle />} title="Draft for review – not final">
          This rota can still change: officers are swapping dates and the admin may adjust it before publishing.
          {rota.data.period.swapDeadline ? ` Swaps close after ${formatLong(rota.data.period.swapDeadline)}.` : ''}
        </Alert>
      )}

      {rota.data && (
        <>
          {myId && (
            <Paper className="panel" p="md" bg="var(--brand-soft)">
              <Group gap="xs" mb={8}>
                <IconUserCheck size={18} stroke={1.5} color="var(--shift-text)" />
                <Text fw={500} c="var(--shift-text)">Your shifts</Text>
                {mine.length > 0 && <Text size="sm" c="var(--shift-text)">({mine.length})</Text>}
              </Group>
              {mine.length === 0
                ? <Text size="sm" c="dimmed">You have no shifts in this rota.</Text>
                : <Group gap={6}>{mine.map(d => (
                    <span key={d.date} className="shift-chip">{formatWeekday(d.date)}</span>
                  ))}</Group>}
            </Paper>
          )}

          <Paper className="panel" p="md">
            <RotaCalendar days={rota.data.days} highlightPersonId={myId} />
            <Group gap="lg" mt="md">
              {myId && <Legend bg="var(--shift-bg)" border="var(--shift-border)" label="Your shift" />}
              <Legend bg="var(--weekend-bg)" border="var(--line)" label="Weekend / public holiday" />
              <Legend bg="var(--leave-bg)" border="var(--leave-border)" label="Unassigned" />
              <Legend bg="white" border="var(--warn-border)" label="Changed by the admin" />
            </Group>
          </Paper>

          <Paper className="panel" p="md">
            <Group justify="space-between" mb="sm">
              <Title order={4}>By person</Title>
              <TextInput placeholder="Find a name" leftSection={<IconSearch size={16} stroke={1.5} />} value={search}
                onChange={e => setSearch(e.currentTarget.value)} w={240} />
            </Group>
            <SimpleGrid cols={1}>
              <Table highlightOnHover className="rota-table">
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Name</Table.Th>
                    <Table.Th>Shifts</Table.Th>
                    <Table.Th>Weekend + PH</Table.Th>
                    <Table.Th>Dates</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {perPerson.map(([id, e]) => (
                    <Table.Tr key={id} bg={id === myId ? 'brand.0' : undefined}>
                      <Table.Td fw={500}>{e.name}</Table.Td>
                      <Table.Td>{e.dates.length}</Table.Td>
                      <Table.Td>{e.weekend}</Table.Td>
                      <Table.Td>{e.dates.map(formatLong).join(', ')}</Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </SimpleGrid>
          </Paper>
        </>
      )}
    </Stack>
  )
}
