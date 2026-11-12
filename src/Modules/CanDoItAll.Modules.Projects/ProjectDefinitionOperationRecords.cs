using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectDefinitionReceiptRecord {
    public const string TableName = "Projects_DefinitionReceipts";
    public const int MaximumPayloadBytes = 2 * 1024 * 1024;
    public Guid NamespaceId { get; set; }
    public long Epoch { get; set; }
    public ProjectMutationCallerKind CallerKind { get; set; }
    public byte[] CallerKey { get; set; } = [];
    public Guid OperationId { get; set; }
    public int SchemaVersion { get; set; }
    public int NormalizationVersion { get; set; }
    public int ResultVersion { get; set; }
    public ProjectDefinitionCommandKind CommandKind { get; set; }
    public byte[] SemanticDigest { get; set; } = [];
    public Guid ProjectId { get; set; }
    public Guid LifetimeId { get; set; }
    public Guid AcceptedRevision { get; set; }
    public DateTimeOffset AcceptedAtUtc { get; set; }
    public byte[] AcceptedPayload { get; set; } = [];
    public string? CreationFingerprint { get; set; }
}

internal sealed class ProjectDefinitionReceiptRecordConfiguration : IEntityTypeConfiguration<ProjectDefinitionReceiptRecord> {
    public void Configure(EntityTypeBuilder<ProjectDefinitionReceiptRecord> builder) {
        builder.ToTable(ProjectDefinitionReceiptRecord.TableName, table => {
            table.HasCheckConstraint("CK_Projects_DefinitionReceipt_Address", "\"NamespaceId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"OperationId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"Epoch\" > 0");
            table.HasCheckConstraint("CK_Projects_DefinitionReceipt_Caller", "\"CallerKind\" IN (1, 2, 3, 4) AND octet_length(\"CallerKey\") = 32");
            table.HasCheckConstraint("CK_Projects_DefinitionReceipt_Digest", "octet_length(\"SemanticDigest\") = 32");
            table.HasCheckConstraint("CK_Projects_DefinitionReceipt_Interpretation", "\"SchemaVersion\" > 0 AND \"NormalizationVersion\" > 0 AND \"ResultVersion\" > 0 AND \"CommandKind\" IN (1, 2, 3)");
            table.HasCheckConstraint("CK_Projects_DefinitionReceipt_Target", "\"ProjectId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"LifetimeId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"AcceptedRevision\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("CK_Projects_DefinitionReceipt_Payload", $"octet_length(\"AcceptedPayload\") BETWEEN 1 AND {ProjectDefinitionReceiptRecord.MaximumPayloadBytes}");
            table.HasCheckConstraint("CK_Projects_DefinitionReceipt_Creation", "(\"CommandKind\" = 1 AND \"CreationFingerprint\" IS NOT NULL AND \"CreationFingerprint\" ~ '^[0-9a-f]{64}$') OR (\"CommandKind\" <> 1 AND \"CreationFingerprint\" IS NULL)");
        });
        builder.HasKey(record => new { record.NamespaceId, record.Epoch, record.CallerKind, record.CallerKey, record.OperationId });
        builder.Property(record => record.CallerKind).HasConversion<int>();
        builder.Property(record => record.CommandKind).HasConversion<int>();
        builder.Property(record => record.CallerKey).IsRequired();
        builder.Property(record => record.SemanticDigest).IsRequired();
        builder.Property(record => record.AcceptedPayload).IsRequired();
        builder.Property(record => record.CreationFingerprint).HasMaxLength(64);
    }
}
