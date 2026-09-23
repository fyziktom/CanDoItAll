using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class ProviderHistoryDbContext(DbContextOptions<ProviderHistoryDbContext> options) : DbContext(options) {
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.ApplyConfiguration(new HistoryPartitionConfiguration());
        modelBuilder.ApplyConfiguration(new HistoryStorageIdentityConfiguration());
        modelBuilder.ApplyConfiguration(new HistoryPolicyConfiguration());
        modelBuilder.ApplyConfiguration(new HistoryPolicyAuditConfiguration());
        modelBuilder.ApplyConfiguration(new HistoryCheckpointConfiguration());
        modelBuilder.ApplyConfiguration(new HistoryEntryConfiguration());
        modelBuilder.ApplyConfiguration(new HistoryDetailConfiguration());
        modelBuilder.ApplyConfiguration(new HistoryHostLeaseConfiguration());
        modelBuilder.ApplyConfiguration(new HistorySourceConfiguration());
        modelBuilder.ApplyConfiguration(new HistoryOwnerConfiguration());
        modelBuilder.ApplyConfiguration(new HistoryOutboxConfiguration());
        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) {
        ApplicationManagedConcurrencyTokens.Stamp(ChangeTracker);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) {
        ApplicationManagedConcurrencyTokens.Stamp(ChangeTracker);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
