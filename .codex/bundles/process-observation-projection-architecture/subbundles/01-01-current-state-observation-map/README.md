# 01-current-state-observation-map

## Status

- `Completed`

## Objective

- Produce a verified map of how the current Processes page observes runtime state, what lazy-loading/performance protections already exist, and where overload risks remain before any architecture code is changed.

## Success Criteria

- The existing Processes page refresh loop, runtime pane loading, active-run summary path, selected-run detail path, analytics path, outbox/escalation/AgentFramework reads, and canvas refresh path are documented with source references.
- Current optimizations are preserved as explicit constraints for later subbundles.
- Baseline commands and any existing performance measurements are captured in `reviews/01-execution-report.md`.
- No production implementation is performed in this subbundle.

## Covered Inputs

- R-001 through R-012 as discovery inputs.
- Codeanalytics snapshot `snap-20260508224200-0d8ff021`.
- Performance scan in `analysis/03-performance-scan.md`.
- Previous bundle context for `process-runtime-ui-performance` and `process-runtime-execution-performance-review`.
- Microsoft Learn Blazor and `IMemoryCache` guidance recorded in `inputs/01-source-artifacts.md`.

## Prerequisites

- none

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Pages\ProcessesPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.LiveRefresh.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Loading.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunDetailsLoader.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.RuntimeReadQuery.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.RuntimeReadQuery.Support.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessRuntimeStateOverviewService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessOperatorControlPlane.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessOutbox.cs`

## Deliverables

- A current-state observation map committed into this bundle or an implementation execution note.
- A list of current behavior that must not regress:
  - tab-aware runtime pane loading
  - active-run summary path avoiding full details for all runs
  - selected-run scoped detail enrichment
  - capped AgentFramework execution scan
  - `AsNoTracking` runtime read-model queries
  - analytics loading only when visible or requested
- A baseline test/performance record for later comparison.

## Dependency Impact

- `02-observation-contracts-and-boundary` depends on this map to avoid designing contracts around guessed UI state.
- `03-projection-cache-and-invalidation` depends on the read-source inventory to choose correct invalidation points.
- `04-ui-observation-shell-and-dialogs` depends on this map to preserve current Processes page behavior.
- Weak proof here invalidates all later performance claims.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Inspect the source references and update the current-state map with concrete method/property names and caller relationships.
2. Confirm UI libraries by scanning project files for BaseLib, CanvasLib, Tailwind, and Radzen usage.
3. Re-run or refresh the targeted performance anti-pattern scan from `analysis/03-performance-scan.md` if source changed since bundle preparation.
4. Identify every process runtime data source the future dashboard will need to observe.
5. Record existing optimizations that later phases must preserve.
6. Run targeted baseline tests only if they are already available and reasonably scoped.
7. Update `reviews/01-execution-report.md` with findings, commands, and any baseline numbers.

## Scope Exceptions

- This subbundle does not introduce new contracts, cache code, UI changes, or AI intent code.
- It may recommend extra tests but should not create them unless a tiny characterization test is needed to freeze current behavior.

## Do Not Do

- Do not refactor `ProcessWorkspace`.
- Do not add `IMemoryCache`.
- Do not change process runtime behavior.
- Do not build new dashboard UI.
- Do not remove existing direct read paths.

## Acceptance Checklist

- Current-state map covers page refresh, loading, details, summaries, analytics, outbox, AgentFramework, escalations, and canvas refresh.
- The map identifies the existing overload risks.
- The map identifies existing performance protections to preserve.
- Commands and proof are recorded in the execution report.
- No production behavior changes were made.

## Proof Required

- `git diff --stat`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessActiveRunSummaryPerformanceTests|FullyQualifiedName~ProcessRuntimeReadQueryServiceTests"` when feasible.
- A short written baseline in `reviews/01-execution-report.md`.

## Browser Validation Logging

- N/A. This subbundle is analysis-only and must not affect browser-visible behavior.

## Progression Gate

- Downstream subbundles may continue only when the current-state map names the concrete source files and preserved optimizations, and the execution report records either successful baseline commands or explicit reasons they were not run.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
