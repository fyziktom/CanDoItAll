using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Projects;

internal sealed class ProjectLifecycleReceiptRecord {
    internal const string TableName = "Projects_LifecycleReceipts";
    public Guid NamespaceId { get; set; }
    public long Epoch { get; set; }
    public ProjectMutationCallerKind CallerKind { get; set; }
    public byte[] CallerKey { get; set; } = [];
    public Guid OperationId { get; set; }
    public Guid DatabaseProfileId { get; set; }
    public int SchemaVersion { get; set; }
    public int NormalizationVersion { get; set; }
    public int ResultVersion { get; set; }
    public ProjectLifecycleCommandKind CommandKind { get; set; }
    public byte[] SemanticDigest { get; set; } = [];
    public Guid ProjectId { get; set; }
    public Guid LifetimeId { get; set; }
    public DateTimeOffset AcceptedAtUtc { get; set; }
    public byte[] AcceptedPayload { get; set; } = [];
}

internal sealed class ProjectLifecycleReceiptRecordConfiguration : IEntityTypeConfiguration<ProjectLifecycleReceiptRecord> {
    public void Configure(EntityTypeBuilder<ProjectLifecycleReceiptRecord> builder) {
        builder.ToTable(ProjectLifecycleReceiptRecord.TableName, table => {
            table.HasCheckConstraint("CK_Projects_LifecycleReceipt_Address", "\"NamespaceId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"OperationId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"DatabaseProfileId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"Epoch\" > 0");
            table.HasCheckConstraint("CK_Projects_LifecycleReceipt_Caller", "\"CallerKind\" IN (1, 2, 3, 4) AND octet_length(\"CallerKey\") = 32");
            table.HasCheckConstraint("CK_Projects_LifecycleReceipt_Digest", "octet_length(\"SemanticDigest\") = 32");
            table.HasCheckConstraint("CK_Projects_LifecycleReceipt_Interpretation", "\"SchemaVersion\" > 0 AND \"NormalizationVersion\" > 0 AND \"ResultVersion\" > 0 AND \"CommandKind\" IN (1, 2, 3, 4)");
            table.HasCheckConstraint("CK_Projects_LifecycleReceipt_Target", "\"ProjectId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"LifetimeId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("CK_Projects_LifecycleReceipt_Time", "isfinite(\"AcceptedAtUtc\")");
            table.HasCheckConstraint("CK_Projects_LifecycleReceipt_Payload", $"octet_length(\"AcceptedPayload\") BETWEEN 1 AND {ProjectLifecycleAccepted.MaximumPayloadBytes}");
        });
        builder.HasKey(record => new { record.NamespaceId, record.Epoch, record.CallerKind, record.CallerKey, record.OperationId });
        builder.Property(record => record.CallerKind).HasConversion<int>();
        builder.Property(record => record.CommandKind).HasConversion<int>();
        builder.Property(record => record.CallerKey).IsRequired();
        builder.Property(record => record.SemanticDigest).IsRequired();
        builder.Property(record => record.AcceptedPayload).IsRequired();
    }
}
