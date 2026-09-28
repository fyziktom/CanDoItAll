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

/// <summary>
/// Outcome of a memory operation request. In the memory-provider HTTP API it is a PascalCase string token and the
/// response status follows it: <c>Completed</c> 200; <c>Accepted</c> 202; <c>ProviderNotFound</c> and
/// <c>NotFound</c> 404; <c>CapabilityDenied</c>, <c>ProviderDenied</c> and <c>AccessDenied</c> 403;
/// <c>DriverUnavailable</c>, <c>DriverFailed</c>, <c>SourceCaptureFailed</c> and <c>Failed</c> 502; <c>TimedOut</c>
/// 504; every other value 409. Values: <c>Completed</c> (finished), <c>Accepted</c> (accepted as an asynchronous
/// operation), <c>NoProviderConfigured</c>, <c>NoEnabledProvider</c>, <c>ProviderNotFound</c>,
/// <c>ProviderDisabled</c>, <c>CapabilityUnavailable</c> (the provider does not advertise the capability),
/// <c>CapabilityDenied</c> (policy denies the capability), <c>CapabilityMismatch</c> (the request does not include its
/// required capability), <c>DriverUnavailable</c> (no usable driver for the provider), <c>SourceCaptureFailed</c>,
/// <c>NotFound</c> (the operation does not exist), <c>Cancelled</c>, <c>Failed</c>, <c>TimedOut</c>,
/// <c>UnsupportedOperation</c> (the provider reported the operation as unsupported), <c>ProviderDenied</c>,
/// <c>ProviderSelectionRequired</c>, <c>AccessDenied</c> (the operation belongs to another caller),
/// <c>ProviderConfigurationFailed</c> and <c>DriverFailed</c> (the provider call failed or returned an invalid
/// result).
/// </summary>
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
