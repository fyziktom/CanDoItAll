# SB14 - Final red-team and next dispatcher cutline

## Status

Prepared.

## Objective

Final red-team review and next safe dispatcher isolation cutline.

## Covered Inputs

- User request to continue small dispatcher isolation steps.
- Branch review summary and source artifacts.
- Large-screen-only proof policy.

## Prerequisites

- Previous subbundle: SB13.
- Latest branch state must be refreshed from `maf-processes-refactor` before starting.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Deliverables

- Source changes, inventories, or proof appropriate to this subbundle.
- Updated proof manifest under `proof/SB14`.
- Updated execution report row.

## Dependency Impact

Downstream dependency: Final closure. If this subbundle changes semantics or cannot prove parity, downstream work must stop and reopen this subbundle.

## Validation Depth

- Source scan required.
- Focused tests required when production behavior changes.
- Full build required at Gate A/B/C/final or when production source movement occurs.

## Implementation Steps

1. Refresh source state and compare with bundle assumptions.
2. Make the smallest complete change for this subbundle only.
3. Add or update tests before or with the production movement.
4. Run focused validation.
5. Record proof transcripts and source assertions.
6. Update execution report.

## Scope Exceptions

No production movement beyond proof/doc closure.

## Do Not Do

- Do not create Process Core.
- Do not create driver packs.
- Do not move EF/UI/storage/MAF composition.
- Do not rename artifact keys or process tools.
- Do not run small/medium/mobile proof.

## Acceptance Checklist

- [ ] Subbundle objective is complete.
- [ ] Tests/source scans are recorded.
- [ ] No prohibited scope was introduced.
- [ ] Execution report updated.
- [ ] Final Gate criteria satisfied where applicable.

## Proof Required

- `proof/SB14/manifest.md`
- `proof/SB14/semantic-invariants.md`
- source assertions and transcripts relevant to this subbundle

## Browser Validation Logging

N/A expected. This is runtime/service refactor work. If UI proof becomes unavoidable, use large desktop/PC only and record why.

## Progression Gate

Final Gate: pass all focused proof for this subbundle before moving to Final closure.

## Suggested Agent Prompt

Implement SB14 only. Keep changes small, preserve validation behavior, and record proof before proceeding.
