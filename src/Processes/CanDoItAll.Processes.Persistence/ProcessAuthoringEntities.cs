using CanDoItAll.Processes.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Processes.Persistence;

public sealed class ProcessAuthoringHeadEntity {
    public Guid DatabaseProfileId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ProjectLifetimeId { get; set; }
    public string DefinitionKey { get; set; } = string.Empty;
    public long Revision { get; set; }
    public ProcessAuthoringLifecycle Lifecycle { get; set; }
    public Guid? PublishedId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Criticality { get; set; } = string.Empty;
    public string OperatingMode { get; set; } = string.Empty;
    public string ContentJson { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class ProcessAuthoringPublicationEntity {
    public Guid Id { get; set; }
    public Guid DatabaseProfileId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ProjectLifetimeId { get; set; }
    public string DefinitionKey { get; set; } = string.Empty;
    public long Revision { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string ContentJson { get; set; } = string.Empty;
    public DateTimeOffset PublishedAtUtc { get; set; }
}

public sealed class ProcessAuthoringReceiptEntity {
    public Guid DatabaseProfileId { get; set; }
    public string CallerId { get; set; } = string.Empty;
    public Guid OperationId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ProjectLifetimeId { get; set; }
    public string DefinitionKey { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
    public string ReceiptJson { get; set; } = string.Empty;
}

internal sealed class ProcessAuthoringHeadConfiguration : IEntityTypeConfiguration<ProcessAuthoringHeadEntity> {
    public void Configure(EntityTypeBuilder<ProcessAuthoringHeadEntity> builder) {
        builder.ToTable("process_authoring_heads", table => {
            table.HasCheckConstraint("CK_process_authoring_heads_scope", "(\"ProjectId\" = '00000000-0000-0000-0000-000000000000'::uuid) = (\"ProjectLifetimeId\" = '00000000-0000-0000-0000-000000000000'::uuid)");
            table.HasCheckConstraint("CK_process_authoring_heads_revision", "\"Revision\" > 0");
        });
        builder.HasKey(item => new { item.DatabaseProfileId, item.ProjectId, item.ProjectLifetimeId, item.DefinitionKey });
        builder.Property(item => item.DefinitionKey).HasMaxLength(200);
        builder.Property(item => item.Revision).IsConcurrencyToken();
        builder.Property(item => item.Lifecycle).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Name).HasMaxLength(500);
        builder.Property(item => item.Summary).HasMaxLength(8000);
        builder.Property(item => item.Criticality).HasMaxLength(64);
        builder.Property(item => item.OperatingMode).HasMaxLength(64);
        builder.Property(item => item.ContentJson).IsRequired();
        builder.HasIndex(item => new { item.DatabaseProfileId, item.ProjectId, item.ProjectLifetimeId, item.Lifecycle });
    }
}

internal sealed class ProcessAuthoringPublicationConfiguration : IEntityTypeConfiguration<ProcessAuthoringPublicationEntity> {
    public void Configure(EntityTypeBuilder<ProcessAuthoringPublicationEntity> builder) {
        builder.ToTable("process_authoring_publications");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.DefinitionKey).HasMaxLength(200);
        builder.Property(item => item.ContentHash).HasMaxLength(71);
        builder.Property(item => item.ContentJson).IsRequired();
        builder.HasIndex(item => new { item.DatabaseProfileId, item.ProjectId, item.ProjectLifetimeId, item.DefinitionKey, item.Revision }).IsUnique();
    }
}

internal sealed class ProcessAuthoringReceiptConfiguration : IEntityTypeConfiguration<ProcessAuthoringReceiptEntity> {
    public void Configure(EntityTypeBuilder<ProcessAuthoringReceiptEntity> builder) {
        builder.ToTable("process_authoring_receipts");
        builder.HasKey(item => new { item.DatabaseProfileId, item.CallerId, item.OperationId });
        builder.Property(item => item.CallerId).HasMaxLength(200);
        builder.Property(item => item.DefinitionKey).HasMaxLength(200);
        builder.Property(item => item.RequestFingerprint).HasMaxLength(71);
        builder.Property(item => item.ReceiptJson).IsRequired();
    }
}
