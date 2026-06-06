# SB107 - Remove ProcessDispatchRouteServices or reduce to explicit adapter-only shim

## Status

- Completed

## Objective

Remove ProcessDispatchRouteServices or reduce to explicit adapter-only shim.

This subbundle belongs to **P9: Factory, source hardening and line-count pass**.

## Covered Inputs

- RAW-001: continue incremental process dispatch isolation.
- RAW-002: do not rush Process Core.
- RAW-003: preserve all existing functionality.
- RAW-004: plan enough phases and force refactor gates.
- RAW-005: prepare for future drivers only as documentation/readiness, not as production API.
- RAW-006: no small/medium/mobile/UI proof.

## Prerequisites

- Previous subbundles in this phase completed.
- Last critical gate before this subbundle passed.
- Branch is still `maf-processes-refactor`.
- No unreviewed behavior changes.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteFacets.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlers.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlerFactory.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteExecution.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Deliverables

- Production code changes only if required by this subbundle objective.
- Focused tests or source assertions tied to this subbundle.
- Updated proof manifest if this subbundle is a critical gate.
- Updated execution report row for SB107.

## Dependency Impact

- Downstream subbundles depend on SB107 preserving behavior and not widening route dependencies.
- If SB107 changes route order, route result semantics, claim behavior, finalizer behavior, or side-effect ownership, downstream proof is invalid and the affected earlier phase must be reopened.

## Validation Depth

- Focused validation for Remove ProcessDispatchRouteServices or reduce to explicit adapter-only shim..
- Cumulative behavior proof is consolidated at the next critical gate for P9: Factory, source hardening and line-count pass.

## Implementation Steps

1. Open the exact source references.
2. Make the smallest complete change for this subbundle objective.
3. Preserve public and internal behavior.
4. Keep new types module-local unless explicitly required otherwise.
5. Avoid any Core, driver API, UI, mobile, or browser-proof work.
6. Update tests/source assertions as needed.
7. Record proof in the execution report.

## Scope Exceptions

- Do not create Process Core.
- Do not create production driver APIs.
- Do not move EF entities.
- Do not alter UI.
- Do not remove route stages.

## Do Not Do

- Do not collapse this subbundle into another row.
- Do not mark complete without source/test proof.
- Do not replace behavior with TODOs, stubs, or no-op placeholders.
- Do not rename without semantic boundary change.
- Do not hide side effects behind pure-sounding helper names.

## Acceptance Checklist

- [ ] Route order is unchanged.
- [ ] No original route behavior path is removed.
- [ ] No Core/driver/UI/mobile drift.
- [ ] Any new route model or service is module-local.
- [ ] Source scans pass.
- [ ] Tests tied to this subbundle or the next gate pass.
- [ ] Execution report contains a row for SB107.

## Proof Required

- Build or relevant focused test transcript.
- Source assertion transcript for this subbundle.
- Anti-stub scan.
- No-Core/no-driver scan.
- No UI/mobile proof scan.


## Browser Validation Logging

- N/A - runtime/service refactor only.
- Do not create browser, mobile, small-screen, or medium-screen proof unless source diff unexpectedly touches UI.

## Progression Gate

- May continue only after local checks for SB107 pass and the execution report has an individual SB107 row.
- Stop and reopen prerequisites if source scans, tests, or critical-gate proof contradict earlier assumptions.

## Suggested Agent Prompt

Implement SB107 from this bundle. Keep changes narrow, preserve existing process route behavior, and do not introduce Process Core or driver APIs. Record exact proof before proceeding.


