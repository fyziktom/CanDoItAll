# SB05 - Subprocess And Recovery Loopbacks

## Status

- Status: `Completed`

## Objective

Extract subprocess state resolution, child root-cause propagation, recovery classification, and diagnostic repair packet behavior.

## Covered Inputs

- GPTPro repair loopback findings.
- GPTPro parent subprocess root-cause loss finding.
- User concern that current process remains blocked similarly.

## Prerequisites

- SB01 baseline complete.
- SB02 boundaries complete.
- SB03 pipeline available for typed diagnostics.
- SB04 artifact contracts available where bridge depends on artifacts.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.Subprocess.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.SubprocessState.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ParentSubprocessArtifactBridge.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRecoveryClassifier.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessStepRecoveryInstructionBuilder.cs`

## Dependency Impact

- May refine runtime contracts and module bridge services.
- Project-reference changes require CodeAnalytics proof.

## Validation Depth

- Direct unit tests for child state.
- Direct unit tests for bridge.
- Direct unit tests for recovery classifier.
- Direct unit tests for repair packet builder.
- Repeated-fingerprint escalation test.

## Do Not Do

- Do not use generic retry text as repair strategy.
- Do not hide child root cause behind parent generic blocker.
- Do not treat file existence as accepted artifact evidence.

## Acceptance Checklist

- [ ] Blocked child root cause appears in parent diagnostic.
- [ ] Safe/idempotent issue routes before manager escalation.
- [ ] Diagnostic-specific repair packet tests pass.
- [ ] Adapter delegates subprocess/recovery behavior.

## Proof Required

- Proof manifest with direct and negative tests.
- Source assertions.
- No-new-partial proof.
- Retry/branch/manager route evidence.

## Browser Validation Logging

- Not applicable except final process E2E in SB07.

## Progression Gate

- SB07 final validation may start only after subprocess and recovery loopback tests pass.

## Suggested Agent Prompt

Implement SB05 only. Extract subprocess and recovery behavior into typed services and prove repair loopbacks without manager escalation for safe/idempotent cases.

## Goal

Extract subprocess state resolution, parent bridge behavior, child root-cause propagation, recovery classification, and diagnostic-specific repair packet generation into focused services.

## Scope

- `AgentFrameworkProcessExecutionAdapter.Subprocess.cs`
- `AgentFrameworkProcessExecutionAdapter.SubprocessState.cs`
- `ParentSubprocessArtifactBridge`
- recovery classifier behavior in runtime
- `ProcessStepRecoveryInstructionBuilder`
- child/root-cause diagnostics

## Implementation Steps

1. Use SB01 characterization tests as baseline.
2. Create/refine `ISubprocessRunStateResolver` with typed results:
   - active child,
   - accepted child,
   - completed no-go,
   - stopped blocked with diagnostics,
   - stopped failed,
   - no matching child.
3. Update parent bridge to use typed child state.
4. Make artifact bridge ledger/slot-first; file fallback only as explicit recovery mode.
5. Refine recovery classifier to use typed diagnostic metadata, retry safety, idempotency, fingerprint, and budget.
6. Refine recovery instruction builder to produce diagnostic-specific packets.
7. Ensure safe/idempotent completion-gate failures route to bounded retry or branch route before manager escalation.
8. Update adapter to delegate subprocess and recovery loopback decisions.
9. Delete moved subprocess/recovery methods from adapter partial files.
10. Add direct unit tests and negative tests.
11. Run targeted tests and build.

## C# Architecture Impact

This subbundle directly addresses escalation loopbacks. It separates detecting a problem from deciding whether it should retry, branch, repair assignment, or escalate.

## Boundary Ownership

Generic child state and recovery decisions belong in `Processes.Runtime` where possible. MAF-specific child artifact mapping remains in `Modules.Processes`. Domain-specific repair advice comes from drivers/providers.

## Dependency Direction

Runtime may depend on driver abstractions for domain recovery advice. It must not depend on module/domain implementation projects.

## Pattern Decision

Use Strategy for recovery advice providers. Use State-like typed result records for subprocess child status. Avoid a broad subprocess manager.

## Testability Contract

Required direct tests:

- Blocked child returns parent diagnostic with child root cause.
- Accepted child uses ledger/slot evidence.
- File fallback produces recovery diagnostic and does not masquerade as accepted evidence.
- Safe/idempotent gate issue becomes bounded retry.
- Unsafe/policy issue becomes manager/assignment repair.
- Repeated same fingerprint over budget escalates with concrete root cause.
- Repair packet includes observed vs expected receipts.

## Partial Class Policy

Delete or shrink:

- `AgentFrameworkProcessExecutionAdapter.Subprocess.cs`
- `AgentFrameworkProcessExecutionAdapter.SubprocessState.cs`
- recovery issue helpers in `RecoveryPolicy.cs` and result conversion partials.

No new partials.

## Architecture Proof Required

- Source assertion that subprocess/recovery behavior moved out of adapter.
- Direct unit tests for child resolver, bridge, recovery classifier, and repair packet builder.
- Negative tests for file-only artifact evidence.
- Domain-boundary assertion for recovery advice providers.
- No-new-partial proof.
