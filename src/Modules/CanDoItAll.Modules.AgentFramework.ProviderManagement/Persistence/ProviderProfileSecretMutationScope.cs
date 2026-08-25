using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class ProviderProfileSecretMutationScope : IAsyncDisposable
{
    private readonly SerializableMutationScope? mutationScope;

    private ProviderProfileSecretMutationScope(
        ProviderProfile? profile,
        SerializableMutationScope? mutationScope)
    {
        Profile = profile;
        this.mutationScope = mutationScope;
    }

    public ProviderProfile? Profile { get; }

    public static async Task<ProviderProfileSecretMutationScope> BeginAsync(
        AppDbContext dbContext,
        Guid? providerProfileId,
        Guid? targetSecretRecordId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        while (true)
        {
            var observedSecretRecordId = providerProfileId.HasValue
                ? await dbContext.Set<ProviderProfile>()
                    .AsNoTracking()
                    .Where(profile => profile.Id == providerProfileId.Value)
                    .Select(profile => profile.ApiKeySecretId)
                    .SingleOrDefaultAsync(cancellationToken)
                : null;
            var scopeKeys = SecretMutationScopeKeys.ForSecretRecords(
                observedSecretRecordId,
                targetSecretRecordId);
            if (scopeKeys.Count == 0)
            {
                var unlockedProfile = providerProfileId.HasValue
                    ? await dbContext.Set<ProviderProfile>()
                        .SingleOrDefaultAsync(
                            profile => profile.Id == providerProfileId.Value,
                            cancellationToken)
                    : null;
                return new(unlockedProfile, mutationScope: null);
            }

            var candidateScope = await SerializableMutationScope.BeginAsync(
                dbContext,
                scopeKeys,
                cancellationToken);
            var lockedProfile = providerProfileId.HasValue
                ? await dbContext.Set<ProviderProfile>()
                    .SingleOrDefaultAsync(
                        profile => profile.Id == providerProfileId.Value,
                        cancellationToken)
                : null;
            if (lockedProfile?.ApiKeySecretId == observedSecretRecordId)
            {
                return new(lockedProfile, candidateScope);
            }

            if (lockedProfile is not null)
            {
                dbContext.Entry(lockedProfile).State = EntityState.Detached;
            }

            await candidateScope.DisposeAsync();
        }
    }

    public Task CommitAsync(CancellationToken cancellationToken)
        => mutationScope?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

    public ValueTask DisposeAsync()
        => mutationScope?.DisposeAsync() ?? ValueTask.CompletedTask;
}
