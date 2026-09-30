import { Alert, Group, SegmentedControl, Stack, Switch, Text, TextInput, Title } from '@mantine/core'
import { useQuery } from '@tanstack/react-query'
import { IconInfoCircle, IconSearch } from '@tabler/icons-react'
import { useMemo, useState } from 'react'
import { api } from '../api'
import { useMe } from '../auth'
import { ErrorBox, Loading, PeriodSelect, StatusBadge } from '../components/common'
import { defaultPeriod, pick } from '../lib'
import { dayOfMonth, formatDow, formatLong, formatMonth, monthsBetween, todayIso } from '../dates'

const ALL = 'all'

/** Read-only sheet like the old Google Sheet: people × days, leave in red, preferred in blue. */
export function OverviewPage() {
  const [chosenId, setPeriodId] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const [onlyLeave, setOnlyLeave] = useState(false)
  const [chosenMonth, setMonth] = useState<string | null>(null)
  const myId = useMe().data?.personId ?? null

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

  // One month at a time fits the screen; "whole period" scrolls sideways like the old sheet.
  const months = data ? monthsBetween(data.period.startDate, data.period.endDate) : []
  const thisMonth = `${todayIso().slice(0, 7)}-01`
  const month = chosenMonth && (chosenMonth === ALL || months.includes(chosenMonth))
    ? chosenMonth
    : months.includes(thisMonth) ? thisMonth : months[0] ?? ALL
  const days = (data?.days ?? []).filter(d => month === ALL || d.date.startsWith(month.slice(0, 7)))
  const fit = month !== ALL

  // Month header spans.
  const monthSpans: { month: string; span: number }[] = []
  for (const d of days) {
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
          <Group justify="space-between" wrap="wrap" gap="sm">
            <Group gap="md">
              <StatusBadge status={data.period.status} />
              <Text size="sm">{formatLong(data.period.startDate)} – {formatLong(data.period.endDate)}</Text>
              <Text size="sm" c="dimmed">{total} people</Text>
            </Group>
            <SegmentedControl value={month} onChange={setMonth} data={[
              ...months.map(m => ({ value: m, label: formatMonth(m) })),
              { value: ALL, label: 'Whole period' },
            ]} />
          </Group>

          <div className="sheet-wrap">
            <table className={`sheet${fit ? ' sheet-fit' : ''}`}>
              <colgroup>
                <col className="name-w" />
                {days.map(d => <col key={d.date} />)}
                <col className="off-w" />
              </colgroup>
              <thead>
                <tr>
                  <th className="name-col" rowSpan={2}>Name</th>
                  {monthSpans.map(m => <th key={m.month} colSpan={m.span} className="month-head">{formatMonth(`${m.month}-01`)}</th>)}
                  <th className="off-col" rowSpan={2} title="Leave days in this view">Off</th>
                </tr>
                <tr>
                  {days.map(d => (
                    <th key={d.date} className={`day-head${d.isWeekendHoliday ? ' wk' : ''}${d.holidayName ? ' ph' : ''}`}
                      title={d.holidayName ?? formatLong(d.date)}>
                      <span className="dow">{d.holidayName ? 'PH' : formatDow(d.date)}</span>
                      <span className="dnum">{dayOfMonth(d.date)}</span>
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {people.map(p => {
                  const leave = new Map(p.leave.map(l => [l.date, l.note]))
                  const preferred = new Set(p.preferred)
                  const off = days.filter(d => leave.has(d.date)).length
                  return (
                    <tr key={p.id} className={p.id === myId ? 'me' : ''}>
                      <td className="name-col" title={`${p.name} (${p.code})`}>
                        <span className="pname">{p.name}</span>
                        <span className="pcode">{p.code}</span>
                      </td>
                      {days.map(d => {
                        if (leave.has(d.date)) {
                          const note = leave.get(d.date)
                          return <td key={d.date} className="cell-leave" title={`${p.name}: leave${note ? ` – ${note}` : ''}`}>{note ? note[0].toUpperCase() : 'L'}</td>
                        }
                        if (preferred.has(d.date))
                          return <td key={d.date} className="cell-pref" title={`${p.name}: preferred on-call`}>★</td>
                        return <td key={d.date} className={d.isWeekendHoliday ? 'wk' : ''} />
                      })}
                      <td className={`off-col${off ? '' : ' zero'}`}>{off}</td>
                    </tr>
                  )
                })}
                {people.length === 0 && (
                  <tr><td className="name-col empty" colSpan={days.length + 2}>No one matches.</td></tr>
                )}
              </tbody>
              <tfoot>
                <tr>
                  <td className="name-col">Available</td>
                  {days.map(d => {
                    const ratio = total ? d.available / total : 1
                    const cls = d.available <= 2 ? 'critical' : ratio < 0.5 ? 'low' : ''
                    return <td key={d.date} className={cls} title={`${d.available} of ${total} available`}>{d.available}</td>
                  })}
                  <td className="off-col" />
                </tr>
              </tfoot>
            </table>
          </div>

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
