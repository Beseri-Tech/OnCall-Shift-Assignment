// Typed client for Rota.Api. Types mirror api/Rota.Api/Contracts.cs; dates are "YYYY-MM-DD".

export type IsoDate = string

export type PeriodStatus = 'Open' | 'Locked' | 'Review' | 'Published'
export type SwapStatus = 'Pending' | 'Accepted' | 'Declined' | 'Cancelled' | 'Expired'
export type OfficerStatus = 'OnCall' | 'Excluded' | 'Left'
export type AccountRole = 'Officer' | 'Admin' | 'Supervisor'

export interface Totals { total: number; weekday: number; weekendHoliday: number }
export interface Period {
  id: string; name: string; startDate: IsoDate; endDate: IsoDate; leaveDeadline: IsoDate | null
  status: PeriodStatus; isEditable: boolean; pointsBudget: number | null; swapDeadline: IsoDate | null
}
export interface Holiday { date: IsoDate; name: string }
/** A public holiday or peak day applied to one rota period. */
export type PeriodDayKind = 'Holiday' | 'Peak'
export interface PeriodDay { date: IsoDate; kind: PeriodDayKind; name: string }
export interface LeaveEntry { date: IsoDate; note: string | null }
export interface RotaDay {
  date: IsoDate; isWeekendHoliday: boolean; holidayName: string | null
  personId: string | null; personName: string | null; isManual: boolean
}

export interface PersonSummary { id: string; code: string; name: string }
export interface PersonProfile { id: string; code: string; name: string; totals: Totals; totalsKnown: boolean }
/** inReview: from a rota still in review (can change before it's published). */
export interface Shift {
  date: IsoDate; isWeekendHoliday: boolean; holidayName: string | null; periodId: string; periodName: string
  inReview: boolean; runId: string
}
export interface Swap {
  id: string; runId: string; periodName: string; fromPersonId: string; fromName: string; fromDate: IsoDate
  toPersonId: string; toName: string; toDate: IsoDate; note: string | null; status: SwapStatus; createdAt: string
  warnings: string[]
}
export interface AppNotification { id: number; kind: string; title: string; body: string | null; link: string | null; createdAt: string; read: boolean }
export interface Notifications { items: AppNotification[]; unread: number }
export interface Entries { leave: LeaveEntry[]; preferred: IsoDate[] }
export interface ParseResponse { dates: IsoDate[]; error: string | null }
export interface OverviewDay { date: IsoDate; isWeekendHoliday: boolean; holidayName: string | null; available: number }
export interface OverviewPerson { id: string; code: string; name: string; leave: LeaveEntry[]; preferred: IsoDate[] }
export interface Overview { period: Period; days: OverviewDay[]; people: OverviewPerson[] }
export interface PublishedRota { period: Period; runId: string; days: RotaDay[]; inReview: boolean }

