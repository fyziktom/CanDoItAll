# 01-01 Performance Scan And Hot-Path Baseline

## Status

- `Completed`

## Objective

Run the .NET performance-pattern scan against the Processes module and pick only concrete hot-path findings worth changing.

## Covered Inputs

- N001, N002, N003, N007

## Prerequisites

- Raw request exists in `C:\repositories\CanDoItAll\.codex\bundles\process-runtime-execution-performance-review\inputs\00-original-request.md`.
- Processes module project exists at `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\CanDoItAll.Modules.Processes.csproj`.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.Runtime.RunStart.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.Runtime.StepTransitions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessRuntimeProgressionPlanner.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunDetailsLoader.cs`

## Scope

- Run scan recipes from the performance skill.
- Record exact counts.
- Classify hot-path candidates conservatively.

## Dependency Impact

- Downstream code edits must stay limited to findings selected here.
- If another hot path is found later, reopen this subbundle and update the scan rationale before editing.

## Validation Depth

- Manual scan plus code analytics snapshot.
- No production code edits in this phase.

## Implementation Steps

1. Build scoped code analytics snapshot.
2. Run PowerShell equivalents of the performance scan recipes.
3. Inspect hits in runtime and dispatch paths.
4. Record high-confidence findings and lower-priority non-findings.

## Scope Exceptions

- Broad LINQ counts are not automatically actionable; only hot-path LINQ patterns with measurable repeated work are eligible.

## Do Not Do

- Do not rewrite all LINQ or all allocations.
- Do not make UI changes based on runtime scan counts alone.

## Acceptance Checklist

- [x] Scan counts recorded.
- [x] Hot-path decision recorded.
- [x] Lower-priority findings documented.

## Proof Required

- Scan checklist in `analysis/01-current-state.md`.
- Execution report command row for scan completion.

## Browser Validation Logging

- N/A. This subbundle does not change browser-visible behavior.

## Progression Gate

- `Passed`: runtime start repeated scan and assignment resolver allocation are concrete enough for subbundle 02.

## Suggested Agent Prompt

Use the scan counts and current-state analysis to implement only the runtime-start repeated-scan repair. Preserve generic process behavior and do not add stack-specific logic.
