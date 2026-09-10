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
    StorageFtpAddressingFact? FtpAddressing) {
    public static StorageCatalogPlanningFact FromCatalogRecord(StorageCatalogRecord storage, bool includeFtpAddressing) {
        ArgumentNullException.ThrowIfNull(storage);
        StorageFtpAddressingFact? ftpAddressing = null;
        if (storage.ProviderKind == StorageProviderKind.Ftp && includeFtpAddressing) {
            var configuration = StorageJson.ParseProviderConfiguration(storage.ConfigJson);
            ftpAddressing = new(configuration.Port, configuration.BasePath);
        }
        return new(
            storage.Id, storage.Name, storage.ProviderKind, storage.IsEnabled, storage.IsSystemDefault,
            storage.IsReadOnly, storage.DisplayOrder, storage.ConnectionMode, storage.EndpointOrRoot,
            storage.RootBindingFormatVersion, storage.RootPlatformFamily, storage.RootPathSyntax,
            storage.RootHostBindingId, storage.RootPathState, storage.RootLastValidatedAtUtc,
            storage.CapabilityMask, storage.CreatedAtUtc, storage.UpdatedAtUtc, ftpAddressing);
    }
}
