# SB031 - Core extraction readiness scorecard

## Status
Completed.

## Objective
Produce a scored decision matrix for route/core/process-module/application/infrastructure ownership after this bundle.

## Covered Inputs
- Preserve existing behavior.
- Do not rush Process Core.
- Avoid production driver API.
- Runtime/service refactor only; browser validation is expected N/A.

## Prerequisites
- SB030 closed.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModels.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerApplicationService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentRuntimeService.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`


## Deliverables
- Code refactor for this slice, or documented N/A if source inspection proves it already complete.
- Focused tests or source assertions for the moved behavior.
- Proof transcript under `codex/bundles/process-core-contract-candidate-driver-readiness-prep-v1/proof/SB031/`.
- Execution report row for `SB031`.

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
- Build/test transcript or explanation if proof is deferred to the next critical gate.
- Source assertion transcript.
- Anti-stub scan.
- No-Core/no-driver scan.

## Completion Notes
- Documentation-only implementation. `bundle://architecture/07-core-extraction-readiness-scorecard.md` scores candidate Core, process-module, application, and infrastructure ownership after the bundle.
- Build/test proof is deferred to SB032 broad smoke because SB031 changed no production source or test source.
- No `CanDoItAll.Processes.Core` project or production driver API was introduced.

## Proof Captured
- `bundle://proof/SB031/transcripts/entry-gate.txt`
- `bundle://proof/SB031/transcripts/source-assertions-and-scans.txt`
- `bundle://proof/SB031/transcripts/build-test-deferred.txt`
- `bundle://proof/SB031/transcripts/closure-gate.txt`
- `bundle://proof/SB031/transcripts/changed-file-hashes.txt`

## Browser Validation Logging
- N/A - runtime/service refactor only. If any UI file changes unexpectedly, stop and reopen the scope.

## Progression Gate
- May proceed to the next subbundle only if source compiles locally or the change is proof/documentation only.

## Suggested Agent Prompt
Implement `SB031` from `process-core-contract-candidate-driver-readiness-prep-v1`. Preserve runtime behavior. Keep this work module-local and do not introduce Process Core or production driver APIs.
