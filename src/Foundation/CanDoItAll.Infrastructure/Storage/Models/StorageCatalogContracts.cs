namespace CanDoItAll.Infrastructure.Storage;

public record StorageCatalogSnapshot {
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; init; } = string.Empty;

    public StorageProviderKind ProviderKind { get; init; }

    public bool IsEnabled { get; init; } = true;

    public bool IsSystemDefault { get; init; }

    public bool IsReadOnly { get; init; }

    public int DisplayOrder { get; init; }

    public StorageConnectionMode ConnectionMode { get; init; } = StorageConnectionMode.Local;

    public string EndpointOrRoot { get; init; } = string.Empty;

    public int RootBindingFormatVersion { get; init; }

    public HostPlatformFamily RootPlatformFamily { get; init; }

    public CanDoItAll.SharedKernel.PhysicalPathSyntax RootPathSyntax { get; init; }

    public string RootHostBindingId { get; init; } = string.Empty;

    public HostBoundPathState RootPathState { get; init; } = HostBoundPathState.NeedsRebind;

    public DateTimeOffset? RootLastValidatedAtUtc { get; init; }

    public StorageCapability CapabilityMask { get; init; } = StorageCapability.Read |
        StorageCapability.Write |
        StorageCapability.Delete |
        StorageCapability.Download |
        StorageCapability.InlinePreview |
        StorageCapability.OpenLocally |
        StorageCapability.MutableUpdate |
        StorageCapability.BatchFolderUpload |
        StorageCapability.BatchTransfer |
        StorageCapability.ConnectionTest;

    public StorageHealthStatus HealthStatus { get; init; } = StorageHealthStatus.Unknown;

    public DateTimeOffset? LastTestedAtUtc { get; init; }

    public string LastHealthMessage { get; init; } = string.Empty;

    public Guid? CredentialSecretId { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }

    public string SourceFingerprint { get; init; } = string.Empty;
}

public sealed record StorageCatalogSaveRequest {
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; init; } = string.Empty;

    public StorageProviderKind ProviderKind { get; init; }

    public bool IsEnabled { get; init; } = true;

    public bool IsSystemDefault { get; init; }

    public bool IsReadOnly { get; init; }

    public int DisplayOrder { get; init; }

    public StorageConnectionMode ConnectionMode { get; init; } = StorageConnectionMode.Local;

    public string EndpointOrRoot { get; init; } = string.Empty;

    public StorageCapability CapabilityMask { get; init; } = StorageCapability.Read |
        StorageCapability.Write |
        StorageCapability.Delete |
        StorageCapability.Download |
        StorageCapability.InlinePreview |
        StorageCapability.OpenLocally |
        StorageCapability.MutableUpdate |
        StorageCapability.BatchFolderUpload |
        StorageCapability.BatchTransfer |
        StorageCapability.ConnectionTest;

    public StorageHealthStatus HealthStatus { get; init; } = StorageHealthStatus.Unknown;

    public DateTimeOffset? LastTestedAtUtc { get; init; }

    public string LastHealthMessage { get; init; } = string.Empty;

    public Guid? CredentialSecretId { get; init; }

    public StorageProviderConfiguration? Configuration { get; init; }

    public static StorageCatalogSaveRequest FromSnapshot(StorageCatalogSnapshot source) {
        ArgumentNullException.ThrowIfNull(source);
        return new() {
            Id = source.Id,
            Name = source.Name,
            ProviderKind = source.ProviderKind,
            IsEnabled = source.IsEnabled,
            IsSystemDefault = source.IsSystemDefault,
            IsReadOnly = source.IsReadOnly,
            DisplayOrder = source.DisplayOrder,
            ConnectionMode = source.ConnectionMode,
            EndpointOrRoot = source.EndpointOrRoot,
            CapabilityMask = source.CapabilityMask,
            HealthStatus = source.HealthStatus,
            LastTestedAtUtc = source.LastTestedAtUtc,
            LastHealthMessage = source.LastHealthMessage,
            CredentialSecretId = source.CredentialSecretId
        };
    }
}

public sealed record StorageRoutingRuleSnapshot {
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; init; } = string.Empty;

    public bool IsEnabled { get; init; } = true;

    public int Priority { get; init; } = 100;

    public StorageRoutingScopeKind ScopeKind { get; init; } = StorageRoutingScopeKind.Workspace;

    public Guid? ProjectId { get; init; }

    public string NodeKey { get; init; } = string.Empty;

    public StorageUsagePurpose UsagePurpose { get; init; } = StorageUsagePurpose.Unknown;

    public StorageContentKind ContentKind { get; init; } = StorageContentKind.Unknown;

    public string MimePattern { get; init; } = string.Empty;

    public long? MinimumContentLength { get; init; }

    public long? MaximumContentLength { get; init; }

    public bool EditIntent { get; init; }

    public bool PreviewRequired { get; init; }

    public bool PublishIntent { get; init; }

    public StorageCapability RequiredCapabilities { get; init; } = StorageCapability.Write;

    public Guid PreferredStorageId { get; init; }

    public string Reason { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record StorageRoutingRuleSaveRequest {
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; init; } = string.Empty;

    public bool IsEnabled { get; init; } = true;

    public int Priority { get; init; } = 100;

    public StorageRoutingScopeKind ScopeKind { get; init; } = StorageRoutingScopeKind.Workspace;

    public Guid? ProjectId { get; init; }

    public string NodeKey { get; init; } = string.Empty;

    public StorageUsagePurpose UsagePurpose { get; init; } = StorageUsagePurpose.Unknown;

    public StorageContentKind ContentKind { get; init; } = StorageContentKind.Unknown;

    public string MimePattern { get; init; } = string.Empty;

    public long? MinimumContentLength { get; init; }

    public long? MaximumContentLength { get; init; }

    public bool EditIntent { get; init; }

    public bool PreviewRequired { get; init; }

    public bool PublishIntent { get; init; }

    public StorageCapability RequiredCapabilities { get; init; } = StorageCapability.Write;

    public Guid PreferredStorageId { get; init; }

    public string Reason { get; init; } = string.Empty;

    public IReadOnlyList<Guid>? AlternativeStorageIds { get; init; }

    public static StorageRoutingRuleSaveRequest FromSnapshot(StorageRoutingRuleSnapshot source) {
        ArgumentNullException.ThrowIfNull(source);
        return new() {
            Id = source.Id,
            Name = source.Name,
            IsEnabled = source.IsEnabled,
            Priority = source.Priority,
            ScopeKind = source.ScopeKind,
            ProjectId = source.ProjectId,
            NodeKey = source.NodeKey,
            UsagePurpose = source.UsagePurpose,
            ContentKind = source.ContentKind,
            MimePattern = source.MimePattern,
            MinimumContentLength = source.MinimumContentLength,
            MaximumContentLength = source.MaximumContentLength,
            EditIntent = source.EditIntent,
            PreviewRequired = source.PreviewRequired,
            PublishIntent = source.PublishIntent,
            RequiredCapabilities = source.RequiredCapabilities,
            PreferredStorageId = source.PreferredStorageId,
            Reason = source.Reason
        };
    }
}

public sealed record StorageCatalogEditorSnapshot(StorageCatalogSnapshot Catalog, StorageProviderConfiguration Configuration);
