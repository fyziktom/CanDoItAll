# 02 Core Runtime Bottleneck Repair

## Status

- Status: `Completed`

## Objective

Replace expensive per-active-run full-detail reads with a batched runtime summary read model and reduce repeated AgentFramework execution scans.

## Covered Inputs

- N001: Multiple active process runs make the UI page slow.
- N004: Repair blockers or bottlenecks.
- N005: Measure core-side performance after optimization.
- N007: Do not break process functionality.

## Prerequisites

- `01-01-current-state-and-measurement` closure gate passed.
- Baseline timing exists in `reviews/01-execution-report.md`.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunDetailsLoader.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.RuntimeReadQuery.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.RuntimeReadQuery.Support.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessRuntimeViewModels.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRuntimeReadQueryServiceTests.cs

## Deliverables

- Strongly typed batched active-run health metrics.
- Active summary loader uses the batched process metrics instead of full run details per active run.
- Active summary loader performs a single bounded execution-run list operation for the active run set.
- Targeted test coverage for active-run metrics.

## Dependency Impact

- This subbundle is a critical foundation for UI timing. If it fails, browser measurements will mainly prove the old bottleneck.

## Validation Depth

- Targeted integration or unit tests around the new read model.
- Core stopwatch comparison against the baseline scenario.

## Implementation Steps

1. Add a compact active-run metrics record keyed by process run id.
2. Add service/read-query API to load metrics for a collection of run ids.
3. Update `LoadActiveRunSummariesAsync` to group active runs, execution runs, agents, and metrics without per-run full details.
4. Add focused tests for metrics and summary mapping.
5. Record after-timing and command proof.

## Do Not Do

- Do not change process step transition semantics.
- Do not remove selected-run detailed execution evidence.
- Do not hide errors from the read model.

## Acceptance Checklist

- Active summaries no longer call `GetRunDetailsAsync` per active run.
- Existing active-run summary fields still resolve.
- Targeted tests pass.
- After-timing is recorded.

## Proof Required

- Code references and test output.
- Before and after core timing row in execution report.

## Browser Validation Logging

- N/A for this subbundle; browser proof belongs to final closure after UI refresh changes.

## Progression Gate

- Passed. Targeted tests pass and the active-run summary timing improved from `239 ms` to `60 ms` for the same 12-run integration scenario.

## Closure Proof

- Added `ProcessActiveRunHealthMetrics` to carry only the active-run counters and step titles needed by the live strip.
- Added `IProcessRuntimeReadQueryService.GetActiveRunHealthMetricsAsync` and `ProcessesService.GetActiveRunHealthMetricsAsync`.
- Updated `ProcessWorkspaceRunDetailsLoader.LoadActiveRunSummariesAsync` to use one batched metrics query and one bounded execution-run scan.
- Kept selected-run `LoadAsync` detail behavior intact.
- Targeted command passed: `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter FullyQualifiedName~ProcessActiveRunSummaryPerformanceTests -v:minimal --logger "console;verbosity=detailed"`.

## Suggested Agent Prompt

Implement the batched active-run summary read model and update the loader. Keep the selected-run detail path intact, then run focused process tests and record timing.
