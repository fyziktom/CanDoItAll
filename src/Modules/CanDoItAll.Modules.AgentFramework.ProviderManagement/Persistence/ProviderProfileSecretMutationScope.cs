using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class ProviderProfileSecretMutationScope : IAsyncDisposable {
    private readonly SerializableMutationScope? mutationScope;
    private readonly IDbContextTransaction? ownedTransaction;

    private ProviderProfileSecretMutationScope(
        ProviderProfile? profile,
        SerializableMutationScope? mutationScope,
        IDbContextTransaction? ownedTransaction = null) {
        Profile = profile;
        this.mutationScope = mutationScope;
        this.ownedTransaction = ownedTransaction;
    }

    public ProviderProfile? Profile { get; }

    internal bool HasMutationScope => mutationScope is not null || ownedTransaction is not null;

    public static async Task<ProviderProfileSecretMutationScope> BeginAsync(
        ProvidersDbContext dbContext,
        Guid? providerProfileId,
        Guid? targetSecretRecordId,
        CancellationToken cancellationToken,
        bool requireTransaction = false) {
        ArgumentNullException.ThrowIfNull(dbContext);

        while (true) {
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
            if (scopeKeys.Count == 0) {
                var transaction = requireTransaction && dbContext.Database.IsRelational()
                    ? await dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
                try {
                    var unlockedProfile = providerProfileId.HasValue
                        ? await dbContext.Set<ProviderProfile>()
                            .SingleOrDefaultAsync(
                                profile => profile.Id == providerProfileId.Value,
                                cancellationToken)
                        : null;
                    return new(unlockedProfile, mutationScope: null, transaction);
                } catch {
                    if (transaction is not null) {
                        await transaction.DisposeAsync();
                    }
                    throw;
                }
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
            if (lockedProfile?.ApiKeySecretId == observedSecretRecordId) {
                return new(lockedProfile, candidateScope);
            }

            if (lockedProfile is not null) {
                dbContext.Entry(lockedProfile).State = EntityState.Detached;
            }

            await candidateScope.DisposeAsync();
        }
    }

    public Task CommitAsync(CancellationToken cancellationToken)
        => mutationScope?.CommitAsync(cancellationToken) ?? ownedTransaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

    public ValueTask DisposeAsync()
        => mutationScope?.DisposeAsync() ?? ownedTransaction?.DisposeAsync() ?? ValueTask.CompletedTask;
}
