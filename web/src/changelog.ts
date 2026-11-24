// Release notes shown on the Changelog page, newest first.
//
// Versioning (semantic, MAJOR.MINOR.PATCH), one version for the whole app, kept in web/package.json:
//   MAJOR  a change that alters how the rota works or needs everyone to relearn something
//   MINOR  a new feature or visible improvement
//   PATCH  a fix with no new behaviour
// To ship a release: bump "version" in web/package.json and add an entry here at the top with the same number.
// 1.x was the original desktop tool; 2.0.0 is the web app.

export type Change = { kind: 'new' | 'improved' | 'fixed'; text: string }
export type Release = { version: string; date: string; title: string; changes: Change[] }

export const CHANGELOG: Release[] = [
  {
    version: '3.0.0', date: '2026-11-24', title: 'More hospitals',
    changes: [
      { kind: 'new', text: 'The app can now run the on-call rota for more than one hospital. Each hospital has its own officers, rota periods, leave, swaps, reports and audit log, and nobody sees another hospital’s data. Everything so far belongs to Hospital Tuanku Fauziah (HTF).' },
      { kind: 'new', text: 'Public holidays and peak days are now kept per state, so each hospital gets its own state’s holidays.' },
      { kind: 'new', text: 'A new Master role looks after the whole app: masters add hospitals, states and districts, switch between hospitals from the header, and can do everything a supervisor can in any hospital.' },
      { kind: 'improved', text: 'When sending feedback you can choose who reads it: your hospital’s admins, or the app team.' },
      { kind: 'improved', text: 'The header shows which hospital you are in.' },
      { kind: 'improved', text: 'Everyone is signed out once with this update. Sign in again with your usual email and password.' },
    ],
  },
  {
    version: '2.8.5', date: '2026-11-18', title: 'Lighter server',
    changes: [
      { kind: 'improved', text: 'The server now runs on a smaller, leaner base system, so it starts faster and has less that needs security patching. Nothing changes in how the app looks or works.' },
    ],
  },
  {
    version: '2.8.4', date: '2026-11-17', title: 'Behind-the-scenes tuning',
    changes: [
      { kind: 'improved', text: 'Tidied how the server reads data so it stays quick as more officers and clinics are added. Nothing changes in how the app looks or works.' },
    ],
  },
  {
    version: '2.8.3', date: '2026-11-17', title: 'Security updates',
    changes: [
      { kind: 'fixed', text: 'Updated two build tools (Vite and source-map-js) to versions with security fixes. Nothing changes in how the app looks or works.' },
    ],
  },
  {
    version: '2.8.2', date: '2026-11-17', title: 'Saving rota reports',
    changes: [
      { kind: 'fixed', text: 'Saving a rota report to private storage no longer fails with a server error. If saving ever fails again, the Reports tab now says why.' },
    ],
  },
  {
    version: '2.8.1', date: '2026-11-16', title: 'Footer stays at the bottom',
    changes: [
      { kind: 'fixed', text: 'On short pages the footer (version, What\'s new, Developed by Beseri-Tech) now stays at the bottom of the screen instead of floating right under the content. On long pages it still follows the content.' },
    ],
  },
  {
    version: '2.8.0', date: '2026-11-16', title: 'Published rota report',
    changes: [
      { kind: 'new', text: 'Admins can download a PDF report of a published rota from the Rota tab ("Report (PDF)"). It has a cover page, the final assignments, why each officer is on each day, every swap, admin overrides with who and when, officer totals and leave points, and the audit trail.' },
      { kind: 'new', text: 'A report is saved to private storage every time a rota is published. The new Reports tab in Admin lists all saved reports so you can download earlier ones, and lets you save a report for a rota that was published before this update.' },
    ],
  },
  {
    version: '2.7.0', date: '2026-11-16', title: 'Shorter generator notes',
    changes: [
      { kind: 'improved', text: 'When an admin reviews a generated rota, the generator notes are collapsed to a short summary so the rota is visible straight away. Press "Show all" to read every note.' },
    ],
  },
  {
    version: '2.6.1', date: '2026-11-16', title: 'Rules page update',
    changes: [
      { kind: 'improved', text: 'The Rules page now explains the CL / EX / RW badges on My leave.' },
    ],
  },
  {
    version: '2.6.0', date: '2026-11-16', title: 'New sign-in page',
    changes: [
      { kind: 'improved', text: 'The sign-in page has a fresh split layout with the logo and a short intro; forgot password and choose-password pages match.' },
      { kind: 'improved', text: 'On phones the sign-in page shows a compact logo header above the form.' },
    ],
  },
  {
    version: '2.5.0', date: '2026-11-16', title: 'Versioning and changelog',
    changes: [
      { kind: 'new', text: 'The app now shows its version in the footer and the account menu.' },
      { kind: 'new', text: 'A Changelog page lists what changed in every release.' },
    ],
  },
  {
    version: '2.4.0', date: '2026-11-06', title: 'Leave requests and document storage',
    changes: [
      { kind: 'new', text: 'Officers can request leave and attach supporting documents, stored privately on Cloudflare R2.' },
      { kind: 'new', text: 'Relief duty, and choosing which officers are in each rota period.' },
      { kind: 'new', text: 'The Rules page has a Manual tab, and admins can upload the manual (up to 100 MB).' },
      { kind: 'new', text: 'Leave point costs and rewards show as CL / EX / RW badges, with a legend.' },
      { kind: 'improved', text: 'Daily leave limits are based on the officers actually available, not counting confirmed leave.' },
      { kind: 'improved', text: 'Officers left out of a period are hidden from its overview and the admin leave list, and told why they cannot add leave.' },
      { kind: 'improved', text: 'Officers see confirmed leave added by an admin; removing leave updates the request.' },
      { kind: 'fixed', text: 'Document upload no longer fails with a server error; the manual tab loads when no manual exists yet.' },
    ],
  },
  {
    version: '2.3.0', date: '2026-11-05', title: 'Confirmed leave and clinic events',
    changes: [
      { kind: 'new', text: 'Confirmed leave can have several date ranges, and counts the officer\'s CR (cuti rehat) days.' },
      { kind: 'new', text: 'Clinic event days can be set per clinic.' },
      { kind: 'fixed', text: 'The Officers page no longer goes blank when the clinic event dialog is closed.' },
      { kind: 'fixed', text: 'Stale leave is cleaned up automatically.' },
    ],
  },
  {
    version: '2.2.0', date: '2026-11-04', title: 'Fairer leave points',
    changes: [
      { kind: 'new', text: 'Reward points for busy-day shifts: public holiday +3, weekend +2, peak day +1, used in the next rota.' },
      { kind: 'new', text: 'An admin can block a weekday for one officer.' },
      { kind: 'improved', text: 'Confirmed leave takes 33% of what its days would have cost, rounded up.' },
      { kind: 'improved', text: 'Peak days count in the points budget.' },
      { kind: 'improved', text: 'Tighter leave gate, and a race when locking a period is fixed.' },
    ],
  },
  {
    version: '2.1.0', date: '2026-11-03', title: 'Rules, feedback and phones',
    changes: [
      { kind: 'new', text: 'Rules and guidelines page for officers, with a downloadable user manual.' },
      { kind: 'new', text: 'Anyone signed in can send feedback; admins read and resolve it.' },
      { kind: 'new', text: 'Confirmed leave for approved absences such as overseas trips.' },
      { kind: 'improved', text: 'Phones get a side menu and the rota shows as a daily list, so nothing is wider than the screen.' },
      { kind: 'improved', text: 'Officer CSV import names the row, column and reason of every problem.' },
    ],
  },
  {
    version: '2.0.2', date: '2026-10-02', title: 'Audit trail and period days',
    changes: [
      { kind: 'new', text: 'An Audit log tab shows before and after values for every change.' },
      { kind: 'new', text: 'Holidays and peak days are set per rota period.' },
      { kind: 'fixed', text: 'The rota period end date picker.' },
    ],
  },
  {
    version: '2.0.1', date: '2026-10-01', title: 'Officer hierarchy',
    changes: [
      { kind: 'new', text: 'Officers are organised as state, district and clinic; add and invite in one step, or import a CSV.' },
      { kind: 'improved', text: 'Branded emails with logo header, button and footer; one bad address no longer blocks the queue.' },
    ],
  },
  {
    version: '2.0.0', date: '2026-09-30', title: 'The web app',
    changes: [
      { kind: 'new', text: 'The rota tool becomes a web app with personal accounts, leave entry, overview, tally and notifications.' },
      { kind: 'new', text: 'Rota review with swaps between officers before the rota is published.' },
      { kind: 'new', text: 'Rewritten rota algorithm, leave limits, and an email outbox.' },
      { kind: 'new', text: 'Healthcare teal theme; hosted on Cloudflare Pages with the API in Docker.' },
    ],
  },
]

export const APP_VERSION = __APP_VERSION__
export const APP_COMMIT = __APP_COMMIT__
