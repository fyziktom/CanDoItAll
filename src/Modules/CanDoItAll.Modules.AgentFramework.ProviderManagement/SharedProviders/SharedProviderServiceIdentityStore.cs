using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Runtime.ExceptionServices;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class SharedProviderServiceIdentityStore(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock)
{
    public async Task<SharedProviderSourceInstanceId> GetOrCreateAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await dbContext.Set<SharedProviderServiceIdentity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                identity => identity.Id == SharedProviderServiceIdentity.SingletonId,
                cancellationToken);
        if (existing is not null)
        {
            return existing.PublicId;
        }

        var identity = SharedProviderServiceIdentity.Create(
            new SharedProviderSourceInstanceId(Guid.NewGuid()),
            clock.GetUtcNow());
        dbContext.Add(identity);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return identity.PublicId;
        }
        catch (DbUpdateException exception) when (!cancellationToken.IsCancellationRequested)
        {
            await using var verification = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var committed = await verification.Set<SharedProviderServiceIdentity>()
                .AsNoTracking()
                .Where(item => item.Id == SharedProviderServiceIdentity.SingletonId)
                .Select(item => item.PublicId)
                .SingleOrDefaultAsync(cancellationToken);
            if (committed == default)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }

            return committed;
        }
    }
}
