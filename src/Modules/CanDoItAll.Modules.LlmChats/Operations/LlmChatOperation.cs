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
}

internal static class LlmChatOperationTransitions
{
    public static LlmChatOperation ClaimDispatch(LlmChatOperation operation)
    {
        RequireStatus(operation, LlmChatOperationStatus.Pending);
        return Advance(operation, LlmChatOperationStatus.Running);
    }

    public static LlmChatOperation MarkTurnAdmitted(
        LlmChatOperation operation,
        DateTimeOffset admittedAtUtc)
    {
        if (operation.TurnAdmittedAtUtc is not null)
        {
            return operation;
        }

        RequireStatus(operation, LlmChatOperationStatus.Running);
        return Advance(operation, operation.Status) with { TurnAdmittedAtUtc = admittedAtUtc };
    }

    public static LlmChatOperation MarkProviderDispatchStarted(
        LlmChatOperation operation,
        DateTimeOffset startedAtUtc)
    {
        if (operation.ProviderDispatchStartedAtUtc is not null)
        {
            return operation;
        }

        RequireStatus(operation, LlmChatOperationStatus.Running);
        if (operation.TurnAdmittedAtUtc is not { } admittedAtUtc || startedAtUtc < admittedAtUtc)
        {
            throw new InvalidOperationException("Provider dispatch cannot start before turn admission evidence exists.");
        }

        return Advance(operation, operation.Status) with { ProviderDispatchStartedAtUtc = startedAtUtc };
    }

    public static LlmChatOperation MarkProviderDispatchReturned(
        LlmChatOperation operation,
        DateTimeOffset returnedAtUtc)
    {
        if (operation.ProviderDispatchReturnedAtUtc is not null)
        {
            return operation;
        }

        RequireStatus(
            operation,
            LlmChatOperationStatus.Running,
            LlmChatOperationStatus.CancellationRequested);
        if (operation.ProviderDispatchStartedAtUtc is not { } startedAtUtc || returnedAtUtc < startedAtUtc)
        {
            throw new InvalidOperationException("Provider dispatch return evidence requires an earlier dispatch start.");
        }

        return Advance(operation, operation.Status) with { ProviderDispatchReturnedAtUtc = returnedAtUtc };
    }

    public static LlmChatOperation CompleteTranscript(
        LlmChatOperation operation,
        DateTimeOffset completedAtUtc,
        long resultingTranscriptRevision,
        Guid assistantEntryId)
    {
        if (operation.Status == LlmChatOperationStatus.Succeeded)
        {
            return operation;
        }

        RequireStatus(
            operation,
            LlmChatOperationStatus.Running,
            LlmChatOperationStatus.RecoveryRequired);
        if (assistantEntryId == Guid.Empty || resultingTranscriptRevision <= operation.ExpectedTranscriptRevision)
        {
            throw new InvalidOperationException("Transcript completion requires an assistant entry and an advanced revision.");
        }

        return Advance(operation, LlmChatOperationStatus.Succeeded) with
        {
            TranscriptCompletedAtUtc = completedAtUtc,
            CompletedAtUtc = completedAtUtc,
            ResultingTranscriptRevision = resultingTranscriptRevision,
            AssistantEntryId = assistantEntryId,
            FailureCode = string.Empty
        };
    }

    public static LlmChatOperation RequestCancellation(
        LlmChatOperation operation,
        DateTimeOffset requestedAtUtc)
    {
        if (operation.IsTerminal || operation.Status == LlmChatOperationStatus.RecoveryRequired)
        {
            return operation;
        }

        if (operation.Status == LlmChatOperationStatus.CancellationRequested)
        {
            return operation;
        }

        RequireStatus(
            operation,
            LlmChatOperationStatus.Pending,
            LlmChatOperationStatus.Running);
        return Advance(operation, LlmChatOperationStatus.CancellationRequested) with
        {
            CancellationRequestedAtUtc = requestedAtUtc,
            CancellationGeneration = checked(operation.CancellationGeneration + 1)
        };
    }

    public static LlmChatOperation CompleteCancellation(
        LlmChatOperation operation,
        DateTimeOffset completedAtUtc)
    {
        if (operation.Status == LlmChatOperationStatus.Cancelled)
        {
            return operation;
        }

        RequireStatus(
            operation,
            LlmChatOperationStatus.Pending,
            LlmChatOperationStatus.Running,
            LlmChatOperationStatus.CancellationRequested,
            LlmChatOperationStatus.RecoveryRequired);
        return Advance(operation, LlmChatOperationStatus.Cancelled) with
        {
            CancellationRequestedAtUtc = operation.CancellationRequestedAtUtc ?? completedAtUtc,
            CompletedAtUtc = completedAtUtc,
            FailureCode = LlmChatErrorCodes.Cancelled
        };
    }

    public static LlmChatOperation CompleteFailure(
        LlmChatOperation operation,
        DateTimeOffset completedAtUtc,
        string failureCode)
    {
        if (operation.Status == LlmChatOperationStatus.Failed)
        {
            return operation;
        }

        RequireStatus(
            operation,
            LlmChatOperationStatus.Pending,
            LlmChatOperationStatus.Running,
            LlmChatOperationStatus.CancellationRequested,
            LlmChatOperationStatus.RecoveryRequired);
        return Advance(operation, LlmChatOperationStatus.Failed) with
        {
            CompletedAtUtc = completedAtUtc,
            FailureCode = NormalizeFailureCode(failureCode)
        };
    }

    public static LlmChatOperation RequireRecovery(
        LlmChatOperation operation,
        string failureCode)
    {
        if (operation.Status == LlmChatOperationStatus.RecoveryRequired)
        {
            return operation;
        }

        RequireStatus(
            operation,
            LlmChatOperationStatus.Pending,
            LlmChatOperationStatus.Running,
            LlmChatOperationStatus.CancellationRequested);
        return Advance(operation, LlmChatOperationStatus.RecoveryRequired) with
        {
            FailureCode = NormalizeFailureCode(failureCode)
        };
    }

    private static LlmChatOperation Advance(
        LlmChatOperation operation,
        LlmChatOperationStatus status)
        => operation with
        {
            Status = status,
            ConcurrencyToken = checked(operation.ConcurrencyToken + 1)
        };

    private static void RequireStatus(
        LlmChatOperation operation,
        params LlmChatOperationStatus[] allowed)
    {
        if (!allowed.Contains(operation.Status))
        {
            throw new InvalidOperationException(
                $"Operation status '{operation.Status}' does not allow this transition.");
        }
    }

    private static string NormalizeFailureCode(string failureCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        var normalized = failureCode.Trim();
        if (normalized.Length > 200)
        {
            throw new ArgumentException("An operation failure code cannot exceed 200 characters.", nameof(failureCode));
        }

        return normalized;
    }
}
