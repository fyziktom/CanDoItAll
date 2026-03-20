# 05_PLAYWRIGHT_TESTS_CSHARP.md

## Goal
Add Playwright UI tests in C# for key product flows, runnable locally and in CI.

## Constraints
- Tests must be stable: avoid brittle selectors; use `data-testid`.
- Keep a small “smoke” suite and a larger “e2e” suite.
- Tests must run headless by default in CI.

## References (existing code to modify for selectors)
- Navigation/layout: `src/App.Blazor/Layouts/MainLayout.razor`
- Pages you will test:
  - `src/App.Blazor/Pages/Onboarding.razor` (to be created)
  - `src/App.Blazor/Pages/Lessons.razor` (to be created)
  - `src/App.Blazor/Pages/Account.razor` (to be created)
  - `src/App.Blazor/Pages/Library.razor` (to be created)
  - `src/App.Blazor/Components/NotationEditor.razor` (existing)
  - MIDI UI: likely new `/harmony` page from prompt 06/07

## Create new test project
Create `tests/App.Web.PlaywrightTests/App.Web.PlaywrightTests.csproj`
- Use xUnit (preferred because repo already uses xUnit) OR NUnit (acceptable).
- Add `Microsoft.Playwright` dependency.
- Follow Playwright .NET setup pattern (install browsers in CI).

## Test strategy
### 1) Smoke tests (fast, <2 minutes)
- App loads `/`
- Dashboard visible
- Navigation works to `/lessons`, `/library`, `/editor`

### 2) E2E tests (key flows)
Minimum required flows (MUST implement):
1) Onboarding / lesson flow:
   - Start onboarding
   - Select a goal
   - Finish onboarding
   - Start first lesson
   - Complete at least one drill step
2) Authentication:
   - Register (or login with test account)
   - Verify user is logged in (e.g., user menu shows email)
3) Editor:
   - Open editor page
   - Create a new score (template)
   - Add a note (or use a toolbar action)
   - Save locally (verify “saved” indicator)
4) MIDI detection view:
   - Open MIDI/chord detection page
   - Verify UI renders and shows “No MIDI device connected” state
5) Library:
   - Open library
   - Create/save a score
   - Verify score appears in “My scores”
   - Open it again

## Test data
- Prefer using a local API with a test DB provider (SQLite/InMemory) for e2e.
- Use deterministic “test user” credentials:
  - Email: `test@example.com`
  - Password: `TestPassword!12345`
- Clean up state between tests:
  - use unique user per test OR API endpoint for test cleanup (admin-only in test env)

## Required selector plan
Add `data-testid` in UI code. Minimum IDs:
- `nav-dashboard`, `nav-lessons`, `nav-library`, `nav-editor`, `nav-harmony`, `nav-settings`, `nav-account`
- `onboarding-start`, `onboarding-goal-sightreading`, `onboarding-finish`
- `lesson-start-first`, `lesson-complete-step`
- `login-email`, `login-password`, `login-submit`, `register-submit`, `logout`
- `library-create-score`, `library-search`, `library-score-card-{id}`
- `editor-save`, `editor-status-saved`
- `midi-status`

## CI integration
- Add GitHub Actions workflow in `.github/workflows/ci.yml`:
  - Setup dotnet
  - Restore/build/test
  - Install Playwright browsers
  - Start App.Web server (and App.Api if needed)
  - Run Playwright tests with `BASE_URL` env var
- Include retries for flaky tests (max 1 retry) but fix root causes.

## Acceptance criteria
- At least 5 Playwright tests covering the required flows.
- Tests pass reliably on local machine and CI.
- Selectors are stable and do not depend on CSS class names.

## Verification steps
- `dotnet test tests/App.Web.PlaywrightTests/App.Web.PlaywrightTests.csproj`
- In CI: ensure the workflow uploads Playwright HTML report as artifact.
