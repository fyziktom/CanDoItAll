using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Memory.Persistence;

public sealed class EfMemorySourceRequestLedgerStore(
    IDbContextFactory<AppDbContext> dbContextFactory) : IMemorySourceRequestLedgerStore
{
    public async Task EnqueueAsync(
        MemorySourceIngestionJobRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.Set<MemorySourceRequestLedgerEntity>().Add(MemorySourceRequestLedgerEntity.FromRecord(record));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemorySourceIngestionJobRecord>> ListByProviderAsync(
        MemoryProviderInstanceId providerInstanceId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await dbContext.Set<MemorySourceRequestLedgerEntity>()
            .AsNoTracking()
            .Where(entity => entity.ProviderInstanceId == providerInstanceId.Value)
            .OrderBy(entity => entity.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);
        return entities.Select(entity => entity.ToRecord()).ToArray();
    }
}
