using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Security;

public sealed class StorageSecretResolver(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ISecretProtector protector) : IStorageSecretResolver
{
    public async Task<string?> ResolveCredentialAsync(Guid? secretId, CancellationToken cancellationToken = default)
    {
        if (!secretId.HasValue)
        {
            return null;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var encryptedPayload = await dbContext.Set<SecretRecord>()
            .Where(secret => secret.Id == secretId.Value)
            .Select(secret => secret.EncryptedPayload)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(encryptedPayload)
            ? null
            : protector.Unprotect(encryptedPayload);
    }
}
