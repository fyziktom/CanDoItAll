# SB021 - Gate G - execution/retry/provider parity

## Status
Completed.

## Objective
Prove direct-agent execution, retry/no-progress, provider fallback/repair, competing-execution guard, and finalizer input behavior remain stable.

## Covered Inputs
- Preserve existing behavior.
- Do not rush Process Core.
- Avoid production driver API.
- Runtime/service refactor only; browser validation is expected N/A.

## Prerequisites
- SB020 closed.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModels.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteFacets.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlers.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerApplicationService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerAdapter.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentExecutionModels.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentExecutionAdapter.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentRuntimeService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCompetingExecutionGuardService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryPackets.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ProviderRecovery.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExecutionAttemptLoopFacade.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProviderRepairCoordinator.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`


## Deliverables
- Code refactor for this slice, or documented N/A if source inspection proves it already complete.
- Focused tests or source assertions for the moved behavior.
- Proof transcript under `codex/bundles/process-core-contract-candidate-driver-readiness-prep-v1/proof/SB021/`.
- Execution report row for `SB021`.

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
- Critical build proof: `bundle://proof/SB021/transcripts/critical-build.txt`.
- Focused unit architecture proof: `bundle://proof/SB021/transcripts/execution-boundary-unit-tests.txt`.
- Focused integration parity proof: `bundle://proof/SB021/transcripts/execution-retry-provider-integration-tests.txt`.
- Source assertion, anti-stub, and no-Core/no-driver proof: `bundle://proof/SB021/transcripts/source-assertions-and-scans.txt`.
- Hash proof: `bundle://proof/SB021/transcripts/changed-file-hashes.txt`.
- Critical manifest: `bundle://proof/SB021/manifest.md`.
- Semantic invariants: `bundle://proof/SB021/semantic-invariants.md`.

## Browser Validation Logging
- N/A - runtime/service refactor only. If any UI file changes unexpectedly, stop and reopen the scope.

## Progression Gate
- Passed. SB022-SB033 may proceed only while direct-agent execution input, route execution run snapshots, retry/no-progress behavior, provider fallback/repair behavior, competing-execution guard behavior, and finalizer context parity remain guarded by `SB021-INV-001`.

## Closure Notes
- Direct-agent execution now enters runtime through `ProcessDispatchDirectAgentExecutionInput`, with dispatcher conversion confined to `ProcessDispatchDirectAgentExecutionAdapter`.
- Route execution outcomes now expose `ProcessRouteExecutionRunSnapshot` for route guard/logging consumers while preserving full dispatcher detail through adapter-backed finalizer compatibility.
- Retry/no-progress/provider repair code paths remained in `ProcessRunAutomationDispatchService.Execution`, `Concurrency`, `RecoveryPackets`, `ProviderRecovery`, and execution attempt collaborators; focused integration tests passed.
- Browser validation remains N/A because no UI files changed.

## Suggested Agent Prompt
Implement `SB021` from `process-core-contract-candidate-driver-readiness-prep-v1`. Preserve runtime behavior. Keep this work module-local and do not introduce Process Core or production driver APIs.
