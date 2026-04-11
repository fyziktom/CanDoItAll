# Scenario Definition And Live Gap Reconciliation

## Status

- `Completed`

## Objective

- Reconcile the latest follow-up notes against the live repository so the bundle is honest about left-click connector authoring, exact badge-anchor geometry, many-to-many join semantics, and canonical layout persistence before more product code changes begin.

## Covered Inputs

- `N009` Proper branching examples around software development.
- `N010` Record architecture troubles and start with process definition and gap analysis first.
- `N013` Many-to-many routing semantics must be supported or blocked honestly.
- `N014` Moved derived nodes must persist and not snap back after later interactions.
- `N015` Repair the bundle before implementing the latest follow-up scope.

## Prerequisites

- none

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasSurfaceFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDevelopmentSeedService.Scenarios.cs`
- `C:\repositories\CanDoItAll\cdi_process_management_audit_bundle\README.md`

## Deliverables

- Updated scenario inventory that covers code-review routing, QA loops, join-style input aggregation, and layout-persistence round trips.
- Updated architecture trouble log that names the current many-to-many and persistence gaps precisely.
- Repaired bundle docs and prepared-stage validator rerun before downstream implementation starts.

## Dependency Impact

- Later subbundles depend on this phase to know whether many-to-many routing is a real supported target or an explicit blocker.
- Weak proof here would allow later UI work to look correct while still relying on non-canonical joins or transient layout state.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Re-read the initial request and both follow-up requests.
2. Update the scenario inventory so join-style inputs and persistence round trips are explicit.
3. Audit the current process model and canvas persistence path for many-to-many and snap-back risks.
4. Update the architecture trouble log, requirements, and traceability so downstream work has an honest target.
5. Rerun the prepared-stage validator before continuing.

## Do Not Do

- Do not start more CanvasLib or process-component edits before the canonical gaps are documented.
- Do not treat browser-only movement or drawn curves as proof of canonical support.

## Acceptance Checklist

- The target scenario inventory explicitly includes a join-style input case and a persistence round-trip case.
- The architecture trouble log names the current many-to-many and derived-layout persistence gaps.
- The bundle docs and traceability reflect the latest left-click and badge-geometry scope.
- The prepared-stage validator is rerun after the bundle repair.

## Proof Required

- Updated bundle documents in `analysis/03-architecture-troubles-log.md`, `inventories/02-target-process-scenarios.md`, and related requirement files.
- Prepared-stage validator output recorded in `reviews/01-execution-report.md`.

## Browser Validation Logging

- `N/A`

## Progression Gate

- Downstream subbundles may continue only after the join-semantics and persistence risks are explicit enough that later phases do not rediscover them mid-implementation.

## Suggested Agent Prompt

```text
Implement this subbundle only. Repair the bundle around the latest follow-up scope, refresh the process scenarios, audit many-to-many and persistence gaps against the live repository, update the trouble log and traceability, and rerun the prepared-stage validator before moving on.
```
