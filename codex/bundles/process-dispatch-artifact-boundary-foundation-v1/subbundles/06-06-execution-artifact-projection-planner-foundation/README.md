# SB06 - Execution artifact projection planner foundation

## Status

- Status: Completed

## Objective

Create planner candidate model for execution artifacts without side effects.

## Covered Inputs

- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-artifacts.md`
- `inputs/03-structured-input.md`
- `inputs/04-large-screen-only-proof-policy.md`

## Prerequisites

- Previous subbundle closure gate passed.
- Working tree inspected and relevant proof directory available.
- No unrelated UI/mobile/small/medium proof work in progress.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactValidator.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
- `repo://src/CanDoItAll.Processes.Contracts/Automation/ProcessAutomationExecutionContracts.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- Source changes only within the subbundle scope.
- Focused tests or source assertions proving the subbundle objective.
- Proof artifacts under `proof/SB06/`.
- Updated execution report entry.

## Dependency Impact

- This subbundle affects downstream artifact-boundary proof. If it is wrong, downstream projection/validation migration cannot be trusted.

## Validation Depth

- Source scans.
- Focused unit tests when production helpers are added.
- Integration smoke when projection/validation behavior changes.
- Full or module build at every refactor gate.

## Implementation Steps

1. Re-read the exact source references.
2. Run entry source scans for MAF neutrality, no core/driver project, and prohibited viewport artifact paths.
3. Implement only the smallest changes required by this subbundle.
4. Add or update tests before claiming behavior preservation.
5. Record command transcripts and source assertions.
6. Update `reviews/01-execution-report.md` with semantic adequacy evidence.

## Scope Exceptions

No all-source migration.

## Do Not Do

- Do not create `CanDoItAll.Processes.Core`.
- Do not create process driver packs.
- Do not move EF entities or Razor/UI view models.
- Do not rename process runtime tools.
- Do not weaken required artifact, lineage, receipt, trust, access, or approval behavior.
- Do not run or record small/medium/mobile viewport proof.

## Acceptance Checklist

- [x] Objective completed.
- [x] Scope exceptions respected.
- [x] Tests/source scans recorded.
- [x] No prohibited viewport proof artifacts.
- [x] No hidden MAF/product dependency.
- [x] No premature Process Core or driver-pack project.
- [x] Execution report updated.

## Proof Required

- `proof/SB06/manifest.md`
- `proof/SB06/semantic-invariants.md`
- `proof/SB06/transcripts/*`
- `proof/SB06/source-assertions/*`

## Browser Validation Logging

- N/A expected. This is service/runtime refactoring. If a rendered UI route is unexpectedly affected, use only large desktop/PC viewport proof and record the exception.

## Progression Gate

- Proceed only when the acceptance checklist is complete and downstream prerequisites remain valid.

## Suggested Agent Prompt

Execute SB06 from `process-dispatch-artifact-boundary-foundation-v1`. Stay strictly within scope, preserve process runtime behavior, and record proof before moving on.
