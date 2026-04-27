# Target Rework and Recovery Architecture

## Principles

1. Process state is the source of truth; MAF session is conversational context only.
2. Failed runs should be analyzed into typed recovery decisions.
3. Retry mode must be explicit and persisted.
4. QA returns should create typed rework packets, not just textual prompts.
5. Proofs can only be reused when fingerprints prove inputs did not change.
6. Rework should prefer minimal corrective deltas over rerunning the whole step.
7. Recovery must have budgets, backoff, loop detection, and escalation.

## Core models

### AgentRecoveryDecision

```csharp
public enum AgentRecoveryMode
{
    FormatRepair,
    FreshStepRetry,
    ReworkContinuation,
    ProviderFallback,
    HumanEscalation
}

public sealed class AgentRecoveryDecision
{
    public required Guid ProcessRunId { get; init; }
    public required Guid StepRunId { get; init; }
    public required Guid SourceExecutionRunId { get; init; }
    public required AgentRecoveryMode Mode { get; init; }
    public required string FailureCategory { get; init; }
    public required string Reason { get; init; }
    public required AgentContextStrategy ContextStrategy { get; init; }
    public AgentReworkPacket? ReworkPacket { get; init; }
    public required IReadOnlyList<string> RequiredProofToolNames { get; init; }
    public required IReadOnlyList<string> InvalidatedProofReceiptIds { get; init; }
    public required IReadOnlyList<string> ReusableProofReceiptIds { get; init; }
    public TimeSpan? BackoffDelay { get; init; }
}
```

### AgentReworkPacket

```csharp
public sealed class AgentReworkPacket
{
    public required Guid Id { get; init; }
    public required Guid ProcessRunId { get; init; }
    public required Guid StepRunId { get; init; }
    public required Guid SourceExecutionRunId { get; init; }
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

### ProofFingerprint

```csharp
public sealed class ProofFingerprint
{
    public required string ToolName { get; init; }
    public required string NormalizedArgumentsHash { get; init; }
    public required string WorkingDirectoryFingerprint { get; init; }
    public required IReadOnlyDictionary<string, string> RelevantInputFileHashes { get; init; }
    public required IReadOnlyDictionary<string, string> ArtifactHashes { get; init; }
    public required IReadOnlyDictionary<string, string> EnvironmentVersions { get; init; }
    public required DateTimeOffset CapturedAtUtc { get; init; }
}
```

## Recovery modes

### FormatRepair

Used when the work is done but the response format is invalid or wrapped in prose. No new process step attempt should be created unless repair fails.

### FreshStepRetry

Used when the previous attempt is untrustworthy: provider failure, poisoned session, invalid finalizer sequence, missing required tools, looped calls, or failed tool policy. Starts fresh MAF session, but includes durable state and a typed recovery decision.

### ReworkContinuation

Used when artifacts exist and QA/build/test/browser feedback identifies concrete remaining work. Starts with a typed packet and direct artifact inspection. It may use a fresh session, but it should not ask the agent to redo all work.

### ProviderFallback

Used when provider capabilities or transient failures block the attempt. Records original provider, fallback provider, reason, and constraints.

### HumanEscalation

Used after retry budget exhaustion, ambiguous destructive repair, repeated loops, or policy-sensitive actions.

## Prompting pattern for rework

The repair prompt should contain:

1. A short natural-language task.
2. A compact JSON serialization of `AgentReworkPacket`.
3. Explicit instruction to inspect referenced artifacts directly.
4. Explicit instruction to preserve completed work.
5. Required proof tools and invalidated proof ids.
6. Prohibited actions.
7. Structured output/finalizer contract.

Do not paste full failed transcripts unless they are short, redacted, and directly useful.
