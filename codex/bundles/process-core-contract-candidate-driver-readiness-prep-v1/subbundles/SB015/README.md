# SB015 - Gate E - pre-execution/start-transition parity

## Status
Completed.

## Objective
Prove database block, materialization request/no-op, start-transition reload, and ContinueCandidates behavior are unchanged.

## Covered Inputs
- Preserve existing behavior.
- Do not rush Process Core.
- Avoid production driver API.
- Runtime/service refactor only; browser validation is expected N/A.

## Prerequisites
- SB014 closed.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchPreExecutionRouteFacts.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchPreExecutionGuardHandler.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessMissingUpstreamArtifactMaterialization.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessMissingUpstreamArtifactMaterializationSideEffects.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchStartTransitionPlanner.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlers.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteExecutionModels.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`


## Deliverables
- Code refactor for this slice, or documented N/A if source inspection proves it already complete.
- Focused tests or source assertions for the moved behavior.
- Proof transcript under `codex/bundles/process-core-contract-candidate-driver-readiness-prep-v1/proof/SB015/`.
- Execution report row for `SB015`.

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
- Build proof: `bundle://proof/SB015/transcripts/critical-build.txt`.
- Unit/source-architecture proof: `bundle://proof/SB015/transcripts/pre-execution-start-transition-unit-tests.txt`.
- Focused integration proof: `bundle://proof/SB015/transcripts/pre-execution-start-transition-integration-tests.txt`.
- Source assertion, anti-stub, and no-Core/no-driver proof: `bundle://proof/SB015/transcripts/source-assertions-and-scans.txt`.
- Critical manifest: `bundle://proof/SB015/manifest.md`.
- Semantic invariants: `bundle://proof/SB015/semantic-invariants.md`.

## Browser Validation Logging
- N/A - runtime/service refactor only. If any UI file changes unexpectedly, stop and reopen the scope.

## Progression Gate
- Passed. SB016-SB033 may proceed only while pre-execution database blocking, upstream materialization, start-transition reload, and `ContinueCandidates` behavior remain guarded by `SB015-INV-001`.

## Closure Notes
- `ProcessDispatchPreExecutionRouteFacts` preserves the route facts needed by database requirement blocking and upstream materialization without exposing the route candidate source payload to the pre-execution rules.
- `ProcessMissingUpstreamArtifactMaterialization` remains pure-rule only; `ProcessMissingUpstreamArtifactMaterializationSideEffects` owns journal persistence, logging, and rerun requests.
- `StartTransitionRouteHandler_SB015_INV_001_preserves_reload_and_continue_candidates_behavior` proves failed start transitions still reload the claimed candidate, continue the candidate loop when no matching `InProgress` candidate is available, and update the route context when the refreshed candidate is usable.
- Browser validation remains N/A because no UI files changed.

## Suggested Agent Prompt
Implement `SB015` from `process-core-contract-candidate-driver-readiness-prep-v1`. Preserve runtime behavior. Keep this work module-local and do not introduce Process Core or production driver APIs.
