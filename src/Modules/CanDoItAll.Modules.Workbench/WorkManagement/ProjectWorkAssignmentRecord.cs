using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectWorkAssignmentRecord {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid PartyId { get; set; }
    public Guid? PartyOrganizationAffiliationId { get; set; }
    public string NodeKey { get; set; } = string.Empty;
    public string PhaseName { get; set; } = string.Empty;
    public Guid? OpportunityId { get; set; }
    public decimal? AllocationPercent { get; set; }
    public DateTimeOffset? StartsAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
    public bool IsPrimary { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    internal ProjectWorkAssignmentFact ToFact() => new(Id, ProjectId, PartyId, PartyOrganizationAffiliationId,
        NodeKey, PhaseName, OpportunityId, AllocationPercent, StartsAtUtc, EndsAtUtc, IsPrimary, Source, Notes);
}

public sealed class ProjectWorkAssignmentRecordConfiguration : IEntityTypeConfiguration<ProjectWorkAssignmentRecord> {
    public void Configure(EntityTypeBuilder<ProjectWorkAssignmentRecord> builder) {
        builder.ToTable("Workbench_WorkAssignments");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.NodeKey).HasMaxLength(160);
        builder.Property(item => item.PhaseName).HasMaxLength(160);
        builder.Property(item => item.Source).HasMaxLength(80);
        builder.Property(item => item.Notes).HasColumnType("TEXT");
        builder.HasIndex(item => new { item.ProjectId, item.PartyId, item.NodeKey });
        builder.HasIndex(item => new { item.ProjectId, item.NodeKey });
        builder.HasIndex(item => item.ProjectId);
        builder.HasIndex(item => item.PartyId);
        builder.HasIndex(item => item.PartyOrganizationAffiliationId);
        builder.HasIndex(item => item.OpportunityId);
    }
}
