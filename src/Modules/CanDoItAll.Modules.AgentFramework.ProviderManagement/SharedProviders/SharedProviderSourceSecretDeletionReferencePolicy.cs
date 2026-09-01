using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class SharedProviderSourceSecretDeletionReferencePolicy :
    ISecretDeletionReferencePolicy
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

        var isReferenced = await dbContext.Set<SharedProviderSource>()
            .AsNoTracking()
            .AnyAsync(
                source => source.ApiTokenSecretId == secretRecordId,
                cancellationToken);
        return isReferenced
            ? new SecretDeletionReference(
                "Remove or replace the secret reference on every shared-provider source before deleting this secret.")
            : null;
    }
}
