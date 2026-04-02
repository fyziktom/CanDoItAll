using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Infrastructure.Storage;

public sealed class StorageCatalogRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public StorageProviderKind ProviderKind { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool IsSystemDefault { get; set; }

    public bool IsReadOnly { get; set; }

    public int DisplayOrder { get; set; }

    public StorageConnectionMode ConnectionMode { get; set; } = StorageConnectionMode.Local;

    public string EndpointOrRoot { get; set; } = string.Empty;

    public string ConfigJson { get; set; } = "{}";

    public StorageCapability CapabilityMask { get; set; } =
        StorageCapability.Read |
        StorageCapability.Write |
        StorageCapability.Delete |
        StorageCapability.Download |
        StorageCapability.InlinePreview |
        StorageCapability.OpenLocally |
        StorageCapability.MutableUpdate |
        StorageCapability.BatchFolderUpload |
        StorageCapability.BatchTransfer |
        StorageCapability.ConnectionTest;

    public StorageHealthStatus HealthStatus { get; set; } = StorageHealthStatus.Unknown;

    public DateTimeOffset? LastTestedAtUtc { get; set; }

    public string LastHealthMessage { get; set; } = string.Empty;

    public Guid? CredentialSecretId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class StorageCatalogRecordConfiguration : IEntityTypeConfiguration<StorageCatalogRecord>
{
    public void Configure(EntityTypeBuilder<StorageCatalogRecord> builder)
    {
        builder.ToTable("Storage_Catalog");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(200).IsRequired();
        builder.Property(item => item.EndpointOrRoot).HasMaxLength(1200).IsRequired();
        builder.Property(item => item.ConfigJson).HasColumnType("TEXT");
        builder.Property(item => item.LastHealthMessage).HasMaxLength(500);
        builder.HasIndex(item => item.Name).IsUnique();
        builder.HasIndex(item => new { item.ProviderKind, item.IsEnabled });
    }
}

public sealed class StorageRoutingRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public int Priority { get; set; } = 100;

    public StorageRoutingScopeKind ScopeKind { get; set; } = StorageRoutingScopeKind.Workspace;

    public Guid? ProjectId { get; set; }

    public string NodeKey { get; set; } = string.Empty;

    public StorageUsagePurpose UsagePurpose { get; set; } = StorageUsagePurpose.Unknown;

    public StorageContentKind ContentKind { get; set; } = StorageContentKind.Unknown;

    public string MimePattern { get; set; } = string.Empty;

    public long? MinimumContentLength { get; set; }

    public long? MaximumContentLength { get; set; }

    public bool EditIntent { get; set; }

    public bool PreviewRequired { get; set; }

    public bool PublishIntent { get; set; }

    public StorageCapability RequiredCapabilities { get; set; } = StorageCapability.Write;

    public Guid PreferredStorageId { get; set; }

    public string AlternativeStorageIdsJson { get; set; } = "[]";

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class StorageRoutingRuleConfiguration : IEntityTypeConfiguration<StorageRoutingRule>
{
    public void Configure(EntityTypeBuilder<StorageRoutingRule> builder)
    {
        builder.ToTable("Storage_RoutingRules");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(200).IsRequired();
        builder.Property(item => item.NodeKey).HasMaxLength(160);
        builder.Property(item => item.MimePattern).HasMaxLength(200);
        builder.Property(item => item.AlternativeStorageIdsJson).HasColumnType("TEXT");
        builder.Property(item => item.Reason).HasMaxLength(500);
        builder.HasIndex(item => new
        {
            item.ScopeKind,
            item.ProjectId,
            item.NodeKey,
            item.Priority,
            item.PreferredStorageId
        });
    }
}
