# SB04 - Gate A: architecture guardrails before movement

## Status

- Status: `Completed`

## Objective

Add/extend architecture tests and source scans for no Process Core, no driver API, no UI proof drift, no hidden file/DB side effects in pure helpers, and preserved wrapper entry points.

## Covered Inputs

- Original user request: continue smaller process dispatch isolation.
- Preserve all original functionality.
- No Process Core.
- No production driver API.
- Longer phased execution with refactor gates.

## Prerequisites

- Previous subbundle: SB03
- Critical foundation: Yes

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactQualityValidationRules.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProviderNativeVisualValidationRules.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactPathValidationRules.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactTextMatchRules.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ImplementationProofBridges.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessImplementationStackRules.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessConcreteProductPathRules.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDotNetHostEvidenceRules.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Deliverables

- Source changes scoped to this subbundle only.
- Updated or added focused tests.
- Proof manifest and semantic invariants.
- Execution report row updated.
- Browser validation analytics row updated as `N/A` unless UI changes unexpectedly.





## Dependency Impact

- Downstream subbundles depend on this subbundle preserving behavior. If this subbundle is wrong, later proof is not trustworthy.

## Validation Depth

- Critical gate: full source scans, focused tests, build as required, red-team review, semantic adequacy proof, artifact-backed proof manifest, and reopen triggers.

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

- [x] Behavior parity proven.
- [x] No Process Core source/project added.
- [x] No production driver API added.
- [x] No hidden side effects in pure helpers.
- [x] No UI/mobile/small/medium proof artifacts.
- [x] Execution report updated.
- [x] Proof manifest completed.

## Proof Artifacts

- Manifest: bundle://proof/SB04/manifest.md.
- Semantic invariants: bundle://proof/SB04/semantic-invariants.md.

## Proof Required

- Focused test transcript.
- Source scan transcript.
- Changed-file hash list.
- Anti-stub scan.
- No-core/no-driver scan.
- No-prohibited-viewport scan.
- For critical gates: semantic adequacy paragraph and downstream reopen triggers.





## Browser Validation Logging

- N/A expected - runtime/service refactor only. If UI changes unexpectedly, record why and use large desktop/PC proof only.

## Progression Gate

- Do not proceed to the next subbundle until this subbundle's closure gate passes. If this is a critical gate, all downstream work is blocked until repaired.

## Suggested Agent Prompt

Implement SB04 only. Do not skip ahead. Preserve behavior exactly. Record proof before continuing.
