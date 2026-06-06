# SB016 - Finalizer application model boundary

## Status

Prepared.

## Objective

Introduce module-local finalizer application models and keep conversion to dispatcher finalizer contexts at one explicit adapter.

## Covered Inputs

- Raw user request in `inputs/raw-user-request.md`.
- Current-state analysis in `analysis/01-current-state-review.md`.
- Phase: P6 - Finalizer and failure closure models

## Prerequisites

- Work from branch `maf-processes-refactor`.
- Previous subbundle and previous critical gate must be closed.
- Prepared bundle validator must pass before production changes.

## Exact Source References

Primary candidate source paths:
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteExecution.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationService.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeService.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerApplicationService.cs`
- `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Scope

Implement only the slice described by this subbundle. Keep the work module-local unless explicitly stated otherwise.

## Dependency Impact

Depends on previous phase gate.

Downstream phases become untrustworthy if this subbundle changes behavior without proof.

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
7. If this is a critical gate, create `proof/SB016/manifest.md` and `proof/SB016/semantic-invariants.md`.

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

- [ ] Behavior preserved.
- [ ] Source movement is meaningful, not cosmetic.
- [ ] Tests updated where needed.
- [ ] Source scans pass.
- [ ] Execution report row completed.

## Proof Required

Minimum proof:
- `dotnet build CanDoItAll.slnx --no-restore`
- relevant focused unit tests
- relevant focused integration tests
- source scan: no Core/no driver/no UI/no mobile/no stubs
- route order scan when route files are touched

## Browser Validation Logging

N/A - runtime/service refactor only. If UI files change unexpectedly, stop and document before proceeding.

## Progression Gate

Do not proceed to downstream subbundles until this subbundle's proof passes. Critical gates must include manifest and semantic invariant files.

## Suggested Agent Prompt

Implement SB016: Finalizer application model boundary. Preserve behavior, avoid Process Core and production driver APIs, update proof artifacts, and close the execution report row.
