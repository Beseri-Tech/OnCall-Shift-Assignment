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
    version: '3.11.0', date: '2026-11-26', title: 'New welcome page and sign-in',
    changes: [
      { kind: 'new', text: 'Opening the site while signed out now shows a welcome page that explains what Rota-OC does, with a button to sign in.' },
      { kind: 'new', text: 'The welcome page walks through the seven stages of a rota period as you scroll, from the release notice to the published rota.' },
      { kind: 'improved', text: 'The sign-in and password pages have a fresh look, with the Rota-OC tagline beside the form.' },
    ],
  },
  {
    version: '3.10.1', date: '2026-11-26', title: 'ETAP Claim page tidy-up',
    changes: [
      { kind: 'improved', text: 'The page is now called "ETAP Claim" everywhere.' },
      { kind: 'fixed', text: 'Until your ETAP profile is verified, the ETAP Claim page is greyed out and can\'t be used. The notice and its link to your profile stay available.' },
    ],
  },
  {
    version: '3.10.0', date: '2026-11-25', title: 'Scheduled rota release',
    changes: [
      { kind: 'new', text: 'The admin can set a time for a new rota period to open ("Open to officers at"). Officers are told straight away, in the app and by email, when it will open.' },
      { kind: 'new', text: 'Until then officers see a countdown on the Leave page and cannot enter leave. They get a reminder a day and an hour before, and a notice the moment it opens, when the page unlocks by itself.' },
      { kind: 'improved', text: 'The admin can move the opening time or open the period right away; officers are told either way. Admins can still enter leave for officers before it opens.' },
    ],
  },
  {
    version: '3.9.1', date: '2026-11-25', title: 'Database backup upload fix',
    changes: [
      { kind: 'fixed', text: 'The nightly database backup now uploads to Cloudflare R2 correctly.' },
    ],
  },
  {
    version: '3.9.0', date: '2026-11-24', title: 'Nightly database backup',
    changes: [
      { kind: 'new', text: 'The rota database is now backed up every night to a private Cloudflare R2 bucket and kept for 30 days, so nothing is lost if the server fails.' },
    ],
  },
  {
    version: '3.8.1', date: '2026-11-24', title: 'Manual from storage only',
    changes: [
      { kind: 'improved', text: 'The user manual is now only the copy a master uploads under All hospitals → Manual. The old copy built into the app is gone, which makes the app download smaller. Until a manual is uploaded, the Help page shows no manual button.' },
    ],
  },
  {
    version: '3.8.0', date: '2026-11-24', title: 'Help page, reorganised',
    changes: [
      { kind: 'improved', text: 'The Rules page is now called Help and is a directory: chapters for rota and leave, on-call work (cases, WhatsApp passover, ETAP), your account and, for admins, running the rota. Jump to any section from the directory, the side list or the search box.' },
      { kind: 'new', text: 'The Help page now explains the case log, the WhatsApp passover, the ETAP claim and your ETAP profile, and which days count as the weekend in your state.' },
      { kind: 'fixed', text: '"Developed by Beseri-Tech" no longer shows twice on the Help page.' },
    ],
  },
  {
    version: '3.7.0', date: '2026-11-24', title: 'WhatsApp passover',
    changes: [
      { kind: 'new', text: 'After recording a case you are asked whether to generate the WhatsApp passover in the seniors’ template (referral, S/T, S/B, patient, P/W or C/O, Ix ticked, Dx, Mx, plan, Rx), ready to copy into the department group. The WhatsApp button in the case log opens it again for your own cases.' },
      { kind: 'new', text: 'An ED referral asks for the zone (green, yellow or red), shown in the passover as e.g. “N/C from ED GREEN ZONE”.' },
      { kind: 'new', text: 'The case form has Spoken to (S/T), Seen by (S/B), case type (trauma: presented with; non-trauma: complaint of), investigations (Ix) and prescription (Rx). They are in the case log and Excel, not on the ETAP form.' },
      { kind: 'improved', text: 'Department and case type are now required and come first: the patient and clinical details open once they are chosen. Department is what the ETAP form shows under Catatan. Diagnosis takes several lines, and each field shows an example.' },
    ],
  },
  {
    version: '3.6.0', date: '2026-11-24', title: 'Patient details for ETAP, state weekends',
    changes: [
      { kind: 'new', text: 'The ETAP download now ends with a patient-details attachment for every day over 4 hours: each patient’s time, details, diagnosis, treatment, plan and DSA, with a “Disahkan oleh” signature space. No more screenshots of the response sheet.' },
      { kind: 'improved', text: 'On the ETAP form a patient can take up to three rows, so longer diagnoses fit; the table continues from page 3 onto page 5.' },
      { kind: 'improved', text: 'The weekend follows your state’s rest days (e.g. Friday + Saturday) in the rota, leave points and ETAP. Changing a state’s weekend only affects rotas generated afterwards; published rotas and tallies stay as they are.' },
      { kind: 'improved', text: 'The ETAP button now says which rota it adds from, e.g. “Add my 4 on-call dates from the October rota”.' },
      { kind: 'new', text: 'The case log Excel download can cover a whole year as well as a month.' },
    ],
  },
  {
    version: '3.5.0', date: '2026-11-24', title: 'ETAP claim form',
    changes: [
      { kind: 'new', text: 'A new ETAP page builds your monthly on-call claim: add your rota dates (or type the days you did), adjust the times to match your punch card, and see each day priced as passive call under or over 4 hours, on a working day or a rest day / public holiday.' },
      { kind: 'new', text: 'A day counts as over 4 hours when your recorded cases for that on-call date add up to 4 hours or more; those patients fill the form’s table (confidential ones without name and registration number).' },
      { kind: 'new', text: 'Download the official ETAP form filled in with your verified profile, days, totals and patients, together with the latest surat arahan. Bring your own punch-card copy and bank statement.' },
      { kind: 'improved', text: 'A day you claim that the published rota gives to someone else is marked, so you can check it before submitting.' },
      { kind: 'new', text: 'Admins upload the surat arahan (Admin → Surat arahan); masters set each state’s weekly rest days (Fri + Sat in Kedah, Kelantan and Terengganu).' },
      { kind: 'improved', text: 'The top bar is tidier: your leave pages sit under Leave, and My on-call, Cases and ETAP under On call, so nothing wraps.' },
    ],
  },
  {
    version: '3.4.0', date: '2026-11-24', title: 'ETAP profile',
    changes: [
      { kind: 'new', text: 'Officers have a “My ETAP profile” page (account menu) for the details on the ETAP claim form: IC, jawatan, gred, address, distance, salary number, phones and bank. Only you can see them.' },
      { kind: 'new', text: 'Verify your profile with your password once it is right. Any later change, including your phone or email, needs a new verification; the ETAP claim form will need a verified profile.' },
    ],
  },
  {
    version: '3.3.0', date: '2026-11-24', title: 'On-call case log',
    changes: [
      { kind: 'new', text: 'Record every patient you see on call on the new Cases page, instead of the Google Form. Pick the on-call date, then whether the patient was seen that day or after midnight; you are the officer in charge, taggers are typed separately, and the DSA on call is always named with their status.' },
      { kind: 'new', text: 'The case log shows the hospital’s cases by month, with search, an officer filter and an Excel download.' },
      { kind: 'new', text: 'Admins can import the existing Google Sheet (downloaded as CSV); cases already in the log are skipped.' },
      { kind: 'improved', text: 'Patient details stay inside the hospital, are never cached by the browser, and every view and download is recorded in the audit log (without the patient’s details).' },
    ],
  },
  {
    version: '3.2.0', date: '2026-11-24', title: 'One record per person',
    changes: [
      { kind: 'improved', text: 'An officer and their sign-in are now one record, managed on Admin → Officers. Adding someone needs their email and invites them straight away; email, role and “can sign in” are changed on the same form. The separate Accounts tab is gone.' },
      { kind: 'new', text: 'Supervisors are people too: add them on the Officers tab with the Supervisor role. They run the admin pages and are never put on the rota, so they have no on call, excluded or left status: when one leaves, switch off “Can sign in”.' },
      { kind: 'improved', text: 'Officers who had no sign-in before this update stay on the rota and are marked “Email needed”. Edit each one to add their email and invite them.' },
      { kind: 'improved', text: 'Each row on the Officers tab can send a new temporary password, and the list can be filtered by role and sign-in.' },
      { kind: 'improved', text: 'CSV import needs an Email for every row and accepts the Supervisor role. Adding officers by name only is gone.' },
      { kind: 'improved', text: 'Masters are listed on All hospitals → Masters.' },
    ],
  },
  {
    version: '3.1.0', date: '2026-11-24', title: 'Clinics page and email switch',
    changes: [
      { kind: 'improved', text: 'Clinics and districts now have their own page (Admin → Clinics) instead of a pop-up: pick a state or district on the left, search, and add, rename or remove clinics in a table. It opens on your hospital’s state.' },
      { kind: 'improved', text: 'The accounts list can be filtered by role and status. Masters see accounts grouped by hospital and can filter by hospital.' },
      { kind: 'new', text: 'Masters can switch email off or on for the whole app (All hospitals → Settings). While it is off, invites show the temporary password to the admin instead of emailing it. The choice is kept.' },
      { kind: 'fixed', text: 'A hospital admin can no longer remove a clinic that another hospital’s officers work at.' },
      { kind: 'improved', text: 'The user manual is the same for every hospital, so it is now uploaded by masters from All hospitals → Manual. Hospital admins no longer see a Manual tab; everyone still reads it from the Rules page.' },
      { kind: 'improved', text: 'The audit log covers the whole app, so it moved to the masters’ All hospitals page, with a Hospital filter and column. Changes to hospitals, states, districts, clinics and settings show as app-wide.' },
      { kind: 'new', text: 'Masters can download every hospital’s rota reports from All hospitals → Reports, filtered by hospital. Hospital admins still have their own Reports tab.' },
    ],
  },
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
