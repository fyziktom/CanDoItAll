# 02-observation-contracts-and-boundary

## Status

- `Ready`

## Objective

- Create the typed, read-only observation contract layer that future Processes UI, dialogs, cache, and AI intent handling will consume.

## Success Criteria

- A new observation service contract exists and does not expose mutations.
- Snapshot, query, key, revision, staleness, and dialog descriptor models are strongly typed.
- Contracts are generic to process runtime concepts and do not encode app-specific workflow semantics.
- Existing Processes page behavior remains unchanged unless this subbundle intentionally wires a no-op/read-only adapter.

## Covered Inputs

- R-001, R-002, R-003, R-004, R-007, R-009, R-012.
- Current-state map from subbundle `01`.
- Architecture target in `architecture/01-target-solution.md`.

## Prerequisites

- `01-current-state-observation-map` is complete and its progression gate is passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.RuntimeReadQuery.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.RuntimeReadQuery.Support.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessRuntimeStateOverviewService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunDetailsLoader.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesModuleServiceCollectionExtensions.cs`

## Deliverables

- `IProcessObservationService`.
- `IProcessObservationInvalidator` interface or placeholder contract needed by subbundle `03`.
- Query/key models for dashboard, run, stage, timeline, and dialog reads.
- Snapshot models for dashboard cards, run summaries, stage summaries, timeline items, dialog descriptors, revision, and staleness.
- DI registrations for the contract implementation when introduced.
- Unit tests or compile-time tests that prove the contract is read-only and generic.
- New files should be placed under a clear boundary such as `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\Observation` or `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\Observation`.

## Dependency Impact

- `03-projection-cache-and-invalidation` uses the query/key models as cache keys.
- `04-ui-observation-shell-and-dialogs` uses snapshots and descriptors as UI input.
- `05-ai-driven-dashboard-intent-bridge` uses typed focus targets and dialog descriptors.
- Weak contracts will leak component state or process mutation into every later phase.

## Validation Depth

- `Critical architecture foundation`

## Implementation Steps

1. Review the current-state map and identify the minimum snapshot shapes needed for current behavior plus future dashboard observation.
2. Add immutable query, key, snapshot, revision, staleness, and dialog descriptor records.
3. Add `IProcessObservationService` with cancellable async read methods.
4. Add a read-only implementation shell only if needed to compile tests; keep behavior delegated to existing read services.
5. Add or update DI registration in `ProcessesModuleServiceCollectionExtensions`.
6. Add tests for query normalization, key equality, and read-only contract behavior.
7. Build and run targeted tests.
8. Update the execution report with files changed, tests, and any contract tradeoffs.

## Scope Exceptions

- Full projection cache implementation belongs to subbundle `03`.
- Full UI migration belongs to subbundle `04`.
- AI intent translation belongs to subbundle `05`.

## Do Not Do

- Do not move current page refresh behavior yet.
- Do not introduce SignalR.
- Do not add write methods to observation contracts.
- Do not encode agent names, process definition names, or app-specific QA/development stages as hard-coded logic.
- Do not add XML documentation comments.

## Acceptance Checklist

- Observation contracts compile.
- Models are strongly typed and immutable.
- All read methods accept `CancellationToken`.
- No observation interface exposes process mutations.
- Existing tests still pass.
- New tests cover key/query equality or normalization where relevant.

## Proof Required

- `dotnet build CanDoItAll.slnx`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessRuntimeReadQueryServiceTests|FullyQualifiedName~ProcessRuntimeOperatorReadModelTests"`
- New contract test command, if new tests are added.

## Browser Validation Logging

- N/A unless implementation touches visible UI. If visible UI changes occur, use the `04` browser proof requirements before closing.

## Progression Gate

- Downstream subbundles may continue only when the observation contracts compile, tests pass, and review confirms no mutation methods, component state types, or app-specific process semantics leaked into the contract.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
