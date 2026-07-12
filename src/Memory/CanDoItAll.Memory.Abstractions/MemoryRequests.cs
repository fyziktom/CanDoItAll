namespace CanDoItAll.Memory.Abstractions;

public sealed record MemoryContextQueryRequest(
    string Query,
    IReadOnlyList<MemoryCapabilityId> RequestedCapabilities,
    MemorySourceProvenance SourceProvenance)
{
    public MemoryRequestContext Context { get; init; } = MemoryRequestContext.Default;
}

public sealed record MemoryIngestionRequest(
    MemorySourceSnapshotId SourceSnapshotId,
    MemorySourceKind SourceKind,
    MemoryPayload Payload,
    IReadOnlyList<MemoryCapabilityId> RequestedCapabilities)
{
    public MemoryRequestContext Context { get; init; } = MemoryRequestContext.Default;
}

public sealed record MemoryFeedbackRequest(
    MemoryContextPackId ContextPackId,
    MemoryFeedbackOutcome Outcome,
    string? Comment,
    MemoryEconomicImpact? EconomicImpact);

public sealed record MemorySourceRequest(
    MemorySourceRequestId SourceRequestId,
    IReadOnlyList<MemorySourceScope> RequestedScopes,
    string Purpose,
    string ProviderVisibleReason);

public sealed record MemoryEventAcknowledgeRequest(
    MemoryProviderEventId EventId,
    bool Accepted,
    string Reason);

public sealed record MemoryOperationStatusRequest(
    MemoryOperationId OperationId);

public sealed record MemoryOperationCancellationRequest(
    MemoryOperationId OperationId,
    string Reason);

public sealed record MemoryEconomicImpact(
    string Currency,
    decimal Amount);
