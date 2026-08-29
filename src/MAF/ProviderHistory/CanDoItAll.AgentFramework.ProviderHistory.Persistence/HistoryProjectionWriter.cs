using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistoryProjectionWriter(IDbContextFactory<AppDbContext> factory) {
    public static Task StageAsync(AppDbContext ownerContext, HistorySourceMutation mutation, CancellationToken cancellationToken) {
        if (ownerContext.Database.CurrentTransaction is null) {
            throw new InvalidOperationException("History projection requires the owner's active transaction.");
        }
        return HistorySourceProjection.ApplyAsync(ownerContext, mutation, cancellationToken);
    }

    public async Task ApplyAsync(HistorySourceMutation mutation, CancellationToken cancellationToken) {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await StageAsync(db, mutation, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
