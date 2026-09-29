import { Tooltip } from '@mantine/core'
import type { IsoDate } from '../api'
import { addDays, dayOfMonth, dayOfWeek, endOfMonth, formatMonth, isWeekend } from '../dates'

const DOW = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']

export interface DayState {
  leave?: boolean
  note?: string | null
  preferred?: boolean
}

interface Props {
  month: IsoDate                       // first day of the month
  min: IsoDate                         // days outside [min, max] are disabled
  max: IsoDate
  holidays: Map<IsoDate, string>
  today: IsoDate
  readOnly?: boolean
  stateOf: (date: IsoDate) => DayState
  onDayDown?: (date: IsoDate) => void
  onDayEnter?: (date: IsoDate) => void
}

/** One month as a Monday-first grid; the parent owns selection and drag painting. */
export function MonthGrid({ month, min, max, holidays, today, readOnly, stateOf, onDayDown, onDayEnter }: Props) {
  const last = endOfMonth(month)
  const leadingBlanks = (dayOfWeek(month) + 6) % 7   // Monday = 0

  const days: IsoDate[] = []
  for (let d = month; d <= last; d = addDays(d, 1)) days.push(d)

  return (
    <div className="month">
      <div className="month-title">{formatMonth(month)}</div>
      <div className="month-grid">
        {DOW.map(d => <div key={d} className="dow">{d}</div>)}
        {Array.from({ length: leadingBlanks }, (_, i) => <div key={`b${i}`} />)}
        {days.map(date => {
          const disabled = date < min || date > max
          const s = disabled ? {} : stateOf(date)
          const holiday = holidays.get(date)
          const cls = [
            'day',
            isWeekend(date) && 'day-weekend',
            holiday && 'day-holiday',
            s.leave && 'day-leave',
            s.preferred && 'day-preferred',
            date === today && 'day-today',
            disabled && 'day-disabled',
            readOnly && 'day-readonly',
          ].filter(Boolean).join(' ')

          const label = [holiday, s.leave ? `Leave${s.note ? `: ${s.note}` : ''}` : null, s.preferred ? 'Preferred on-call' : null]
            .filter(Boolean).join(' · ')

          const cell = (
            <button
              key={date}
              type="button"
              className={cls}
              disabled={disabled}
              aria-pressed={!!(s.leave || s.preferred)}
              aria-label={`${date}${label ? ` (${label})` : ''}`}
              onPointerDown={e => {
                if (disabled || readOnly) return
                e.preventDefault()
                ;(e.currentTarget as HTMLElement).releasePointerCapture?.(e.pointerId)
                onDayDown?.(date)
              }}
              onPointerEnter={() => { if (!disabled && !readOnly) onDayEnter?.(date) }}
              // Releasing over a day ends the drag there even if its enter event was skipped.
              onPointerUp={() => { if (!disabled && !readOnly) onDayEnter?.(date) }}
            >
              <span className="day-num">{dayOfMonth(date)}</span>
              {s.leave && s.note && <span className="day-note">{s.note}</span>}
            </button>
          )

          return label ? <Tooltip key={date} label={label} openDelay={400} withArrow>{cell}</Tooltip> : cell
        })}
      </div>
    </div>
  )
}
