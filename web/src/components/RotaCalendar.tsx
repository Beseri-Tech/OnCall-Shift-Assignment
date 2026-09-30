import { SimpleGrid, Text } from '@mantine/core'
import type { IsoDate, RotaDay } from '../api'
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
  if (days.length === 0) return null
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

                const cls = [
                  'rota-day',
                  day.isWeekendHoliday && 'day-weekend',
                  !day.personId && 'unassigned',
                  highlightPersonId && day.personId === highlightPersonId && 'mine',
                  day.isManual && 'manual',
                  onDayClick && 'clickable',
                ].filter(Boolean).join(' ')

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
