# SB11 - Step transition request builder

## Objective

Extract pure transition request construction and artifact validation context mapping.


## Status

Prepared.

## Covered Inputs

- User request to continue small dispatcher isolation.
- Current `maf-processes-refactor` branch review.
- Previous tool-validation/recovery bundle closure.
- No Process Core / no production driver API constraint.
- Large-screen-only proof policy.

## Prerequisites

- Current branch builds before this subbundle.
- Previous subbundle closure gate passed.
- No unresolved regressions from previous bundle.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.ToolValidation.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRunAutomationDispatchServiceTests.cs`

## Dependency Impact

Downstream subbundles must not proceed if this subbundle changes behavior without parity proof.

## Validation Depth

Use source scans, focused unit/integration tests, and build. Compile-only proof is not sufficient for production movement.

## Scope Exceptions

Process Core and production driver APIs are explicitly out of scope.

## Do Not Do

- Do not create `CanDoItAll.Processes.Core`.
- Do not create `IProcessDriverPack` or driver packs.
- Do not change UI files.
- Do not create small/medium/mobile proof.
- Do not rename process tools or artifact statuses.
- Do not move EF entities or DbContext-owned behavior into helper files unless the subbundle explicitly allows read orchestration.

## Browser Validation Logging

N/A expected. Runtime/service refactor only. If UI unexpectedly changes, stop and record a scope exception. Large desktop/PC proof only after explicit justification.

## Suggested Agent Prompt

Execute this subbundle only. Preserve all prior behavior, record proof, update execution report, and stop at the progression gate.


## Deliverables

- Transition request builder.
- Artifact-validation context field parity tests.

## Implementation Steps

1. Re-read the exact source references.
2. Confirm the subbundle prerequisites.
3. Make only the narrow source changes required for this subbundle.
4. Add or update focused tests before broad tests.
5. Record source assertions and command transcripts under `proof/SB11`.
6. Update `reviews/01-execution-report.md`.
7. Stop if the progression gate fails.

## Acceptance Checklist

- [ ] Scope stayed within SB11.
- [ ] No Process Core or driver production API was added.
- [ ] Behavior parity was proven.
- [ ] Source scans are recorded.
- [ ] Tests/build are recorded.
- [ ] No prohibited viewport proof artifacts exist.
- [ ] Execution report was updated.

## Proof Required

- Source assertion file under `proof/SB11/source-assertions/`.
- Command transcript under `proof/SB11/transcripts/`.
- Hashes for changed production files.
- Focused test transcript.
- Build transcript when this is a gate or production movement subbundle.

## Progression Gate

Downstream work may continue only after acceptance checklist is complete and no scope exception is open.
