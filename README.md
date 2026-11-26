# Rota-OC

A web app for building a fair on-call rota and keeping on-call records, designed for hospital and clinic teams. Officers enter their leave and preferred on-call days; an admin locks leave, generates a balanced rota, adjusts it and publishes it. Developed by Beseri-Tech.

This repository was called `OnCall-Shift-Assignment` until November 2026. GitHub redirects the old address, but the container image moved to `ghcr.io/beseri-tech/rota-oc`.

The original WinForms desktop app lives on the [`legacy`](../../tree/legacy) branch.

## What it does

1. **Hospitals, officers and accounts.** The app runs the rota for more than one hospital; each has its own officers, periods, leave, swaps, reports and audit log, and nobody sees another hospital's data. Officers are organised as state → district → clinic → officer. An admin adds an officer and invites them by email in one step, or imports many from a CSV file. Roles: **Officer**, **Admin** (an officer who also runs the admin pages), **Supervisor** and **Master** (looks after the whole app and every hospital). Everything needs sign-in; signed-out visitors see a welcome page with a video tour. Pages are listed in a collapsible side menu (Leave, On call, Rota, Admin, Help), which becomes a menu button on phones.
2. **Leave.** Officers mark leave and preferred on-call days until the period's deadline, within the leave limits (points for weekends, public holidays and peak days, a weekday allowance and a per-day cap). Points show as CL / EX / RW badges. Officers can request leave with a supporting document. An admin can set a time for a period to open ("Open to officers at"): officers are notified, get reminders and see a countdown, and leave unlocks by itself at that time.
3. **Confirmed leave.** Admins record approved absences (for example an overseas trip) with several date ranges. They include the officer's CR (cuti rehat) days and cost 33% of what the days would have cost in points.
4. **Rewards.** Busy-day shifts earn points for the next rota: public holiday +3, weekend +2, peak day +1. Each state sets which days are its weekend.
5. **The rota.** Admins lock leave and generate a draft rota with weekend and public-holiday shifts balanced and back-to-back shifts avoided. Generator notes explain each choice. Admins override days (with warnings for leave clashes), send the draft for review, and **publish**. Published shifts count in each officer's tally.
6. **Swaps.** Officers swap a shift with a colleague, who accepts or declines.
7. **Cases and passover.** Officers record their on-call cases in a case log (exportable to Excel by month or year) and can generate the WhatsApp passover in the department's template.
8. **ETAP.** Officers keep an ETAP profile and download the ETAP claim form for their on-call dates, with the patient-details attachment for days over 4 hours. The ETAP Claim page stays greyed out until the officer's ETAP profile is verified.
9. **Reports.** Publishing saves a PDF report (assignments, reasons, swaps, overrides, officer totals). Admins download them from the Reports tab, masters from all hospitals. Timetable, tally and leave export to Excel.
10. **Help.** The Help page is a directory of how everything works, with search and the user manual (uploaded by a master and stored in R2).
11. **Feedback, audit and email.** Anyone can send feedback from the header. Important actions go to an audit log. Masters can switch email off for the whole app.
12. **Changelog.** The footer shows the app version and links to the Changelog page. The database is backed up every night to R2 and kept for 30 days (see below).

## Run with Docker

Needs Docker with Compose.

```bash
cp .env.example .env
```

Generate the first admin's password hash and paste it into `.env` with `ADMIN_EMAIL` (keep the single quotes):

```bash
docker run --rm ghcr.io/beseri-tech/rota-oc hash-password "your-admin-password"
```

Also set `POSTGRES_PASSWORD` in `.env`, then:

```bash
docker compose pull && docker compose up -d
```

CI publishes the image to GHCR: every push to `master` updates `:latest`, and a `v1.2.3` tag publishes `:1.2.3`. Set `APP_TAG` to pin a version. If the package is private, sign in on the server first with a token that has `read:packages`: `docker login ghcr.io -u <github-user>`. To build from source instead, use `docker compose up -d --build`.

The app is at <http://localhost:8080> and the admin page at `/admin`. Database migrations run on startup; data lives in the `pgdata` volume; see [Database backups to R2](#database-backups-to-r2-optional). Put a reverse proxy with HTTPS in front for anything beyond a trusted network.

