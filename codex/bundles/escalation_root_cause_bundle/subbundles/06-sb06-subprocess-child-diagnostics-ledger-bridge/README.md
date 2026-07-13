# SB06 - Subprocess Child Diagnostics And Ledger Bridge

## Status

- `Completed`
- Critical foundation: yes

## Objective

Propagate child subprocess root-cause diagnostics to the parent and use accepted artifact slots/ledger evidence as the primary bridge truth. Parent processes must not see only generic "child blocked" text, and child physical output files must not satisfy required parent evidence without accepted slot proof.

## Covered Inputs

- GPTPro child diagnostics and ledger bridge finding.
- REQ-004, REQ-008, REQ-009, REQ-012, REQ-016, REQ-017, REQ-018.
- The blocked parent run where the parent lost the child missing-helper root cause.

## Prerequisites

- SB02 aggregate diagnostics complete.
- SB05 artifact acceptance order complete.
- Current subprocess bridge and contract source references refreshed.

## Exact Source References

- `bundle://codex/05-subprocess-child-diagnostics-and-ledger-bridge.md`
- `bundle://evidence/incident-facts.json`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ParentSubprocessArtifactBridge.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessSubprocessContractResolver.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.Subprocess.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.SubprocessState.cs`
- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://Templates/Processes/processes/dotnet-development-slice/definition.json`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessMafHardeningRegressionTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs`

## Deliverables

- Child run state resolver that distinguishes completed, blocked, failed, no-go, and stopped states.
- Parent diagnostic code such as `process.adapter.subprocess_child_blocked` that carries child diagnostic metadata.
- Result kinds or typed fields for `ChildStoppedBlocked` and `ChildStoppedFailed`.
- Ledger/accepted-slot-first artifact bridge behavior with explicit file fallback only when policy allows it.
- Parent packet that includes child run id, child step id, child diagnostic code, missing receipt, failed readback, and attempted repair/budget state.
- Tests for blocked child, failed child, accepted child artifact, rejected child artifact, and physical-file-only negative case.

## Dependency Impact

- SB09 uses the bridge semantics when migrating subprocess parent templates.
- SB12 uses child propagation proof for the 5032 incident.
- Manager escalation after retry budget exhaustion depends on parent packets carrying the child root cause.

## Validation Depth

- Critical foundation with bridge unit tests, parent packet tests, and subprocess template integration tests.
- Semantic proof must show parent behavior changes for the incident, not only child state labels.

## Implementation Steps

1. Trace current parent subprocess bridge behavior for completed, blocked, failed, skipped, and no-go child runs.
2. Define a typed child state resolution result that carries child diagnostics and accepted artifact slots.
3. Stop skipping stopped non-completed children without preserving root-cause diagnostics.
4. Build parent diagnostics from child aggregate diagnostics.
5. Replace file-existence-first output resolution with ledger/accepted-slot-first resolution.
6. Permit file fallback only behind an explicit recovery policy and label it as fallback evidence.
7. Add parent packet fields for child diagnostic code, receipt miss, failed readback, and retry state.
8. Add negative tests where a child output file exists but accepted slot evidence is absent.
9. Add positive tests where accepted child slots bridge to parent.
10. Update subprocess contract tests to verify no-go and blocked paths remain distinguishable.

## Do Not Do

- Do not collapse all child stopped states into generic blocked text.
- Do not accept child output files without ledger/slot evidence.
- Do not hide child diagnostics when escalating the parent.
- Do not encode child result kinds as scattered strings.

## Acceptance Checklist

- [x] Parent packet includes child diagnostic code for the incident.
- [x] Parent packet includes missing `workspace_pwsh_run_script` when child aggregate has it.
- [x] Blocked and failed child states are distinguishable.
- [x] Accepted child artifact slots bridge to parent.
- [x] Physical-file-only child output is rejected unless explicit fallback policy applies.
- [x] Subprocess no-go outputs remain distinct from repairable blocked outputs.

## Proof Required

- `proof/SB06/manifest.md`
- `proof/SB06/semantic-invariants.md`
- Failing-first parent-loses-child-root-cause test.
- Passing bridge and parent packet tests.
- Source assertions for ledger-first bridge behavior.
- Production Behavior Artifact Matrix if child state/result records are introduced.

## Browser Validation Logging

- `N/A` unless parent process detail UI wording changes; if UI changes, capture the affected process detail route.

## Progression Gate

- SB09 may migrate subprocess templates only after parent bridge behavior proves accepted/no-go/blocked child outcomes are typed and observable.

## C# Architecture Impact

Separates child state resolution from artifact slot bridging and parent packet construction.

## Boundary Ownership

Subprocess bridge owns child-to-parent transfer; artifact lifecycle remains owned by completion/artifact acceptance services.

## Dependency Direction

The bridge must not depend on template markdown or Workbench-specific launch variables.

## Pattern Decision

Use PSR-006: resolver plus bridge facade.

## Testability Contract

Bridge tests must construct child states and ledger slots directly, without needing live agent execution.

## Partial Class Policy

Adapter subprocess partial changes must remain thin. Extract child resolution logic if it grows beyond plumbing.

## Architecture Proof Required

- Child state resolver responsibilities documented.
- Ledger-first proof and physical-file negative proof captured.

## Suggested Agent Prompt

```text
Execute SB06 only. Propagate child root-cause diagnostics into parent subprocess packets and make accepted artifact slots the primary bridge evidence. Prove physical file existence alone cannot satisfy required child outputs.
```
