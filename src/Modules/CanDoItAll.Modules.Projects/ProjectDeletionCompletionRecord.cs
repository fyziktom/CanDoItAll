using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Projects;

internal sealed class ProjectDeletionCompletionRecord {
    internal const string TableName = "Projects_DeletionCompletions";
    internal static string AwaitingFilter => FormattableString.Invariant($"\"ReleaseState\" = {(int)ProjectDeletionCompletionReleaseState.AwaitingOwnerRelease}");
    public Guid NamespaceId { get; set; }
    public Guid CompletionId { get; set; }
    public Guid DatabaseProfileId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid LifetimeId { get; set; }
    public int SchemaVersion { get; set; }
    public ProjectDeletionCompletionOrigin OriginKind { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public ProjectDeletionCompletionReleaseState ReleaseState { get; set; }
    public DateTimeOffset? ReleasedAtUtc { get; set; }
    public byte[] ReferencePayload { get; set; } = [];
    public byte[] RecoveryPayload { get; set; } = [];
    public byte[] RecoveryDigest { get; set; } = [];
}

internal sealed class ProjectDeletionCompletionRecordConfiguration : IEntityTypeConfiguration<ProjectDeletionCompletionRecord> {
    public void Configure(EntityTypeBuilder<ProjectDeletionCompletionRecord> builder) {
        builder.ToTable(ProjectDeletionCompletionRecord.TableName, table => {
            table.HasCheckConstraint("CK_Projects_DeletionCompletion_Address", "\"NamespaceId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"CompletionId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("CK_Projects_DeletionCompletion_Target", "\"DatabaseProfileId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"ProjectId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"LifetimeId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("CK_Projects_DeletionCompletion_Interpretation", "\"SchemaVersion\" = 1 AND \"OriginKind\" IN (1, 2, 3, 4)");
            table.HasCheckConstraint("CK_Projects_DeletionCompletion_Release", "isfinite(\"CreatedAtUtc\") AND ((\"ReleaseState\" = 1 AND \"ReleasedAtUtc\" IS NULL) OR (\"ReleaseState\" = 2 AND \"ReleasedAtUtc\" IS NOT NULL AND isfinite(\"ReleasedAtUtc\")))");
            table.HasCheckConstraint("CK_Projects_DeletionCompletion_References", $"octet_length(\"ReferencePayload\") BETWEEN 1 AND {ProjectDeletionCompletionCodec.MaximumEncodedBytes}");
            table.HasCheckConstraint("CK_Projects_DeletionCompletion_Recovery", $"octet_length(\"RecoveryPayload\") BETWEEN 1 AND {ProjectDeletionRecoveryCodec.MaximumEncodedBytes} AND octet_length(\"RecoveryDigest\") = 32");
        });
        builder.HasKey(record => new { record.NamespaceId, record.CompletionId });
        builder.HasIndex(record => new { record.NamespaceId, record.ProjectId, record.LifetimeId }).IsUnique();
        builder.HasIndex(record => new { record.NamespaceId, record.ReleaseState, record.CreatedAtUtc });
        builder.HasIndex(record => record.CompletionId, "IX_Projects_DeletionCompletions_Awaiting")
            .HasFilter(ProjectDeletionCompletionRecord.AwaitingFilter);
        builder.Property(record => record.OriginKind).HasConversion<int>();
        builder.Property(record => record.ReleaseState).HasConversion<int>();
        builder.Property(record => record.ReferencePayload).IsRequired();
        builder.Property(record => record.RecoveryPayload).IsRequired();
        builder.Property(record => record.RecoveryDigest).IsRequired();
    }
}
