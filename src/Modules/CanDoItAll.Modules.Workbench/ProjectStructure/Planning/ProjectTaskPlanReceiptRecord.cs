using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectTaskPlanReceiptRecord {
    public const string TableName = "Workbench_TaskPlanReceipts";
    public const int MaximumPayloadBytes = 2 * 1024 * 1024;
    public Guid NamespaceId { get; set; }
    public long Epoch { get; set; }
    public int CallerKind { get; set; }
    public byte[] CallerKey { get; set; } = [];
    public Guid OperationId { get; set; }
    public int SchemaVersion { get; set; }
    public int NormalizationVersion { get; set; }
    public int ResultVersion { get; set; }
    public byte[] SemanticDigest { get; set; } = [];
    public Guid ProjectId { get; set; }
    public Guid LifetimeId { get; set; }
    public Guid OriginalDefinitionRevision { get; set; }
    public Guid DecisionId { get; set; }
    public long DecisionGeneration { get; set; }
    public int SourceKind { get; set; }
    public int SourceVersion { get; set; }
    public byte[] SourceFingerprint { get; set; } = [];
    public DateTimeOffset AcceptedAtUtc { get; set; }
    public byte[] AcceptedPayload { get; set; } = [];
}

internal sealed class ProjectTaskPlanReceiptRecordConfiguration : IEntityTypeConfiguration<ProjectTaskPlanReceiptRecord> {
    public void Configure(EntityTypeBuilder<ProjectTaskPlanReceiptRecord> builder) {
        builder.ToTable(ProjectTaskPlanReceiptRecord.TableName, table => {
            table.HasCheckConstraint("CK_Workbench_TaskPlanReceipt_Address", "\"NamespaceId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"OperationId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"Epoch\" > 0");
            table.HasCheckConstraint("CK_Workbench_TaskPlanReceipt_Caller", "\"CallerKind\" = 1 AND octet_length(\"CallerKey\") = 32");
            table.HasCheckConstraint("CK_Workbench_TaskPlanReceipt_Digest", "octet_length(\"SemanticDigest\") = 32 AND octet_length(\"SourceFingerprint\") = 32");
            table.HasCheckConstraint("CK_Workbench_TaskPlanReceipt_Interpretation", "\"SchemaVersion\" > 0 AND \"NormalizationVersion\" > 0 AND \"ResultVersion\" > 0 AND \"SourceVersion\" > 0 AND \"SourceKind\" = 1");
            table.HasCheckConstraint("CK_Workbench_TaskPlanReceipt_Target", "\"ProjectId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"LifetimeId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"OriginalDefinitionRevision\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("CK_Workbench_TaskPlanReceipt_Decision", "\"DecisionId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"DecisionGeneration\" > 0");
            table.HasCheckConstraint("CK_Workbench_TaskPlanReceipt_Payload", $"octet_length(\"AcceptedPayload\") BETWEEN 1 AND {ProjectTaskPlanReceiptRecord.MaximumPayloadBytes}");
        });
        builder.HasKey(record => new { record.NamespaceId, record.Epoch, record.CallerKind, record.CallerKey, record.OperationId });
        builder.Property(record => record.CallerKey).IsRequired();
        builder.Property(record => record.SemanticDigest).IsRequired();
        builder.Property(record => record.SourceFingerprint).IsRequired();
        builder.Property(record => record.AcceptedPayload).IsRequired();
    }
}