export interface AdminPerson {
  id: string; code: string; name: string; status: OfficerStatus; statusReason: string | null; excludedUntil: IsoDate | null
  clinicId: string | null; clinicName: string | null; districtId: string | null; district: string | null
  stateId: string | null; state: string | null; phone: string | null
  sortOrder: number; extraShift: boolean; preferWeekendHoliday: boolean; weekendWeight: number
  /** Sign-in email and role, once the officer has an account. invitePending: hasn't signed in and set a password yet. */
  email: string | null; role: AccountRole | null; invitePending: boolean; accountEnabled: boolean
}
/** email invites the officer in the same step (only while they have no account). */
export interface UpsertPerson {
  name: string; code: string | null; status: OfficerStatus; extraShift: boolean; preferWeekendHoliday: boolean
  weekendWeight: number; statusReason: string | null; excludedUntil: IsoDate | null; clinicId: string | null; phone: string | null
  email: string | null; role: AccountRole
}
/** invite is set when an email was given. */
export interface SavePersonResult { id: string; invite: InviteResult | null }
export interface Me {
  accountId: string; email: string; role: AccountRole; personId: string | null; personName: string | null
  mustChangePassword: boolean
}
export interface Account {
  id: string; email: string; role: AccountRole; personId: string | null; personName: string | null; enabled: boolean
  mustChangePassword: boolean; tempPasswordExpiresAt: string | null; lastLoginAt: string | null
}
/** tempPassword only comes back when the email could not be sent. */
export interface InviteResult { accountId: string; emailSent: boolean; tempPassword: string | null }
export interface TallyRow {
  personId: string; code: string; name: string; status: OfficerStatus; clinicName: string | null; district: string | null
  weekday: number; weekendHoliday: number; total: number; publishedWeekday: number; publishedWeekendHoliday: number
  known: boolean
}
// State -> district -> clinic -> officer.
export interface District { id: string; name: string; clinics: number }
export interface State { id: string; name: string; districts: District[] }
export interface Clinic { id: string; name: string; districtId: string; district: string; state: string; people: number }
export interface UpsertClinic { name: string; districtId: string }
export interface ImportRow {
  line: number; name: string; code: string | null; email: string | null; role: AccountRole | null; phone: string | null
  state: string | null; district: string | null; clinic: string | null; errors: string[]
}
export interface IssuedPassword { name: string; email: string; tempPassword: string }
/** committed: false = only checked (or something was wrong); nothing is saved unless every row is valid. */
export interface ImportResult {
  rows: ImportRow[]; fileErrors: string[]; committed: boolean; added: number; invited: number; passwords: IssuedPassword[]
}
export interface UpsertPeriod {
  name: string; startDate: IsoDate; endDate: IsoDate; leaveDeadline: IsoDate | null; pointsBudget: number | null
}
/** Leave limits for one officer in one period; budget excludes the admin's extra points. */
export interface LeaveRules {
  budget: number; extra: number; extraReason: string | null; weekdayAllowance: number
  busyDayCap: number; weekdayCap: number; onCall: number
  weekendCost: number; holidayCost: number; peakCost: number
  peaks: Holiday[]; othersOff: Record<IsoDate, number>
}
export interface PointsRow {
  personId: string; code: string; name: string; pointsUsed: number; budget: number; extra: number; reason: string | null
  weekdaysUsed: number; weekdayAllowance: number
}

export interface Run {
  id: string; periodId: string; createdAt: string; seed: number; isPublished: boolean; isInReview: boolean
  warnings: string[]; unassigned: number; consecutivePairs: number; manualChanges: number
}
export interface RunStat {
  personId: string; code: string; name: string; total: number; weekday: number; weekendHoliday: number
  leaveDays: number; priorWeekendHoliday: number | null
}
export interface RunDetail { run: Run; days: RotaDay[]; stats: RunStat[] }
export interface OverrideResponse { day: RotaDay; warnings: string[] }

/** One audit-log entry. detail, before and after are whatever the change was logged with (before is null for something
 * new, after for something deleted). */
export interface AuditEntry {
  id: number; at: string; accountId: string | null; actorEmail: string | null; actorName: string | null; action: string
  entity: string | null; entityId: string | null; personId: string | null; personName: string | null
  detail: unknown; before: unknown; after: unknown; ip: string | null; userAgent: string | null
}
export interface AuditPage { items: AuditEntry[]; total: number; page: number; pageSize: number }
export interface AuditFilters { actors: { accountId: string; email: string; name: string | null }[]; actions: string[]; entities: string[] }
export interface AuditQuery {
  from?: IsoDate; to?: IsoDate; accountId?: string; action?: string; entity?: string; personId?: string; page?: number
  pageSize?: number
}

