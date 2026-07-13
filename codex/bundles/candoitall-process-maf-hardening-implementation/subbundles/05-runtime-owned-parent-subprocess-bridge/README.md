# SB05 - Runtime Owned Parent Subprocess Bridge

## Status

- `Completed`
- Critical foundation: yes

## Objective

Make controlled `StepKind=Subprocess` execution deterministic: runtime launches or resolves child runs, waits/defer while active, synthesizes parent evidence from accepted child outputs, and propagates no-go child outputs as concrete blockers.

## Covered Inputs

- F03, F04, F09.
- R07, R08, R13, R15.
- GPTPro B02.

## Prerequisites

- SB04 typed subprocess contract model passes.
- SB02 packet categories available for bridge diagnostics.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.Subprocess.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.SubprocessState.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ManagedArtifacts.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ResultConversion.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessStrategyContracts.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeArtifactContracts.cs`

## Deliverables

- `IParentSubprocessArtifactBridge` request/result contract.
- Focused bridge implementation that validates typed contract outputs.
- Result cases: `NoMatchingChildRun`, `ChildActive`, `AcceptedChildOutputBridged`, `NoGoChildOutputFound`, `ChildCompletedWithoutAcceptedOutput`, `BridgeInfrastructureFailure`.
- Parent managed artifact synthesis for accepted child output.
- Adapter path that bypasses normal agent execution for runtime-owned subprocess when bridge can decide.
- Compatibility/manual fallback for agent-owned launch only when contract permits it.

## Dependency Impact

- SB08 template migration depends on runtime support. SB09 final proof depends on accepted/no-go bridge behavior.

## Validation Depth

- Critical foundation with semantic adequacy gate.

## Implementation Steps

1. Define bridge request/result contracts with typed result cases.
2. Implement child run lookup and state classification behind testable abstractions.
3. Validate accepted and no-go child outputs from typed `SubprocessContract`.
4. Synthesize parent evidence file with parent run/step, parent expectation, child run, child step/artifact, child managed ref, materialization mode, and content hash.
5. Return waiting/deferred result when child is active.
6. Return concrete no-go blocker when no-go child output exists.
7. Return concrete missing accepted output blocker when child completed without accepted/no-go evidence.
8. Modify adapter entry point to call bridge before `ExecuteRunAsync` for runtime-owned contracts.
9. Add tests for every result case and for `ExecuteRunAsync` bypass.

## Scope Exceptions

- Template file migration is SB08.
- Content-grounded produced artifact identity is completed in SB06; SB05 should use available materialization contracts and leave TODO-free integration points.

## Do Not Do

- Do not accept child folder existence as successful handoff.
- Do not ask a normal agent to launch controlled child processes when runtime-owned contract can decide.
- Do not hide no-go output as retryable missing evidence.
- Do not put bridge logic directly into old adapter partials.

## Acceptance Checklist

- [ ] `prepare-solution-skeleton` completes from `setup-handoff`.
- [ ] `prepare-solution-skeleton` completes from `setup-handoff-after-repair`.
- [ ] `setup-repair-escalation` blocks parent with concrete no-go evidence.
- [ ] child active defers parent.
- [ ] child completed without accepted output blocks with concrete diagnostic.
- [ ] adapter does not call `ExecuteRunAsync` when bridge handles runtime-owned step.

## Proof Required

- `proof/SB05/manifest.md`
- `proof/SB05/semantic-invariants.md`
- Failing-first tests for child folder-only evidence and repaired handoff missing from metadata.
- Passing bridge result tests.
- Source assertion that bridge behavior lives outside adapter partial cluster.
- Changed-file hashes.
- Anti-stub audit.
- Production Behavior Artifact Matrix for parent bridge result and runtime-synthesized parent artifact.

## Browser Validation Logging

- `N/A`.

## Progression Gate

- SB08 may migrate templates only after bridge tests prove accepted/repaired/no-go outcomes for representative child processes.

## C# Architecture Impact

Extracts subprocess orchestration from adapter partial cluster into focused service.

## Boundary Ownership

Contracts in abstractions/drivers-abstractions; implementation in module integration where managed artifact and AgentFramework child state can be accessed.

## Dependency Direction

Runtime must not reference module implementation. Adapter/module can call bridge implementation.

## Pattern Decision

Strategy plus Builder. Bridge result is discriminated by typed result cases.

## Testability Contract

Tests use fake child-state/artifact providers and do not construct live AgentFramework services.

## Partial Class Policy

Adapter partial edits must be thin delegation only.

## Architecture Proof Required

- Source assertion for extracted bridge.
- Direct unit tests.
- CodeAnalytics refresh if references change.

## Suggested Agent Prompt

```text
Execute SB05 only. Implement runtime-owned parent subprocess bridge using SB04 typed contracts. Prove accepted, repaired, active, no-go, and missing-output states. Do not migrate all templates yet.
```
