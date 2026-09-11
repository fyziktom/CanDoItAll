using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Composition;

internal sealed class ProjectWorkAssignmentCanonicalConfiguration : IEntityTypeConfiguration<ProjectWorkAssignmentRecord> {
    public void Configure(EntityTypeBuilder<ProjectWorkAssignmentRecord> builder) {
        builder.HasOne<PartyOrganizationAffiliation>()
            .WithMany()
            .HasForeignKey(item => item.PartyOrganizationAffiliationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
