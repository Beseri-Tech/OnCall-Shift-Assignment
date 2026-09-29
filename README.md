# OnCall Shift Assignment

A web app for building a fair on-call rota, designed for hospital teams. Staff enter their leave and preferred on-call days; an admin locks leave, generates a balanced rota, adjusts it and publishes it.

The original WinForms desktop app lives on the [`legacy`](../../tree/legacy) branch.

## How it works

1. **Staff** open the site, pick their name and mark leave (with a note) and preferred on-call days until the period's deadline. No login.
2. **Admin** (single password) manages people, periods and public holidays, imports leave / opening shift totals from Excel, then locks leave and generates a draft rota. Weekend and public-holiday shifts are balanced, back-to-back shifts avoided.
3. The admin overrides days if needed (warnings for leave clashes) and **publishes**. Everyone sees the rota, and published shifts count towards each person's running totals. Timetable, totals and leave can be exported to Excel.

## Run with Docker

Needs Docker with Compose.

```bash
cp .env.example .env
```

Generate the admin password hash and paste it into `.env` (keep the single quotes):

```bash
docker build -t oncall-rota .
docker run --rm oncall-rota hash-password "your-admin-password"
```

Also set `POSTGRES_PASSWORD` in `.env`, then:

```bash
docker compose up -d --build
```

The app is at <http://localhost:8080> and the admin page at `/admin`. Database migrations run on startup; data lives in the `pgdata` volume, so back it up. Put a reverse proxy with HTTPS in front for anything beyond a trusted network.

| Variable | Purpose | Default |
|---|---|---|
| `POSTGRES_PASSWORD` | database password | required |
| `ADMIN_PASSWORD_HASH` | admin password hash | required |
| `APP_PORT` | published port | `8080` |
| `TIME_ZONE` | used for "today" and deadlines | `Asia/Kuala_Lumpur` |

## Develop

Needs the .NET 10 SDK, Node 22 and Docker (API tests start a throwaway Postgres via Testcontainers).

```bash
docker compose up -d db          # Postgres on 127.0.0.1:5432
cd api && dotnet run --project Rota.Api   # set Database__MigrateOnStartup=true on first run
cd web && npm ci && npm run dev  # Vite dev server
dotnet test api/Rota.sln         # tests
```

Layout: `api/Rota.Core` (rota algorithm, Excel, leave parsing), `api/Rota.Api` (endpoints, EF/Postgres), `web/` (React + Mantine). CI runs tests, lint, build and the Docker image build.

## License

[AGPL-3.0](LICENSE.txt)
