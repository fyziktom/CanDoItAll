# Process Failure, Retry, and Rework Analysis

## Current behavior

The process automation dispatcher retries or recovers the current step rather than restarting the whole process. That is a sound base design.

Current pattern observed:

1. Execute agent step with structured output contract.
2. If a run succeeds but lacks required proof/tools/artifacts, classify it as incomplete.
3. If a run fails in a recoverable way, optionally repair provider assignment.
4. Clear the chat session for recovery attempts to avoid stale context.
5. Build a text recovery directive.
6. Retry the current step.
7. Carry forward successful tool names across attempts.

## What is missing

### Typed recovery decision

The dispatcher needs a durable, structured decision object that says why a step is being retried and what kind of recovery is being used.

Recommended enum:

```csharp
public enum AgentRecoveryMode
{
    None,
    FormatRepair,
    FreshStepRetry,
    ReworkContinuation,
    ProviderFallback,
    HumanEscalation,
    Abort
}
```

Recommended DTO:

```csharp
public sealed class AgentRecoveryDecision
{
    public required AgentRecoveryMode Mode { get; init; }
    public required string Category { get; init; }
    public required string Reason { get; init; }
    public required IReadOnlyList<Guid> SourceExecutionRunIds { get; init; }
    public required IReadOnlyList<string> ValidationErrors { get; init; }
    public required IReadOnlyList<string> MissingProofs { get; init; }
    public required IReadOnlyList<string> InvalidatedProofs { get; init; }
    public required bool RequiresFreshSession { get; init; }
    public required bool RequiresHumanReview { get; init; }
}
```

### Typed rework packet

A QA/build/test/browser failure should not only say “try again”. It should produce a packet instructing the agent to complete targeted delta work.

Recommended DTO:

```csharp
public sealed class AgentReworkPacket
{
    public required Guid Id { get; init; }
    public required Guid ProcessRunId { get; init; }
    public required Guid StepRunId { get; init; }
    public required IReadOnlyList<Guid> SourceExecutionRunIds { get; init; }
    public required AgentRecoveryMode RecoveryMode { get; init; }
    public required string Objective { get; init; }
    public required IReadOnlyList<AgentReworkFinding> Findings { get; init; }
    public required IReadOnlyList<AgentReworkArtifactRef> ArtifactsToInspect { get; init; }
    public required IReadOnlyList<AgentToolReceiptRef> FailedToolReceipts { get; init; }
    public required IReadOnlyList<AgentProofRequirement> ProofsToRerun { get; init; }
    public required IReadOnlyList<AgentReusableProofRef> ReusableProofs { get; init; }
    public required IReadOnlyList<string> MinimalNextActions { get; init; }
    public required IReadOnlyList<string> ProhibitedActions { get; init; }
    public string? HumanDirective { get; init; }
}
```

### Proof fingerprints

Current carry-forward uses tool names. A proof can be reused only if the fingerprint still matches: tool name, normalized arguments, working directory, relevant source/config/artifact hashes, environment/tool version, status, receipt id, and captured timestamp.

### Context strategy

Recommended strategy:

- Never use failed chat history as source of truth.
- For `FreshStepRetry`, start a fresh session with a structured recovery decision summary.
- For `ReworkContinuation`, start a fresh or controlled session, but include only the typed rework packet, relevant artifact references, short prior outcome summary, and reusable proof references.
- For approval continuations, preserve the original MAF session and structured output contract.
- For format repair, avoid a full agent rerun when deterministic JSON extraction/repair is enough.
