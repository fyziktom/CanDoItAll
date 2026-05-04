# lazy-run-detail-loading

## Status

- `Completed`

## Objective

Prevent the process page from loading full selected-run details until the Runs tab or a direct `runId` query actually needs them.

## Covered Inputs

- N006, N007
- R005, R006

## Prerequisites

- `01-runtime-state-overview-service` completed and trusted.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Loading.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.LiveRefresh.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunDetailsLoader.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.RunsPresenter.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsTab.razor`

## Deliverables

- `LoadWorkspaceAsync` does not load full run details unless `detailTab == "runs"` or `RunIdQuery` is present and valid.
- Selecting the Runs tab triggers detail loading when a run is focused.
- Active run summaries avoid full per-run detail loading when lightweight run/step/outbox projections are enough.
- Runtime refresh refreshes only the data required by the current tab/focus.

## Dependency Impact

- Subbundle 03 depends on the reload behavior so stop actions refresh badges/list state without causing expensive unnecessary detail reloads.

## Validation Depth

- Critical UI foundation with code/test proof and browser page-open proof.

## Implementation Steps

1. Introduce an explicit predicate for when selected-run details are needed.
2. Stop default-selecting the first run for non-Runs tab page loads unless a `runId` query exists.
3. Load details when switching to Runs tab or selecting/opening a run.
4. Change active summary loading to use lightweight status/outbox/step count projections from the runtime state service when possible.
5. Ensure refresh loops keep active monitoring but do not load details for inactive UI surfaces.

## Scope Exceptions

- Deep query-level profiling is out of scope unless tests expose regressions. The goal is removing known eager detail calls.

## Do Not Do

- Do not hide run history or launch data to make loading appear faster.
- Do not add silent fallback data if a detail load fails.
- Do not break direct `runId` route behavior.

## Acceptance Checklist

- Opening processes page without `runId` and without Runs tab active does not call full run details loader.
- Opening Runs tab or selecting a run loads details as expected.
- Direct `runId` query still focuses the run and opens the Runs tab.
- No Blazor error UI appears after tab changes.

## Proof Required

- Focused test if practical for load gating.
- Targeted integration tests remain green.
- Browser navigation proof to processes page without opening Runs tab and then with Runs tab.

## Browser Validation Logging

- Route: `https://localhost:7271/processes`.
- Viewport: large desktop.
- Actions/assertions: navigate, confirm page renders, open Runs tab, select/open a run, confirm details appear only after need.
- Screenshots: record page and Runs tab if browser is available.

## Progression Gate

- Downstream work may continue only if initial page load no longer performs full selected-run detail loading by default and direct run routes still work.

## Suggested Agent Prompt

```text
Implement subbundle 02 only: make full run detail loading lazy while preserving run list, badges, direct runId focus, and runtime refresh behavior.
```