/** Error carrying the API's problem-details message. */
export class ApiError extends Error {
  readonly status: number
  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

/** Where the API lives: empty when it serves this app itself (and in dev, via the Vite proxy); set VITE_API_URL at build
 * time when the app is hosted separately, e.g. on Cloudflare Pages. */
const API = (import.meta.env.VITE_API_URL ?? '').replace(/\/+$/, '')

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const isForm = body instanceof FormData
  const res = await fetch(API + url, {
    method,
    credentials: 'include',
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
  shifts: (personId: string) => get<Shift[]>(`/api/people/${personId}/shifts`),
  periods: (status?: PeriodStatus) => get<Period[]>(`/api/periods${q({ status })}`),
  holidays: (from?: IsoDate, to?: IsoDate) => get<Holiday[]>(`/api/holidays${q({ from, to })}`),
  entries: (personId: string, periodId: string) => get<Entries>(`/api/people/${personId}/entries${q({ periodId })}`),
  saveEntries: (personId: string, periodId: string, body: Entries) =>
    put<Entries>(`/api/people/${personId}/entries${q({ periodId })}`, body),
  parse: (text: string, reference: IsoDate) => post<ParseResponse>('/api/leave/parse', { text, reference }),
  overview: (periodId: string) => get<Overview>(`/api/periods/${periodId}/overview`),
  rota: (periodId: string) => get<PublishedRota>(`/api/periods/${periodId}/rota`),
  swaps: () => get<Swap[]>('/api/swaps'),
  requestSwap: (runId: string, fromDate: IsoDate, toDate: IsoDate, note: string | null) =>
    post<Swap>('/api/swaps', { runId, fromDate, toDate, note }),
  acceptSwap: (id: string) => post<void>(`/api/swaps/${id}/accept`),
  declineSwap: (id: string) => post<void>(`/api/swaps/${id}/decline`),
  cancelSwap: (id: string) => post<void>(`/api/swaps/${id}/cancel`),
  notifications: () => get<Notifications>('/api/notifications'),
  readNotification: (id: number) => post<void>(`/api/notifications/${id}/read`),
  readAllNotifications: () => post<void>('/api/notifications/read-all'),
  leaveRules: (periodId: string, personId: string) => get<LeaveRules>(`/api/periods/${periodId}/leave-rules${q({ personId })}`),

  auth: {
    me: () => get<Me>('/api/auth/me'),
    login: (email: string, password: string) => post<Me>('/api/auth/login', { email, password }),
    logout: () => post<void>('/api/auth/logout'),
    changePassword: (currentPassword: string, newPassword: string) =>
      post<Me>('/api/auth/change-password', { currentPassword, newPassword }),
    forgot: (email: string) => post<void>('/api/auth/forgot', { email }),
    reset: (token: string, newPassword: string) => post<void>('/api/auth/reset', { token, newPassword }),
  },

  admin: {
    accounts: () => get<Account[]>('/api/admin/accounts'),
    invite: (email: string, role: AccountRole, personId: string | null) =>
      post<InviteResult>('/api/admin/accounts', { email, role, personId }),
    resendInvite: (id: string) => post<InviteResult>(`/api/admin/accounts/${id}/resend`),
    updateAccount: (id: string, a: { email: string; role: AccountRole; personId: string | null; enabled: boolean }) =>
      put<void>(`/api/admin/accounts/${id}`, a),

    tally: () => get<TallyRow[]>('/api/admin/tally'),
    saveTally: (rows: { personId: string; weekday: number; weekendHoliday: number }[]) => put<TallyRow[]>('/api/admin/tally', rows),

    people: () => get<AdminPerson[]>('/api/admin/people'),
    createPerson: (p: UpsertPerson) => post<SavePersonResult>('/api/admin/people', p),
    updatePerson: (id: string, p: UpsertPerson) => put<SavePersonResult>(`/api/admin/people/${id}`, p),
    importPeople: (csv: string, commit: boolean) => post<ImportResult>('/api/admin/people/import', { csv, commit }),
    deletePerson: (id: string) => del<void>(`/api/admin/people/${id}`),
    reorder: (ids: string[]) => post<void>('/api/admin/people/reorder', { ids }),

    states: () => get<State[]>('/api/admin/states'),
    createState: (name: string) => post<string>('/api/admin/states', { name }),
    updateState: (id: string, name: string) => put<void>(`/api/admin/states/${id}`, { name }),
    deleteState: (id: string) => del<void>(`/api/admin/states/${id}`),
    createDistrict: (name: string, stateId: string) => post<string>('/api/admin/districts', { name, stateId }),
    updateDistrict: (id: string, name: string, stateId: string) => put<void>(`/api/admin/districts/${id}`, { name, stateId }),
    deleteDistrict: (id: string) => del<void>(`/api/admin/districts/${id}`),

    clinics: () => get<Clinic[]>('/api/admin/clinics'),
    createClinic: (c: UpsertClinic) => post<string>('/api/admin/clinics', c),
    updateClinic: (id: string, c: UpsertClinic) => put<void>(`/api/admin/clinics/${id}`, c),
    deleteClinic: (id: string) => del<void>(`/api/admin/clinics/${id}`),

    periods: () => get<Period[]>('/api/admin/periods'),
    createPeriod: (p: UpsertPeriod) => post<Period>('/api/admin/periods', p),
    updatePeriod: (id: string, p: UpsertPeriod) => put<Period>(`/api/admin/periods/${id}`, p),
    deletePeriod: (id: string) => del<void>(`/api/admin/periods/${id}`),
    lock: (id: string) => post<Period>(`/api/admin/periods/${id}/lock`),
    unlock: (id: string) => post<Period>(`/api/admin/periods/${id}/unlock`),
    points: (id: string) => get<PointsRow[]>(`/api/admin/periods/${id}/points`),
    grantPoints: (id: string, personId: string, points: number, reason: string | null) =>
      put<void>(`/api/admin/periods/${id}/points/${personId}`, { points, reason }),
    periodDays: (id: string) => get<PeriodDay[]>(`/api/admin/periods/${id}/days`),
    upsertPeriodDay: (id: string, d: PeriodDay) => put<void>(`/api/admin/periods/${id}/days`, d),
    deletePeriodDay: (id: string, d: PeriodDay) => del<void>(`/api/admin/periods/${id}/days/${d.kind}/${d.date}`),
    copyMasterDays: (id: string) => post<{ added: number }>(`/api/admin/periods/${id}/days/copy-master`),

    peakDays: () => get<Holiday[]>('/api/admin/peak-days'),
    upsertPeakDay: (h: Holiday) => put<void>('/api/admin/peak-days', h),
    deletePeakDay: (date: IsoDate) => del<void>(`/api/admin/peak-days/${date}`),

    holidays: () => get<Holiday[]>('/api/admin/holidays'),
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
    sendForReview: (runId: string, swapDeadline: IsoDate | null) => post<void>(`/api/admin/runs/${runId}/review`, { swapDeadline }),
    withdrawReview: (runId: string) => post<void>(`/api/admin/runs/${runId}/withdraw-review`),
    unpublish: (runId: string) => post<void>(`/api/admin/runs/${runId}/unpublish`),
    deleteRun: (runId: string) => del<void>(`/api/admin/runs/${runId}`),

    audit: ({ page, pageSize, ...f }: AuditQuery) =>
      get<AuditPage>(`/api/admin/audit${q({ ...f, page: page?.toString(), pageSize: pageSize?.toString() })}`),
    auditFilters: () => get<AuditFilters>('/api/admin/audit/filters'),

    exports: {
      timetable: (runId: string) => `${API}/api/admin/runs/${runId}/timetable.xlsx`,
      tally: () => `${API}/api/admin/tally/export.xlsx`,
      leave: (periodId: string) => `${API}/api/admin/periods/${periodId}/leave.xlsx`,
    },
  },
}
