using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class SharedProviderSourceSecretDeletionReferencePolicy(
    DbContextOptions<ProvidersDbContext> options,
    CoordinatedDatabaseTransaction transactions) : ISecretDeletionReferencePolicy {
    public async Task<SecretDeletionReference?> FindReferenceAsync(Guid secretRecordId, CancellationToken cancellationToken) {
        if (secretRecordId == Guid.Empty) {
            throw new ArgumentException("The secret record id cannot be empty.", nameof(secretRecordId));
        }

        await using var dbContext = await transactions.CreateEnlistedAsync(options,
            static value => new ProvidersDbContext(value), cancellationToken);
        var isReferenced = await dbContext.Set<SharedProviderSource>().AsNoTracking()
            .AnyAsync(item => item.ApiTokenSecretId == secretRecordId, cancellationToken);
        return isReferenced ? new SecretDeletionReference(
            "Remove or replace the secret reference on every shared-provider source before deleting this secret.") : null;
    }
}
