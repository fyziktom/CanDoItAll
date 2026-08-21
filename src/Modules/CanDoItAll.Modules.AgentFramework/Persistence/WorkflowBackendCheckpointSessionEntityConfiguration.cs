using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class WorkflowBackendCheckpointSessionEntityConfiguration :
    IEntityTypeConfiguration<WorkflowBackendCheckpointSessionEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowBackendCheckpointSessionEntity> builder)
    {
        builder.ToTable("AgentFramework_WorkflowBackendCheckpointSessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).HasMaxLength(300);
        builder.Property(session => session.Format).HasMaxLength(120).IsRequired();
        builder.Property(session => session.TopologyFingerprint).HasMaxLength(64).IsRequired();
        builder.HasIndex(session => session.RunId)
            .IsUnique()
            .HasDatabaseName("UX_AF_WfCheckpointSessions_Run");
        builder.HasIndex(session => new { session.WorkflowId, session.WorkflowVersionId })
            .HasDatabaseName("IX_AF_WfCheckpointSessions_WorkflowVersion");
        builder.HasOne<WorkflowRunRecordEntity>()
            .WithOne()
            .HasForeignKey<WorkflowBackendCheckpointSessionEntity>(session => session.RunId)
            .HasConstraintName("FK_AF_WfCheckpointSessions_Runs")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
