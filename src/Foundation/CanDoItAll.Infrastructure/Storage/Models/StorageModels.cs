using System.Text.Json.Serialization;

namespace CanDoItAll.Infrastructure.Storage;

[Flags]
public enum StorageCapability
{
    None = 0,
    Read = 1 << 0,
    Write = 1 << 1,
    Delete = 1 << 2,
    InlinePreview = 1 << 3,
    Download = 1 << 4,
    OpenLocally = 1 << 5,
    DirectUrl = 1 << 6,
    MutableUpdate = 1 << 7,
    BatchFolderUpload = 1 << 8,
    BatchTransfer = 1 << 9,
    ConnectionTest = 1 << 10
}

public enum StorageProviderKind
{
    FileSystem,
    Ipfs,
    Ftp
}

public enum StorageConnectionMode
{
    Local,
    Remote
}

public enum StorageHealthStatus
{
    Unknown,
    Healthy,
    Degraded,
    Unavailable
}

public enum StorageUsagePurpose
{
    Unknown,
    ProjectAsset,
    PromptAttachment,
    PromptExport,
    Evidence,
    RecordingMedia,
    DeploymentMirror,
    SnapshotPackage,
    ReleasePackage,
    WorkspaceExport
}

public enum StorageContentKind
{
    Unknown,
    Text,
    Json,
    Markdown,
    Mermaid,
    Log,
    Docx,
    Excel,
    Pdf,
    Image,
    Screenshot,
    Audio,
    Video,
    Archive,
    ReleasePackage
}

public enum StorageRoutingScopeKind
{
    Workspace,
    Project,
    Node
}

public enum StorageLocatorKind
{
    RelativePath,
    ContentAddress,
    RemotePath,
    AbsoluteUrl
}

public sealed record StorageSelectionContext(
    string FileName,
    string ContentType,
    StorageUsagePurpose UsagePurpose,
    StorageContentKind ContentKind = StorageContentKind.Unknown,
    Guid? ProjectId = null,
    string? NodeKey = null,
    long? ContentLength = null,
    bool EditIntent = false,
    bool PreviewRequired = false,
    bool PublishIntent = false,
    StorageCapability RequiredCapabilities = StorageCapability.Write);

public sealed record StorageRecommendationCandidate(
    Guid StorageId,
    string StorageName,
    StorageProviderKind ProviderKind,
    StorageCapability CapabilityMask,
    StorageHealthStatus HealthStatus,
    bool IsReadOnly,
    string Reason);

public sealed record StorageRecommendation(
    StorageRecommendationCandidate? PrimaryCandidate,
    IReadOnlyList<StorageRecommendationCandidate> Alternatives,
    string Reason,
    IReadOnlyList<string> Warnings);

public sealed record StorageObjectReference(
    Guid? StorageId,
    StorageProviderKind ProviderKind,
    StorageLocatorKind LocatorKind,
    string Locator,
    string DisplayName = "",
    string ContentType = "application/octet-stream",
    long? ContentLength = null,
    string Route = "",
    string MetadataJson = "{}");

public sealed record StorageAccessDescriptor(
    string PreviewUrl,
    string DownloadUrl,
    string? DirectUrl,
    bool SupportsInlinePreview,
    bool SupportsDownload,
    bool SupportsOpenLocally,
    string DisplayFileName,
    string ContentType,
    long? ContentLength,
    string ReasonWhenUnavailable);

public sealed record StorageConnectionTestResult(
    bool IsSuccess,
    string Message,
    StorageHealthStatus HealthStatus,
    StorageCapability CapabilityMask,
    DateTimeOffset TestedAtUtc);

public sealed record StorageWriteRequest(
    string FileName,
    string ContentType,
    byte[] Content,
    StorageUsagePurpose UsagePurpose,
    StorageContentKind ContentKind = StorageContentKind.Unknown,
    Guid? ProjectId = null,
    string? NodeKey = null,
    string? RelativePathHint = null,
    bool PreviewRequired = false,
    bool PublishIntent = false);

public sealed record StorageWriteResult(
    StorageObjectReference Reference,
    StorageAccessDescriptor AccessDescriptor);

public sealed record StorageTransferItem(
    string SourcePath,
    string TargetPath,
    string ContentType,
    StorageUsagePurpose UsagePurpose,
    StorageContentKind ContentKind = StorageContentKind.Unknown);

public delegate ValueTask StorageTransferProgressCallback(
    StorageTransferProgress progress,
    CancellationToken cancellationToken);

public delegate ValueTask<bool> StorageTransferRetryCallback(
    StorageTransferRetryContext context,
    CancellationToken cancellationToken);

public delegate ValueTask<StorageTransferVerificationResult> StorageTransferVerificationCallback(
    StorageTransferVerificationContext context,
    CancellationToken cancellationToken);

public sealed record StorageTransferProgress(
    int TotalCount,
    int CompletedCount,
    int SuccessCount,
    int FailureCount,
    StorageTransferItemResult CurrentItem);

public sealed record StorageTransferRetryContext(
    StorageTransferItem Item,
    int AttemptNumber,
    Exception Exception);

public sealed record StorageTransferVerificationContext(
    StorageTransferItem Item,
    StorageObjectReference Reference,
    string SourceSha256,
    string TargetSha256,
    long SourceLength,
    long TargetLength);

public sealed record StorageTransferVerificationResult(
    bool IsSuccess,
    string Message);

public sealed record StorageTransferOptions(
    int MaxConcurrency = 4,
    int MaxAttempts = 1,
    bool VerifyTargetContent = false,
    StorageTransferProgressCallback? ProgressCallback = null,
    StorageTransferRetryCallback? RetryCallback = null,
    StorageTransferVerificationCallback? VerificationCallback = null);

public sealed record StorageTransferManifest(
    Guid? SourceStorageId,
    Guid? TargetStorageId,
    IReadOnlyList<StorageTransferItem> Items,
    StorageCatalogRecord? SourceStorage = null,
    StorageCatalogRecord? TargetStorage = null,
    StorageTransferOptions? Options = null);

public sealed record StorageTransferItemResult(
    string SourcePath,
    string TargetPath,
    bool IsSuccess,
    string Message,
    StorageObjectReference? Reference = null);

public sealed record StorageTransferResult(
    int TotalCount,
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<StorageTransferItemResult> Items);

public sealed class StorageProviderConfiguration
{
    public string GatewayBaseUrl { get; set; } = string.Empty;

    public int? Port { get; set; }

    public bool PinOnUpload { get; set; } = true;

    public string Username { get; set; } = string.Empty;

    public string BasePath { get; set; } = string.Empty;

    public bool UseSsl { get; set; } = true;

    public bool UsePassiveMode { get; set; } = true;

    public string MetadataJson { get; set; } = "{}";
}

public sealed class StorageReferenceEnvelope
{
    [JsonPropertyName("reference")]
    public StorageObjectReference? Reference { get; set; }
}
