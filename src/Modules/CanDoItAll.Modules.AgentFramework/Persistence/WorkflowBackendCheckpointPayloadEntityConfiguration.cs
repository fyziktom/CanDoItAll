using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class WorkflowBackendCheckpointPayloadEntityConfiguration :
    IEntityTypeConfiguration<WorkflowBackendCheckpointPayloadEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowBackendCheckpointPayloadEntity> builder)
    {
        builder.ToTable("AgentFramework_WorkflowBackendCheckpointPayloads");
        builder.HasKey(checkpoint => checkpoint.Id);
        builder.Property(checkpoint => checkpoint.Id).HasMaxLength(300);
        builder.Property(checkpoint => checkpoint.SessionId).HasMaxLength(300).IsRequired();
        builder.Property(checkpoint => checkpoint.ParentCheckpointId).HasMaxLength(300);
        builder.Property(checkpoint => checkpoint.ProtectedPayload).HasColumnType("TEXT").IsRequired();
        builder.Property(checkpoint => checkpoint.PayloadHash).HasMaxLength(64).IsRequired();
        builder.Property(checkpoint => checkpoint.BackendRequestId).HasMaxLength(300);
        builder.Property(checkpoint => checkpoint.BackendRequestPortId).HasMaxLength(300);
        builder.HasIndex(checkpoint => new { checkpoint.SessionId, checkpoint.CommitOrdinal })
            .IsUnique()
            .HasDatabaseName("UX_AF_WfBackendCheckpoints_SessionOrdinal");
        builder.HasIndex(checkpoint => checkpoint.ExternalRequestId)
            .IsUnique()
            .HasFilter("\"ExternalRequestId\" IS NOT NULL")
            .HasDatabaseName("UX_AF_WfBackendCheckpoints_ExternalRequest");
        builder.HasIndex(checkpoint => checkpoint.ParentCheckpointId)
            .HasDatabaseName("IX_AF_WfBackendCheckpoints_Parent");
        builder.HasOne<WorkflowBackendCheckpointSessionEntity>()
            .WithMany()
            .HasForeignKey(checkpoint => checkpoint.SessionId)
            .HasConstraintName("FK_WfBackendCheckpointPayloads_Sessions")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<WorkflowBackendCheckpointPayloadEntity>()
            .WithMany()
            .HasForeignKey(checkpoint => checkpoint.ParentCheckpointId)
            .HasConstraintName("FK_WfBackendCheckpointPayloads_Parent")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
