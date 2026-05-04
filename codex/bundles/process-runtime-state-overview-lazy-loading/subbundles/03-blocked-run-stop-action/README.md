# blocked-run-stop-action

## Status

- `Completed`

## Objective

Add a service-backed UI action in the selected process Runs tab to stop blocked process runs by explicitly cancelling them.

## Covered Inputs

- N005
- R007, R008

## Prerequisites

- `01-runtime-state-overview-service` completed.
- `02-lazy-run-detail-loading` completed or verified not to conflict with runtime reloads.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.RuntimeOperations.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.RunsPresenter.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsLifecycleSection.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.Runtime.Operations.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Persistence\Entities\ProcessRuntimeModels.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs`

## Deliverables

- Explicit service method to stop/cancel blocked runs.
- Validation failure for missing or non-blocked runs.
- Durable journal entry for the stop decision.
- UI button/action on blocked run list items in the Runs tab.
- Runtime state reload after successful stop.

## Dependency Impact

- Final proof depends on this behavior because it closes the direct user request for stopping blocked processes.

## Validation Depth

- Process-critical closure with integration-test and browser-visible UI proof.

## Implementation Steps

1. Add a `ProcessRunStopRequest` or equivalent strongly typed request.
2. Implement stop/cancel service logic with status validation, timestamps, journal entry, and project-structure sync.
3. Expose the operation through `ProcessWorkspace` and `ProcessWorkspaceRunsTabPresenter`.
4. Add a stop button to blocked run history items only.
5. Reload state through the projection service after success.
6. Add tests for success and failure cases.

## Scope Exceptions

- Bulk stop-all-blocked action is out of scope unless the user asks for it. This subbundle adds per-run stop in the list.

## Do Not Do

- Do not delete runs.
- Do not stop active or failed runs through this blocked-run action.
- Do not skip audit journal creation.

## Acceptance Checklist

- Blocked run list item shows a stop action.
- Non-blocked run list items do not show the blocked-run stop action.
- Stopped run becomes `Cancelled`.
- Late step transitions are rejected by existing terminal guards.
- Counts refresh after stop.

## Proof Required

- Integration test for stopping a blocked run.
- Integration test for rejecting stop of non-blocked run.
- Browser proof that the action appears in the Runs tab when blocked run data is present or documented dataset blocker.

## Browser Validation Logging

- Route: `https://localhost:7271/processes`.
- Viewport: large desktop.
- Actions/assertions: open Runs tab, inspect run history, confirm stop action on blocked rows.
- Screenshots: record blocked-run list action if local data has blocked runs.

## Progression Gate

- Final validation may start only after stop behavior is service-proven and UI-visible or an explicit local-data blocker is documented.

## Suggested Agent Prompt

```text
Implement subbundle 03 only: add explicit blocked-run cancellation service behavior and surface a stop action for blocked rows in the selected process Runs tab.
```
