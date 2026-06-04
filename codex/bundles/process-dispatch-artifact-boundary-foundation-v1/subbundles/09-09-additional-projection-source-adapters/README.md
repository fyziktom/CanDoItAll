# SB09 - Additional projection source adapters

## Status

Prepared.

## Objective

Add planning adapters for mock/response/workspace/managed/provider-native sources as safe, preferably without full migration.

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

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactValidator.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
- `src/CanDoItAll.Processes.Contracts/Automation/ProcessAutomationExecutionContracts.cs`
- `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- Source changes only within the subbundle scope.
- Focused tests or source assertions proving the subbundle objective.
- Proof artifacts under `proof/SB09/`.
- Updated execution report entry.

## Dependency Impact

This subbundle affects downstream artifact-boundary proof. If it is wrong, downstream projection/validation migration cannot be trusted.

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

No broad rewrite.

## Do Not Do

- Do not create `CanDoItAll.Processes.Core`.
- Do not create process driver packs.
- Do not move EF entities or Razor/UI view models.
- Do not rename process runtime tools.
- Do not weaken required artifact, lineage, receipt, trust, access, or approval behavior.
- Do not run or record small/medium/mobile viewport proof.

## Acceptance Checklist

- [ ] Objective completed.
- [ ] Scope exceptions respected.
- [ ] Tests/source scans recorded.
- [ ] No prohibited viewport proof artifacts.
- [ ] No hidden MAF/product dependency.
- [ ] No premature Process Core or driver-pack project.
- [ ] Execution report updated.

## Proof Required

- `proof/SB09/manifest.md`
- `proof/SB09/semantic-invariants.md`
- `proof/SB09/transcripts/*`
- `proof/SB09/source-assertions/*`

## Browser Validation Logging

N/A expected. This is service/runtime refactoring. If a rendered UI route is unexpectedly affected, use only large desktop/PC viewport proof and record the exception.

## Progression Gate

Proceed only when the acceptance checklist is complete and downstream prerequisites remain valid.

## Suggested Agent Prompt

Execute SB09 from `process-dispatch-artifact-boundary-foundation-v1`. Stay strictly within scope, preserve process runtime behavior, and record proof before moving on.
