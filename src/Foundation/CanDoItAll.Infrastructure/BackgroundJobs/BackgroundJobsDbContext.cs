using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Infrastructure.BackgroundJobs;

public sealed class BackgroundJobsDbContext(DbContextOptions<BackgroundJobsDbContext> options) : DbContext(options) {
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.ApplyConfiguration(new BackgroundJobRecordConfiguration());
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
