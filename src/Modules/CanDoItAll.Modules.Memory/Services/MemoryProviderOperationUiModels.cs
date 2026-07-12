using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Modules.Memory.Services;

public sealed record MemoryProviderOperationUiRecord(
    MemoryOperationId OperationId,
    MemoryProviderInstanceId ProviderInstanceId,
    MemoryCapabilityId RequestedCapability,
    MemoryOperationKind OperationKind,
    MemoryLedgerStatus Status,
    string StatusReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    MemoryOperationAccepted? AcceptedOperation,
    MemoryFeedbackHandle? FeedbackHandle);

public sealed record MemoryProviderFeedbackUiRecord(
    MemoryFeedbackRecordId FeedbackRecordId,
    MemoryProviderInstanceId ProviderInstanceId,
    MemoryFeedbackStage Stage,
    MemoryFeedbackOutcome Outcome,
    MemoryFeedbackMatchState MatchState,
    MemoryLedgerStatus Status,
    string? UnmatchedReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record MemoryProviderEventUiRecord(
    MemoryEventInboxRecordId InboxRecordId,
    MemoryProviderInstanceId ProviderInstanceId,
    MemoryProviderEventId ProviderEventId,
    MemoryProviderEventKind EventKind,
    MemoryEventPriority Priority,
    MemoryLedgerStatus Status,
    string StatusReason,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record MemoryProviderQueryUiResult(
    MemoryOperationHandlerStatus Status,
    string Diagnostic,
    MemoryProviderOperationUiRecord? Operation,
    MemoryContextPack? ContextPack,
    MemoryOperationAccepted? AcceptedOperation,
    MemoryFeedbackHandle? FeedbackHandle,
    bool DriverDispatchAttempted)
{
    public bool HasContextPack => ContextPack is not null;
}

public sealed record MemoryProviderOperationUiResult(
    MemoryOperationHandlerStatus Status,
    string Diagnostic,
    MemoryProviderOperationUiRecord? Operation);

public sealed record MemoryProviderFeedbackUiResult(
    MemoryOperationHandlerStatus Status,
    string Diagnostic,
    MemoryProviderFeedbackUiRecord? Feedback);

public sealed record MemoryProviderManualIngestionUiResult(
    MemoryOperationHandlerStatus Status,
    string Diagnostic,
    Guid JobId,
    MemoryOperationId OperationId,
    string CapturedSnapshotId,
    MemoryProviderOperationUiRecord? Operation);

public sealed record MemoryProviderEventAcknowledgeUiResult(
    MemoryOperationHandlerStatus Status,
    string Diagnostic,
    MemoryProviderEventId EventId);
