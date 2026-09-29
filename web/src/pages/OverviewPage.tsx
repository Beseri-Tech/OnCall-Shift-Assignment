import { Alert, Group, Paper, Stack, Switch, Text, TextInput, Title } from '@mantine/core'
import { useLocalStorage } from '@mantine/hooks'
import { useQuery } from '@tanstack/react-query'
import { IconInfoCircle, IconSearch } from '@tabler/icons-react'
import { useMemo, useState } from 'react'
import { api } from '../api'
import { ErrorBox, Loading, PeriodSelect, StatusBadge } from '../components/common'
import { defaultPeriod, pick } from '../lib'
import { dayOfMonth, formatDow, formatLong, formatMonth } from '../dates'

/** Read-only sheet like the old Google Sheet: people × days, leave in red, preferred in blue. */
export function OverviewPage() {
  const [chosenId, setPeriodId] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const [onlyLeave, setOnlyLeave] = useState(false)
  const [myId] = useLocalStorage<string | null>({ key: 'rota.personId', defaultValue: null })

  const periods = useQuery({ queryKey: ['periods'], queryFn: () => api.periods() })
  const periodId = pick(periods.data, chosenId, defaultPeriod(periods.data))?.id ?? null

  const overview = useQuery({
    queryKey: ['overview', periodId],
    queryFn: () => api.overview(periodId!),
    enabled: !!periodId,
  })

  const people = useMemo(() => {
    const s = search.trim().toLowerCase()
    return (overview.data?.people ?? []).filter(p =>
      (!s || p.name.toLowerCase().includes(s) || p.code.toLowerCase().includes(s)) &&
      (!onlyLeave || p.leave.length > 0))
  }, [overview.data, search, onlyLeave])

  if (periods.isLoading) return <Loading />
  if (periods.error) return <ErrorBox error={periods.error} />
  if (!periods.data?.length)
    return <Alert icon={<IconInfoCircle />} title="No rota periods yet">The admin hasn't created any periods.</Alert>

  const data = overview.data
  const total = data?.people.length ?? 0

  // Month header spans.
  const monthSpans: { month: string; span: number }[] = []
  for (const d of data?.days ?? []) {
    const m = d.date.slice(0, 7)
    const last = monthSpans[monthSpans.length - 1]
    if (last?.month === m) last.span++
    else monthSpans.push({ month: m, span: 1 })
  }

  return (
    <Stack gap="md">
      <Group justify="space-between" align="flex-end" wrap="wrap">
        <div>
          <Title order={2}>Leave overview</Title>
          <Text c="dimmed" size="sm">Everyone's leave for the period. The bottom row shows how many people are available each day.</Text>
        </div>
        <Group align="flex-end" wrap="wrap">
          <TextInput label="Find" placeholder="Name or code" leftSection={<IconSearch size={16} />}
            value={search} onChange={e => setSearch(e.currentTarget.value)} w={220} />
          <Switch label="Only people with leave" checked={onlyLeave} onChange={e => setOnlyLeave(e.currentTarget.checked)} mb={8} />
          <PeriodSelect periods={periods.data} value={periodId} onChange={setPeriodId} />
        </Group>
      </Group>

      {overview.isLoading && <Loading />}
      {overview.error && <ErrorBox error={overview.error} />}

      {data && (
        <>
          <Group gap="md">
            <StatusBadge status={data.period.status} />
            <Text size="sm">{formatLong(data.period.startDate)} – {formatLong(data.period.endDate)}</Text>
            <Text size="sm" c="dimmed">{total} people</Text>
          </Group>

          <Paper p={0} withBorder={false}>
            <div className="sheet-wrap">
              <table className="sheet">
                <thead>
                  <tr>
                    <th className="name-col" rowSpan={2}>Name</th>
                    {monthSpans.map(m => <th key={m.month} colSpan={m.span}>{formatMonth(`${m.month}-01`)}</th>)}
                  </tr>
                  <tr>
                    {data.days.map(d => (
                      <th key={d.date} className={d.isWeekendHoliday ? 'wk' : ''} title={d.holidayName ?? formatLong(d.date)}>
                        <div className={d.holidayName ? 'ph' : ''} style={{ lineHeight: 1.1 }}>
                          <div style={{ fontSize: 10, fontWeight: 600 }}>{formatDow(d.date)}</div>
                          {dayOfMonth(d.date)}
                        </div>
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {people.map(p => {
                    const leave = new Map(p.leave.map(l => [l.date, l.note]))
                    const preferred = new Set(p.preferred)
                    return (
                      <tr key={p.id} className={p.id === myId ? 'me' : ''}>
                        <td className="name-col" title={`${p.name} (${p.code})`}>{p.name}</td>
                        {data.days.map(d => {
                          if (leave.has(d.date)) {
                            const note = leave.get(d.date)
                            return <td key={d.date} className="cell-leave" title={`${p.name}: leave${note ? ` – ${note}` : ''}`}>{note ? note[0].toUpperCase() : 'L'}</td>
                          }
                          if (preferred.has(d.date))
                            return <td key={d.date} className="cell-pref" title={`${p.name}: preferred on-call`}>★</td>
                          return <td key={d.date} className={d.isWeekendHoliday ? 'wk' : ''} />
                        })}
                      </tr>
                    )
                  })}
                </tbody>
                <tfoot>
                  <tr>
                    <td className="name-col">Available</td>
                    {data.days.map(d => {
                      const ratio = total ? d.available / total : 1
                      const cls = d.available <= 2 ? 'critical' : ratio < 0.5 ? 'low' : ''
                      return <td key={d.date} className={cls} title={`${d.available} of ${total} available`}>{d.available}</td>
                    })}
                  </tr>
                </tfoot>
              </table>
            </div>
          </Paper>

          <Group gap="lg">
            <Text size="xs"><span className="legend-swatch" style={{ background: 'var(--leave-bg)', borderColor: 'var(--leave-border)' }} /> Leave (letter = note, e.g. A = Annual)</Text>
            <Text size="xs"><span className="legend-swatch" style={{ background: 'var(--pref-bg)', borderColor: 'var(--pref-border)' }} /> ★ Preferred on-call</Text>
            <Text size="xs"><span className="legend-swatch" style={{ background: '#fef3c7', borderColor: '#f59e0b' }} /> Under half available</Text>
          </Group>
        </>
      )}
    </Stack>
  )
}
