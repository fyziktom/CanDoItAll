# SB59 - Known unrelated failure cleanup note

## Status

Prepared.

## Objective

Track historical fixture failures separately without blocking this cutline.

## Covered Inputs

- Original request: continue smaller isolation steps, do not rush Process Core, preserve behavior, plan more phases, no UI/mobile proof.
- Branch review: current projection coordinator boundary is nested and needs dependency narrowing.

## Prerequisites

Previous subbundle closure gate must pass.

## Exact Source References

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjectionCoordinators.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionPlanner.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionWriteCoordinator.cs`
- `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- reviews/known-unrelated-failures.md

## Dependency Impact

This subbundle is part of Phase K: Driver-readiness documentation and no-core review. Downstream subbundles depend on its proof. If this subbundle changes behavior or weakens boundaries, reopen it before continuing.

## Validation Depth

Focused proof appropriate to this movement plus downstream dependency check.

## Implementation Steps

1. Re-read the current source references before changing code.
2. Make only the movement or proof required by this subbundle.
3. Preserve projection source-family order and candidate state semantics.
4. Add or update focused tests only for this slice.
5. Run the required proof commands.
6. Update the execution report row and proof manifest.

## Scope Exceptions

No Process Core and no production driver API in this subbundle.

## Do Not Do

- Do not create `CanDoItAll.Processes.Core`.
- Do not add `IProcessDriverPack`, `IProcessDriverRegistry`, `ProcessDriverRegistry`, or driver packages.
- Do not touch UI/Razor/CSS/JS/TS files.
- Do not create small/medium/mobile/phone/tablet proof artifacts.
- Do not change projection source-family order.
- Do not remove behavior without focused tests.


## Acceptance Checklist

- [ ] Behavior-preserving refactor only.
- [ ] Exact source-family order preserved if projection orchestration is touched.
- [ ] No broad hidden dispatcher dependency is introduced.
- [ ] Side effects remain explicit.
- [ ] Focused tests or source scans prove the change.
- [ ] Execution report updated.
- [ ] No UI/prohibited viewport proof paths.

## Proof Required

- Build or focused test transcript, as applicable.
- Source assertion transcript.
- Anti-stub scan for changed files.
- No-core/no-driver scan.
- No-UI/no-viewport scan.
- Critical gate manifest if this is a gate subbundle.

## Browser Validation Logging

N/A expected. Runtime/service-only refactor. If UI files change, revert the UI changes instead of adding small/medium/mobile proof.

## Progression Gate

Proceed only after closure proof is recorded and downstream dependency check passes.

## Suggested Agent Prompt

Implement SB59 only. Do not jump ahead. Preserve behavior and update proof before moving on.