| Variable | Purpose | Default |
|---|---|---|
| `POSTGRES_PASSWORD` | database password | required |
| `ADMIN_EMAIL` | first master's sign-in email (created while no master exists) | required |
| `ADMIN_PASSWORD_HASH` | first master's password hash | required |
| `APP_BASE_URL` | public address, used in email links | the request's address |
| `SMTP_HOST`, `SMTP_PORT`, `SMTP_USER`, `SMTP_PASSWORD`, `SMTP_FROM` | email for invites and password resets; without it invites show the temporary password to the admin | off |
| `SMTP_ENABLED` | whether email starts switched on; masters can change it in the app (All hospitals → Settings) and that choice is kept | `true` |
| `APP_PORT` | published port | `8080` |
| `APP_TAG` | image tag to run | `latest` |
| `R2_ACCOUNT_ID`, `R2_BUCKET_NAME`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY` | Cloudflare R2 bucket for the manual and uploaded documents (see below) | off |
| `TIME_ZONE` | used for "today" and deadlines | `Asia/Kuala_Lumpur` |
| `WEB_ORIGIN` | address of a separately hosted web app allowed to call the API (see below) | off |

### Web app on Cloudflare Pages (optional)

The API serves the web app itself, so this is optional. To host the web app on Cloudflare Pages instead:

1. Put both on the same domain, e.g. `rota.example.com` (Pages) and `rota-api.example.com` (API). The sign-in cookie is `SameSite=Strict`, so a `*.pages.dev` address can't sign in.
2. On the server, set `WEB_ORIGIN=https://rota.example.com` and `APP_BASE_URL=https://rota.example.com` (email links point at the web app).
3. In Cloudflare, create a Pages project with **Direct Upload**, named `oncall-rota`, and add the custom domain.
4. In GitHub repo settings → Secrets and variables → Actions, add the secrets `CLOUDFLARE_API_TOKEN` (permission: Account → Cloudflare Pages → Edit) and `CLOUDFLARE_ACCOUNT_ID`. Optionally add the variables `VITE_API_URL` (the API address) and `CLOUDFLARE_PAGES_PROJECT`.

Every push to `master` then deploys the web app to Pages after the tests pass.

### Staging

`staging` is a permanent branch. Changes go in by pull request into `staging`; once staging has been checked, `staging` is merged into `master` by pull request. Every push to `staging`, after the tests pass:

- deploys the web app to the Pages project `rota-staging` (`rota-staging.zogami.uk`; its production branch must be `staging`), built against `https://rota-staging-api.zogami.uk` and showing an amber **STAGING** ribbon and a `[STAGING]` tab title;
- publishes the image as `ghcr.io/beseri-tech/rota-oc:staging`.

In Portainer, the staging stack runs `ghcr.io/beseri-tech/rota-oc:staging` and the production stack `:latest` (or a release such as `:3.0.0`). The staging API runs from its own `.env` on its own database: `APP_TAG=staging`, `APP_BASE_URL=https://rota-staging.zogami.uk`, `WEB_ORIGIN=https://rota-staging.zogami.uk`, `R2_BUCKET_NAME=rota-staging`, `SMTP_ENABLED=false` (no email while testing; a master can switch it on under All hospitals → Settings), and its own admin and database password. Pulling and restarting the staging container (`docker compose pull && docker compose up -d`) happens on the server, not in CI. The variables `STAGING_VITE_API_URL` and `STAGING_PAGES_PROJECT` override the defaults.


## Hosting and CI

- Production is <https://rota-oc.zogami.uk> and staging <https://rota-staging.zogami.uk>. The web app is on Cloudflare Pages (deployed with Wrangler from CI). The Docker image hosts the API only; it runs on servers from Portainer stacks, and Watchtower pulls new images.
- CI (`.github/workflows/ci.yml`) runs only the affected jobs: `web/` runs the web checks and Pages deploy, `api/` runs the API tests and the Docker image. Workflow changes and version tags run everything.
- Images: `ghcr.io/beseri-tech/rota-oc` with `:latest` (master), `:staging`, `:sha-…` and release tags. Feature branches go by pull request into `staging`, then `staging` into `master`.
- Documents (the manual, leave attachments) and nightly database backups live in private Cloudflare R2 buckets (see below).

## Develop

Needs the .NET 10 SDK, Node 22 and Docker (API tests start a throwaway Postgres via Testcontainers).

```bash
docker compose up -d db          # Postgres on 127.0.0.1:5432
cd api && dotnet run --project Rota.Api   # sign in as admin@localhost / admin; emails land in .mail/
cd web && npm ci && npm run dev  # Vite dev server
dotnet test api/Rota.sln         # tests
```

Layout: `api/Rota.Core` (rota algorithm, Excel, leave parsing), `api/Rota.Api` (endpoints, EF/Postgres), `web/` (React + Mantine). CI runs tests, lint, build and the Docker image build.

