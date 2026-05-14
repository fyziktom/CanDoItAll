# 01 Current State And Measurement

## Status

- Status: `Completed`

## Objective

Confirm the current process runtime and UI observation path, capture baseline core timing, and decide the first repair target from evidence.

## Covered Inputs

- N001: Process UI is slow when multiple process runs are active.
- N002: Visual Studio adds overhead but app behavior still needs improvement.
- N003: Analyze process runs and UI observation deeply.
- N005: Measure core-side performance before optimization.

## Prerequisites

- Bundle readiness gate is prepared.
- Process module source references are available in the repo.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.LiveRefresh.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Loading.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunDetailsLoader.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.RuntimeReadQuery.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.Dispatch.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Execution\AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Storage\FileSandboxWorkspaceExecutionSliceStore.cs

## Deliverables

- Baseline core timing recorded in `reviews/01-execution-report.md`.
- Confirmation of the first bottleneck to repair.
- Any measurement helper or test added in a maintainable form.

## Dependency Impact

- This subbundle unlocks the core repair. If baseline evidence is missing, later improvement claims are untrustworthy.

## Validation Depth

- Source review plus at least one timed core scenario.
- Prefer stopwatch output around active-run summary loading or a targeted process read-model test.

## Implementation Steps

1. Reopen the listed source files and confirm the current call graph.
2. Run a core timing scenario before optimization.
3. Record the baseline timing and bottleneck interpretation.
4. Update this subbundle and execution report gate rows.

## Do Not Do

- Do not change runtime dispatch behavior in this phase.
- Do not start browser validation before the core repair exists.

## Acceptance Checklist

- Baseline timing row is filled.
- Bottleneck decision names the exact method or call path.
- Downstream subbundle can proceed without guessing.

## Proof Required

- Stopwatch timing output.
- Current-state notes in `analysis/01-current-state.md` or execution report.

## Browser Validation Logging

- N/A: this subbundle does not change browser-visible behavior.

## Progression Gate

- Passed. Baseline timing was `239 ms` for `LoadActiveRunSummariesAsync` over 12 active runs, and the first repair target was `ProcessWorkspaceRunDetailsLoader.LoadActiveRunSummariesAsync`.

## Closure Proof

- `ProcessWorkspaceRunDetailsLoader.LoadActiveRunSummariesAsync` performed one `ListExecutionRunsAsync` and one `GetRunDetailsAsync` per active run before the repair.
- `ProcessWorkspace.LiveRefresh.RefreshRuntimeWorkspaceAsync` loaded analytics on every runtime refresh even when the Runs tab was active.
- Baseline command: `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter FullyQualifiedName~ProcessActiveRunSummaryPerformanceTests -v:minimal --logger "console;verbosity=detailed"`.
- Baseline output: `LoadActiveRunSummariesAsync elapsed: 239 ms for 12 active runs.`

## Suggested Agent Prompt

Measure active process observation before changing code. Record exact command, scenario, elapsed time, and the call path that explains the cost.
