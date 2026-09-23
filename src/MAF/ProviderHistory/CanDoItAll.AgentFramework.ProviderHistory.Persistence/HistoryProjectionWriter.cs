using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistoryProjectionWriter(
    IDbContextFactory<ProviderHistoryDbContext> factory,
    DbContextOptions<ProviderHistoryDbContext> options,
    CoordinatedDatabaseTransaction transactions) {
    public async Task StageAsync(HistorySourceMutation mutation, CancellationToken cancellationToken) {
        await using var db = await transactions.CreateEnlistedAsync(options, static value => new ProviderHistoryDbContext(value), cancellationToken);
        await StageAsync(db, mutation, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    internal static Task StageAsync(ProviderHistoryDbContext ownerContext, HistorySourceMutation mutation, CancellationToken cancellationToken) {
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
