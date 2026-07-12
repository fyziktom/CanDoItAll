using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Memory.Persistence;

public sealed class EfMemoryFeedbackLedgerStore(
    IDbContextFactory<AppDbContext> dbContextFactory) : IMemoryFeedbackLedgerStore
{
    public async Task SubmitAsync(
        MemoryFeedbackRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.Set<MemoryFeedbackLedgerEntity>().Add(MemoryFeedbackLedgerEntity.FromRecord(record));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemoryFeedbackRecord>> ListDueForDeliveryAsync(
        DateTimeOffset nowUtc,
        TimeSpan staleAfter,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (staleAfter < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(staleAfter), "Feedback delivery staleness cannot be negative.");
        }

        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "Feedback delivery page size must be positive.");
        }

        var staleBefore = nowUtc.Subtract(staleAfter);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await dbContext.Set<MemoryFeedbackLedgerEntity>()
            .AsNoTracking()
            .Where(entity => entity.Status == (int)MemoryLedgerStatus.Pending || entity.Status == (int)MemoryLedgerStatus.Running)
            .Where(entity => entity.UpdatedAtUtc <= staleBefore || entity.ExpiresAtUtc <= nowUtc)
            .OrderBy(entity => entity.UpdatedAtUtc)
            .Take(take)
            .ToArrayAsync(cancellationToken);
        return entities.Select(entity => entity.ToRecord()).ToArray();
    }

    public async Task<IReadOnlyList<MemoryFeedbackRecord>> ListByProviderAsync(
        MemoryProviderInstanceId providerInstanceId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await dbContext.Set<MemoryFeedbackLedgerEntity>()
            .AsNoTracking()
            .Where(entity => entity.ProviderInstanceId == providerInstanceId.Value)
            .OrderBy(entity => entity.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);
        return entities.Select(entity => entity.ToRecord()).ToArray();
    }

    public async Task<MemoryFeedbackRecord> TransitionAsync(
        MemoryFeedbackRecordId feedbackRecordId,
        MemoryLedgerStatus nextStatus,
        DateTimeOffset transitionedAtUtc,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<MemoryFeedbackLedgerEntity>()
            .SingleOrDefaultAsync(item => item.FeedbackRecordId == feedbackRecordId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Memory feedback '{feedbackRecordId}' was not found.");

        var transitioned = MemoryLedgerTransitionRules.TransitionFeedback(
            entity.ToRecord(),
            nextStatus,
            transitionedAtUtc,
            reason);
        entity.UpdateRecord(transitioned);
        await dbContext.SaveChangesAsync(cancellationToken);
        return transitioned;
    }

    public async Task<MemoryFeedbackRecord> DeferAsync(
        MemoryFeedbackRecordId feedbackRecordId,
        DateTimeOffset deferredAtUtc,
        bool incrementRetry,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<MemoryFeedbackLedgerEntity>()
            .SingleOrDefaultAsync(item => item.FeedbackRecordId == feedbackRecordId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Memory feedback '{feedbackRecordId}' was not found.");

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
}
