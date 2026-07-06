using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public static class MemoryOperationRequestBuilder
{
    public static MemoryOperationHandlerRequest<MemoryContextQueryRequest> Query(
        MemoryOperationCaller caller,
        MemoryProviderSelectionPolicy selectionPolicy,
        MemoryContextQueryRequest query,
        MemoryLedgerRetentionPolicy retention)
    {
        ArgumentNullException.ThrowIfNull(query);
        IReadOnlyList<MemorySourceSnapshotId> snapshotIds = query.SourceProvenance.SourceSnapshotId is { } sourceSnapshotId
            ? new[] { sourceSnapshotId }
            : [];
        return Create(caller, selectionPolicy, MemoryOperationKind.ContextQuery, snapshotIds, retention, query);
    }

    public static MemoryOperationHandlerRequest<MemoryIngestionRequest> Ingestion(
        MemoryOperationCaller caller,
        MemoryProviderSelectionPolicy selectionPolicy,
        MemoryIngestionRequest ingestion,
        MemoryLedgerRetentionPolicy retention)
    {
        ArgumentNullException.ThrowIfNull(ingestion);
        return Create(
            caller,
            selectionPolicy,
            MemoryOperationKind.Ingestion,
            [ingestion.SourceSnapshotId],
            retention,
            ingestion);
    }

    public static MemoryOperationHandlerRequest<MemorySourceCaptureOperationRequest> SourceCapture(
        MemoryOperationCaller caller,
        MemoryProviderSelectionPolicy selectionPolicy,
        MemorySourceCaptureOperationRequest sourceCapture,
        MemoryLedgerRetentionPolicy retention)
    {
        ArgumentNullException.ThrowIfNull(sourceCapture);
        return Create(caller, selectionPolicy, MemoryOperationKind.Ingestion, [], retention, sourceCapture);
    }

    public static MemoryOperationHandlerRequest<MemoryFeedbackOperationRequest> Feedback(
        MemoryOperationCaller caller,
        MemoryProviderSelectionPolicy selectionPolicy,
        MemoryFeedbackRequest feedback,
        MemoryLedgerRetentionPolicy retention)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        return Create(
            caller,
            selectionPolicy,
            MemoryOperationKind.Feedback,
            [],
            retention,
            new MemoryFeedbackOperationRequest(
                feedback,
                MemoryFeedbackStage.ContextUsed,
                "Feedback was submitted without a persisted context delivery record."));
    }

    public static MemoryOperationHandlerRequest<MemoryOperationStatusRequest> Status(
        MemoryOperationCaller caller,
        MemoryProviderSelectionPolicy selectionPolicy,
        MemoryOperationStatusRequest status,
        MemoryLedgerRetentionPolicy retention)
    {
        ArgumentNullException.ThrowIfNull(status);
        return Create(caller, selectionPolicy, MemoryOperationKind.OperationStatus, [], retention, status);
    }

    public static MemoryOperationHandlerRequest<MemoryOperationCancellationRequest> Cancellation(
        MemoryOperationCaller caller,
        MemoryProviderSelectionPolicy selectionPolicy,
        MemoryOperationCancellationRequest cancellation,
        MemoryLedgerRetentionPolicy retention)
    {
        ArgumentNullException.ThrowIfNull(cancellation);
        return Create(caller, selectionPolicy, MemoryOperationKind.OperationStatus, [], retention, cancellation);
    }

    public static MemoryOperationHandlerRequest<MemoryEventAcknowledgeRequest> EventAcknowledge(
        MemoryOperationCaller caller,
        MemoryProviderSelectionPolicy selectionPolicy,
        MemoryEventAcknowledgeRequest acknowledgement,
        MemoryLedgerRetentionPolicy retention)
    {
        ArgumentNullException.ThrowIfNull(acknowledgement);
        return Create(caller, selectionPolicy, MemoryOperationKind.EventAcknowledge, [], retention, acknowledgement);
    }

    public static MemoryOperationHandlerRequest<MemorySourceRequest> SourceRequest(
        MemoryOperationCaller caller,
        MemoryProviderSelectionPolicy selectionPolicy,
        MemorySourceRequest sourceRequest,
        MemoryLedgerRetentionPolicy retention)
    {
        ArgumentNullException.ThrowIfNull(sourceRequest);
        return Create(caller, selectionPolicy, MemoryOperationKind.SourceRequest, [], retention, sourceRequest);
    }

    private static MemoryOperationHandlerRequest<TPayload> Create<TPayload>(
        MemoryOperationCaller caller,
        MemoryProviderSelectionPolicy selectionPolicy,
        MemoryOperationKind operationKind,
        IReadOnlyList<MemorySourceSnapshotId> sourceSnapshotIds,
        MemoryLedgerRetentionPolicy retention,
        TPayload payload)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(selectionPolicy);
        ArgumentNullException.ThrowIfNull(retention);
        ArgumentNullException.ThrowIfNull(payload);
        return new MemoryOperationHandlerRequest<TPayload>(
            caller,
            selectionPolicy,
            operationKind,
            sourceSnapshotIds.ToArray(),
            retention,
            payload);
    }
}
