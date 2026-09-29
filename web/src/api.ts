// Typed client for Rota.Api. Types mirror api/Rota.Api/Contracts.cs; dates are "YYYY-MM-DD".

export type IsoDate = string

export type PeriodStatus = 'Open' | 'Locked' | 'Published'
export type NameMatchType = 'None' | 'Fuzzy' | 'Exact'
export type ImportAction = 'Match' | 'New' | 'Skip'

export interface Totals { total: number; weekday: number; weekendHoliday: number }
export interface Period {
  id: string; name: string; startDate: IsoDate; endDate: IsoDate; leaveDeadline: IsoDate | null
  status: PeriodStatus; isEditable: boolean
}
export interface Holiday { date: IsoDate; name: string }
export interface LeaveEntry { date: IsoDate; note: string | null }
export interface RotaDay {
  date: IsoDate; isWeekendHoliday: boolean; holidayName: string | null
  personId: string | null; personName: string | null; isManual: boolean
}

export interface PersonSummary { id: string; code: string; name: string }
export interface PersonProfile { id: string; code: string; name: string; totals: Totals; totalsKnown: boolean }
export interface Entries { leave: LeaveEntry[]; preferred: IsoDate[] }
export interface ParseResponse { dates: IsoDate[]; error: string | null }
export interface OverviewDay { date: IsoDate; isWeekendHoliday: boolean; holidayName: string | null; available: number }
export interface OverviewPerson { id: string; code: string; name: string; leave: LeaveEntry[]; preferred: IsoDate[] }
export interface Overview { period: Period; days: OverviewDay[]; people: OverviewPerson[] }
export interface PublishedRota { period: Period; runId: string; days: RotaDay[] }

export interface AdminPerson {
  id: string; code: string; name: string; active: boolean; sortOrder: number
  extraShift: boolean; preferWeekendHoliday: boolean; weekendWeight: number
  openingTotal: number | null; openingWeekday: number | null; openingWeekendHoliday: number | null
  totals: Totals; totalsKnown: boolean
}
export interface UpsertPerson {
  name: string; code: string | null; active: boolean; extraShift: boolean; preferWeekendHoliday: boolean
  weekendWeight: number; openingTotal: number | null; openingWeekday: number | null; openingWeekendHoliday: number | null
}
export interface UpsertPeriod { name: string; startDate: IsoDate; endDate: IsoDate; leaveDeadline: IsoDate | null }

export interface Run {
  id: string; periodId: string; createdAt: string; seed: number; isPublished: boolean
  warnings: string[]; unassigned: number; consecutivePairs: number; manualChanges: number
}
export interface RunStat {
  personId: string; code: string; name: string; total: number; weekday: number; weekendHoliday: number
  leaveDays: number; priorWeekendHoliday: number | null
}
export interface RunDetail { run: Run; days: RotaDay[]; stats: RunStat[] }
export interface OverrideResponse { day: RotaDay; warnings: string[] }

export interface LeaveImportRow {
  rawName: string; personId: string | null; personName: string | null; matchType: NameMatchType
  leave: LeaveEntry[]; warnings: string[]
}
export interface LeaveImportPreview { months: IsoDate[]; rows: LeaveImportRow[] }
export interface LeaveImportCommitRow { rawName: string; action: ImportAction; personId: string | null; leave: LeaveEntry[] }
export interface ImportResult { updated: number; added: number; skipped: number; warnings: string[] }
export interface OpeningImportRow {
  rawName: string; value: number | null; personId: string | null; personName: string | null; matchType: NameMatchType
}
export interface OpeningImportPreview { rows: OpeningImportRow[]; peopleWithoutRow: string[] }

/** Error carrying the API's problem-details message. */
export class ApiError extends Error {
  readonly status: number
  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const isForm = body instanceof FormData
  const res = await fetch(url, {
    method,
    credentials: 'same-origin',
    headers: body === undefined || isForm ? undefined : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : isForm ? body : JSON.stringify(body),
  })

  if (!res.ok) throw new ApiError(res.status, await problemMessage(res))
  if (res.status === 204 || res.status === 201 && res.headers.get('content-length') === '0') return undefined as T
  const text = await res.text()
  return (text ? JSON.parse(text) : undefined) as T
}

async function problemMessage(res: Response): Promise<string> {
  try {
    const p = await res.json()
    if (p.errors) return Object.values(p.errors as Record<string, string[]>).flat().join(' ')
    return p.detail ?? p.title ?? res.statusText
  } catch {
    return res.status === 401 ? 'Please log in again.' : res.statusText || `Request failed (${res.status})`
  }
}

const get = <T,>(url: string) => request<T>('GET', url)
const post = <T,>(url: string, body?: unknown) => request<T>('POST', url, body ?? {})
const put = <T,>(url: string, body: unknown) => request<T>('PUT', url, body)
const del = <T,>(url: string) => request<T>('DELETE', url)

