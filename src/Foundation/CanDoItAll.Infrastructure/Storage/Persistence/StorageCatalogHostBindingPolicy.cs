namespace CanDoItAll.Infrastructure.Storage;

public static class StorageCatalogHostBindingPolicy
{
    public static void BindCurrent(
        StorageCatalogRecord storage,
        string rootPath,
        DateTimeOffset validatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(storage);
        HostBoundPathRecord binding = HostBoundPathPolicy.BindCurrent(rootPath, validatedAtUtc);
        Apply(storage, binding);
    }

    public static void ImportLegacy(
        StorageCatalogRecord storage,
        string fallbackRoot)
    {
        ArgumentNullException.ThrowIfNull(storage);
        string candidate = string.IsNullOrWhiteSpace(storage.EndpointOrRoot)
            ? fallbackRoot
            : storage.EndpointOrRoot;
        HostBoundPathRecord binding = HostBoundPathPolicy.ImportLegacy(
            candidate,
            HostPathContext.CaptureCurrent());
        Apply(storage, binding);
        if (binding.State != HostBoundPathState.Active)
        {
            storage.IsEnabled = false;
            storage.HealthStatus = StorageHealthStatus.Unavailable;
            storage.LastHealthMessage = "Storage root requires explicit host rebind.";
        }
    }

    public static bool TryResolve(
        StorageCatalogRecord storage,
        string fallbackRoot,
        out string rootPath,
        out string diagnostic)
    {
        ArgumentNullException.ThrowIfNull(storage);
        if (storage.ProviderKind != StorageProviderKind.FileSystem)
        {
            rootPath = string.Empty;
            diagnostic = "The storage provider does not own a physical filesystem root.";
            return false;
        }

        if (storage.RootBindingFormatVersion == 0)
        {
            string candidate = string.IsNullOrWhiteSpace(storage.EndpointOrRoot)
                ? fallbackRoot
                : storage.EndpointOrRoot;
            HostBoundPathRecord imported = HostBoundPathPolicy.ImportLegacy(
                candidate,
                HostPathContext.CaptureCurrent());
            return HostBoundPathPolicy.TryResolve(
                imported,
                HostPathContext.CaptureCurrent(),
                out rootPath,
                out diagnostic);
        }

        return HostBoundPathPolicy.TryResolve(
            ToRecord(storage),
            HostPathContext.CaptureCurrent(),
            out rootPath,
            out diagnostic);
    }

    public static string ResolveRequired(StorageCatalogRecord storage, string fallbackRoot)
    {
        if (TryResolve(storage, fallbackRoot, out string rootPath, out string diagnostic))
        {
            return rootPath;
        }

        throw new InvalidOperationException($"The filesystem storage root is unavailable. {diagnostic}");
    }

    private static HostBoundPathRecord ToRecord(StorageCatalogRecord storage)
    {
        return new HostBoundPathRecord
        {
            FormatVersion = storage.RootBindingFormatVersion,
            PlatformFamily = storage.RootPlatformFamily,
            PathSyntax = storage.RootPathSyntax,
            HostBindingId = storage.RootHostBindingId,
            Path = storage.EndpointOrRoot,
            State = storage.RootPathState,
            LastValidatedAtUtc = storage.RootLastValidatedAtUtc
        };
    }

    private static void Apply(StorageCatalogRecord storage, HostBoundPathRecord binding)
    {
        storage.EndpointOrRoot = binding.Path;
        storage.RootBindingFormatVersion = binding.FormatVersion;
        storage.RootPlatformFamily = binding.PlatformFamily;
        storage.RootPathSyntax = binding.PathSyntax;
        storage.RootHostBindingId = binding.HostBindingId;
        storage.RootPathState = binding.State;
        storage.RootLastValidatedAtUtc = binding.LastValidatedAtUtc;
    }
}
