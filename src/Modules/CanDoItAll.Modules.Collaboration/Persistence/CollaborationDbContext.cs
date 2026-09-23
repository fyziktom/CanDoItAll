using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Collaboration;

public sealed class CollaborationDbContext(DbContextOptions<CollaborationDbContext> options) : DbContext(options) {
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.ApplyConfiguration(new CollaborationThreadRecordConfiguration());
        modelBuilder.ApplyConfiguration(new CollaborationParticipantRecordConfiguration());
        modelBuilder.ApplyConfiguration(new CollaborationMessageRecordConfiguration());
        modelBuilder.ApplyConfiguration(new CollaborationInboxItemRecordConfiguration());
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
