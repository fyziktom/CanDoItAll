# Finalizer Driver Isolation

## Status

- `Completed`

## Objective

- Isolate required-finalizer behavior behind a driver, strategy, or focused finalizer helper boundary while preserving process-critical semantics.

## Covered Inputs

- N003
- Requirements R03, R09, R10

## Prerequisites

- SB01 closure gate passed.
- SB02 closure gate passed.
- SB03 closure gate passed because finalizer recovery uses session serialization.
- Current finalizer behavior tests are green before extraction.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Finalizers/AgentFinalizerPolicy.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentFinalizerPolicyTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkExecutionRunTrackingIntegrationTests.cs`

## Deliverables

- Finalizer driver/strategy boundary with explicit typed inputs and outputs.
- Streamed finalizer invocation recorder moved out of `MafAgentRuntime`.
- Required-finalizer repair prompt/build/run-option logic moved behind the finalizer boundary or a related focused collaborator.
- Tests proving missing, malformed, duplicate, valid, recovery, transcript, sequence, and provider-failure finalizer behavior.

## Dependency Impact

- Process automation completion, workflow runtime proof, provider usage, and transcript persistence depend on this subbundle.
- SB07 must not start until finalizer behavior passes semantic proof.
- SB08 UI/process smoke proof depends on this subbundle.

## Validation Depth

- `Process-critical closure`
- `Critical foundation`

## Implementation Steps

1. Add failing-first or characterization proof for current finalizer behavior.
2. Define finalizer driver input/output records and boundary methods.
3. Move repair prompt, JSON repair, invocation normalization, streamed capture, early short-circuit, provider-failure recovery, process artifact recovery, and runtime response building into focused collaborators.
4. Keep validation order and sequence checks unchanged.
5. Update runtime orchestration to delegate to the driver.
6. Run unit and integration tests before touching SB07.

## Scope Exceptions

- Do not redesign finalizer policy models unless a test proves the current shape blocks extraction.

## Do Not Do

- Do not let assistant prose override required finalizer output.
- Do not make finalizer recovery silently succeed without validation.
- Do not skip finalizer sequence validation.
- Do not create a monolithic `FinalizerHelpers` replacement with all old runtime code.

## Acceptance Checklist

- Finalizer-heavy methods no longer live in `MafAgentRuntime.cs`.
- The finalizer boundary exposes clear success/failure outputs.
- Required finalizer missing/malformed/multiple-call failures still fail predictably.
- Provider failure after valid finalizer still persists governed output and diagnostics.
- Usage source phases remain correct.

## Proof Required

- `proof/SB06/manifest.md`
- `proof/SB06/semantic-invariants.md`
- Failing-first or characterization finalizer transcript.
- Passing `AgentFinalizerPolicyTests` transcript.
- Passing focused `AgentFrameworkExecutionRunTrackingIntegrationTests` transcript.
- Source assertions for driver delegation and validation order.
- Changed-file hashes.
- Anti-stub audit.
- Semantic Adequacy Gate: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.
- Production Behavior Artifact Matrix if any new finalizer state, event, record, usage source, or process signal is introduced.

## Browser Validation Logging

- Deferred to SB08, but this subbundle must record any finalizer/process UI risk in `reviews/01-execution-report.md`.

## Progression Gate

- SB07 may start only after finalizer semantic proof passes and integration tests prove required-finalizer sequence, recovery, transcript, and usage behavior.

## Suggested Agent Prompt

```text
Implement SB06 only. Isolate finalizer behavior behind a focused driver or strategy boundary, preserve required-finalizer semantics exactly, capture artifact-backed semantic proof under proof/SB06, and stop if validation order or process recovery semantics change unexpectedly.
```
