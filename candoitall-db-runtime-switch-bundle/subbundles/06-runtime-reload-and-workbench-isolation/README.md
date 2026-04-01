# 06 Runtime Reload and Workbench Isolation

## Status

- `Completed`

## Objective

- Guarantee that a runtime database switch reloads active browser circuits safely, isolates workbench/browser state per profile, and recovers gracefully from stale artifact routes.

## Covered Inputs

- `RQ-004` runtime switch without restart
- `RQ-006` reload all active modules/routes safely
- `RQ-007` profile-isolated workbench state
- `RQ-021` component coverage for stale-artifact fallback
- `RQ-022` browser proof for runtime switching
- Raw notes `N-02`, `N-09`, `N-10`, `N-11`

## Prerequisites

- `subbundles/03-dynamic-runtime-db-and-bootstrap` completed with runtime switch notifications.
- `subbundles/05-storage-isolation-and-managed-files-serving` completed so file-backed routes do not point at the wrong profile.
- `subbundles/04-migrations-and-legacy-upgrade-path` stable enough that switched profiles can be opened reliably.

## Exact Source References

- `C:\repositories\CanDoItAll/src/CanDoItAll.Web/Infrastructure/BrowserWorkspaceStateStore.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.SharedKernel/WorkbenchTabState.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/WorkbenchTabState.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Web/Components/Layout/MainLayout.razor`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Web/wwwroot/js/browserState.js`
- `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Unit/WorkbenchStateServiceTests.cs`
- `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Playwright/PlaywrightAppFixture.cs`

## Deliverables

- Profile-aware browser storage-key computation for workbench state.
- Workbench session snapshot/profile metadata updates.
- Browser/server notification path that forces current and other tabs/circuits to reload safely after a switch.
- Safe stale-artifact fallback behavior for project structure/calendar and other artifact-first routes.
- Optional dirty-tab warning/confirmation behavior before switching when unsaved work would be lost.
- Unit, component, integration, and Playwright proof for profile isolation and stale-route recovery.

## Dependency Impact

- The user explicitly called out “reload all running modules/services with new data”; this phase is the core of that requirement.
- The UI in subbundle 07 is unsafe unless this phase proves that switching will not leave broken tabs or stale local-storage state behind.
- Final clone/versioning proof in subbundle 08 must reuse the same runtime reload path.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Refactor `BrowserWorkspaceStateStore` so the storage key is computed from `WorkbenchOptions.BrowserStorageKey` plus the active profile or fingerprint.
2. Extend `WorkbenchSessionSnapshot` and related restore logic with profile metadata and version compatibility handling.
3. Add browser-side switch notification support (storage event or `BroadcastChannel`) so other tabs detect profile changes.
4. Update `MainLayout` or equivalent shell-scoped service to react to switch notifications by navigating to a safe route with `forceLoad: true`.
5. Update artifact-loading services/pages to return safe not-found/recover states instead of throwing when the referenced project/artifact does not exist in the new DB.
6. Add dirty-tab handling if the workbench reports unsaved state that would be lost on switch.
7. Add unit/component/browser tests proving isolated restore keys, safe stale-route fallback, and cross-tab reload behavior.

## Scope Exceptions

- The visible switch controls themselves land in subbundle 07.
- This subbundle may implement shared shell/runtime services used by the UI, but it should not complete the user-facing layout work yet.

## Do Not Do

- Do not leave the local-storage key global and still claim profile isolation is done.
- Do not rely on page-local refresh logic alone; other browser tabs must also react.
- Do not claim safe switching if stale artifact routes still throw or show the Blazor error UI.

## Acceptance Checklist

- Each database profile has its own browser workbench storage namespace.
- A switch in one open page causes the current page and at least one second page/tab to reload safely.
- Project structure/calendar routes recover gracefully when the target project does not exist in the new profile.
- Unsaved/dirty work is not silently discarded without at least a warning if dirty-tab handling is in scope.
- Browser and component tests exist for stale-route recovery and workbench key isolation.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~Workbench|FullyQualifiedName~DatabaseSwitch|FullyQualifiedName~BrowserState"`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProjectStructure|FullyQualifiedName~ProjectCalendar|FullyQualifiedName~Workbench|FullyQualifiedName~Database"`
- `dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --filter "FullyQualifiedName~Switch|FullyQualifiedName~Workbench|FullyQualifiedName~Structure|FullyQualifiedName~Calendar"`
- Capture screenshots of a stale artifact route before and after switching and a second page/tab reacting to the same switch.
- Record local-storage key evidence or DOM assertions that prove profile isolation.

## Browser Validation Logging

- Target routes: `/projects/{projectId}/structure`, `/projects/{projectId}/calendar`, and a second open page such as `/projects` or `/validation`.
- Required viewport passes: `1600x1000` desktop and `1100x900` follow-up if warning/modals wrap.
- Required actions: open artifact route in profile A, open a second page, switch to profile B where the artifact does not exist, assert safe fallback/no error UI, verify second page reloads, inspect local-storage key(s).
- Required evidence paths: `evidence/db-switch-stale-artifact-recovery-desktop.png`, `evidence/db-switch-cross-tab-desktop.png`, `evidence/db-switch-stale-artifact-responsive.png`.
- Screenshot review questions:
  - Is there any Blazor error UI or unhandled exception surface?
  - Is the fallback route/message understandable?
  - Is the active profile change obvious after reload?

## Progression Gate

- Browser proof for stale-route recovery and cross-tab reload must exist before subbundle 07 exposes the switch UI broadly.
- Unit/component proof for profile-isolated workbench state must exist before subbundle 08 claims runtime switching is trustworthy.

## Suggested Agent Prompt

```text
Implement subbundle 06 only.

Finish the runtime reload contract:
- profile-aware workbench/localStorage keys
- switch notifications across browser tabs
- force-load safe route after switch
- stale artifact fallback UI/service handling
- tests and browser proof for multi-tab reload + stale-route recovery

Do not finish the visible switcher UI yet.
Record screenshots and localStorage evidence honestly.
```
