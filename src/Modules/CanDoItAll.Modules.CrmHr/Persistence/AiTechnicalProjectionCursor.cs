using CanDoItAll.AgentFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.CrmHr;

public sealed class AiTechnicalProjectionCursor {
    public Guid DatabaseProfileId { get; set; }
    public WorkspaceScopeKind SourceScopeKind { get; set; }
    public string SourceScopeKey { get; set; } = string.Empty;
    public long CatalogRevision { get; set; }
    public string ProjectionSha256 { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class AiTechnicalProjectionCursorConfiguration : IEntityTypeConfiguration<AiTechnicalProjectionCursor> {
    public void Configure(EntityTypeBuilder<AiTechnicalProjectionCursor> builder) {
        builder.ToTable("CrmHr_AiTechnicalProjectionCursors");
        builder.HasKey(cursor => new { cursor.DatabaseProfileId, cursor.SourceScopeKind, cursor.SourceScopeKey });
        builder.Property(cursor => cursor.SourceScopeKind).HasConversion<string>().HasMaxLength(32);
        builder.Property(cursor => cursor.SourceScopeKey).HasMaxLength(200);
        builder.Property(cursor => cursor.ProjectionSha256).HasMaxLength(64);
    }
}
