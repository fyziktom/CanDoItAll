using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class WorkflowExternalRequestBoundaryEntityConfiguration :
    IEntityTypeConfiguration<WorkflowExternalRequestBoundaryEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowExternalRequestBoundaryEntity> builder)
    {
        builder.ToTable("AgentFramework_WorkflowExternalRequestBoundaries");
        builder.HasKey(boundary => boundary.RequestId);
        builder.Property(boundary => boundary.ResponseContractJson).HasColumnType("TEXT").IsRequired();
        builder.Property(boundary => boundary.ContinuationJson).HasColumnType("TEXT").IsRequired();
        builder.Property(boundary => boundary.RequestPayloadHash).HasMaxLength(64).IsRequired();
        builder.Property(boundary => boundary.AuthorizationPolicyJson).HasColumnType("TEXT");
        builder.Property(boundary => boundary.ConcurrencyToken).IsConcurrencyToken();
        builder.HasOne<WorkflowExternalRequestRecordEntity>()
            .WithOne()
            .HasForeignKey<WorkflowExternalRequestBoundaryEntity>(boundary => boundary.RequestId)
            .HasConstraintName("FK_AF_WfRequestBoundaries_Requests")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
