using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using Operations = CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

internal sealed class LlmChatWorkspacePage<TItem, TKey, TCursor>(
    Func<TItem, TKey> keySelector,
    int maximumCount)
    where TKey : notnull
    where TCursor : struct
{
    private readonly List<TItem> items = [];

    public IReadOnlyList<TItem> Items => items;

    public TCursor? NextCursor { get; private set; }

    public bool HasMore => NextCursor.HasValue;

    public void Replace(IEnumerable<TItem> source, TCursor? nextCursor)
    {
        items.Clear();
        Append(source, nextCursor);
    }

    public void Append(IEnumerable<TItem> source, TCursor? nextCursor)
    {
        var keys = items.Select(keySelector).ToHashSet();
        foreach (var item in source)
        {
            if (items.Count >= maximumCount)
            {
                break;
            }

            if (keys.Add(keySelector(item)))
            {
                items.Add(item);
            }
        }

        NextCursor = items.Count < maximumCount ? nextCursor : null;
    }

    public void UpsertFirst(TItem item)
    {
        var key = keySelector(item);
        var index = items.FindIndex(candidate => EqualityComparer<TKey>.Default.Equals(keySelector(candidate), key));
        if (index >= 0)
        {
            items[index] = item;
            return;
        }

        items.Insert(0, item);
        if (items.Count > maximumCount)
        {
            items.RemoveRange(maximumCount, items.Count - maximumCount);
        }
    }

    public void Clear()
    {
        items.Clear();
        NextCursor = null;
    }
}

internal sealed class LlmChatOperationWorkspaceState
{
    private AdmissionAttempt? admissionAttempt;

    public LlmChatPendingTurn? PendingTurn { get; private set; }

    public LlmChatOperationView? ActiveOperation { get; private set; }

    public LlmChatOperationProjectionState? Projection { get; private set; }

    public bool RecoveryEvidenceConfirmed { get; private set; }

    public Guid GetAdmissionOperationId(Guid conversationId, string message)
    {
        if (admissionAttempt is not
            {
                ConversationId: var existingConversationId,
                Message: var existingMessage
            } ||
            existingConversationId != conversationId ||
            !string.Equals(existingMessage, message, StringComparison.Ordinal))
        {
            admissionAttempt = new(Guid.NewGuid(), conversationId, message);
        }

        return admissionAttempt.OperationId;
    }

    public void Prepare()
    {
        Projection = null;
        RecoveryEvidenceConfirmed = false;
    }

    public void Restore(LlmChatOperationView? operation)
        => ActiveOperation = operation;

    public void Start(LlmChatOperationView operation, string message, DateTimeOffset admittedAtUtc)
    {
        ActiveOperation = operation;
        Projection = LlmChatOperationProjectionState.Initial(operation.OperationId);
        PendingTurn = new(operation.OperationId, message, admittedAtUtc);
    }

    public void ApplyProjection(LlmChatOperationProjectionState projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        if (ActiveOperation?.OperationId != projection.OperationId)
        {
            return;
        }

        Projection = projection;
        ActiveOperation = ActiveOperation with
        {
            Status = projection.Status,
            Failure = projection.Failure
        };
    }

    public void CompleteRefresh(LlmChatOperationView operation)
    {
        PendingTurn = null;
        Projection = null;
        ActiveOperation = operation;
        RecoveryEvidenceConfirmed = false;
    }

    public void CompleteMutation(LlmChatOperationView operation, bool recoveryEvidenceConfirmed)
    {
        ActiveOperation = operation;
        RecoveryEvidenceConfirmed = recoveryEvidenceConfirmed &&
            operation.Status == Operations.LlmChatOperationStatus.RecoveryRequired;
    }

    public void Reset()
    {
        PendingTurn = null;
        ActiveOperation = null;
        Projection = null;
        admissionAttempt = null;
        RecoveryEvidenceConfirmed = false;
    }

    public string StatusText => ActiveOperation?.Status switch
    {
        Operations.LlmChatOperationStatus.Pending => "Queued",
        Operations.LlmChatOperationStatus.Running => "Responding",
        Operations.LlmChatOperationStatus.CancellationRequested => "Cancelling",
        Operations.LlmChatOperationStatus.RecoveryRequired => "Recovery required",
        Operations.LlmChatOperationStatus.Succeeded => "Completed",
        Operations.LlmChatOperationStatus.Failed => "Failed",
        Operations.LlmChatOperationStatus.Cancelled => "Cancelled",
        _ => string.Empty
    };

    private sealed record AdmissionAttempt(Guid OperationId, Guid ConversationId, string Message);
}
