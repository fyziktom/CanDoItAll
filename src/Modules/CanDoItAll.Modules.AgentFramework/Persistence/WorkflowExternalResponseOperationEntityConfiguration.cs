using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class WorkflowExternalResponseOperationEntityConfiguration :
    IEntityTypeConfiguration<WorkflowExternalResponseOperationEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowExternalResponseOperationEntity> builder)
    {
        builder.ToTable("AgentFramework_WorkflowExternalResponseOperations");
        builder.HasKey(operation => operation.Id);
        builder.Property(operation => operation.IdempotencyKeyHash).HasMaxLength(64).IsRequired();
        builder.Property(operation => operation.ResponsePayloadHash).HasMaxLength(64).IsRequired();
        builder.Property(operation => operation.ActorScopeFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(operation => operation.ProtectedResponsePayload).HasColumnType("TEXT").IsRequired();
        builder.Property(operation => operation.ActorSubjectId).HasMaxLength(500).IsRequired();
        builder.Property(operation => operation.CorrelationId).HasMaxLength(500).IsRequired();
        builder.Property(operation => operation.LeaseOwnerId).HasMaxLength(300);
        builder.Property(operation => operation.SafeMessage).HasColumnType("TEXT");
        builder.Property(operation => operation.FinalResultJson).HasColumnType("TEXT");
        builder.Property(operation => operation.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(operation => operation.RequestId)
            .IsUnique()
            .HasDatabaseName("UX_AF_WfResponseOperations_Request");
        builder.HasIndex(operation => new
            {
                operation.RequestId,
                operation.IdempotencyKeyHash,
                operation.ActorScopeFingerprint
            })
            .IsUnique()
            .HasDatabaseName("UX_AF_WfResponseOperations_Fingerprint");
        builder.HasIndex(operation => new
            {
                operation.State,
                operation.LeaseExpiresAtUtc,
                operation.AcceptedAtUtc
            })
            .HasDatabaseName("IX_AF_WfResponseOperations_Recovery");
        builder.HasIndex(operation => operation.RunId)
            .HasDatabaseName("IX_AF_WfResponseOperations_Run");
        builder.HasOne<WorkflowExternalRequestRecordEntity>()
            .WithOne()
            .HasForeignKey<WorkflowExternalResponseOperationEntity>(operation => operation.RequestId)
            .HasConstraintName("FK_AF_WfResponseOperations_Requests")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<WorkflowRunRecordEntity>()
            .WithMany()
            .HasForeignKey(operation => operation.RunId)
            .HasConstraintName("FK_AF_WfResponseOperations_Runs")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
