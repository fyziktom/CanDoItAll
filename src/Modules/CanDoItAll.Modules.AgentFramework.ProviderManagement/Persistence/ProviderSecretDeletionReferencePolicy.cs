using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class ProviderSecretDeletionReferencePolicy(
    DbContextOptions<ProvidersDbContext> options,
    CoordinatedDatabaseTransaction transactions) : ISecretDeletionReferencePolicy {
    public async Task<SecretDeletionReference?> FindReferenceAsync(Guid secretRecordId, CancellationToken cancellationToken) {
        if (secretRecordId == Guid.Empty) {
            throw new ArgumentException("The secret record id cannot be empty.", nameof(secretRecordId));
        }

        await using var dbContext = await transactions.CreateEnlistedAsync(options,
            static value => new ProvidersDbContext(value), cancellationToken);
        var isReferenced = await dbContext.Set<ProviderProfile>().AsNoTracking()
            .AnyAsync(item => item.ApiKeySecretId == secretRecordId, cancellationToken);
        return isReferenced ? new SecretDeletionReference(
            "Remove or replace the secret reference on every provider profile before deleting this secret.") : null;
    }
}
