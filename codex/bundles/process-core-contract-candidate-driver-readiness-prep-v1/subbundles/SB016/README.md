# SB016 - Subprocess orchestration model boundary

## Status
Completed.

## Objective
Introduce subprocess route/runtime input models and remove dispatcher aliases from ProcessDispatchSubprocessRuntimeService where practical.

## Covered Inputs
- Preserve existing behavior.
- Do not rush Process Core.
- Avoid production driver API.
- Runtime/service refactor only; browser validation is expected N/A.

## Prerequisites
- SB015 closed.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeModels.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteFacets.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlers.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessLifecycleRules.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionPlanBuilder.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionWriterCoordinator.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionGapJournalCoordinator.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`


## Deliverables
- Code refactor for this slice, or documented N/A if source inspection proves it already complete.
- Focused tests or source assertions for the moved behavior.
- Proof transcript under `codex/bundles/process-core-contract-candidate-driver-readiness-prep-v1/proof/SB016/`.
- Execution report row for `SB016`.

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
- Focused unit architecture proof: `bundle://proof/SB016/transcripts/subprocess-runtime-route-input-architecture-tests.txt`.
- Source assertion, anti-stub, and no-Core/no-driver proof: `bundle://proof/SB016/transcripts/source-assertions-and-scans.txt`.
- Hash proof: `bundle://proof/SB016/transcripts/changed-file-hashes.txt`.

## Browser Validation Logging
- N/A - runtime/service refactor only. If any UI file changes unexpectedly, stop and reopen the scope.

## Progression Gate
- Passed. The focused unit proof compiles the affected source, and SB017 may proceed with subprocess projection persistence extraction.

## Closure Notes
- Added `ProcessDispatchSubprocessRuntimeInput` as the subprocess route/runtime boundary.
- `ProcessDispatchSubprocessRuntimeService` no longer carries dispatcher candidate or dispatcher claim aliases, no longer calls `ProcessDispatchRouteModelAdapters.ToDispatcherCandidate`, and finalizes completed subprocesses through route-owned finalizer input.
- Projection plan, writer, and gap journal helpers now consume the subprocess runtime input while SB017 still owns the persistence extraction from the runtime method.
- Browser validation remains N/A because no UI files changed.

## Suggested Agent Prompt
Implement `SB016` from `process-core-contract-candidate-driver-readiness-prep-v1`. Preserve runtime behavior. Keep this work module-local and do not introduce Process Core or production driver APIs.
