import { Badge, SimpleGrid, Text } from '@mantine/core'
import type { IsoDate, RotaDay } from '../api'
import { usePhone } from '../lib'
import { addDays, dayOfMonth, dayOfWeek, endOfMonth, formatMonth, isWeekend, monthsBetween } from '../dates'

const DOW = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']

interface Props {
  days: RotaDay[]
  highlightPersonId?: string | null
  onDayClick?: (day: RotaDay) => void
  /** Draw every date of each month, not just the given days (for a personal view with few days). */
  fillMonths?: boolean
  /** Public holidays for dates without a RotaDay (only used with fillMonths). */
  holidays?: Map<IsoDate, string>
}

/** Month calendars showing who is on call each day. */
export function RotaCalendar({ days, highlightPersonId, onDayClick, fillMonths, holidays }: Props) {
  const phone = usePhone()
  if (days.length === 0) return null
  // A personal calendar only says "On call", which fits a phone's grid; a full rota names someone every day, so phones get a list.
  if (phone && !fillMonths) return <RotaList days={days} highlightPersonId={highlightPersonId} onDayClick={onDayClick} />
  const byDate = new Map(days.map(d => [d.date, d]))
  const months = monthsBetween(days[0].date, days[days.length - 1].date)

  return (
    <SimpleGrid cols={{ base: 1, lg: 2 }} spacing="xl">
      {months.map(month => {
        const cells: (IsoDate | null)[] = Array.from({ length: (dayOfWeek(month) + 6) % 7 }, () => null)
        for (let d = month; d <= endOfMonth(month); d = addDays(d, 1)) cells.push(d)

        return (
          <div key={month}>
            <Text fw={700} mb={6}>{formatMonth(month)}</Text>
            <div className="month-grid">
              {DOW.map(d => <div key={d} className="dow">{d}</div>)}
              {cells.map((date, i) => {
                const day = date ? byDate.get(date) : undefined
                if (date && !day && fillMonths) {
                  const ph = holidays?.get(date)
                  return (
                    <div key={date} className={`rota-day empty${isWeekend(date) || ph ? ' day-weekend' : ''}`} title={ph}>
                      <Text size="xs" fw={700} c={ph ? 'green.8' : 'dimmed'}>{dayOfMonth(date)}{ph ? ' · PH' : ''}</Text>
                    </div>
                  )
                }
                if (!date || !day) return <div key={date ?? `b${i}`} />

                const cls = `rota-day ${dayClasses(day, highlightPersonId, !!onDayClick)}`

                return (
                  <div
                    key={date}
                    className={cls}
                    role={onDayClick ? 'button' : undefined}
                    tabIndex={onDayClick ? 0 : undefined}
                    onClick={() => onDayClick?.(day)}
                    onKeyDown={e => { if (onDayClick && (e.key === 'Enter' || e.key === ' ')) onDayClick(day) }}
                    title={day.holidayName ?? undefined}
                  >
                    <Text size="xs" fw={700} c={day.holidayName ? 'green.8' : 'dimmed'}>
                      {dayOfMonth(date)}{day.holidayName ? ' · PH' : ''}
                    </Text>
                    <div className="rota-name">{day.personName ?? 'Unassigned'}</div>
                  </div>
                )
              })}
            </div>
          </div>
        )
      })}
    </SimpleGrid>
  )
}

const dayClasses = (day: RotaDay, highlightPersonId: string | null | undefined, clickable: boolean) => [
  day.isWeekendHoliday && 'day-weekend',
  !day.personId && 'unassigned',
  highlightPersonId && day.personId === highlightPersonId && 'mine',
  day.isManual && 'manual',
  clickable && 'clickable',
].filter(Boolean).join(' ')

/** Phone layout: one row per day, grouped by month, so full names fit. */
function RotaList({ days, highlightPersonId, onDayClick }: Pick<Props, 'days' | 'highlightPersonId' | 'onDayClick'>) {
  const months = monthsBetween(days[0].date, days[days.length - 1].date)
  return (
    <div>
      {months.map(month => {
        const inMonth = days.filter(d => d.date >= month && d.date <= endOfMonth(month))
        if (inMonth.length === 0) return null
        return (
          <section key={month} className="rota-list-month">
            <Text fw={700} mb={6}>{formatMonth(month)}</Text>
            <div className="rota-list">
              {inMonth.map(day => (
                <div
                  key={day.date}
                  className={`rota-row ${dayClasses(day, highlightPersonId, !!onDayClick)}`}
                  role={onDayClick ? 'button' : undefined}
                  tabIndex={onDayClick ? 0 : undefined}
                  onClick={() => onDayClick?.(day)}
                  onKeyDown={e => { if (onDayClick && (e.key === 'Enter' || e.key === ' ')) onDayClick(day) }}
                >
                  <div className="rota-row-date">
                    <span className="rota-row-dow">{DOW[(dayOfWeek(day.date) + 6) % 7]}</span>
                    <span className="rota-row-num">{dayOfMonth(day.date)}</span>
                  </div>
                  <div className="rota-row-name">
                    {day.personName ?? 'Unassigned'}
                    {day.holidayName && <Text size="xs" c="green.8" fw={600} truncate>{day.holidayName}</Text>}
                  </div>
                  {day.holidayName && <Badge size="xs" color="green" variant="light">PH</Badge>}
                </div>
              ))}
            </div>
          </section>
        )
      })}
    </div>
  )
}
