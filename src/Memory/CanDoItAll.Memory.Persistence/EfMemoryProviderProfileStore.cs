using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Memory.Persistence;

public sealed class EfMemoryProviderProfileStore(
    IDbContextFactory<AppDbContext> dbContextFactory) : IMemoryProviderProfileStore
{
    public async Task UpsertAsync(
        MemoryProviderProfile profile,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var set = dbContext.Set<MemoryProviderProfileEntity>();
        var entity = await set.FindAsync([profile.InstanceId.Value], cancellationToken);
        if (entity is null)
        {
            set.Add(MemoryProviderProfileEntity.FromProfile(profile, updatedAtUtc));
        }
        else
        {
            entity.UpdateFrom(profile, updatedAtUtc);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<MemoryProviderProfile?> GetAsync(
        MemoryProviderInstanceId providerId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<MemoryProviderProfileEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.InstanceId == providerId.Value,
                cancellationToken);
        return entity?.ToProfile();
    }

    public async Task<IReadOnlyList<MemoryProviderProfile>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await dbContext.Set<MemoryProviderProfileEntity>()
            .AsNoTracking()
            .OrderBy(entity => entity.InstanceId)
            .ToArrayAsync(cancellationToken);
        return entities.Select(entity => entity.ToProfile()).ToArray();
    }
}
