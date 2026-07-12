using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public static class MemoryLedgerTransitionRules
{
    public static MemoryOperationRecord TransitionOperation(
        MemoryOperationRecord record,
        MemoryLedgerStatus nextStatus,
        DateTimeOffset transitionedAtUtc,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(record);
        EnsureOperationTransition(record.Status, nextStatus, record.OperationId);
        return record with
        {
            Status = nextStatus,
            UpdatedAtUtc = transitionedAtUtc,
            CompletedAtUtc = IsTerminal(nextStatus) ? transitionedAtUtc : record.CompletedAtUtc,
            TransitionCount = record.TransitionCount + 1,
            StatusReason = NormalizeReason(reason)
        };
    }

    public static MemoryFeedbackRecord TransitionFeedback(
        MemoryFeedbackRecord record,
        MemoryLedgerStatus nextStatus,
        DateTimeOffset transitionedAtUtc,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(record);
        EnsureGenericTransition(record.Status, nextStatus, $"memory feedback '{record.FeedbackRecordId}'");
        return record with
        {
            Status = nextStatus,
            UpdatedAtUtc = transitionedAtUtc,
            RetryCount = nextStatus == MemoryLedgerStatus.Running ? record.RetryCount + 1 : record.RetryCount
        };
    }

    public static MemoryEventInboxRecord TransitionInboxEvent(
        MemoryEventInboxRecord record,
        MemoryLedgerStatus nextStatus,
        DateTimeOffset transitionedAtUtc,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(record);
        EnsureGenericTransition(record.Status, nextStatus, $"memory event inbox '{record.InboxRecordId}'");
        return record with
        {
            Status = nextStatus,
            UpdatedAtUtc = transitionedAtUtc,
            StatusReason = NormalizeReason(reason),
            RetryCount = nextStatus == MemoryLedgerStatus.Running ? record.RetryCount + 1 : record.RetryCount
        };
    }

    public static MemoryEventOutboxRecord TransitionOutboxEvent(
        MemoryEventOutboxRecord record,
        MemoryLedgerStatus nextStatus,
        DateTimeOffset transitionedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(record);
        EnsureGenericTransition(record.Status, nextStatus, $"memory event outbox '{record.OutboxRecordId}'");
        return record with
        {
            Status = nextStatus,
            UpdatedAtUtc = transitionedAtUtc,
            RetryCount = nextStatus == MemoryLedgerStatus.Running ? record.RetryCount + 1 : record.RetryCount
        };
    }

    private static void EnsureOperationTransition(
        MemoryLedgerStatus current,
        MemoryLedgerStatus next,
        MemoryOperationId operationId)
    {
        if (IsAllowed(current, next))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Cannot transition memory operation '{operationId}' from '{current}' to '{next}'.");
    }

    private static void EnsureGenericTransition(
        MemoryLedgerStatus current,
        MemoryLedgerStatus next,
        string subject)
    {
        if (IsAllowed(current, next))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Cannot transition {subject} from '{current}' to '{next}'.");
    }

    private static bool IsAllowed(
        MemoryLedgerStatus current,
        MemoryLedgerStatus next)
    {
        return current switch
        {
            MemoryLedgerStatus.Pending => next is MemoryLedgerStatus.Accepted or MemoryLedgerStatus.Running or MemoryLedgerStatus.Completed or MemoryLedgerStatus.Failed or MemoryLedgerStatus.TimedOut or MemoryLedgerStatus.Cancelled or MemoryLedgerStatus.Expired,
            MemoryLedgerStatus.Accepted => next is MemoryLedgerStatus.Running or MemoryLedgerStatus.Completed or MemoryLedgerStatus.Failed or MemoryLedgerStatus.TimedOut or MemoryLedgerStatus.Cancelled or MemoryLedgerStatus.Expired,
            MemoryLedgerStatus.Running => next is MemoryLedgerStatus.Completed or MemoryLedgerStatus.Failed or MemoryLedgerStatus.TimedOut or MemoryLedgerStatus.Cancelled or MemoryLedgerStatus.Expired,
            MemoryLedgerStatus.Completed or MemoryLedgerStatus.Failed or MemoryLedgerStatus.TimedOut or MemoryLedgerStatus.Cancelled or MemoryLedgerStatus.Expired => next == MemoryLedgerStatus.Forgotten,
            MemoryLedgerStatus.Forgotten => false,
            _ => false
        };
    }

    private static bool IsTerminal(MemoryLedgerStatus status)
    {
        return status is MemoryLedgerStatus.Completed or MemoryLedgerStatus.Failed or MemoryLedgerStatus.TimedOut or MemoryLedgerStatus.Cancelled or MemoryLedgerStatus.Expired or MemoryLedgerStatus.Forgotten;
    }

    private static string NormalizeReason(string reason)
    {
        return string.IsNullOrWhiteSpace(reason)
            ? "transitioned"
            : reason.Trim();
    }
}
