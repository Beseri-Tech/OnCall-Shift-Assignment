# Instructions for AI agents and contributors

## Versioning and changelog (required for every change)

The app has one semantic version (MAJOR.MINOR.PATCH), kept in `web/package.json` (and the root entry of `web/package-lock.json`). It shows in the footer and account menu, and the Changelog page (`/changelog`) renders `web/src/changelog.ts`.

Every pull request that changes app behaviour, UI, rules or fixes a bug must:

1. Bump `version` in `web/package.json` and the matching two lines at the top of `web/package-lock.json`:
   - **MAJOR**: changes how the rota works or needs everyone to relearn something.
   - **MINOR**: a new feature or visible improvement.
   - **PATCH**: a fix with no new behaviour.
2. Add an entry at the **top** of `CHANGELOG` in `web/src/changelog.ts` with the same version, today's date, a short title and plain-language changes (kind `new`, `improved` or `fixed`). Write for officers, not developers.
3. Keep the two in step: the newest changelog entry must equal the `package.json` version.

Docs-only or CI-only changes need no bump. Details of the scheme are also in the header comment of `web/src/changelog.ts`.

If a change affects what users see or the rules, also update the Rules page and the user manual.

## Credit

User-facing pages and documents carry "Developed by Beseri-Tech".

## CI

CI runs only the affected jobs (`web/` runs web and pages, `api/` runs api and docker).
