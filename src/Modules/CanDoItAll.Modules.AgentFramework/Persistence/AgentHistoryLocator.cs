using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentHistoryLocator : IHasConcurrencyToken {
    public Guid PartitionId { get; set; }
    public Guid EvidenceId { get; set; }
    public Guid OwnerId { get; set; }
    public WorkspaceScopeKind ScopeKind { get; set; }
    public string ScopeKey { get; set; } = "";
    public Guid? ProjectId { get; set; }
    public long SourceVersion { get; set; }
    public bool IsDeleted { get; set; }
    public Guid ConcurrencyToken { get; set; }
}

internal sealed class AgentHistoryLocatorConfiguration : IEntityTypeConfiguration<AgentHistoryLocator> {
    public void Configure(EntityTypeBuilder<AgentHistoryLocator> builder) {
        builder.ToTable("AgentFramework_HistoryLocators");
        builder.HasKey(row => new { row.PartitionId, row.EvidenceId });
        builder.Property(row => row.ScopeKey).HasMaxLength(256);
        builder.Property(row => row.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(row => new { row.PartitionId, row.ProjectId, row.IsDeleted });
    }
}
