// Live feedback for the leave limits; mirrors api/Rota.Core/Leave/LeavePolicy.cs (the server has the final say).

import type { IsoDate, LeaveRules } from './api'
import { isWeekend } from './dates'

export type DayKind = 'weekday' | 'weekend' | 'holiday' | 'peak'

/** A public holiday on a weekend is a holiday; peak days only matter on weekdays. */
export const dayKind = (date: IsoDate, holidays: Map<IsoDate, string>, peaks: Map<IsoDate, string>): DayKind =>
  holidays.has(date) ? 'holiday' : isWeekend(date) ? 'weekend' : peaks.has(date) ? 'peak' : 'weekday'

export const dayCost = (kind: DayKind, r: LeaveRules) =>
  kind === 'weekend' ? r.weekendCost : kind === 'holiday' ? r.holidayCost : kind === 'peak' ? r.peakCost : 0

export const dayCap = (kind: DayKind, r: LeaveRules) => (kind === 'weekday' ? r.weekdayCap : r.busyDayCap)

export function usage(dates: Iterable<IsoDate>, r: LeaveRules, kindOf: (d: IsoDate) => DayKind) {
  let points = 0
  let weekdays = 0
  for (const d of dates) {
    const kind = kindOf(d)
    points += dayCost(kind, r)
    if (kind === 'weekday') weekdays++
  }
  return { points, weekdays }
}
