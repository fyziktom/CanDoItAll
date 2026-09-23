using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class WorkbenchDbContext(DbContextOptions<WorkbenchDbContext> options) : DbContext(options) {
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.ApplyConfiguration(new ProjectObjectRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectObjectLinkRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectWorkbenchViewStateRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectStructureProjectionLayoutRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectStructureOperationAnalyticsRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectStructureLeaseRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectNodeBindingRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectNodeReferenceRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectNodeLifecycleEventRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectCrossModuleMutationRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectWorkflowContributionRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectWorkflowAdmissionRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectWorkAssignmentRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectProcessAssetContributionRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectWorkAssignmentHistoryRecordConfiguration());
        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) {
        ApplicationManagedConcurrencyTokens.Stamp(ChangeTracker);
        RecordDomainChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) {
        ApplicationManagedConcurrencyTokens.Stamp(ChangeTracker);
        RecordDomainChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // Lease bookkeeping and operation analytics never change the project an agent tool invocation acted on.
    private void RecordDomainChanges() {
        if (ChangeTracker.Entries().Any(entry =>
                entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
                entry.Entity is not (ProjectStructureLeaseRecord or ProjectStructureOperationAnalyticsRecord))) {
            ProjectStructureToolEffectObservation.RecordDomainWrite();
        }
    }
}
