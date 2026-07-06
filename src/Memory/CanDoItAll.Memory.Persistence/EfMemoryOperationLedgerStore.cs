using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Memory.Persistence;

public sealed class EfMemoryOperationLedgerStore(
    IDbContextFactory<AppDbContext> dbContextFactory) : IMemoryOperationLedgerStore
{
    public async Task CreateAsync(
        MemoryOperationRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.Set<MemoryOperationLedgerEntity>().Add(MemoryOperationLedgerEntity.FromRecord(record));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<MemoryOperationRecord?> GetAsync(
        MemoryOperationId operationId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<MemoryOperationLedgerEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == operationId.Value, cancellationToken);
        return entity?.ToRecord();
    }

    public async Task<IReadOnlyList<MemoryOperationRecord>> ListDueForPollingAsync(
        DateTimeOffset nowUtc,
        TimeSpan staleAfter,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (staleAfter < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(staleAfter), "Polling staleness cannot be negative.");
        }

        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "Operation polling page size must be positive.");
        }

        var staleBefore = nowUtc.Subtract(staleAfter);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await dbContext.Set<MemoryOperationLedgerEntity>()
            .AsNoTracking()
            .Where(item => item.Status == (int)MemoryLedgerStatus.Accepted || item.Status == (int)MemoryLedgerStatus.Running)
            .Where(item => item.UpdatedAtUtc <= staleBefore || item.ExpiresAtUtc <= nowUtc)
            .OrderBy(item => item.UpdatedAtUtc)
            .Take(take)
            .ToArrayAsync(cancellationToken);
        return entities.Select(entity => entity.ToRecord()).ToArray();
    }

    public async Task<IReadOnlyList<MemoryOperationRecord>> ListByProviderAsync(
        MemoryProviderInstanceId providerInstanceId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "Operation ledger page size must be positive.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await dbContext.Set<MemoryOperationLedgerEntity>()
            .AsNoTracking()
            .Where(item => item.ProviderInstanceId == providerInstanceId.Value)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenByDescending(item => item.CreatedAtUtc)
            .Take(take)
            .ToArrayAsync(cancellationToken);
        return entities.Select(entity => entity.ToRecord()).ToArray();
    }

    public async Task<MemoryOperationRecord> TransitionAsync(
        MemoryOperationId operationId,
        MemoryLedgerStatus nextStatus,
        DateTimeOffset transitionedAtUtc,
        string reason,
        CancellationToken cancellationToken = default)
    {
        return await TransitionCoreAsync(
            operationId,
            nextStatus,
            transitionedAtUtc,
            reason,
            extensions: null,
            cancellationToken);
    }

    public async Task<MemoryOperationRecord> TransitionAsync(
        MemoryOperationId operationId,
        MemoryLedgerStatus nextStatus,
        DateTimeOffset transitionedAtUtc,
        string reason,
        MemoryExtensionData extensions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(extensions);

        return await TransitionCoreAsync(
            operationId,
            nextStatus,
            transitionedAtUtc,
            reason,
            extensions,
            cancellationToken);
    }

    private async Task<MemoryOperationRecord> TransitionCoreAsync(
        MemoryOperationId operationId,
        MemoryLedgerStatus nextStatus,
        DateTimeOffset transitionedAtUtc,
        string reason,
        MemoryExtensionData? extensions,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<MemoryOperationLedgerEntity>()
            .SingleOrDefaultAsync(item => item.OperationId == operationId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Memory operation '{operationId}' was not found.");

        var record = entity.ToRecord();
        if (extensions is not null)
        {
            record = record with
            {
                Extensions = extensions
            };
        }

        var transitioned = MemoryLedgerTransitionRules.TransitionOperation(
            record,
            nextStatus,
            transitionedAtUtc,
            reason);
        entity.UpdateRecord(transitioned);
        await dbContext.SaveChangesAsync(cancellationToken);
        return transitioned;
    }

    public async Task<MemoryOperationRecord> DeferAsync(
        MemoryOperationId operationId,
        DateTimeOffset deferredAtUtc,
        string reason,
        bool incrementRetry,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<MemoryOperationLedgerEntity>()
            .SingleOrDefaultAsync(item => item.OperationId == operationId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Memory operation '{operationId}' was not found.");

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
}
