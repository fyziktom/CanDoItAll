using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Memory.Persistence;

public sealed class MemoryDbContext(DbContextOptions<MemoryDbContext> options) : DbContext(options) {
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.ApplyConfiguration(new MemoryProviderProfileEntityConfiguration());
        modelBuilder.ApplyConfiguration(new MemoryOperationLedgerEntityConfiguration());
        modelBuilder.ApplyConfiguration(new MemoryFeedbackLedgerEntityConfiguration());
        modelBuilder.ApplyConfiguration(new MemoryEventInboxLedgerEntityConfiguration());
        modelBuilder.ApplyConfiguration(new MemoryEventOutboxLedgerEntityConfiguration());
        modelBuilder.ApplyConfiguration(new MemorySourceRequestLedgerEntityConfiguration());
        modelBuilder.ApplyConfiguration(new MemoryWorkerLeaseEntityConfiguration());
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