const q = (params: Record<string, string | undefined>) => {
  const s = new URLSearchParams(Object.entries(params).filter((e): e is [string, string] => e[1] !== undefined)).toString()
  return s ? `?${s}` : ''
}

export const api = {
  people: () => get<PersonSummary[]>('/api/people'),
  person: (id: string) => get<PersonProfile>(`/api/people/${id}`),
  periods: (status?: PeriodStatus) => get<Period[]>(`/api/periods${q({ status })}`),
  holidays: (from?: IsoDate, to?: IsoDate) => get<Holiday[]>(`/api/holidays${q({ from, to })}`),
  entries: (personId: string, periodId: string) => get<Entries>(`/api/people/${personId}/entries${q({ periodId })}`),
  saveEntries: (personId: string, periodId: string, body: Entries) =>
    put<Entries>(`/api/people/${personId}/entries${q({ periodId })}`, body),
  parse: (text: string, reference: IsoDate) => post<ParseResponse>('/api/leave/parse', { text, reference }),
  overview: (periodId: string) => get<Overview>(`/api/periods/${periodId}/overview`),
  rota: (periodId: string) => get<PublishedRota>(`/api/periods/${periodId}/rota`),

  admin: {
    login: (password: string) => post<void>('/api/admin/login', { password }),
    logout: () => post<void>('/api/admin/logout'),
    me: () => get<{ admin: boolean }>('/api/admin/me'),

    people: () => get<AdminPerson[]>('/api/admin/people'),
    createPerson: (p: UpsertPerson) => post<string>('/api/admin/people', p),
    bulkAdd: (names: string) => post<{ added: string[]; skipped: string[] }>('/api/admin/people/bulk', { names }),
    updatePerson: (id: string, p: UpsertPerson) => put<void>(`/api/admin/people/${id}`, p),
    deletePerson: (id: string) => del<void>(`/api/admin/people/${id}`),
    reorder: (ids: string[]) => post<void>('/api/admin/people/reorder', { ids }),

    periods: () => get<Period[]>('/api/admin/periods'),
    createPeriod: (p: UpsertPeriod) => post<Period>('/api/admin/periods', p),
    updatePeriod: (id: string, p: UpsertPeriod) => put<Period>(`/api/admin/periods/${id}`, p),
    deletePeriod: (id: string) => del<void>(`/api/admin/periods/${id}`),
    lock: (id: string) => post<Period>(`/api/admin/periods/${id}/lock`),
    unlock: (id: string) => post<Period>(`/api/admin/periods/${id}/unlock`),

    upsertHoliday: (h: Holiday) => put<void>('/api/admin/holidays', h),
    bulkHolidays: (text: string, reference: IsoDate, name: string) =>
      post<{ added: number }>('/api/admin/holidays/bulk', { text, reference, name }),
    deleteHoliday: (date: IsoDate) => del<void>(`/api/admin/holidays/${date}`),

    generate: (periodId: string, seed?: number) => post<RunDetail>(`/api/admin/periods/${periodId}/generate`, { seed: seed ?? null }),
    runs: (periodId: string) => get<Run[]>(`/api/admin/periods/${periodId}/runs`),
    run: (id: string) => get<RunDetail>(`/api/admin/runs/${id}`),
    override: (runId: string, date: IsoDate, personId: string | null) =>
      put<OverrideResponse>(`/api/admin/runs/${runId}/assignments/${date}`, { personId }),
    publish: (runId: string) => post<void>(`/api/admin/runs/${runId}/publish`),
    unpublish: (runId: string) => post<void>(`/api/admin/runs/${runId}/unpublish`),
    deleteRun: (runId: string) => del<void>(`/api/admin/runs/${runId}`),

    importLeave: (periodId: string, file: File) => {
      const form = new FormData()
      form.append('file', file)
      return request<LeaveImportPreview>('POST', `/api/admin/periods/${periodId}/import-leave`, form)
    },
    commitLeave: (periodId: string, rows: LeaveImportCommitRow[]) =>
      post<ImportResult>(`/api/admin/periods/${periodId}/import-leave/commit`, { rows }),
    importOpening: (file: File) => {
      const form = new FormData()
      form.append('file', file)
      return request<OpeningImportPreview>('POST', '/api/admin/people/import-opening', form)
    },
    commitOpening: (rows: { personId: string; openingWeekendHoliday: number | null }[]) =>
      post<ImportResult>('/api/admin/people/import-opening/commit', { rows }),

    exports: {
      timetable: (runId: string) => `/api/admin/runs/${runId}/timetable.xlsx`,
      totals: () => '/api/admin/people/totals.xlsx',
      leave: (periodId: string) => `/api/admin/periods/${periodId}/leave.xlsx`,
    },
  },
}
