using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class ProviderSecretDeletionReferencePolicy
    : ISecretDeletionReferencePolicy
{
    public async Task<SecretDeletionReference?> FindReferenceAsync(
        AppDbContext dbContext,
        Guid secretRecordId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (secretRecordId == Guid.Empty)
        {
            throw new ArgumentException(
                "The secret record id cannot be empty.",
                nameof(secretRecordId));
        }

        var isProviderReferenced = await dbContext.Set<ProviderProfile>()
            .AsNoTracking()
            .AnyAsync(
                profile => profile.ApiKeySecretId == secretRecordId,
                cancellationToken);
        return isProviderReferenced
            ? new SecretDeletionReference(
                "Remove or replace the secret reference on every provider profile before deleting this secret.")
            : null;
    }
}
