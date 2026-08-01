using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Memory.Persistence;

public sealed class EfMemoryEventLedgerStore(
    IDbContextFactory<AppDbContext> dbContextFactory) : IMemoryEventLedgerStore
{
    public async Task EnqueueInboxAsync(
        MemoryEventInboxRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.Set<MemoryEventInboxLedgerEntity>().Add(MemoryEventInboxLedgerEntity.FromRecord(record));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task EnqueueOutboxAsync(
        MemoryEventOutboxRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.Set<MemoryEventOutboxLedgerEntity>().Add(MemoryEventOutboxLedgerEntity.FromRecord(record));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ContainsInboxDedupeKeyAsync(
        MemoryEventDedupeKey dedupeKey,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<MemoryEventInboxLedgerEntity>()
            .AsNoTracking()
            .AnyAsync(entity => entity.DedupeKey == dedupeKey.Value, cancellationToken);
    }

    public async Task<IReadOnlyList<MemoryEventInboxRecord>> ListPendingInboxAsync(
        MemoryProviderInstanceId providerInstanceId,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        EnsurePageSize(take);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await dbContext.Set<MemoryEventInboxLedgerEntity>()
            .AsNoTracking()
            .Where(entity => entity.ProviderInstanceId == providerInstanceId.Value)
            .Where(entity => entity.Status == (int)MemoryLedgerStatus.Pending || entity.Status == (int)MemoryLedgerStatus.Running)
            .OrderBy(entity => entity.UpdatedAtUtc)
            .Take(take)
            .ToArrayAsync(cancellationToken);
        return entities.Select(entity => entity.ToRecord()).ToArray();
    }

    public async Task<IReadOnlyList<MemoryEventOutboxRecord>> ListPendingOutboxAsync(
        MemoryProviderInstanceId providerInstanceId,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        EnsurePageSize(take);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await dbContext.Set<MemoryEventOutboxLedgerEntity>()
            .AsNoTracking()
            .Where(entity => entity.ProviderInstanceId == providerInstanceId.Value)
            .Where(entity => entity.Status == (int)MemoryLedgerStatus.Pending || entity.Status == (int)MemoryLedgerStatus.Running)
            .OrderBy(entity => entity.UpdatedAtUtc)
            .Take(take)
            .ToArrayAsync(cancellationToken);
        return entities.Select(entity => entity.ToRecord()).ToArray();
    }

    public async Task<MemoryEventInboxRecord> TransitionInboxAsync(
        MemoryEventInboxRecordId inboxRecordId,
        MemoryLedgerStatus nextStatus,
        DateTimeOffset transitionedAtUtc,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<MemoryEventInboxLedgerEntity>()
            .SingleOrDefaultAsync(item => item.InboxRecordId == inboxRecordId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Memory event inbox '{inboxRecordId}' was not found.");
        var transitioned = MemoryLedgerTransitionRules.TransitionInboxEvent(
            entity.ToRecord(),
            nextStatus,
            transitionedAtUtc,
            reason);
        entity.UpdateRecord(transitioned);
        await dbContext.SaveChangesAsync(cancellationToken);
        return transitioned;
    }

    public async Task<MemoryEventInboxRecord> DeferInboxAsync(
        MemoryEventInboxRecordId inboxRecordId,
        DateTimeOffset deferredAtUtc,
        string reason,
        bool incrementRetry,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<MemoryEventInboxLedgerEntity>()
            .SingleOrDefaultAsync(item => item.InboxRecordId == inboxRecordId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Memory event inbox '{inboxRecordId}' was not found.");
        var record = entity.ToRecord();
        var deferred = record with
        {
            RetryCount = incrementRetry ? record.RetryCount + 1 : record.RetryCount,
            UpdatedAtUtc = deferredAtUtc,
            StatusReason = string.IsNullOrWhiteSpace(reason) ? "deferred" : reason.Trim()
        };
        entity.UpdateRecord(deferred);
        await dbContext.SaveChangesAsync(cancellationToken);
        return deferred;
    }

    public async Task<MemoryEventOutboxRecord> TransitionOutboxAsync(
        MemoryEventOutboxRecordId outboxRecordId,
        MemoryLedgerStatus nextStatus,
        DateTimeOffset transitionedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<MemoryEventOutboxLedgerEntity>()
            .SingleOrDefaultAsync(item => item.OutboxRecordId == outboxRecordId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Memory event outbox '{outboxRecordId}' was not found.");
        var transitioned = MemoryLedgerTransitionRules.TransitionOutboxEvent(
            entity.ToRecord(),
            nextStatus,
            transitionedAtUtc);
        entity.UpdateRecord(transitioned);
        await dbContext.SaveChangesAsync(cancellationToken);
        return transitioned;
    }

    public async Task<MemoryEventOutboxRecord> DeferOutboxAsync(
        MemoryEventOutboxRecordId outboxRecordId,
        DateTimeOffset deferredAtUtc,
        bool incrementRetry,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<MemoryEventOutboxLedgerEntity>()
            .SingleOrDefaultAsync(item => item.OutboxRecordId == outboxRecordId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Memory event outbox '{outboxRecordId}' was not found.");
        var record = entity.ToRecord();
        var deferred = record with
        {
            RetryCount = incrementRetry ? record.RetryCount + 1 : record.RetryCount,
            UpdatedAtUtc = deferredAtUtc
        };
        entity.UpdateRecord(deferred);
        await dbContext.SaveChangesAsync(cancellationToken);
        return deferred;
    }

    private static void EnsurePageSize(int take)
    {
        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "Event ledger page size must be positive.");
        }
    }
}
