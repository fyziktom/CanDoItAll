# SB013 - Subprocess runtime model split

## Status

- Status: `Completed`
## Objective

Introduce subprocess route/run snapshots and reduce dispatcher alias usage in subprocess runtime service.

## Covered Inputs

- Raw user request in `inputs/raw-user-request.md`.
- Current-state analysis in `analysis/01-current-state-review.md`.
- Phase: P5 - Subprocess runtime and projection isolation

## Prerequisites

- Work from branch `maf-processes-refactor`.
- Previous subbundle and previous critical gate must be closed.
- Prepared bundle validator must pass before production changes.

## Exact Source References

Primary candidate source paths:
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteExecution.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerApplicationService.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Scope

- Implement only the slice described by this subbundle.
## Dependency Impact

- Depends on the previous numeric subbundle and previous critical gate where applicable.
- Downstream phases become untrustworthy if this subbundle changes behavior without proof.
## Validation Depth

- Build after meaningful source movement.
- Focused unit tests for moved rules/models.
- Focused integration tests for dispatcher behavior when runtime paths move.
- Source scans for no Core, no driver API, no UI/mobile proof, no stubs.

## Implementation Steps

1. Inspect current source before editing.
2. Identify behavior that must remain exactly equivalent.
3. Move or isolate the target boundary in small commits.
4. Add or adjust tests proving parity.
5. Run proof commands.
6. Update execution report row for this subbundle.
7. If this is a critical gate, create `proof/SB013/manifest.md` and `proof/SB013/semantic-invariants.md`.

## Scope Exceptions

- Do not create `CanDoItAll.Processes.Core`.
- Do not create process driver APIs.
- Do not remove old wrappers unless all consumers have migrated and tests prove parity.

## Do Not Do

- Do not simplify route behavior.
- Do not skip claim-held checks.
- Do not collapse execution report rows.
- Do not add small/medium/mobile screenshots or viewport proof.
- Do not introduce UI changes.

## Acceptance Checklist

- [x] Behavior preserved.
- [x] Source movement is meaningful, not cosmetic.
- [x] Tests updated where needed.
- [x] Source scans pass.
- [x] Execution report row completed.

## Proof Required

Minimum proof:
- `dotnet build CanDoItAll.slnx --no-restore`
- relevant focused unit tests
- relevant focused integration tests
- source scan: no Core/no driver/no UI/no mobile/no stubs
- route order scan when route files are touched

## Browser Validation Logging

- N/A - runtime/service refactor only.
## Progression Gate

- Do not proceed to downstream subbundles until this subbundle proof passes.
## Suggested Agent Prompt

Implement SB013: Subprocess runtime model split. Preserve behavior, avoid Process Core and production driver APIs, update proof artifacts, and close the execution report row.


