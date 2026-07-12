using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public sealed record MemoryOperationHandlerRequest<TPayload>(
    MemoryOperationCaller Caller,
    MemoryProviderSelectionPolicy SelectionPolicy,
    MemoryOperationKind OperationKind,
    IReadOnlyList<CanDoItAll.Memory.Abstractions.MemorySourceSnapshotId> SourceSnapshotIds,
    MemoryLedgerRetentionPolicy Retention,
    TPayload Payload)
{
    public MemoryCorrelationId CorrelationId { get; init; } = MemoryCorrelationId.New();

    public MemoryCausationId CausationId { get; init; } = MemoryCausationId.New();

    public MemoryExtensionData Extensions { get; init; } = MemoryExtensionData.Empty;
}

public enum MemoryOperationHandlerStatus
{
    Completed = 0,
    Accepted = 1,
    NoProviderConfigured = 2,
    NoEnabledProvider = 3,
    ProviderNotFound = 4,
    ProviderDisabled = 5,
    CapabilityUnavailable = 6,
    CapabilityDenied = 7,
    CapabilityMismatch = 8,
    DriverUnavailable = 9,
    SourceCaptureFailed = 10,
    NotFound = 11,
    Cancelled = 12,
    Failed = 13,
    TimedOut = 14,
    UnsupportedOperation = 15,
    ProviderDenied = 16,
    ProviderSelectionRequired = 17,
    AccessDenied = 18,
    ProviderConfigurationFailed = 19,
    DriverFailed = 20
}

public sealed record MemoryOperationHandlerResult<TOutput>(
    MemoryOperationHandlerStatus Status,
    MemoryProviderSelectionResult Selection,
    MemoryOperationRecord? OperationRecord,
    TOutput? Output,
    MemoryOperationAccepted? AcceptedOperation,
    MemoryFeedbackHandle? FeedbackHandle,
    bool DriverDispatchAttempted,
    string Diagnostic);

public sealed record MemorySourceCaptureOperationRequest(
    MemoryProviderInstanceId ProviderInstanceId,
    MemorySourceGatewayRequest SourceGatewayRequest,
    string StatusReason);

public sealed record MemorySourceCaptureOperationResult(
    MemorySourceIngestionJobRecord JobRecord,
    IReadOnlyList<MemorySourcePayloadForm> PayloadForms);

public sealed record MemoryFeedbackOperationRequest(
    MemoryFeedbackRequest Feedback,
    MemoryFeedbackStage Stage,
    string UnmatchedReason);
