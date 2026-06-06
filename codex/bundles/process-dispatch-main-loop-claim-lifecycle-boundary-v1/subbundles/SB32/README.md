# SB32 - Route pipeline skeleton no behavior movement

## Status

- Status: Completed

## Objective

Route pipeline skeleton no behavior movement as part of Phase D: Dispatch route context and route-order contracts.

## Covered Inputs

- Continue smaller dispatcher isolation.
- Do not rush Process Core.
- Preserve all original process automation functionality.
- Plan enough phased work and force regular refactor gates.

## Prerequisites

- All previous subbundles must be completed and closed.
- If this subbundle is a gate, all immediately preceding production movement subbundles must have source and test proof.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRoutePlanner.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchLeaseHeartbeat.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchGuardLease.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationLoader.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`


## Deliverables

- Module-local helper/coordinator/rule movement as named by this subbundle.
- Tests/source scans proving semantic parity.


## Dependency Impact

- Non-critical production/support slice, but still must close before downstream numeric subbundles.
- Potentially invalidates downstream proof if route order, claim lifecycle, failure closure, or dispatch side-effect ownership changes.

## Validation Depth

- `dotnet build CanDoItAll.slnx --no-restore` when production source changes.
- Focused unit/integration tests relevant to claim lifecycle, route ordering, dispatch failures, workflow/subprocess/direct-agent paths.
- Source scans for no Process Core, no production driver API, no UI proof drift, no stubs.
- Line-count/source assertions where this subbundle moves production logic.

## Implementation Steps

1. Re-open the source references before editing.
2. Move only the behavior owned by this subbundle.
3. Keep side effects explicit and named as stores/coordinators/handlers.
4. Preserve existing log messages unless the subbundle explicitly updates tests for equivalent text.
5. Add or update focused tests before closing.
6. Record proof under `proof/SB32/`.
7. Update `reviews/01-execution-report.md` with this exact subbundle row.

## Scope Exceptions

- Do not extract Process Core.
- Do not introduce production driver APIs.
- Do not change UI or browser proof.

## Do Not Do

- Do not collapse this subbundle into a later gate row.
- Do not hide EF writes or service-scope calls inside `Rules` helpers.
- Do not reorder routes.
- Do not silently weaken claim/heartbeat semantics.
- Do not delete functionality as a shortcut.

## Acceptance Checklist

- [x] Code compiles.
- [x] Relevant focused tests pass.
- [x] Source scans pass.
- [x] No Core/driver/UI/prohibited viewport artifacts.
- [x] Execution report row for `SB32` is updated.
- [x] Critical gate proof exists if this is a gate.

## Proof Required

- `proof/SB32/manifest.md`
- `proof/SB32/semantic-invariants.md` for critical gates or production movement.
- `proof/SB32/transcripts/` with build/test/source-scan output.

## Browser Validation Logging

- N/A. This subbundle must not touch browser-visible UI. If UI files change, stop and reopen the subbundle as a scope violation.

## Progression Gate

- Downstream subbundles may proceed only after this subbundle's closure checklist passes. Critical gate status: No.

## Suggested Agent Prompt

Execute `SB32 - Route pipeline skeleton no behavior movement` only. Preserve current behavior. Do not start Process Core or driver APIs. Update proof and execution report before proceeding.


