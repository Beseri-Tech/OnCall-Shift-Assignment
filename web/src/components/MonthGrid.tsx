import { Tooltip } from '@mantine/core'
import type { IsoDate } from '../api'
import { addDays, dayOfMonth, dayOfWeek, endOfMonth, formatMonth, isWeekend } from '../dates'

const DOW = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']

export interface DayState {
  leave?: boolean
  note?: string | null
  preferred?: boolean
  /** Confirmed leave (entered by an admin): shown locked, never toggled. The string is its reason. */
  confirmed?: string | null
}

interface Props {
  month: IsoDate                       // first day of the month
  min: IsoDate                         // days outside [min, max] are disabled
  max: IsoDate
  holidays: Map<IsoDate, string>
  peaks?: Map<IsoDate, string>          // busy weekdays (e.g. eve of Raya)
  full?: Set<IsoDate>                   // days where the leave cap is reached
  blocked?: { days: Set<IsoDate>; reason: string | null; reasons?: Map<IsoDate, string | null> }   // days the admin says this officer can never be on call
  today: IsoDate
  readOnly?: boolean
  stateOf: (date: IsoDate) => DayState
  onDayDown?: (date: IsoDate) => void
  onDayEnter?: (date: IsoDate) => void
}

/** One month as a Monday-first grid; the parent owns selection and drag painting. */
export function MonthGrid({ month, min, max, holidays, peaks, full, blocked, today, readOnly, stateOf, onDayDown, onDayEnter }: Props) {
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
          const peak = !holiday && !isWeekend(date) ? peaks?.get(date) : undefined
          const confirmed = s.confirmed !== undefined
          const isFull = !disabled && !s.leave && !confirmed && full?.has(date)
          const isBlocked = !disabled && !!blocked?.days.has(date)
          const locked = disabled || readOnly || confirmed || isBlocked
          const cls = [
            'day',
            isWeekend(date) && 'day-weekend',
            holiday && 'day-holiday',
            peak && 'day-peak',
            isFull && 'day-full',
            s.leave && 'day-leave',
            confirmed && 'day-confirmed',
            isBlocked && 'day-blocked',
            s.preferred && 'day-preferred',
            date === today && 'day-today',
            disabled && 'day-disabled',
            readOnly && 'day-readonly',
          ].filter(Boolean).join(' ')

          const label = [
            holiday, peak && `Peak day: ${peak}`, isFull ? 'Full – too many officers are already off' : null,
            s.leave ? `Leave${s.note ? `: ${s.note}` : ''}` : null, s.preferred ? 'Preferred on-call' : null,
            isBlocked ? `Can't be on call on this day${(blocked?.reasons?.has(date) ? blocked.reasons.get(date) : blocked?.reason) ? `: ${blocked?.reasons?.has(date) ? blocked.reasons.get(date) : blocked?.reason}` : ''}` : null,
            confirmed ? `Confirmed leave${s.confirmed ? `: ${s.confirmed}` : ''} – only the admin can change it` : null,
          ].filter(Boolean).join(' · ')

          const cell = (
            <button
              key={date}
              type="button"
              className={cls}
              disabled={disabled}
              aria-pressed={!!(s.leave || s.preferred || confirmed)}
              aria-label={`${date}${label ? ` (${label})` : ''}`}
              onPointerDown={e => {
                if (locked) return
                e.preventDefault()
                ;(e.currentTarget as HTMLElement).releasePointerCapture?.(e.pointerId)
                onDayDown?.(date)
              }}
              onPointerEnter={() => { if (!locked) onDayEnter?.(date) }}
              // Releasing over a day ends the drag there even if its enter event was skipped.
              onPointerUp={() => { if (!locked) onDayEnter?.(date) }}
            >
              <span className="day-num">{dayOfMonth(date)}</span>
              {s.leave && s.note && <span className="day-note">{s.note}</span>}
              {confirmed && <span className="day-note">Confirmed</span>}
              {isBlocked && !confirmed && <span className="day-note">No duty</span>}
            </button>
          )

          return label ? <Tooltip key={date} label={label} openDelay={400} withArrow>{cell}</Tooltip> : cell
        })}
      </div>
    </div>
  )
}
