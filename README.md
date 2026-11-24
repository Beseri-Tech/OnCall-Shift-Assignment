# Rota-OC

A web app for building a fair on-call rota, designed for hospital and clinic teams. Officers enter their leave and preferred on-call days; an admin locks leave, generates a balanced rota, adjusts it and publishes it. Developed by Beseri-Tech.

This repository was called `OnCall-Shift-Assignment` until November 2026. GitHub redirects the old address, but the container image moved to a new path (see [Run with Docker](#run-with-docker)).

The original WinForms desktop app lives on the [`legacy`](../../tree/legacy) branch.

## What it does

1. **Officers and accounts.** Officers are organised as state → district → clinic → officer (Perlis with Kangar and Arau to start; admins add clinics, and more districts or states if needed). An admin adds an officer and invites them by email in one step, or imports many from a CSV file (template in the app); the invite has a temporary password that must be changed at first sign-in. Forgotten passwords are reset by email. Roles: **Officer**, **Admin** (an officer who also runs the admin pages) and **Supervisor** (admin only, not on the rota). Everything needs sign-in.
2. **Leave.** Officers mark their own leave (with a note) and preferred on-call days until the period's deadline, within the leave limits: points for weekends, public holidays and peak days, a weekday allowance and a per-day cap. Points show as CL / EX / RW badges. Officers can also request leave with a supporting document.
3. **Confirmed leave.** Admins record approved absences (for example an overseas trip) with several date ranges. They include the officer's CR (cuti rehat) days and cost 33% of what the days would have cost in points.
4. **Rewards.** Busy-day shifts earn points for the next rota: public holiday +3, weekend +2, peak day +1.
5. **The rota.** Admins manage officers, clinics, districts, periods, public holidays, peak days and the shift **tally**, edit anyone's leave, then lock leave and generate a draft rota. Weekend and public-holiday shifts are balanced and back-to-back shifts avoided. Generator notes explain each choice. Admins can override days (with warnings for leave clashes), send the draft for review, and **publish**.
6. **Swaps.** Officers can swap a shift with a colleague; the colleague accepts or declines.
7. **Reports.** Publishing saves a PDF report (assignments, reasons, swaps, overrides with who and when, officer totals). Admins list and download them under Admin → Reports. Timetable, tally and leave can be exported to Excel.
8. **Rules and manual.** The Rules page explains how leave, points and the rota work, with a Manual tab for the user manual.
9. **Feedback and audit.** Anyone signed in can send feedback from the header; admins read, filter and resolve it. Important actions are kept in an audit log.
10. **Changelog.** The footer shows the app version and links to the Changelog page.

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

The app is at <http://localhost:8080> and the admin page at `/admin`. Database migrations run on startup; data lives in the `pgdata` volume, so back it up. Put a reverse proxy with HTTPS in front for anything beyond a trusted network.

| Variable | Purpose | Default |
|---|---|---|
| `POSTGRES_PASSWORD` | database password | required |
| `ADMIN_EMAIL` | first admin's sign-in email (created while no admin exists) | required |
| `ADMIN_PASSWORD_HASH` | first admin's password hash | required |
| `APP_BASE_URL` | public address, used in email links | the request's address |
| `SMTP_HOST`, `SMTP_PORT`, `SMTP_USER`, `SMTP_PASSWORD`, `SMTP_FROM` | email for invites and password resets; without it invites show the temporary password to the admin | off |
| `APP_PORT` | published port | `8080` |
| `APP_TAG` | image tag to run | `latest` |
| `WEB_ORIGIN` | address of a separately hosted web app allowed to call the API (see below) | off |
| `R2_ACCOUNT_ID`, `R2_BUCKET_NAME`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY` | Cloudflare R2 bucket for the manual and uploaded documents (see below) | off |
| `TIME_ZONE` | used for "today" and deadlines | `Asia/Kuala_Lumpur` |

### Web app on Cloudflare Pages (optional)

The API serves the web app itself, so this is optional. To host the web app on Cloudflare Pages instead:

1. Put both on the same domain, e.g. `rota.example.com` (Pages) and `rota-api.example.com` (API). The sign-in cookie is `SameSite=Strict`, so a `*.pages.dev` address can't sign in.
2. On the server, set `WEB_ORIGIN=https://rota.example.com` and `APP_BASE_URL=https://rota.example.com` (email links point at the web app).
3. In Cloudflare, create a Pages project with **Direct Upload**, named `oncall-rota`, and add the custom domain.
4. In GitHub repo settings → Secrets and variables → Actions, add the secrets `CLOUDFLARE_API_TOKEN` (permission: Account → Cloudflare Pages → Edit) and `CLOUDFLARE_ACCOUNT_ID`. Optionally add the variables `VITE_API_URL` (the API address) and `CLOUDFLARE_PAGES_PROJECT`.

Every push to `master` then deploys the web app to Pages after the tests pass. (The Pages project name is separate from the repository name, so it stays `oncall-rota` unless you change it.)

## Hosting and CI

- Production runs at <https://rota-oc.zogami.uk>. The web app is on Cloudflare Pages (deployed with Wrangler from CI); the API runs from the Docker image on a server, started from a Portainer stack, and Watchtower pulls new images.
- CI (`.github/workflows/ci.yml`) runs only the affected jobs: changes under `web/` run the web checks and the Pages deploy, changes under `api/` run the API tests and the Docker image. Changes to the workflow itself, and version tags, run everything.
- A push to `master` deploys the web app and publishes the image as `ghcr.io/beseri-tech/rota-oc:latest` (image tag `v1.2.3` gives `:1.2.3`).

## Develop

Needs the .NET 10 SDK, Node 22 and Docker (API tests start a throwaway Postgres via Testcontainers).

```bash
docker compose up -d db          # Postgres on 127.0.0.1:5432
cd api && dotnet run --project Rota.Api   # sign in as admin@localhost / admin; emails land in .mail/
cd web && npm ci && npm run dev  # Vite dev server
dotnet test api/Rota.sln         # tests
```

Layout: `api/Rota.Core` (rota algorithm, Excel, leave parsing), `api/Rota.Api` (endpoints, EF/Postgres), `web/` (React + Mantine). CI runs tests, lint, build and the Docker image build.

## Versioning and changelog

The app has one semantic version in `web/package.json`, shown in the footer. Each change that users can see bumps it (MAJOR for changes to how the rota works, MINOR for a feature, PATCH for a fix) and adds an entry to `web/src/changelog.ts`, which feeds the in-app Changelog page. Docs-only and CI-only changes need no bump. See [AGENTS.md](AGENTS.md) for the rules.

## Document storage on Cloudflare R2 (optional)

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

Back up the bucket separately: database backups don't include the files.

## Credit and licence

Developed by Beseri-Tech. Licensed under [AGPL-3.0](LICENSE.txt).
