# SB14 Process Run Artifact Browsing

## Status

- `Completed`

## Objective

- Browse authorized managed/output/product artifacts from process-run history with Processes-owned root policy and always-current behavior.

## Covered Inputs

- N008, N010-N014; R020, R024-R030.

## Prerequisites

- SB13 Completed; Process and backbone foundations trusted.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchArtifactContracts.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/WorkspaceProcessLaunchArtifactInitializer.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRunArtifactRootPolicy.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/WorkspaceProcessRunArtifactPathTests.cs`
- `repo://tests/Playwright/CanDoItAll.Tests.Playwright/ProcessShellSmokeTests.cs`

## Deliverables

- Processes-owned pure run-root resolution policy covering managed/output/product roots from current run/launch data.
- `IProcessRunFileScopeProvider` implementation and focused `ProcessRunFilesDialog`/coordinator.
- Dashboard retains only current run ID/open-close state; no browser behavior added to 2,888-line component.
- Host and session caching Disabled; re-enumerate on open/refresh and observe agent/external mutation.
- Workbench consumes Processes policy if needed; Processes never references Workbench.

## Dependency Impact

- SB15/SB17 depend on correct live-source and ownership behavior.

## Validation Depth

- Proof tier: `Behavioral`.
- Live mutable artifact story.

## Implementation Steps

1. Characterize managed/output/product root derivation and Workbench projection behavior.
2. Extract/move pure policy to Processes.Application or smallest correct existing process project.
3. Implement scope provider/dialog/coordinator with Disabled policies.
4. Add direct policy/provider tests and dashboard component tests.
5. Prove live file creation/replacement after initial browse plus unauthorized root negative.
6. Run desktop Playwright/visual/console and C# gate.

## C# Architecture Impact

- Responsibility movement from Workbench/spread launch knowledge to Processes-owned policy; focused UI extraction from dashboard.

## Boundary Ownership

- Processes owns run semantics; Infrastructure browses; integration authorizes; module UI renders.

## Dependency Direction

- Workbench may reference Processes.Application already; Processes must not reference Workbench. Refresh graph.

## Pattern Decision

- Cohesive policy/service is sufficient; no Strategy/factory unless multiple real root policies emerge.

## Testability Contract

- Root policy/provider direct tests without dashboard/runtime host; browser proves real wiring/mutation.

## Partial Class Policy

- No dashboard partial/facade that hides behavior.

## Architecture Proof Required

- Ownership move/source assertions, direct tests, dashboard responsibility delta, dependency/cycle, Disabled path call count, C# gate.

## Scope Exceptions

- No cache for live run roots and no generalized process history redesign.

## Do Not Do

- Do not infer authority from launch path, cache agent-written folders, duplicate process policy in Workbench, or add browser logic to dashboard.

## Acceptance Checklist

- [x] Managed/output/product roots resolve from current run data.
- [x] Unauthorized/escaped roots fail.
- [x] New/replaced file is visible next open/refresh.
- [x] Dashboard stays thin; dependency direction correct.
- [x] Desktop browser/console/C# gate pass.

## Proof Required

- Behavioral policy/provider/component/browser proof, live mutation and unauthorized negatives, source/dependency/Disabled assertions, DOM/screenshots/review.

## Browser Validation Logging

- Routes `/processes/live` and project-scoped live route if applicable; `1900x1200`, `1440x900`.
- Open run details/files, switch roots, search/open, mutate fixture on server, refresh and observe, close/reopen, error/no-files state.
- Assert scroll/overlay/dialog, freshness, zero unexpected console/page/network errors.

## Progression Gate

- Passed 2026-07-13. Processes ownership, Disabled freshness, UI, lifetime, dependency, and C# gates pass; SB15 is unlocked.

## Closure Evidence

- `bundle://proof/SB14/behavioral-proof.md`
- Focused unit `17/17`, affected lifetime/authority unit `43/43`, component `3/3`, integration `8/8`.
- Warning-as-error Release Web build: 0 warnings, 0 errors.
- Managed browser proof at 1900x1200 and 1440x900 covers real run detail entry, current managed root, read-only open, live creation/replacement, refresh, console/network review, and fixture cleanup.

## Reopen Triggers

- Stale run listing, wrong root/run, Workbench reverse ownership, dashboard growth, or path authorization defect reopens SB14 and downstream live-source proof.

## Suggested Agent Prompt

```text
Implement only always-current process-run artifact browsing. Put root policy in Processes, keep the dashboard thin, disable both host cache and session retention, and prove live mutation plus unauthorized-root failure at desktop.
```
