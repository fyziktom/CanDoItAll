# Structured Input

## Core Objective

- Replace the current startup-only database configuration with a first-class **database profile** system.
- Let the user select the database to work with, start from the last active database, and switch databases during runtime.
- Make SQLite a first-class provider alongside PostgreSQL, including creation, opening/importing, and clone/snapshot workflows.
- Guarantee that switching databases reloads active modules, routes, browser state, and data-backed services safely.

## Hard Constraints

- Runtime switching must not require restarting the web process.
- The active database choice cannot be stored inside the currently selected database because that would create a circular dependency.
- Unit tests, integration tests, component tests, and Playwright/browser proof are mandatory.
- Database switching must not leave stale artifact routes or stale workbench tabs that crash against the new database.
- Managed files and other profile-scoped storage must stay aligned with the active database.
- The implementation agent must not claim completion without proof; blocked proof must stay blocked.

## Source Artifacts

- `/mnt/data/CanDoItAll-toolbox-repair.zip`
- `/mnt/data/work/CanDoItAll-toolbox-repair`
- `README.md`, `Program.cs`, `InfrastructureServiceCollectionExtensions.cs`, `WorkspaceStorage.cs`, `BrowserWorkspaceStateStore.cs`, `SettingsPage.razor`, `MainLayout.razor`, the five schema initializer files, and the existing test harness files listed in `inputs/01-source-artifacts.md`

## Input Coverage Signals

- The user explicitly wants database selection plus **runtime switching**.
- The user explicitly wants SQLite support, even though the repo already contains partial startup-time SQLite wiring.
- The user explicitly wants startup behavior that remembers the last database and still asks whether to continue or switch.
- The user explicitly wants SQLite sources from file dialog/path/AppData existing DBs/IPFS.
- The user explicitly wants PostgreSQL sources from localhost, Docker-hosted localhost, or remote servers.
- The user explicitly wants **create new database** for both SQLite and PostgreSQL.
- The user explicitly wants optional **clone/snapshot** behavior that can act like a branch/version point.
- The user explicitly warns that Codex sometimes fakes validation or skips work, so the bundle must make that impossible to pass silently.

## Dependency And Sequencing Signals

- The control plane and active-profile catalog must exist before runtime switching can be trustworthy.
- The dynamic `DbContext` factory must exist before pages/services can switch providers at runtime.
- EF migrations and the legacy SQLite upgrade path must be established before provider parity and database creation can be trusted.
- Storage isolation and managed-file serving must be fixed before clone/snapshot workflows are complete.
- Runtime reload and workbench isolation must land before the UI can safely expose switching to users.
- Snapshot/clone and final E2E proof are last because they depend on all earlier foundations being stable.

## Validation Expectations

- Unit coverage must prove catalog behavior, driver selection, encrypted control-plane secrets, workbench storage-key isolation, and switch-coordinator rules.
- Integration coverage must prove bootstrap, switching, migrations/legacy upgrade, create/clone flows, and provider parity across SQLite and PostgreSQL.
- Component coverage must prove startup modal, global database switcher, settings Data Sources UX, and safe stale-artifact UI fallbacks.
- Playwright coverage must prove browser-visible runtime switching, two-page or two-circuit reload behavior, modal behavior, per-profile workbench restore, and managed-files availability after switch.
- The execution report must include command results, browser analytics rows, screenshot paths, and raw-note closure rows.

## UI Validation Strategy

- Large-screen browser proof is required for `MainLayout.razor`, the startup modal, the global active-database badge/switcher, and the new Settings Data Sources tab.
- The initial browser pass must use a maximized or desktop-equivalent viewport and capture screenshots of:
  - startup continue/switch modal
  - top-bar active database indicator and switcher
  - settings data-source list/editor
  - an artifact route before and after switching
- A narrower-width follow-up pass is required for modal and settings responsive layout if controls wrap or the dialog grows.
- The UI proof must answer the questions: Is the active database obvious? Is switching blocked while unsafe? Does the stale route recover safely? Do DB-specific forms remain readable?

## Browser Validation Analytics

- Subbundle 06 logs route reload and profile-isolated workbench local-storage evidence.
- Subbundle 07 logs startup-modal and settings-switcher proof.
- Subbundle 08 logs end-to-end switching, clone, and snapshot/IPFS flows.
- Every browser row must capture route, viewport, Playwright actions/assertions, screenshot path(s), and result in `reviews/01-execution-report.md`.

## Working Assumptions

- The app is effectively a local single-user workspace today, so app-wide active database state is acceptable in v1.
- A full circuit reload to a safe route is acceptable during database switch; preserving in-place artifact editing across providers is not required.
- IPFS should be treated as a transport for snapshot packages or cached SQLite sources, not as a live mutable database backend.
- The execution environment used later will have a working .NET SDK, browser tooling, and optionally Docker/PostgreSQL available.

## Primary Risks

- Existing SQLite databases were created via `EnsureCreated` and custom SQL, not EF migrations, so upgrade/baseline logic is the highest schema risk.
- `UseStaticFiles` currently binds to one fixed managed-files root at startup, which is incompatible with per-profile storage.
- Workbench session storage currently uses one global local-storage key, which would leak stale tabs across database profiles.
- Runtime switching affects all open browser circuits because the active database is app-wide, so cross-tab reload proof is mandatory.
- PostgreSQL create-database operations may require a privileged connection distinct from the target database connection.
