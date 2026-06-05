# SB23 - Refactor Gate E: carry/mock/write parity

## Status

Prepared.

## Objective

Tests for carry-forward, historical mutation, process mock proof, recorded artifact write satisfaction.

## Covered Inputs

- Preserve behavior.
- Continue module-local dispatcher isolation.
- Do not rush Process Core.
- Prepare driver readiness only as documentation.
- No small/medium/mobile proof artifacts.

## Prerequisites

- Previous subbundle closure gate passed: `SB22`.
- Branch: `maf-processes-refactor`.
- Current source references exist.

## Exact Source References

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ImplementationProof.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryPackets.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Deliverables

- Implement the narrow scope described by this subbundle.
- Update proof manifest under `proof/SB23/manifest.md`.
- Update semantic invariants under `proof/SB23/semantic-invariants.md`.
- Update execution report row.

## Dependency Impact

Downstream subbundle: `SB24`.

This is a critical foundation gate. Downstream work must not continue unless this gate passes.

## Validation Depth

Deep: build + focused tests + source scans + anti-stub + no-core/no-driver + no UI/proof path scan.

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

- [ ] Scope implemented.
- [ ] Existing behavior preserved.
- [ ] Tests/source scans recorded.
- [ ] No Process Core.
- [ ] No production driver API.
- [ ] No UI/proof drift.
- [ ] Execution report updated.

## Proof Required

- Build/test transcript.
- Source scan transcript.
- Semantic invariants.
- Anti-stub audit.
- No-core/no-driver/no-UI scan.

## Browser Validation Logging

N/A expected. Runtime/service refactor only. If UI files change unexpectedly, stop and reopen scope. Do not create small/medium/mobile proof artifacts.

## Progression Gate

Critical gate.

## Suggested Agent Prompt

Execute `SB23 - Refactor Gate E: carry/mock/write parity` from `codex/bundles/process-dispatch-implementation-proof-evidence-boundary-v1`. Stay inside the declared scope, update proof, and stop if any behavior parity test fails.
