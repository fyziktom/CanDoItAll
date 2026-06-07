# SB018 - Gate F - subprocess lifecycle/projection parity

## Status
Completed.

## Objective
Prove child-run observation, capability-gap block, terminal mirror, completed projection, gap journal, and parent finalizer behavior are unchanged.

## Covered Inputs
- Preserve existing behavior.
- Do not rush Process Core.
- Avoid production driver API.
- Runtime/service refactor only; browser validation is expected N/A.

## Prerequisites
- SB017 closed.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeModels.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionPersistenceService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessRunObservationCoordinator.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessCapabilityGapInspector.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessLifecycleRules.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionPlanBuilder.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionWriterCoordinator.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionGapJournalCoordinator.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerApplicationService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerInputs.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`


## Deliverables
- Code refactor for this slice, or documented N/A if source inspection proves it already complete.
- Focused tests or source assertions for the moved behavior.
- Proof transcript under `codex/bundles/process-core-contract-candidate-driver-readiness-prep-v1/proof/SB018/`.
- Execution report row for `SB018`.

## Dependency Impact
- Downstream phases may proceed only after this closes.

## Validation Depth
- Critical gate validation: build + unit/focused integration + source scans + manifest + semantic invariants.

## Implementation Steps
1. Re-read the exact source references and update the local inventory for this slice.
2. Identify the smallest behavior-preserving move for this subbundle.
3. Keep side effects explicitly named; do not hide EF, storage, workspace, AgentFramework, or transition writes behind misleading pure helpers.
4. Update or add focused tests before closing the subbundle.
5. Run source scans for forbidden Process Core, driver API, UI/media, stub, and adapter leakage.
6. Record proof and update the execution report.

## Scope Exceptions
- Do not create `CanDoItAll.Processes.Core`.
- Do not create production process driver interfaces or registries.
- Do not broaden into UI/mobile/browser proof.

## Do Not Do
- Do not simplify behavior.
- Do not remove recovery/finalizer/projection paths.
- Do not collapse multiple subbundle rows into one report row.
- Do not leave adapter logic in route-facing services unless this subbundle explicitly owns a named edge adapter.

## Acceptance Checklist
- [x] Behavior preserved.
- [x] Source references inspected.
- [x] Tests/source scans updated.
- [x] No Core project.
- [x] No production driver API.
- [x] No UI/mobile proof drift.
- [x] Execution report row added.

## Proof Required
- Critical build proof: `bundle://proof/SB018/transcripts/critical-build.txt`.
- Focused unit architecture proof: `bundle://proof/SB018/transcripts/subprocess-boundary-unit-tests.txt`.
- Focused integration parity proof: `bundle://proof/SB018/transcripts/subprocess-lifecycle-projection-integration-tests.txt`.
- Source assertion, anti-stub, and no-Core/no-driver proof: `bundle://proof/SB018/transcripts/source-assertions-and-scans.txt`.
- Hash proof: `bundle://proof/SB018/transcripts/changed-file-hashes.txt`.
- Critical manifest: `bundle://proof/SB018/manifest.md`.
- Semantic invariants: `bundle://proof/SB018/semantic-invariants.md`.

## Browser Validation Logging
- N/A - runtime/service refactor only. If any UI file changes unexpectedly, stop and reopen the scope.

## Progression Gate
- Passed. SB019-SB033 may proceed only while subprocess lifecycle/projection/finalizer parity remains guarded by `SB018-INV-001`.

## Closure Notes
- `ProcessDispatchSubprocessRuntimeInput` gives subprocess orchestration a route-owned input boundary.
- `ProcessSubprocessProjectionPersistenceService` owns completed-child projection queries, gap journaling, artifact writes, and `SaveChangesAsync`.
- `ProcessDispatchSubprocessRuntimeService` still owns subprocess observation, start/block/mirror orchestration, capability-gap block routing, and parent finalizer dispatch.
- Browser validation remains N/A because no UI files changed.

## Suggested Agent Prompt
Implement `SB018` from `process-core-contract-candidate-driver-readiness-prep-v1`. Preserve runtime behavior. Keep this work module-local and do not introduce Process Core or production driver APIs.
