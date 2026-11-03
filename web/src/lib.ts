import { useMediaQuery } from '@mantine/hooks'
import { notifications } from '@mantine/notifications'
import type { Period } from './api'

export function notifyError(e: unknown, title = 'Something went wrong') {
  notifications.show({ color: 'red', title, message: e instanceof Error ? e.message : String(e) })
}

export function notifyOk(message: string, title = 'Saved') {
  notifications.show({ color: 'green', title, message })
}

/** Pick a sensible default period: a preferred one, else the latest open/locked, else the latest. */
export function defaultPeriod(periods: Period[] | undefined, prefer: (p: Period) => boolean = p => p.isEditable): Period | undefined {
  const sorted = [...(periods ?? [])].sort((a, b) => b.startDate.localeCompare(a.startDate))
  return sorted.find(prefer) ?? sorted.find(p => p.status !== 'Published') ?? sorted[0]
}

/** Keep an explicit choice if it still exists, otherwise fall back to the default. */
export function pick<T extends { id: string }>(items: T[] | undefined, chosen: string | null, fallback: T | undefined): T | undefined {
  return items?.find(i => i.id === chosen) ?? fallback
}

/** True on phones: narrower than Mantine's sm breakpoint (48em, 768px). Read on first render so layouts don't flash. */
export function usePhone() {
  return useMediaQuery('(max-width: 47.99em)', false, { getInitialValueInEffect: false })
}
