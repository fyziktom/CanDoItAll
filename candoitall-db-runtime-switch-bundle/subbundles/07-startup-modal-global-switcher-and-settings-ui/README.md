# 07 Startup Modal, Global Switcher, and Settings UI

## Status

- `Completed`

## Objective

- Expose the database-profile feature to end users through a startup continue/switch modal, a global active-database indicator/switcher, and a Settings Data Sources management surface.

## Covered Inputs

- `RQ-003` startup continue/switch modal
- `RQ-010` SQLite UI flows
- `RQ-011` PostgreSQL UI flows
- `RQ-012` empty create UI flows
- `RQ-018` explicit override compatibility in the UI
- `RQ-021` component coverage for UI
- `RQ-022` browser proof for UI
- Raw notes `N-01`, `N-03`, `N-12`, `N-13`, `N-14`, `N-15`

## Prerequisites

- Critical foundation gate passed for subbundles 02–06.
- `subbundles/03-dynamic-runtime-db-and-bootstrap` and `subbundles/06-runtime-reload-and-workbench-isolation` completed so visible switching is real and safe.
- `subbundles/05-storage-isolation-and-managed-files-serving` completed so file-backed profiles will behave correctly after switching.

## Exact Source References

- `C:\repositories\CanDoItAll/src/CanDoItAll.Web/Components/Layout/MainLayout.razor`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Web/Program.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Web/Infrastructure/BrowserWorkspaceStateStore.cs`
- `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Playwright/PlaywrightAppFixture.cs`
- `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Components/ComponentTestHarness.cs`

## Deliverables

- Startup continue/switch/create modal that appears after initial profile resolution.
- Global active-database badge/summary and quick-switch entry point in the shell.
- Settings Data Sources tab with:
  - profile list
  - active-profile indicator
  - create new SQLite
  - open/import SQLite
  - create/test PostgreSQL
  - activate/delete/edit actions
  - override-locked read-only messaging when explicit runtime config is active
- Component and browser tests proving the major flows and layout/readability.

## Dependency Impact

- This subbundle is the public face of all prior runtime-switch foundations.
- If the UX is exposed before the backend/storage/workbench foundations are real, users will hit broken switches and the bundle will fail its main goal.
- Subbundle 08 clone/snapshot flows will likely extend the same Data Sources surface, so layout and service boundaries here should remain extensible.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Add a dedicated database-profile service/facade for the UI to query profile summaries, validation results, create requests, and activation results without coupling the UI to storage details.
2. Update `MainLayout` to display the active database, switch status, and quick-switch action.
3. Implement the startup modal that shows the resolved active profile and offers continue/switch/create behavior.
4. Extend `SettingsPage` with a new Data Sources tab and database-profile editor/list surfaces.
5. Add SQLite forms for managed create, open/import external file, and existing managed-profile selection.
6. Add PostgreSQL forms for connection metadata, connection testing, and empty DB creation.
7. Add override-locked messaging/disabled controls when the app is running under explicit runtime override configuration.
8. Add component and Playwright coverage for the startup modal, active badge/switcher, and settings Data Sources flows.

## Scope Exceptions

- Clone/snapshot/IPFS flows may land as follow-on panels or actions in subbundle 08 if the UI would otherwise become too large to validate safely in one phase.
- This subbundle assumes the backend/runtime behavior is already correct; if browser proof exposes backend weaknesses, reopen the owning earlier subbundle.

## Do Not Do

- Do not expose a switch control that calls unfinished or unproven backend contracts.
- Do not hide override-lock behavior; make it explicit when the UI cannot switch because startup config owns the provider.
- Do not ship a Data Sources UI that only supports one provider while claiming provider parity.

## Acceptance Checklist

- The active database is visible globally without entering settings.
- The startup modal clearly names the current database/profile and offers continue/switch/create behavior.
- Settings contains a dedicated Data Sources surface for SQLite and PostgreSQL profile management.
- SQLite and PostgreSQL forms are readable and validate input sensibly.
- Explicit runtime override mode is clearly surfaced and prevents dishonest switching.
- Component and browser tests cover the major happy-path and locked-mode flows.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~Settings|FullyQualifiedName~Database|FullyQualifiedName~Startup|FullyQualifiedName~Layout"`
- `dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --filter "FullyQualifiedName~Settings|FullyQualifiedName~Database|FullyQualifiedName~Startup|FullyQualifiedName~Layout"`
- Capture screenshots for:
  - startup modal
  - top-bar active database indicator/switcher
  - settings Data Sources tab (desktop)
  - responsive/narrower-width follow-up if the dialog or editor wraps
- Record explicit screenshot review notes in the execution report.

## Browser Validation Logging

- Target routes: `/`, `/settings`, and any route used to demonstrate override-locked mode.
- Required viewport passes: `1600x1000` desktop first, `1100x900` responsive follow-up.
- Required actions: start on last-used profile, observe startup modal, continue once, reopen and switch profile, inspect active badge, open Data Sources tab, create/test forms, verify locked-mode messaging when applicable.
- Required evidence paths: `evidence/db-switch-startup-modal-desktop.png`, `evidence/db-switch-topbar-switcher-desktop.png`, `evidence/db-switch-settings-data-sources-desktop.png`, `evidence/db-switch-responsive-followup.png`.
- Screenshot review questions:
  - Is the active DB visible and understandable?
  - Are provider-specific forms readable without overflow/clipping?
  - Is locked-mode behavior obvious when switching is disabled?

## Progression Gate

- Desktop and responsive browser proof for the startup modal and Data Sources UI must exist before subbundle 08 extends the surface with clone/snapshot actions.
- The execution report must include reviewed screenshots, not just raw files.

## Suggested Agent Prompt

```text
Implement subbundle 07 only.

Expose the finished runtime database profile system in the UI:
- startup continue/switch/create modal
- global active database badge/switcher
- Settings Data Sources tab for SQLite and PostgreSQL
- override-locked messaging
- component and Playwright proof with reviewed screenshots

Do not fake backend behavior; reopen earlier subbundles if browser proof shows a foundation is weak.
```
