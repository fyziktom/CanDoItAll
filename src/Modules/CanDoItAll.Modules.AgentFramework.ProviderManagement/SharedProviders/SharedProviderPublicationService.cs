using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class SharedProviderPublicationStore(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock)
{
    public async Task<SharedProviderPublicationWriteResult> GetOrCreateAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default)
    {
        if (providerProfileId == Guid.Empty)
        {
            throw new ArgumentException("The provider profile id cannot be empty.", nameof(providerProfileId));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await SerializableMutationScope.BeginAsync(
            dbContext,
            $"shared-provider-publication:{providerProfileId:D}",
            cancellationToken);
        if (!await dbContext.Set<ProviderProfile>()
            .AsNoTracking()
            .AnyAsync(profile => profile.Id == providerProfileId, cancellationToken))
        {
            throw new KeyNotFoundException($"Provider profile '{providerProfileId:D}' was not found.");
        }

        var publication = await dbContext.Set<ProviderSharePublication>()
            .SingleOrDefaultAsync(item => item.ProviderProfileId == providerProfileId, cancellationToken);
        if (publication is null)
        {
            publication = SharedProviderPublicationTransitions.Create(
                providerProfileId,
                CreatePublicId(providerProfileId),
                clock.GetUtcNow());
            dbContext.Add(publication);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await mutationScope.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            SharedProviderPersistenceConflictClassifier.IsPublicationProviderIdentityConflict(exception))
        {
            await mutationScope.DisposeAsync();
            await using var verification = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var committed = await verification.Set<ProviderSharePublication>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.ProviderProfileId == providerProfileId,
                    cancellationToken);
            if (committed is null)
            {
                throw new SharedProviderConcurrencyException(
                    nameof(ProviderSharePublication),
                    providerProfileId,
                    exception);
            }

            publication = committed;
        }
        catch (Exception exception) when (SerializableMutationScope.IsConflict(exception))
        {
            throw new SharedProviderConcurrencyException(
                nameof(ProviderSharePublication),
                publication.Id,
                exception);
        }

        return new SharedProviderPublicationWriteResult(
            publication.Id,
            publication.PublicId,
            publication.IsPublished,
            publication.ConcurrencyToken);
    }

    private static SharedProviderPublicationId CreatePublicId(Guid providerProfileId)
    {
        Guid value;
        do
        {
            value = Guid.NewGuid();
        }
        while (value == providerProfileId);

        return new SharedProviderPublicationId(value);
    }
}
