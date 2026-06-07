# SB027 - Gate I - pure-rule parity and Core candidate list

## Status
Completed.

## Objective
Prove rule migration parity and update a Core-candidate inventory that says which pure decisions could move later and which must not.

## Covered Inputs
- Preserve existing behavior.
- Do not rush Process Core.
- Avoid production driver API.
- Runtime/service refactor only; browser validation is expected N/A.

## Prerequisites
- SB026 closed.

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
- Proof transcript under `codex/bundles/process-core-contract-candidate-driver-readiness-prep-v1/proof/SB027/`.
- Execution report row for `SB027`.

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
- Build/test transcript or explanation if proof is deferred to the next critical gate.
- Source assertion transcript.
- Anti-stub scan.
- No-Core/no-driver scan.

## Completion Notes
- Gate I passed. SB026 pure-rule migration parity is covered by focused unit architecture proof and integration parity tests.
- `bundle://architecture/04-core-readiness-decision-matrix-template.md` is now filled and separates candidate-later pure rules from application-local EF, filesystem, transition, workspace, AgentFramework, and adapter behavior.
- No `CanDoItAll.Processes.Core` project or production driver API was introduced.

## Proof Captured
- `bundle://proof/SB027/manifest.md`
- `bundle://proof/SB027/semantic-invariants.md`
- `bundle://proof/SB027/transcripts/critical-build.txt`
- `bundle://proof/SB027/transcripts/gate-i-architecture-tests.txt`
- `bundle://proof/SB027/transcripts/gate-i-integration-parity-tests.txt`
- `bundle://proof/SB027/transcripts/source-assertions-and-scans.txt`
- `bundle://proof/SB027/transcripts/proof-sanity-check.txt`
- `bundle://proof/SB027/transcripts/closure-gate.txt`
- `bundle://proof/SB027/transcripts/changed-file-hashes.txt`

## Browser Validation Logging
- N/A - runtime/service refactor only. If any UI file changes unexpectedly, stop and reopen the scope.

## Progression Gate
- Do not proceed past this critical gate until all proof is complete.

## Suggested Agent Prompt
Implement `SB027` from `process-core-contract-candidate-driver-readiness-prep-v1`. Preserve runtime behavior. Keep this work module-local and do not introduce Process Core or production driver APIs.
