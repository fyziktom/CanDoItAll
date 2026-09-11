using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Prompts;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : DbContext(options) {
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.ApplyConfiguration(new WorkflowDefinitionRecordConfiguration());
        modelBuilder.ApplyConfiguration(new WorkflowDefinitionHeadRecordConfiguration());
        modelBuilder.ApplyConfiguration(new WorkflowComponentRecordConfiguration());
        modelBuilder.ApplyConfiguration(new WorkflowSettingsRecordConfiguration());
        modelBuilder.ApplyConfiguration(new WorkflowRunRecordEntityConfiguration());
        modelBuilder.ApplyConfiguration(new WorkflowEventRecordEntityConfiguration());
        modelBuilder.ApplyConfiguration(new WorkflowExternalRequestRecordEntityConfiguration());
        modelBuilder.ApplyConfiguration(new WorkflowCheckpointRecordEntityConfiguration());
        modelBuilder.ApplyConfiguration(new WorkflowArtifactRecordEntityConfiguration());
        modelBuilder.ApplyConfiguration(new WorkflowLaunchIdempotencyRecordEntityConfiguration());
        modelBuilder.ApplyConfiguration(new WorkflowUsageObservationRecordEntityConfiguration());
        modelBuilder.ApplyConfiguration(new WorkflowExecutorInvocationRecordEntityConfiguration());
        modelBuilder.ApplyConfiguration(new WorkflowBackendCheckpointPayloadEntityConfiguration());
        modelBuilder.ApplyConfiguration(new WorkflowBackendCheckpointSessionEntityConfiguration());
        modelBuilder.ApplyConfiguration(new WorkflowExternalRequestBoundaryEntityConfiguration());
        modelBuilder.ApplyConfiguration(new WorkflowExternalResponseOperationEntityConfiguration());
        modelBuilder.ApplyConfiguration(new WorkflowStructureOutputRecordConfiguration());
        modelBuilder.Ignore<PromptArtifact>();
        modelBuilder.Ignore<PromptVersion>();
        modelBuilder.Entity<WorkflowComponentRecord>().HasIndex(component => component.PromptVersionId);
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