### Database backups to R2 (optional)

Every night at 03:00 (clinic time) the API runs `pg_dump -Fc` and uploads the dump to a **separate private** R2 bucket, as `<prefix>/rota-<date>-<time>.dump`. It is off until the four `BACKUP_R2_*` values are set. A night is skipped if the container is restarting at that moment; there is no catch-up run. The dump holds patient details, password hashes and the audit log, so treat it as sensitive and delete downloaded copies after use.

1. In R2 create a second private bucket (e.g. `rota-backups`) with its own API token: **Object Read & Write limited to that bucket only** (R2 has no write-only token, so the scoping is the safeguard). Tokens are per bucket, not per folder: use a **separate bucket (or token) for staging**, otherwise a compromised staging server could delete production backups. Consider an R2 bucket lock/retention rule too, and choose the bucket location with patient data in mind.
2. Bucket → Settings → **Object lifecycle rules**: delete objects 30 days after upload.
3. Set `BACKUP_R2_ACCOUNT_ID`, `BACKUP_R2_BUCKET_NAME`, `BACKUP_R2_ACCESS_KEY_ID`, `BACKUP_R2_SECRET_ACCESS_KEY` in `.env`. `BACKUP_PREFIX` is `production` by default; staging uses `staging`. Optional: `BACKUP_HOUR` (0-23), `BACKUP_ENABLED=false`.
4. Check once: set `BACKUP_RUN_ON_STARTUP=true`, restart, and look for "Database backup uploaded" in `docker compose logs app` (and the object in R2). Then remove it.
5. Optional: `BACKUP_PING_URL` (a healthchecks-style URL) is called after each good backup; set the service to alert when pings stop, since failures are otherwise only in the log.

**Restore** (the dump must come from the same app version, because migrations run on startup). Download the dump from the R2 dashboard, then:

```bash
docker compose stop app
docker compose exec -T db pg_restore --clean --if-exists --no-owner --single-transaction -U rota -d rota < rota-YYYY-MM-DD-HHMM.dump
docker compose start app
```

Try a restore into a scratch database once before you need it. `pg_dump` in the image (`postgresql17-client`) must stay the same major version as, or newer than, the `postgres` image: change them together.

## Versioning and changelog

The app has one semantic version in `web/package.json`, shown in the footer. Each change that users can see bumps it (MAJOR for changes to how the rota works, MINOR for a feature, PATCH for a fix) and adds an entry to `web/src/changelog.ts`, which feeds the in-app Changelog page. Docs-only and CI-only changes need no bump. See [AGENTS.md](AGENTS.md) for the rules.

### Document storage on Cloudflare R2 (optional)

The user manual and officers' uploaded documents (e.g. proof for a leave request) live in a **private** R2 bucket. Files go straight between the browser and R2 through short-lived links (5 minutes) that the API only issues after checking who is asking; R2 credentials stay on the server. Until the four `R2_*` variables are set, upload endpoints answer 503 and nothing else is affected.

1. In Cloudflare → R2, create a bucket (e.g. `rota-documents`) and leave **public access off**. Note your **Account ID** (R2 overview page).
2. R2 → Manage API tokens → create a token with **Object Read & Write**, limited to that bucket. Copy the **Access Key ID** and **Secret Access Key** (shown once).
3. On the bucket → Settings → **CORS policy**, allow the web app's address (`APP_BASE_URL`, plus `WEB_ORIGIN` if separate):

   ```json
   [{ "AllowedOrigins": ["https://rota.example.com"], "AllowedMethods": ["GET", "PUT", "HEAD"],
      "AllowedHeaders": ["Content-Type"], "ExposeHeaders": ["ETag"], "MaxAgeSeconds": 3600 }]
   ```

4. Put the four values in `.env` and restart. An admin then uploads the manual from **Admin → Manual**.

Uploads are checked on the server before they become downloadable (size, file type and the file's real signature; PDF, PNG, JPG or DOCX, up to 20 MB), and a new manual replaces the old one. Housekeeping runs in the background every 6 hours (`R2__CleanupEveryHours`, off with `R2__CleanupEnabled=false`): uploads never confirmed within 24 hours (`R2__QuarantineHours`) are deleted, and so are the documents of rejected leave requests after 30 days (`R2__RejectedRetentionDays`). A withdrawn request's document is deleted straight away. Approved leave keeps its document.

**The documents bucket is not backed up** by the database backup below, and R2 keeps no old versions: a deleted or overwritten file is gone.

## Credit and licence

Developed by Beseri-Tech. Licensed under [AGPL-3.0](LICENSE.txt).
