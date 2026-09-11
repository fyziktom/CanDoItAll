using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Security;

public interface ISecretDatabaseTransferParticipant {
    Task<DatabaseTransferTable> GetTargetTableAsync(DatabaseTransferOwnerRequest transfer, CancellationToken cancellationToken = default);
    Task<int> CopySelectedAsync(DatabaseTransferOwnerRequest transfer, IReadOnlyCollection<Guid> secretIds, CancellationToken cancellationToken = default);
}

public sealed class SecretDatabaseTransferParticipant(DatabaseTransferOwnerSessionRunner sessions) : ISecretDatabaseTransferParticipant {
    public async Task<DatabaseTransferTable> GetTargetTableAsync(DatabaseTransferOwnerRequest transfer, CancellationToken cancellationToken = default) {
        await using var target = await sessions.CreateTargetAsync<SecurityDbContext>(transfer, static options => new SecurityDbContext(options), cancellationToken);
        return DatabaseTransferTable.From(target.Model.FindEntityType(typeof(SecretRecord))
            ?? throw new InvalidOperationException("The Security transfer owner has no secret record mapping."));
    }

    public async Task<int> CopySelectedAsync(DatabaseTransferOwnerRequest transfer, IReadOnlyCollection<Guid> secretIds,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(secretIds);
        await using var source = await sessions.CreateSourceAsync<SecurityDbContext>(transfer, static options => new SecurityDbContext(options), cancellationToken);
        await using var target = await sessions.CreateTargetAsync<SecurityDbContext>(transfer, static options => new SecurityDbContext(options), cancellationToken);
        if (secretIds.Count == 0) {
            return 0;
        }
        var selected = await source.Set<SecretRecord>().AsNoTracking().Where(secret => secretIds.Contains(secret.Id)).ToArrayAsync(cancellationToken);
        var replaced = await target.Set<SecretRecord>().Where(secret => secretIds.Contains(secret.Id)).ToArrayAsync(cancellationToken);
        target.RemoveRange(replaced);
        await target.SaveChangesAsync(cancellationToken);
        target.ChangeTracker.Clear();
        target.AddRange(selected);
        await target.SaveChangesAsync(cancellationToken);
        return selected.Length;
    }
}
