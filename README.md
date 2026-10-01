# OnCall Shift Assignment

A web app for building a fair on-call rota, designed for hospital teams. Staff enter their leave and preferred on-call days; an admin locks leave, generates a balanced rota, adjusts it and publishes it.

The original WinForms desktop app lives on the [`legacy`](../../tree/legacy) branch.

## How it works

1. **Officers and accounts.** Officers are organised as state → district → clinic → officer (Perlis with Kangar and Arau to start; admins add clinics, and more districts or states if needed). An admin adds an officer and invites them by email in one step, or imports many from a CSV file (template in the app); the invite has a temporary password that must be changed at first sign-in. Forgotten passwords are reset by email. Roles: **Officer**, **Admin** (an officer who also runs the admin pages) and **Supervisor** (admin only, not on the rota). Everything needs sign-in.
2. **Officers** mark their own leave (with a note) and preferred on-call days until the period's deadline, within the leave limits (points for weekends/public holidays/peak days, a weekday allowance and a per-day cap).
3. **Admins** manage officers, clinics, districts, periods, public holidays, peak days and the shift **tally**, edit anyone's leave, then lock leave and generate a draft rota. Weekend and public-holiday shifts are balanced, back-to-back shifts avoided.
4. The admin overrides days if needed (warnings for leave clashes) and **publishes**. Everyone sees the rota, and published shifts are added to each officer's tally. Timetable, tally and leave can be exported to Excel.

## Run with Docker

Needs Docker with Compose.

```bash
cp .env.example .env
```

Generate the first admin's password hash and paste it into `.env` with `ADMIN_EMAIL` (keep the single quotes):

```bash
docker run --rm ghcr.io/beseri-tech/oncall-shift-assignment hash-password "your-admin-password"
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

### Web app on Cloudflare Pages (optional)

The API serves the web app itself, so this is optional. To host the web app on Cloudflare Pages instead:

1. Put both on the same domain, e.g. `rota.example.com` (Pages) and `rota-api.example.com` (API). The sign-in cookie is `SameSite=Strict`, so a `*.pages.dev` address can't sign in.
2. On the server, set `WEB_ORIGIN=https://rota.example.com` and `APP_BASE_URL=https://rota.example.com` (email links point at the web app).
3. In Cloudflare, create a Pages project with **Direct Upload**, named `oncall-rota`, and add the custom domain.
4. In GitHub repo settings → Secrets and variables → Actions, add the secrets `CLOUDFLARE_API_TOKEN` (permission: Account → Cloudflare Pages → Edit) and `CLOUDFLARE_ACCOUNT_ID`. Optionally add the variables `VITE_API_URL` (the API address) and `CLOUDFLARE_PAGES_PROJECT`.

Every push to `master` then deploys the web app to Pages after the tests pass.
| `TIME_ZONE` | used for "today" and deadlines | `Asia/Kuala_Lumpur` |

## Develop

Needs the .NET 10 SDK, Node 22 and Docker (API tests start a throwaway Postgres via Testcontainers).

```bash
docker compose up -d db          # Postgres on 127.0.0.1:5432
cd api && dotnet run --project Rota.Api   # sign in as admin@localhost / admin; emails land in .mail/
cd web && npm ci && npm run dev  # Vite dev server
dotnet test api/Rota.sln         # tests
```

Layout: `api/Rota.Core` (rota algorithm, Excel, leave parsing), `api/Rota.Api` (endpoints, EF/Postgres), `web/` (React + Mantine). CI runs tests, lint, build and the Docker image build.

## License

[AGPL-3.0](LICENSE.txt)
