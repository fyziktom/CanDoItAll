# Target Rework and Recovery Architecture

## Principles

1. The process engine owns state and transitions.
2. The agent owns reasoning and local implementation, but only inside the current step or repair step.
3. Failed chat history is never the source of truth.
4. Durable artifacts, tool receipts, process state, structured outputs, and typed rework packets are the source of truth.
5. Recovery mode determines context strategy.
6. QA rework should be minimal-delta repair, not a full restart.
7. Proof reuse requires fingerprints, not just tool names.

## Proposed flow

```text
Agent execution fails or produces incomplete work
  -> capture execution run, receipts, output, validation errors
  -> classify failure
  -> update retry/rework ledger
  -> if format issue: run output repair only
  -> if provider issue: provider fallback fresh retry
  -> if QA/build/test/browser/artifact issue: create AgentReworkPacket
  -> build context using recovery mode
  -> run current/repair step with finalizer required
  -> rerun invalidated proofs
  -> validate structured outcome
  -> persist result or escalate
```

## Recovery modes

```csharp
public enum AgentRecoveryMode
{
    None,
    FormatRepair,
    FreshStepRetry,
    ReworkContinuation,
    ProviderFallbackRetry,
    ApprovalContinuation,
    HumanEscalation
}
```

## Failure categories

Suggested categories:

```text
StructuredOutputInvalid
FinalizerMissing
FinalizerInvalid
MissingRequiredTool
CriticalToolFailure
ProviderFailure
BuildFailure
TestFailure
BrowserProofFailure
QaRejected
ArtifactMissing
PermissionDenied
RepeatedToolLoop
Timeout
HumanRequestedRerun
```

## Context strategies

| Mode | Session | Context source | Expected action |
|---|---|---|---|
| FormatRepair | none | raw output + schema + validation errors | clean output only |
| FreshStepRetry | new session | process state + artifacts + receipts + recovery summary | redo current step safely |
| ReworkContinuation | new/controlled session | rework packet + target artifacts + proof receipts | minimal delta repair |
| ProviderFallbackRetry | new session | provider failure + process state + artifacts | rerun current step on fallback provider |
| ApprovalContinuation | same compatible session | approval response + pending approval content | continue paused tool call |
| HumanEscalation | none or UI | ledger + packet + failure summary | ask human |

## Rework packet storage

Persist packet as either:

- a process journal event with JSON payload;
- a dedicated process rework table/entity;
- an execution metadata record linked to the process run and step run.

Do not store it only in prompt text.

## Finalizer/structured output requirement

Governed process steps must continue using required finalizer and structured output. Rework packets do not replace the finalizer; they enrich the input context and evidence.

## Proof reuse

A proof receipt can be reused only if:

- tool name and command match;
- working directory/project path match;
- relevant input file hashes match;
- relevant artifact hashes match;
- environment/tool version is compatible;
- proof has not expired;
- no later mutation invalidates it.

Otherwise, rerun the proof tool.
