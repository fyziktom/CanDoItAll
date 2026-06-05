# SB10 - Concrete product path classification

## Status

- Status: `Completed`

## Objective

Extract concrete product/deliverable/source/project path checks and ignored path segments.

## Covered Inputs

- Preserve behavior.
- Continue module-local dispatcher isolation.
- Do not rush Process Core.
- Prepare driver readiness only as documentation.
- No small/medium/mobile proof artifacts.

## Prerequisites

- Previous subbundle closure gate passed: `SB09`.
- Branch: `maf-processes-refactor`.
- Current source references exist.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ImplementationProof.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryPackets.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Deliverables

- Implement the narrow scope described by this subbundle.
- Update proof manifest under `proof/SB10/manifest.md`.
- Update semantic invariants under `proof/SB10/semantic-invariants.md`.
- Update execution report row.

## Dependency Impact

- Downstream subbundle: `SB11`.
- Downstream work depends on this subbundle preserving behavior and updating proof.

## Validation Depth

- Focused: compile or targeted test proof plus source assertions appropriate to the slice.

## Implementation Steps

1. Re-read current source before editing.
2. Make the smallest behavior-preserving extraction for this subbundle.
3. Keep existing wrapper methods unless explicitly justified.
4. Add or update focused tests before claiming parity.
5. Record source assertions and proof commands.
6. Decide continue/reopen.

## Scope Exceptions

- No Process Core.
- No production process driver API.
- No driver registry/driver pack.
- No UI/browser work.

## Do Not Do

- Do not change exact behavior, status routing, summary strings, retry decisions, missing tool lists, or proof carry-forward semantics unless a failing test proves the old behavior is wrong.
- Do not introduce public contracts.
- Do not hide side effects in pure-looking helpers.
- Do not create mobile/small/medium proof.

## Acceptance Checklist

- [x] Scope implemented.
- [x] Existing behavior preserved.
- [x] Tests/source scans recorded.
- [x] No Process Core.
- [x] No production driver API.
- [x] No UI/proof drift.
- [x] Execution report updated.

## Proof Required

- Build/test transcript.
- Source scan transcript.
- Semantic invariants.
- Anti-stub audit.
- No-core/no-driver/no-UI scan.

## Browser Validation Logging

- N/A expected: runtime/service refactor only. If UI files change unexpectedly, stop and reopen scope. Do not create small/medium/mobile proof artifacts.

## Progression Gate

- No filesystem traversal beyond existing semantics.

## Suggested Agent Prompt

Execute `SB10 - Concrete product path classification` from `codex/bundles/process-dispatch-implementation-proof-evidence-boundary-v1`. Stay inside the declared scope, update proof, and stop if any behavior parity test fails.
