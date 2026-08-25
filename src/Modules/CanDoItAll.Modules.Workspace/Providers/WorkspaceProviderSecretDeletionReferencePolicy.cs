using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workspace;

public sealed class WorkspaceProviderSecretDeletionReferencePolicy
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
        var isSourceReferenced = await dbContext.Set<SharedProviderSource>()
            .AsNoTracking()
            .AnyAsync(
                source => source.ApiTokenSecretId == secretRecordId,
                cancellationToken);
        return (isProviderReferenced, isSourceReferenced) switch
        {
            (false, false) => null,
            (true, false) => new SecretDeletionReference(
                "Remove or replace the secret reference on every provider profile before deleting this secret."),
            (false, true) => new SecretDeletionReference(
                "Remove or replace the secret reference on every shared-provider source before deleting this secret."),
            (true, true) => new SecretDeletionReference(
                "Remove or replace the secret references on every provider profile and shared-provider source before deleting this secret.")
        };
    }
}
