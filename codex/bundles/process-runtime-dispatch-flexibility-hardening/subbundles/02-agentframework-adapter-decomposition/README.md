# AgentFramework Adapter Decomposition

## Status

- `Completed`

## Objective

- Extract and decompose the AgentFramework/MAF process driver implementation below the Processes dependency boundary while preserving AgentFramework process execution, subprocess behavior, structured output validation, manager signals, and result conversion.

## Covered Inputs

- `R001` Preserve behavior.
- `R002` Split integration file.
- `R004` Isolate AgentFramework driver behavior.
- `R006` Isolate subprocess behavior.
- `R010` Keep diagnostics explicit and actionable.
- `R013` Driver ports own completion evidence, prompt composition, and step execution dispatch behavior.
- `R014` Maintain one-way dependency direction from MAF/AgentFramework to Processes contracts.

## Prerequisites

- SB01 boundary and project placement gate passed.
- SB01 must identify the target owner for MAF/AgentFramework driver orchestration, subprocess lifecycle, output validation, result mapping services, and dependency-direction proof.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessSubprocessLaunchContracts.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchDeferredException.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessExecutionAdapterContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.RuntimeToolReceipts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests.cs`

## Deliverables

- MAF/AgentFramework process driver implementation below `src/Processes`, or an explicit blocker if project placement cannot satisfy the dependency rule.
- Focused driver orchestration class with explicit collaborators.
- Subprocess lifecycle service covering existing pending, stopped, launched, deferred, and synthesized outcome behavior.
- Agent invocation service or wrapper isolating workspace execution calls and execution metadata creation.
- Structured output validation/result conversion service that maps `ProcessStepOutcomeResult` to `ProcessExecutionAdapterResult`.
- Transient execution and output-contract retry classifier extracted from adapter private methods.
- Direct tests for each extracted service plus adapter-level regression tests.

## Dependency Impact

- SB04 depends on the driver extraction because completion evidence policy currently lives inside the adapter and must become driver-owned.
- SB07 depends on this phase to prove the behavior is not still coupled to a single giant adapter test fixture.

## Validation Depth

- Critical foundation.
- Requires Semantic Adequacy Gate proof and artifact-backed proof manifest.

## Implementation Steps

1. Identify the smallest cohesive extraction set for MAF driver step execution dispatch, subprocess lifecycle, agent invocation, output validation, result conversion, and retry classification.
2. Add focused services in the SB01-approved MAF-owned driver location or stop with a blocker if that placement cannot compile.
3. Move behavior without changing messages, diagnostic codes, result hashes, manager signal semantics, or retry/idempotency classifications unless a test proves a correction is needed.
4. Wire services through DI or constructor injection.
5. Split or add focused tests for active child wait, stopped child completion, blocked child propagation, missing coordinator, invalid structured output, missing agent, readiness failure, transient provider failure, output contract failure, and no Processes-to-MAF references.
6. Keep adapter-level tests as regression coverage until direct service tests cover the extracted behavior.
7. Update proof artifacts and execution report.

## Scope Exceptions

- Product completion, required receipt, required path, file content, managed artifact materialization, and grounding policy extraction are owned by SB04 unless a small move is required to keep adapter extraction coherent.

## Do Not Do

- Do not change process result semantics or manager diagnostic codes for cleanup-only reasons.
- Do not add any MAF/AgentFramework project reference to `src/Processes/*`.
- Do not collapse subprocess launch into Workbench-specific logic; keep the driver side coordinator-agnostic.
- Do not delete adapter regression tests until focused service tests prove equivalent behavior.

## Acceptance Checklist

- Adapter orchestration is readable and delegates to focused services.
- AgentFramework/MAF driver implementation references Processes abstractions from below; Processes projects do not reference it.
- Subprocess lifecycle behavior has direct positive and negative tests.
- Agent invocation path still creates the expected `ExecutionRunRequest` context metadata.
- Structured output invalid cases still return `StrategyOutcome.Failed` or `NeedsManager` as before.
- Diagnostic safe summaries include actionable run/step context.

## Proof Required

- `proof/SB02/manifest.md` with changed-file hashes, command transcripts, source assertions, anti-stub audit, and downstream smoke proof.
- `proof/SB02/semantic-invariants.md` with invariants for subprocess deferral, completed child synthesis, agent output validation, and manager signal classification.
- Failing-first proof for at least one shallow adapter extraction that would skip child-run handling or accept invalid structured output.
- Dependency-direction scan transcript proving no Processes-to-MAF reference.
- Passing unit test transcript for focused adapter service tests.
- Passing adapter regression test transcript.

## Browser Validation Logging

- N/A - no browser-visible behavior should change in SB02.

## Progression Gate

- SB04 may start only after MAF driver orchestration no longer hides subprocess lifecycle and output validation in the old monolith, proof shows extracted services preserve current behavior, and dependency-direction scans pass.

## Suggested Agent Prompt

```text
Implement SB02 only. Extract/decompose AgentFrameworkProcessExecutionAdapter into MAF/AgentFramework process driver services approved by SB01. Preserve diagnostic codes, result semantics, subprocess lifecycle behavior, and structured output validation. Do not add any MAF reference to src/Processes. Add direct tests for each extracted service and keep adapter regression tests until equivalent proof exists. Capture proof/SB02/manifest.md and proof/SB02/semantic-invariants.md before marking the phase complete.
```

