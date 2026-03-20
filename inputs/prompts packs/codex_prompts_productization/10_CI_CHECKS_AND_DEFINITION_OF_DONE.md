# 10_CI_CHECKS_AND_DEFINITION_OF_DONE.md

## Goal
Add CI checks and define a “quality bar” so the app behaves like a professional product:
- build + tests + lint/format
- Playwright smoke tests
- security baseline (dependency review, secret scanning guidance)
- basic observability hooks (non-invasive telemetry + error reporting design)

## CI workflow (GitHub Actions)
Create `.github/workflows/ci.yml` with jobs:

### Job: build_and_test
- checkout
- setup dotnet (10.x)
- restore
- build
- run unit tests:
  - `tests/MusicTheory.Tests`
  - `src/App.Api.Tests` (if present)
  - `tests/OMR.Service.Tests` (existing)
- collect coverage (optional but recommended)

### Job: e2e_playwright
- depends on build_and_test
- install Playwright browsers:
  - `pwsh tests/App.Web.PlaywrightTests/bin/Debug/net*/playwright.ps1 install --with-deps`
- start servers:
  - `dotnet run --project src/App.Api` (test env, SQLite/InMemory)
  - `dotnet run --project src/App.Web`
- run Playwright tests with `BASE_URL` env var
- upload Playwright report artifacts

## Formatting/linting
- Add `dotnet format` step (or enforce analyzers).
- Add EditorConfig if missing (`.editorconfig` at repo root).
- Ensure nullable and warnings as errors for new projects (at least App.Api).

## Definition of Done document
Create `docs/product/definition-of-done.md` including:

### Product quality bar
- Performance:
  - app interactive in < 3s on mid-range device (target)
  - harmonic assistant update < 250ms
  - drum scheduling stable at 120 BPM
- Accessibility:
  - keyboard navigation on core flows
  - aria labels for key controls
- i18n readiness:
  - strings centralized (`resx` or similar), no hard-coded text in UI for new pages
- Offline-first:
  - core features work offline
  - clear UI states for offline/online/syncing
- Reliability:
  - autosave with clear status
  - conflict handling UI exists
- Security:
  - JWT + refresh rotation
  - password hashing
  - rate limiting
  - audit logs for admin actions
  - secure storage guidance for tokens
- Testing:
  - unit tests for engines
  - integration tests for API
  - Playwright smoke/e2e coverage for key flows
- Observability (non-invasive):
  - structured logs server-side
  - client-side error boundary + optional opt-in telemetry events (no PII)

## Acceptance criteria
- CI workflow runs on push/PR and passes on main branch.
- Definition of Done doc exists and is referenced from README.
- Playwright report artifact is uploaded in CI.

## Verification steps
- Push to a branch and confirm GitHub Actions run successfully.
