# SB020 - Execution outcome route snapshot slimming

## Status
Completed.

## Objective
Introduce a thinner process-owned execution outcome snapshot for route/guard/finalizer consumers while preserving full detail only where required.

## Covered Inputs
- Preserve existing behavior.
- Do not rush Process Core.
- Avoid production driver API.
- Runtime/service refactor only; browser validation is expected N/A.

## Prerequisites
- SB019 closed.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModels.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlers.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCompetingExecutionGuardService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerApplicationService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerAdapter.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentRuntimeService.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`


## Deliverables
- Code refactor for this slice, or documented N/A if source inspection proves it already complete.
- Focused tests or source assertions for the moved behavior.
- Proof transcript under `codex/bundles/process-core-contract-candidate-driver-readiness-prep-v1/proof/SB020/`.
- Execution report row for `SB020`.

## Dependency Impact
- Feeds the next critical gate.

## Validation Depth
- Focused source/test proof; full gate proof can be accumulated by the next critical gate.

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
- Build transcript: `bundle://proof/SB020/transcripts/build.txt`.
- Focused unit proof: `bundle://proof/SB020/transcripts/execution-outcome-snapshot-unit-tests.txt`.
- Source assertion, anti-stub, and no-Core/no-driver proof: `bundle://proof/SB020/transcripts/source-assertions-and-scans.txt`.

## Browser Validation Logging
- N/A - runtime/service refactor only. If any UI file changes unexpectedly, stop and reopen the scope.

## Progression Gate
- Passed. SB021 may proceed with route consumers using `ProcessRouteExecutionRunSnapshot` and full execution detail retained only through the dispatcher outcome adapter source.

## Closure Notes
- Added `ProcessRouteExecutionRunSnapshot` and changed `ProcessRouteExecutionOutcome` to expose `ExecutionRun` instead of full `ProcessAutomationExecutionRunDetail`.
- `ProcessDispatchCompetingExecutionGuardService` and `CompetingExecutionGuardRouteHandler` now use `executionOutcome.ExecutionRun.Id`.
- `ProcessDispatchRouteModelAdapters` still preserves the full dispatcher execution outcome in `DispatcherExecutionOutcomeSource` for finalizer compatibility.
- Browser validation remains N/A because no UI files changed.

## Suggested Agent Prompt
Implement `SB020` from `process-core-contract-candidate-driver-readiness-prep-v1`. Preserve runtime behavior. Keep this work module-local and do not introduce Process Core or production driver APIs.
