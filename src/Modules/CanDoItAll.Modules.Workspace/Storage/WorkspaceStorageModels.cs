using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.Workspace;

public sealed record StorageCatalogSummary(
    Guid Id,
    string Name,
    StorageProviderKind ProviderKind,
    StorageConnectionMode ConnectionMode,
    string EndpointOrRoot,
    int DisplayOrder,
    bool IsEnabled,
    bool IsSystemDefault,
    bool IsReadOnly,
    StorageCapability CapabilityMask,
    StorageHealthStatus HealthStatus,
    DateTimeOffset? LastTestedAtUtc,
    string LastHealthMessage);

public sealed class StorageCatalogEditorModel
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public StorageProviderKind ProviderKind { get; set; } = StorageProviderKind.FileSystem;

    public StorageConnectionMode ConnectionMode { get; set; } = StorageConnectionMode.Local;

    public string EndpointOrRoot { get; set; } = string.Empty;

    public Guid? CredentialSecretId { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool IsSystemDefault { get; set; }

    public bool IsReadOnly { get; set; }

    public int DisplayOrder { get; set; }

    public StorageCapability CapabilityMask { get; set; } = StorageCapability.None;

    public StorageHealthStatus HealthStatus { get; set; } = StorageHealthStatus.Unknown;

    public DateTimeOffset? LastTestedAtUtc { get; set; }

    public string LastHealthMessage { get; set; } = string.Empty;

    public string GatewayBaseUrl { get; set; } = string.Empty;

    public int? Port { get; set; }

    public bool PinOnUpload { get; set; } = true;

    public string Username { get; set; } = string.Empty;

    public string BasePath { get; set; } = string.Empty;

    public bool UseSsl { get; set; } = true;

    public bool UsePassiveMode { get; set; } = true;

    public List<StorageUsagePurpose> DefaultPurposes { get; set; } = [];
}

public sealed record StorageCatalogTestResult(
    bool Success,
    string Message,
    StorageHealthStatus HealthStatus,
    StorageCapability CapabilityMask,
    DateTimeOffset TestedAtUtc);

public sealed record StorageRoutingPreferenceSummary(
    StorageUsagePurpose UsagePurpose,
    Guid? PreferredStorageId,
    string PreferredStorageName,
    bool IsEnabled,
    string Reason);

public static class WorkspaceStorageDefaults
{
    public static IReadOnlyList<StorageUsagePurpose> TrackedPurposes { get; } =
    [
        StorageUsagePurpose.ProjectAsset,
        StorageUsagePurpose.PromptAttachment,
        StorageUsagePurpose.PromptExport,
        StorageUsagePurpose.Evidence,
        StorageUsagePurpose.RecordingMedia,
        StorageUsagePurpose.SnapshotPackage,
        StorageUsagePurpose.ReleasePackage,
        StorageUsagePurpose.DeploymentMirror
    ];

    public static string DescribePurpose(StorageUsagePurpose purpose)
    {
        return StoragePresentation.DescribeUsagePurpose(purpose);
    }

    public static int ResolveTrackedPurposeOrder(StorageUsagePurpose purpose)
    {
        for (var index = 0; index < TrackedPurposes.Count; index++)
        {
            if (TrackedPurposes[index] == purpose)
            {
                return index;
            }
        }

        return int.MaxValue;
    }
}
