using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Security;

public sealed class SecretReferenceQuery(
    IDbContextFactory<SecurityDbContext> factory,
    DbContextOptions<SecurityDbContext> options,
    CoordinatedDatabaseTransaction transactions) {
    public async Task<IReadOnlySet<Guid>> GetExistingIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(ids);
        cancellationToken.ThrowIfCancellationRequested();
        if (ids.Count == 0) {
            return new HashSet<Guid>();
        }
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Set<SecretRecord>().AsNoTracking().Where(secret => ids.Contains(secret.Id))
            .Select(secret => secret.Id).ToHashSetAsync(cancellationToken);
    }

    public async Task<bool> ExistsForMutationAsync(Guid id, CancellationToken cancellationToken) {
        await using var db = await transactions.CreateEnlistedAsync(options,
            static value => new SecurityDbContext(value), cancellationToken);
        return await db.Set<SecretRecord>().AnyAsync(secret => secret.Id == id, cancellationToken);
    }
}
