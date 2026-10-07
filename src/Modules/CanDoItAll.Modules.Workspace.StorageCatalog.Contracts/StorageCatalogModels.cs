using System.Collections.Immutable;

namespace CanDoItAll.Modules.Workspace.StorageCatalog.Contracts;

public enum CatalogProvider { FileSystem, Ipfs, Ftp }
public enum CatalogConnection { Local, Remote }
public enum CatalogHealth { Unknown, Healthy, Degraded, Unavailable }
public enum CatalogPurpose { Unknown, ProjectAsset, PromptAttachment, PromptExport, Evidence, RecordingMedia, DeploymentMirror, SnapshotPackage, ReleasePackage, WorkspaceExport }

[Flags]
public enum CatalogCapability {
    None = 0, Read = 1, Write = 2, Delete = 4, InlinePreview = 8, Download = 16,
    OpenLocally = 32, DirectUrl = 64, MutableUpdate = 128, BatchFolderUpload = 256,
    BatchTransfer = 512, ConnectionTest = 1024
}

public sealed record CatalogContext(Guid ProfileId, long Generation);
public sealed record CatalogChoice<T>(T Value, string Label);
public sealed record CatalogProviderChoice(CatalogProvider Value, string Label, CatalogEdit Template,
    CatalogCapability WritableCapabilities, CatalogCapability ReadOnlyCapabilities);
public sealed record CatalogChoices(ImmutableArray<CatalogProviderChoice> Providers,
    ImmutableArray<CatalogChoice<CatalogConnection>> Connections, ImmutableArray<CatalogChoice<CatalogPurpose>> Purposes,
    ImmutableArray<CatalogChoice<CatalogCapability>> Capabilities);
public sealed record CatalogSecret(Guid Id, string Name, bool IsEnabled);
public sealed record CatalogRoute(CatalogPurpose Purpose, Guid? StorageId, string Name, bool IsEnabled);
public sealed record CatalogHealthFact(CatalogHealth Status, CatalogCapability Capabilities, DateTimeOffset? TestedAtUtc, string Message);
public sealed record CatalogRow(Guid Id, string Name, CatalogProvider ProviderKind, CatalogConnection ConnectionMode,
    string EndpointOrRoot, int DisplayOrder, bool IsEnabled, bool IsSystemDefault, bool IsReadOnly, CatalogHealthFact Health);

public sealed record CatalogEdit {
    public Guid? Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public CatalogProvider ProviderKind { get; init; }
    public CatalogConnection ConnectionMode { get; init; }
    public string EndpointOrRoot { get; init; } = string.Empty;
    public Guid? CredentialSecretId { get; init; }
    public bool IsEnabled { get; init; } = true;
    public bool IsSystemDefault { get; init; }
    public bool IsReadOnly { get; init; }
    public int DisplayOrder { get; init; }
    public string GatewayBaseUrl { get; init; } = string.Empty;
    public int? Port { get; init; }
    public bool PinOnUpload { get; init; }
    public string Username { get; init; } = string.Empty;
    public string BasePath { get; init; } = string.Empty;
    public bool UseSsl { get; init; }
    public bool UsePassiveMode { get; init; }
    public ImmutableArray<CatalogPurpose> DefaultPurposes { get; init; } = [];
    public CatalogHealthFact Health { get; init; } = new(CatalogHealth.Unknown, CatalogCapability.None, null, string.Empty);
}
