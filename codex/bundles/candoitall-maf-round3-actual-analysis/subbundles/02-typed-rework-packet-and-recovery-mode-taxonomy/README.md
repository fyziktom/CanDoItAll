# 02 - Typed Rework Packet and Recovery Mode Taxonomy

## Problem

Current recovery is mostly a bounded retry loop plus text recovery directive. That is safe but inefficient for partial work and QA returns.

## Required implementation

Introduce typed models:

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

public sealed class AgentRecoveryDecision
{
    public required AgentRecoveryMode Mode { get; init; }
    public required string FailureCategory { get; init; }
    public required string Reason { get; init; }
    public required int AttemptNumber { get; init; }
    public string? SourceExecutionRunId { get; init; }
    public Guid? ReworkPacketId { get; init; }
}
```

Add `AgentReworkPacket` and supporting DTOs.

## Acceptance criteria

- Retry decision code returns typed decision, not only bool/string reason.
- Text recovery directive is rendered from a typed decision/packet.
- Packet JSON is persisted independently from the prompt.
- Execution metadata/journal includes recovery mode and failure category.

## Tests

- Build failure maps to `ReworkContinuation` or `FreshStepRetry` depending on context.
- Provider failure maps to `ProviderFallbackRetry`.
- Wrapped JSON maps to `FormatRepair` and does not create a new agent run.
- QA rejection maps to `ReworkContinuation` with packet.

## Execution status

Completed. `AgentRecoveryDecision`, `AgentRecoveryMode`, failure categories, and `AgentReworkPacket` are implemented with behavior tests.
