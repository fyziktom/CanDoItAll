using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Memory.Persistence;

public sealed class EfMemoryProviderProfileStore(
    IDbContextFactory<MemoryDbContext> dbContextFactory) : IMemoryProviderProfileStore
{
    public async Task<MemoryProviderProfile> UpsertAsync(
        MemoryProviderProfile profile,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutation = await SerializableMutationScope.BeginAsync(dbContext, $"memory-provider:{profile.InstanceId.Value}", cancellationToken);
        var set = dbContext.Set<MemoryProviderProfileEntity>();
        var entity = await set.FindAsync([profile.InstanceId.Value], cancellationToken);
        if (profile.ConfigurationRevision != entity?.ConcurrencyToken) {
            throw new MemoryProviderProfileConflictException();
        }
        if (entity is null)
        {
            entity = MemoryProviderProfileEntity.FromProfile(profile, updatedAtUtc);
            set.Add(entity);
        }
        else
        {
            entity.UpdateFrom(profile, updatedAtUtc);
            dbContext.Entry(entity).Property(item => item.ConcurrencyToken).IsModified = true;
        }

        try {
            await dbContext.SaveChangesAsync(cancellationToken);
            await mutation.CommitAsync(cancellationToken);
            return entity.ToProfile();
        } catch (Exception exception) when (SerializableMutationScope.IsConflict(exception)) {
            throw new MemoryProviderProfileConflictException(exception);
        }
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
