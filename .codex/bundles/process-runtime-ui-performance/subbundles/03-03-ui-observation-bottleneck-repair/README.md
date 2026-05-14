# 03 UI Observation Bottleneck Repair

## Status

- Status: `Completed`

## Objective

Reduce unnecessary work in the Blazor live refresh loop after the core read path is cheaper.

## Covered Inputs

- N001: Process UI is slow with multiple active process runs.
- N004: Repair blockers or bottlenecks.
- N007: Do not break process functionality.

## Prerequisites

- `02-02-core-runtime-bottleneck-repair` closure gate passed.
- Active summary loader uses the batched read model.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.LiveRefresh.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Loading.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsTab.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsActiveSection.razor

## Deliverables

- Runs-tab refresh does not reload analytics when only Runs data is visible.
- Refresh behavior still updates analytics when the Analytics tab is active.
- No unnecessary component or state churn is introduced.

## Dependency Impact

- This subbundle prepares browser timing. If the UI still refreshes unrelated analytics, route timing can remain noisy.

## Validation Depth

- Source review plus build or targeted component coverage if available.
- Browser validation happens in the next subbundle.

## Implementation Steps

1. Update refresh logic to load analytics only for the Analytics tab.
2. Confirm runtime pane data still refreshes on Runs and Analytics routes as intended.
3. Run focused build or process tests.
4. Update report rows.

## Do Not Do

- Do not redesign the process workspace UI.
- Do not change tab contents or navigation behavior without measured need.

## Acceptance Checklist

- Runs-tab live refresh avoids hidden analytics load.
- Analytics-tab behavior still refreshes analytics.
- Code compiles and targeted tests still pass.

## Proof Required

- Code diff and test or build output.

## Browser Validation Logging

- Browser logging deferred to final closure subbundle.

## Progression Gate

- Passed. Runs-tab refresh skips hidden analytics work, targeted integration test passes, and the full solution build passes with zero warnings.

## Closure Proof

- Updated `ProcessWorkspace.LiveRefresh.RefreshRuntimeWorkspaceAsync` so analytics refresh runs only when `DetailTabAnalytics` is active.
- Runtime overview and runtime pane data still refresh for Runs and Analytics according to the existing refresh-loop gate.
- Validation commands passed:
  - `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter FullyQualifiedName~ProcessActiveRunSummaryPerformanceTests -v:minimal --logger "console;verbosity=detailed"`
  - `dotnet build CanDoItAll.slnx -v:minimal`

## Suggested Agent Prompt

Trim the ProcessWorkspace refresh loop to update only the visible runtime surfaces. Keep behavior explicit and preserve analytics refresh on the Analytics tab.
