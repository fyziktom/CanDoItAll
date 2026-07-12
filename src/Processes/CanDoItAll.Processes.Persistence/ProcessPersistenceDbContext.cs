using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class ProcessPersistenceDbContext(DbContextOptions<ProcessPersistenceDbContext> options) : DbContext(options)
{
    public DbSet<ProcessInstancePlanEntity> InstancePlans => Set<ProcessInstancePlanEntity>();

    public DbSet<ProcessRuntimeStateEntity> RuntimeStates => Set<ProcessRuntimeStateEntity>();

    public DbSet<ProcessRuntimeStepAssignmentEntity> RuntimeStepAssignments => Set<ProcessRuntimeStepAssignmentEntity>();

    public DbSet<ProcessRuntimeStepEntity> RuntimeSteps => Set<ProcessRuntimeStepEntity>();

    public DbSet<ProcessDispatchClaimEntity> DispatchClaims => Set<ProcessDispatchClaimEntity>();

    public DbSet<ProcessStrategyResultReceiptEntity> StrategyResultReceipts => Set<ProcessStrategyResultReceiptEntity>();

    public DbSet<ProcessRuntimeAvailableArtifactSlotEntity> AvailableArtifactSlots => Set<ProcessRuntimeAvailableArtifactSlotEntity>();

    public DbSet<ProcessRuntimeInputArtifactEntity> RuntimeInputArtifacts => Set<ProcessRuntimeInputArtifactEntity>();

    public DbSet<ProcessRuntimeEventEntity> RuntimeEvents => Set<ProcessRuntimeEventEntity>();

    public DbSet<ProcessOutboxMessageEntity> OutboxMessages => Set<ProcessOutboxMessageEntity>();

    public DbSet<ProcessArtifactLedgerEventEntity> ArtifactLedgerEvents => Set<ProcessArtifactLedgerEventEntity>();

    public DbSet<ProcessRuntimeIdempotencyEntity> IdempotencyKeys => Set<ProcessRuntimeIdempotencyEntity>();

    public DbSet<ProcessProjectionSnapshotEntity> ProjectionSnapshots => Set<ProcessProjectionSnapshotEntity>();

    public DbSet<ProcessProjectionHistoryEntity> ProjectionHistory => Set<ProcessProjectionHistoryEntity>();

    public DbSet<ProcessProjectorOffsetEntity> ProjectorOffsets => Set<ProcessProjectorOffsetEntity>();

    public DbSet<ProcessProjectionDeadLetterEntity> ProjectionDeadLetters => Set<ProcessProjectionDeadLetterEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProcessPersistenceDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
