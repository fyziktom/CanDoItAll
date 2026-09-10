namespace CanDoItAll.Infrastructure.Storage;

public sealed record StorageFtpAddressingFact(int? Port, string BasePath);

public sealed record StorageCatalogPlanningFact(
    Guid Id,
    string Name,
    StorageProviderKind ProviderKind,
    bool IsEnabled,
    bool IsSystemDefault,
    bool IsReadOnly,
    int DisplayOrder,
    StorageConnectionMode ConnectionMode,
    string EndpointOrRoot,
    int RootBindingFormatVersion,
    HostPlatformFamily RootPlatformFamily,
    CanDoItAll.SharedKernel.PhysicalPathSyntax RootPathSyntax,
    string RootHostBindingId,
    HostBoundPathState RootPathState,
    DateTimeOffset? RootLastValidatedAtUtc,
    StorageCapability CapabilityMask,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    StorageFtpAddressingFact? FtpAddressing);
