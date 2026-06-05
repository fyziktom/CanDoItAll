# SB08 - Gate B: recorded/fresh artifact parity

## Status

Prepared.

## Objective

Run focused tests for required artifacts, current-attempt implementation artifacts, workspace write satisfaction, process mock satisfaction, and build/test/validation ordering. Reopen SB05-SB07 on any parity drift.

## Covered Inputs

- Original user request: continue smaller process dispatch isolation.
- Preserve all original functionality.
- No Process Core.
- No production driver API.
- Longer phased execution with refactor gates.

## Prerequisites

- Previous subbundle: SB07
- Critical foundation: Yes

## Exact Source References

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactQualityValidationRules.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProviderNativeVisualValidationRules.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactPathValidationRules.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactTextMatchRules.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ImplementationProofBridges.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessImplementationStackRules.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessConcreteProductPathRules.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDotNetHostEvidenceRules.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Deliverables

- Source changes scoped to this subbundle only.
- Updated or added focused tests.
- Proof manifest and semantic invariants.
- Execution report row updated.
- Browser validation analytics row updated as `N/A` unless UI changes unexpectedly.

## Dependency Impact

Downstream subbundles depend on this subbundle preserving behavior. If this subbundle is wrong, later proof is not trustworthy.

## Validation Depth

Critical gate: full source scans, focused tests, build as required, red-team review, and reopen triggers.

## Implementation Steps

1. Re-open the current source before editing.
2. Confirm the relevant methods still exist and match inventory assumptions.
3. Add or update focused tests for the rule family.
4. Move only the targeted logic into module-local helper(s).
5. Preserve wrapper methods when existing code calls them.
6. Run focused tests.
7. Record proof transcripts and source assertions.
8. Update `reviews/01-execution-report.md`.

## Scope Exceptions

- Process Core extraction is out of scope.
- Production process drivers are out of scope.
- UI/browser visual validation is out of scope unless UI files unexpectedly change.

## Do Not Do


- Do not create `CanDoItAll.Processes.Core`.
- Do not create production process driver APIs.
- Do not move EF entities or public contracts.
- Do not change runtime behavior to simplify tests.
- Do not add UI/mobile/small/medium proof artifacts.


## Acceptance Checklist

- [ ] Behavior parity proven.
- [ ] No Process Core source/project added.
- [ ] No production driver API added.
- [ ] No hidden side effects in pure helpers.
- [ ] No UI/mobile/small/medium proof artifacts.
- [ ] Execution report updated.
- [ ] Proof manifest completed.

## Proof Required

- Focused test transcript.
- Source scan transcript.
- Changed-file hash list.
- Anti-stub scan.
- No-core/no-driver scan.
- No-prohibited-viewport scan.
- For critical gates: semantic adequacy paragraph and downstream reopen triggers.

## Browser Validation Logging

N/A expected - runtime/service refactor only. If UI changes unexpectedly, record why and use large desktop/PC proof only.

## Progression Gate

Do not proceed to the next subbundle until this subbundle's closure gate passes. If this is a critical gate, all downstream work is blocked until repaired.

## Suggested Agent Prompt

Implement SB08 only. Do not skip ahead. Preserve behavior exactly. Record proof before continuing.
