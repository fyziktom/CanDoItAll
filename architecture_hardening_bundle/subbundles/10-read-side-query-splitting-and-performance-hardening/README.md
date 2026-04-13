# Read-side query splitting and performance hardening

## Status

- `Completed`
- `2026-04-13`: `ProcessesService` now delegates definition-list and runtime read aggregation to focused query services, definition/run summary queries no longer load whole versions/roles/steps/runs tables by default, analytics now project only the scalar fields they need, and the targeted build/integration/MCP proof passed.

## Objective

- Split broad query logic into focused projection/query services and reduce unnecessary in-memory aggregation or full-graph loading for common read surfaces.

## Covered Inputs

- `U003` Performance and maintainability concerns.
- `BRQ-011` Read-side query hardening.
- `F007` Read-side small-data assumption.

## Prerequisites

- `09-runtime-state-machine-and-transition-policy-extraction` passed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Reads.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeViewModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureAssemblyService.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessImportMetadataIntegrationTests.cs

## Deliverables

- Focused query/projection services for definition list, editor load, run detail, analytics, or equivalent read seams.
- Reduced avoidable broad-load and in-memory aggregation patterns in the common read paths.
- Proof that read-side behavior stayed correct after the split.

## Dependency Impact

- Gate C depends on this phase to confirm the module is moving toward healthier query shapes.
- Later UI decomposition benefits from cleaner read surfaces.

## Validation Depth

- `High`

## Implementation Steps

1. Identify the most expensive or broad current read paths and give them focused query-service ownership.
2. Shape projections as close to the database as practical without creating a second canonical store.
3. Keep the public read surface compatible where that reduces churn.
4. Add or update tests that prove summary counts, detail loads, and analytics remain correct.

## Scope Exceptions

- This phase is not a full performance-program initiative; it is a focused hardening of obviously broad read shapes.
- Do not add speculative caching unless proof shows it is needed.

## Do Not Do

- Do not create a shadow canonical read model with mutation logic.
- Do not optimize obscure queries while leaving the main broad-load patterns untouched.
- Do not hide broad loads behind new helper names.

## Acceptance Checklist

- Common read surfaces now have clearer query ownership.
- Obvious broad in-memory aggregation patterns are reduced or removed.
- Read-side behavior remains correct after the split.
- The query layer remains projection-only.

## Proof Required

- Focused integration tests proving query correctness.
- Any query-shape notes or profiling observations recorded in the execution report.
- Evidence that the main broad-load hotspots were actually addressed.

## Browser Validation Logging

- N/A unless query changes require visible workspace proof during execution.

## Progression Gate

- The read side has clearer projection ownership, the main broad-load assumptions are reduced, and proof shows the split improved shape rather than just moving code.

## Suggested Agent Prompt

```text
Implement only subbundle 10. Split the main Process read paths into focused query/projection services, reduce obvious broad-load patterns, preserve correctness, and stop before template consolidation or workspace decomposition.
```
