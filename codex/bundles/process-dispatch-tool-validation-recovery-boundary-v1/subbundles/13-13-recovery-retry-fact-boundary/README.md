# SB13 - Recovery retry fact boundary

## Status

Prepared.

## Objective

Extract pure retry/rework decision facts only. Keep recovery journal persistence, rework packet creation, provider mutation and step transition in dispatcher.

## Covered Inputs

- User request to continue small dispatcher isolation.
- Previous completed artifact validation rule boundary.
- ToolValidation and recovery/finalization hotspot review.
- No Process Core / no driver-pack policy.
- Large-screen-only / no mobile proof policy.

## Prerequisites

SB12 closure gate passed.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryDirective.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryPackets.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Scope

Implement only this subbundle's described slice. Keep all changes local and incremental.

## Dependency Impact

Downstream subbundles depend on the exact proof generated here. If this subbundle changes source shape, update line counts and source inventory before proceeding.

## Validation Depth

Focused validation: targeted source scans, unit/helper tests where applicable, integration slice if production behavior changes, and anti-stub audit.

## Implementation Steps

1. Refresh the live source state and exact line counts.
2. Apply the smallest code/test/documentation changes required by this subbundle.
3. Preserve behavior and names exactly.
4. Run focused tests and source scans.
5. Record proof under `codex/bundles/process-dispatch-tool-validation-recovery-boundary-v1/proof/SB13/`.
6. Update `reviews/01-execution-report.md`.

## Scope Exceptions

- Process Core is explicitly out of scope.
- Driver packs are explicitly out of scope.
- Browser/UI proof is N/A unless a UI file unexpectedly changes.

## Do Not Do


- Do not create `CanDoItAll.Processes.Core`.
- Do not create process driver pack APIs or implementations.
- Do not move EF, storage, file-system, provider fallback mutation, recovery journal persistence, final transitions, or UI code into helper classes.
- Do not rename tool names, provider keys, required-tool names, or process artifact keys.
- Do not add small, medium, mobile, phone, tablet, Android, iPhone, or responsive proof artifacts.


## Acceptance Checklist

- [ ] Subbundle objective implemented.
- [ ] Focused tests/scans recorded.
- [ ] No Process Core or driver-pack production surface introduced.
- [ ] No MAF/Tooling product dependency regression.
- [ ] No prohibited viewport proof artifacts.
- [ ] Execution report updated.

## Proof Required

- Source assertion file.
- Command transcripts.
- Anti-stub audit.
- Changed-file hashes.
- Focused tests where behavior changes.
- Gate proof if this is a gate subbundle.

## Browser Validation Logging

N/A expected. Runtime/service refactor only. If UI proof unexpectedly becomes necessary, record why and use large desktop/PC only.

## Progression Gate

Do not proceed to SB14 until this subbundle's closure proof is complete.

## Suggested Agent Prompt

Implement SB13 of `process-dispatch-tool-validation-recovery-boundary-v1`. Keep scope narrow, preserve behavior, avoid Process Core and driver packs, run focused tests, and record proof before moving on.
