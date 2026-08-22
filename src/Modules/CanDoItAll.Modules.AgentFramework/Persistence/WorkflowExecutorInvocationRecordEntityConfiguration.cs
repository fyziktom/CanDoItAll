using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class WorkflowExecutorInvocationRecordEntityConfiguration :
    IEntityTypeConfiguration<WorkflowExecutorInvocationRecordEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowExecutorInvocationRecordEntity> builder)
    {
        builder.ToTable("AgentFramework_WorkflowExecutorInvocations");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.ScopeKey).HasMaxLength(64).IsRequired();
        builder.Property(record => record.InvocationKey).HasMaxLength(64).IsRequired();
        builder.Property(record => record.IdempotencyKey).HasMaxLength(64).IsRequired();
        builder.Property(record => record.NodeId).HasMaxLength(128).IsRequired();
        builder.Property(record => record.ExecutorId).HasMaxLength(256).IsRequired();
        builder.Property(record => record.ExecutorContractVersion).HasMaxLength(128).IsRequired();
        builder.Property(record => record.InputHash).HasMaxLength(64).IsRequired();
        builder.Property(record => record.State).HasConversion<int>();
        builder.Property(record => record.LeaseOwnerId).HasMaxLength(128);
        builder.Property(record => record.ProtectedStoredResult).HasColumnType("TEXT").IsRequired();
        builder.Property(record => record.StoredResultHash).HasMaxLength(64).IsRequired();
        builder.Property(record => record.FailureCode).HasMaxLength(128).IsRequired();
        builder.Property(record => record.SafeMessage).HasMaxLength(1024).IsRequired();
        builder.HasIndex(record => record.ScopeKey)
            .IsUnique()
            .HasDatabaseName("UX_AF_WorkflowExecutorInvocations_Scope");
        builder.HasIndex(record => record.InvocationKey)
            .IsUnique()
            .HasDatabaseName("UX_AF_WorkflowExecutorInvocations_Key");
        builder.HasIndex(record => record.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("UX_AF_WorkflowExecutorInvocations_IdempotencyKey");
        builder.HasIndex(record => new { record.State, record.LeaseExpiresAtUtc })
            .HasDatabaseName("IX_AF_WorkflowExecutorInvocations_Lease");
        builder.HasIndex(record => new
            {
                record.RunId,
                record.CausationRequestId,
                record.CausationOperationId
            })
            .HasDatabaseName("IX_AF_WorkflowExecutorInvocations_Causation");
    }
}
