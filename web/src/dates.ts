// Date helpers on "YYYY-MM-DD" strings. Everything is a calendar date (no times/time zones),
// so we work with UTC-midnight Date objects internally to avoid DST/local offsets.

import type { IsoDate } from './api'

const MS_PER_DAY = 86_400_000

export const toDate = (iso: IsoDate) => new Date(`${iso}T00:00:00Z`)
export const toIso = (d: Date) => d.toISOString().slice(0, 10)

export const addDays = (iso: IsoDate, n: number) => toIso(new Date(toDate(iso).getTime() + n * MS_PER_DAY))
export const diffDays = (a: IsoDate, b: IsoDate) => Math.round((toDate(b).getTime() - toDate(a).getTime()) / MS_PER_DAY)
export const dayOfWeek = (iso: IsoDate) => toDate(iso).getUTCDay() // 0 = Sunday
// The hospital's weekly rest days (0 = Sunday ... 6 = Saturday): set by the layout from the signed-in user's hospital.
// ponytail: module-level, one hospital per tab; pass it down if a page ever shows two hospitals at once.
let weekendDays: readonly number[] = [0, 6]
export const setWeekendDays = (days: readonly number[] | null | undefined) => { weekendDays = days?.length ? days : [0, 6] }
export const isWeekend = (iso: IsoDate) => weekendDays.includes(dayOfWeek(iso))
export const dayOfMonth = (iso: IsoDate) => Number(iso.slice(8, 10))

export function eachDay(from: IsoDate, to: IsoDate): IsoDate[] {
  const out: IsoDate[] = []
  for (let d = from; d <= to; d = addDays(d, 1)) out.push(d)
  return out
}

/** First day of each month touching [from, to]. */
export function monthsBetween(from: IsoDate, to: IsoDate): IsoDate[] {
  const out: IsoDate[] = []
  let y = Number(from.slice(0, 4))
  let m = Number(from.slice(5, 7))
  for (;;) {
    const first = `${y}-${String(m).padStart(2, '0')}-01`
    if (first > to) break
    out.push(first)
    m++
    if (m > 12) { m = 1; y++ }
  }
  return out
}

export const endOfMonth = (firstOfMonth: IsoDate) => {
  const d = toDate(firstOfMonth)
  return toIso(new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth() + 1, 0)))
}

const fmt = (opts: Intl.DateTimeFormatOptions) => new Intl.DateTimeFormat('en-GB', { timeZone: 'UTC', ...opts })
const longFmt = fmt({ day: 'numeric', month: 'short', year: 'numeric' })
const shortFmt = fmt({ day: 'numeric', month: 'short' })
const weekdayFmt = fmt({ weekday: 'short', day: 'numeric', month: 'short' })
const monthFmt = fmt({ month: 'long', year: 'numeric' })
const dowFmt = fmt({ weekday: 'narrow' })

export const formatLong = (iso: IsoDate) => longFmt.format(toDate(iso))
export const formatShort = (iso: IsoDate) => shortFmt.format(toDate(iso))
export const formatWeekday = (iso: IsoDate) => weekdayFmt.format(toDate(iso))
export const formatMonth = (iso: IsoDate) => monthFmt.format(toDate(iso))
export const formatDow = (iso: IsoDate) => dowFmt.format(toDate(iso))

/** "3–5 Oct, 12 Oct" style summary of dates grouped into consecutive runs. */
export function toRanges(dates: Iterable<IsoDate>): [IsoDate, IsoDate][] {
  const sorted = [...new Set(dates)].sort()
  const ranges: [IsoDate, IsoDate][] = []
  for (const d of sorted) {
    const last = ranges[ranges.length - 1]
    if (last && diffDays(last[1], d) === 1) last[1] = d
    else ranges.push([d, d])
  }
  return ranges
}

export const formatRange = ([a, b]: [IsoDate, IsoDate]) =>
  a === b ? formatShort(a) : `${formatShort(a)} – ${formatShort(b)}`

export function todayIso(): IsoDate {
  // Local calendar date of the user.
  const now = new Date()
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`
}

const momentFmt = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short', year: 'numeric', hour: 'numeric', minute: '2-digit', hour12: true })

/** A moment (ISO instant) in the viewer's own time, e.g. "15 Nov 2026, 8:00 pm". */
export const formatMoment = (iso: string) => momentFmt.format(new Date(iso))

/** True while an ISO instant is still ahead of now. */
export const isFuture = (iso: string | null | undefined) => !!iso && Date.parse(iso) > Date.now()

/** "2d 3h 14m", "3h 05m" or "42s": the time left until an instant (never negative). */
export function formatCountdown(iso: string, now = Date.now()) {
  let s = Math.max(0, Math.floor((Date.parse(iso) - now) / 1000))
  const d = Math.floor(s / 86400); s -= d * 86400
  const h = Math.floor(s / 3600); s -= h * 3600
  const m = Math.floor(s / 60); s -= m * 60
  if (d > 0) return `${d}d ${h}h ${m}m`
  if (h > 0) return `${h}h ${String(m).padStart(2, '0')}m`
  if (m > 0) return `${m}m ${String(s).padStart(2, '0')}s`
  return `${s}s`
}
