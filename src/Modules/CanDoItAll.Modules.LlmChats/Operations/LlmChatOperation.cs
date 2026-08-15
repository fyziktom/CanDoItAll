using CanDoItAll.Modules.LlmChats.Common;

namespace CanDoItAll.Modules.LlmChats.Operations;

public enum LlmChatOperationKind
{
    SendTurn,
    Cancel,
    Recover
}

public enum LlmChatOperationStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    CancellationRequested,
    Cancelled,
    RecoveryRequired
}

public enum LlmChatDispatchPhase
{
    Queued,
    Claimed,
    ProviderDispatchStarted,
    ProviderDispatchReturned
}

public sealed record LlmChatOperation
{
    public LlmChatOperation(
        LlmChatOperationId id,
        LlmChatConversationId conversationId,
        LlmChatOperationKind kind,
        LlmChatRequestFingerprint requestFingerprint,
        long expectedTranscriptRevision,
        LlmChatOperationStatus status,
        DateTimeOffset startedAtUtc,
        long concurrencyToken)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("An operation requires an id.", nameof(id));
        }

        if (conversationId.Value == Guid.Empty)
        {
            throw new ArgumentException("An operation requires a conversation id.", nameof(conversationId));
        }

        if (string.IsNullOrWhiteSpace(requestFingerprint.Value))
        {
            throw new ArgumentException("An operation requires a request fingerprint.", nameof(requestFingerprint));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown operation kind.");
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown operation status.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(expectedTranscriptRevision);
        ArgumentOutOfRangeException.ThrowIfNegative(concurrencyToken);

        Id = id;
        ConversationId = conversationId;
        Kind = kind;
        RequestFingerprint = requestFingerprint;
        ExpectedTranscriptRevision = expectedTranscriptRevision;
        Status = status;
        StartedAtUtc = startedAtUtc;
        ConcurrencyToken = concurrencyToken;
    }

    public LlmChatOperationId Id { get; init; }

    public LlmChatConversationId ConversationId { get; init; }

    public LlmChatOperationKind Kind { get; init; }

    public LlmChatRequestFingerprint RequestFingerprint { get; init; }

    public long ExpectedTranscriptRevision { get; init; }

    public LlmChatOperationStatus Status { get; init; }

    public DateTimeOffset? CancellationRequestedAtUtc { get; init; }

    public long CancellationGeneration { get; init; }

    public LlmChatExecutionOwnerId? ExecutionOwnerId { get; init; }

    public long ExecutionEpoch { get; init; }

    public DateTimeOffset? ClaimedAtUtc { get; init; }

    public DateTimeOffset? HeartbeatAtUtc { get; init; }

    public DateTimeOffset? LeaseExpiresAtUtc { get; init; }

    public LlmChatDispatchPhase DispatchPhase { get; init; } = LlmChatDispatchPhase.Queued;

    public DateTimeOffset? TurnAdmittedAtUtc { get; init; }

    public DateTimeOffset? ProviderDispatchStartedAtUtc { get; init; }

    public DateTimeOffset? ProviderDispatchReturnedAtUtc { get; init; }

    public DateTimeOffset? TranscriptCompletedAtUtc { get; init; }

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public long? ResultingTranscriptRevision { get; init; }

    public Guid? AssistantEntryId { get; init; }

    public string FailureCode { get; init; } = string.Empty;

    public long ConcurrencyToken { get; init; }

    public bool IsTerminal => Status is
        LlmChatOperationStatus.Succeeded or
        LlmChatOperationStatus.Failed or
        LlmChatOperationStatus.Cancelled;

    public bool HasLiveExecutionLease(DateTimeOffset observedAtUtc)
        => ExecutionOwnerId is not null && LeaseExpiresAtUtc > observedAtUtc;
}
