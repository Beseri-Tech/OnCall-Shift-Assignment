# Instructions for AI agents and contributors

## Versioning and changelog (required for every change)

The app has one semantic version (MAJOR.MINOR.PATCH), kept in `web/package.json` (and the root entry of `web/package-lock.json`). It shows in the footer and account menu, and the Changelog page (`/changelog`) renders `web/src/changelog.ts`.

Every pull request that changes app behaviour, UI, rules or fixes a bug must:

1. Bump `version` in `web/package.json` and the matching two lines at the top of `web/package-lock.json`:
   - **MAJOR**: changes how the rota works or needs everyone to relearn something (for example 3.0.0, running more than one hospital).
   - **MINOR**: a new feature or visible improvement (for example 3.3.0, the case log).
   - **PATCH**: a fix with no new behaviour (for example 2.8.2, saving rota reports).
2. Add an entry at the **top** of `CHANGELOG` in `web/src/changelog.ts` with the same version, today's date (`YYYY-MM-DD`; take it from the system date, don't guess), a short title and plain-language changes (kind `new`, `improved` or `fixed`). Write for officers, not developers.
3. Keep the two in step: the newest changelog entry must equal the `package.json` version.

Docs-only or CI-only changes need no bump. Details of the scheme are also in the header comment of `web/src/changelog.ts`.

If a change affects what users see or the rules, also update the Rules page and the user manual (see Documents below).

## Branches

`staging` is permanent. Branch from `staging`, open pull requests into `staging` (it deploys to rota-staging.zogami.uk), and once staging passes, open a pull request from `staging` into `master` (production). Don't open feature pull requests straight into `master`.

## Checks before you push

- Web (from `web/`): `npm ci`, `npm run lint`, `npm run build`.
- API: `dotnet test api/Rota.sln --configuration Release` (the tests start a throwaway Postgres with Testcontainers, so Docker must be available).

## Credit

User-facing pages and documents carry "Developed by Beseri-Tech".

## CI

CI (`.github/workflows/ci.yml`) runs only the affected jobs: `web/` runs web and pages, `api/` runs api and docker. Details:

- Any change under `.github/workflows/**` runs every job, and version tags (`v*`) always run everything.
- `pages` deploys the web app on a push to `staging` or `master` only; pull requests just build and lint.
- `docker` builds the API image on pull requests and pushes it to GHCR on pushes and tags (`:staging`, `:latest`, `:sha-…`, semver tags).

## Hosting

- The web frontend is on Cloudflare Pages, deployed with Wrangler. Production is https://rota-oc.zogami.uk; staging is the `rota-staging` Pages project.
- The Docker image hosts the API only, not the web app.
- Uploaded documents live in a private Cloudflare R2 bucket (S3 keys come from `.env`; never commit them).

## Documents

- The user manual is stored in R2 only. Don't bundle it in `web/public`.
- The Rules page, user manual and PDF are updated together when rules or visible behaviour change. The manual footer carries the Beseri-Tech credit.
- The functional specification and the demo presentation are separate documents with their own owners. Don't edit them as part of a code change unless asked.
