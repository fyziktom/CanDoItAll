using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessOutboxStore(ProcessPersistenceDbContext dbContext) : IProcessOutboxWriter
{
    public async Task EnqueueAsync(
        IReadOnlyList<ProcessOutboxMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var createdAtUtc = DateTimeOffset.UtcNow;
        foreach (var message in messages)
        {
            dbContext.OutboxMessages.Add(ProcessPersistenceMappers.ToOutboxEntity(message, createdAtUtc));
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProcessOutboxDeliveryMessage>> ClaimPendingAsync(
        DateTimeOffset nowUtc,
        int take,
        ProcessOutboxLockId lockId,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take), take, "Outbox claim size must be positive.");
        }

        var rows = await dbContext.OutboxMessages
            .Where(message =>
                message.Status == ProcessOutboxDeliveryStatus.Pending &&
                (message.AvailableAtUtc == null || message.AvailableAtUtc <= nowUtc))
            .OrderBy(message => message.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var claimed = new List<ProcessOutboxDeliveryMessage>(rows.Count);
        foreach (var row in rows)
        {
            row.Status = ProcessOutboxDeliveryStatus.Locked;
            row.LockedAtUtc = nowUtc;
            row.LockId = lockId.Value;
            row.AttemptCount++;
            claimed.Add(ToDeliveryMessage(row));
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return claimed;
    }

    public async Task MarkDeliveredAsync(
        RuntimeOutboxMessageId messageId,
        ProcessOutboxLockId lockId,
        DateTimeOffset deliveredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var row = await LoadLockedMessageAsync(messageId, lockId, cancellationToken).ConfigureAwait(false);
        row.Status = ProcessOutboxDeliveryStatus.Delivered;
        row.DeliveredAtUtc = deliveredAtUtc;
        row.LockId = null;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(
        RuntimeOutboxMessageId messageId,
        ProcessOutboxLockId lockId,
        string errorClass,
        DateTimeOffset nextAttemptAtUtc,
        CancellationToken cancellationToken = default)
    {
        var row = await LoadLockedMessageAsync(messageId, lockId, cancellationToken).ConfigureAwait(false);
        row.Status = ProcessOutboxDeliveryStatus.Pending;
        row.AvailableAtUtc = nextAttemptAtUtc;
        row.LockedAtUtc = null;
        row.LockId = null;
        row.LastErrorClass = string.IsNullOrWhiteSpace(errorClass)
            ? throw new ArgumentException("Outbox error class must be present.", nameof(errorClass))
            : errorClass.Trim();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProcessOutboxMessageEntity> LoadLockedMessageAsync(
        RuntimeOutboxMessageId messageId,
        ProcessOutboxLockId lockId,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.OutboxMessages
            .SingleOrDefaultAsync(message => message.MessageId == messageId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            throw new InvalidOperationException($"Outbox message '{messageId}' was not found.");
        }

        if (row.Status != ProcessOutboxDeliveryStatus.Locked || !string.Equals(row.LockId, lockId.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Outbox message '{messageId}' is not locked by '{lockId}'.");
        }

        return row;
    }

    private static ProcessOutboxDeliveryMessage ToDeliveryMessage(ProcessOutboxMessageEntity row)
    {
        return new ProcessOutboxDeliveryMessage(
            new RuntimeOutboxMessageId(row.MessageId),
            new RuntimeEventId(row.EventId),
            row.SubscriberKind,
            row.PayloadHash,
            row.AttemptCount);
    }
}

public readonly record struct ProcessOutboxLockId
{
    public ProcessOutboxLockId(string value)
    {
        Value = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Outbox lock id cannot be empty.", nameof(value))
            : value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record ProcessOutboxDeliveryMessage(
    RuntimeOutboxMessageId MessageId,
    RuntimeEventId EventId,
    ProcessOutboxSubscriberKind SubscriberKind,
    string PayloadHash,
    int AttemptCount);
