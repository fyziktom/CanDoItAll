using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistoryTargetWriteSession {
    private readonly DbContextOptions<ProviderHistoryDbContext> options;

    public HistoryTargetWriteSession(ResolvedDatabaseProfile targetProfile, TimeProvider clock) {
        ArgumentNullException.ThrowIfNull(targetProfile);
        ArgumentNullException.ThrowIfNull(clock);
        var builder = new DbContextOptionsBuilder<ProviderHistoryDbContext>();
        AppDbContextOptionsConfigurator.Configure(builder, targetProfile);
        options = builder.Options;
        var factory = new TargetContextFactory(options);
        Transactions = CoordinatedDatabaseTransaction.ForProfile(targetProfile);
        Partitions = new(factory, options, Transactions);
        Outbox = new(options, Transactions, clock);
        Projection = new(factory, options, Transactions);
        Retention = new(factory, options, Transactions, clock);
    }

    public CoordinatedDatabaseTransaction Transactions { get; }
    public HistoryPartitionStore Partitions { get; }
    public HistoryOutboxWriter Outbox { get; }
    public HistoryProjectionWriter Projection { get; }
    public HistoryRetentionStore Retention { get; }

    public async Task<bool> HasRetainedSourceOrPendingMutationsAsync(HistorySourceKind kind, CancellationToken cancellationToken) {
        await using var db = await Transactions.CreateEnlistedAsync(options,
            static value => new ProviderHistoryDbContext(value), cancellationToken);
        return await db.Set<HistorySourceRow>().AnyAsync(row => row.Kind == kind, cancellationToken)
            || await db.Set<HistoryOutboxRow>().AnyAsync(cancellationToken);
    }

    private sealed class TargetContextFactory(DbContextOptions<ProviderHistoryDbContext> options)
        : IDbContextFactory<ProviderHistoryDbContext> {
        public ProviderHistoryDbContext CreateDbContext() => new(options);

        public Task<ProviderHistoryDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }
}
