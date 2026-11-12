using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Resources;

internal sealed record ResourcePromotionProject(Guid Id, string Name, ProjectWriteAdmission Admission);

internal sealed record ResourceFileSourceDescriptor(
    ResourceFileSourceKey Key,
    ResourceFileSourceClass SourceClass,
    string DisplayName,
    string Detail,
    FileToolsSemanticScope Scope,
    Guid? StorageId,
    StorageProviderKind? ProviderKind,
    bool IsReadOnly,
    StorageHealthStatus? HealthStatus);

internal sealed record ResourceFileSourceCatalogSnapshot(
    IReadOnlyList<ResourceFileSourceDescriptor> Sources,
    IReadOnlyList<ResourcePromotionProject> Projects,
    string Fingerprint);

internal interface IResourceFileSourceCatalog
{
    Task<ResourceFileSourceCatalogSnapshot> LoadAsync(CancellationToken cancellationToken = default);

    Task<ResourceFileSourceDescriptor> ResolveAsync(
        ResourceFileSourceKey key,
        CancellationToken cancellationToken = default);
}

internal sealed class ResourceFileSourceCatalog(
    ProjectWriteSelectionQuery projectQueries,
    IStorageCatalogService storageCatalog,
    IStorageBrowseDriverRegistry browseDrivers) : IResourceFileSourceCatalog
{
    internal const int MaximumSourceCount = 512;

    public async Task<ResourceFileSourceCatalogSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        ResourcePromotionProject[] projects = (await projectQueries.ListAsync(
                MaximumSourceCount + 1, cancellationToken))
            .Select(project => new ResourcePromotionProject(project.Id, project.Name, project.Admission))
            .ToArray();
        if (projects.Length > MaximumSourceCount)
        {
            throw new InvalidOperationException(
                $"The Resources browse catalog contains more than {MaximumSourceCount} project sources.");
        }

        IReadOnlyList<StorageCatalogSnapshot> storages = await storageCatalog.ListAsync(cancellationToken);
        var registeredKinds = browseDrivers.RegisteredKinds.ToHashSet();

        var sources = new List<ResourceFileSourceDescriptor>(projects.Length + storages.Count);
        sources.AddRange(projects.Select(CreateProjectSource));
        sources.AddRange(storages
            .Where(storage => IsBrowsable(storage, registeredKinds))
            .OrderBy(storage => storage.DisplayOrder)
            .ThenBy(storage => storage.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(storage => storage.Id)
            .Select(CreateStorageSource));

        if (sources.Count > MaximumSourceCount)
        {
            throw new InvalidOperationException(
                $"The Resources browse catalog contains {sources.Count} sources; the supported maximum is {MaximumSourceCount}.");
        }

        return new ResourceFileSourceCatalogSnapshot(sources, projects, BuildFingerprint(sources));
    }

    public async Task<ResourceFileSourceDescriptor> ResolveAsync(
        ResourceFileSourceKey key,
        CancellationToken cancellationToken = default)
    {
        ResourceFileSourceCatalogSnapshot snapshot = await LoadAsync(cancellationToken);
        return snapshot.Sources.SingleOrDefault(source => source.Key == key)
            ?? throw new InvalidOperationException("The selected Resources file source is no longer available.");
    }

    private static ResourceFileSourceDescriptor CreateProjectSource(ResourcePromotionProject project)
    {
        var scope = new FileToolsSemanticScope(
            FileToolsSemanticScopeKind.Project,
            new FileToolsSemanticScopeId(project.Id.ToString("N")),
            project.Name);
        return new ResourceFileSourceDescriptor(
            ResourceFileSourceKey.ForProject(project.Id),
            ResourceFileSourceClass.Project,
            project.Name,
            "Project managed files",
            scope,
            null,
            null,
            false,
            null);
    }

    private static ResourceFileSourceDescriptor CreateStorageSource(StorageCatalogSnapshot storage)
    {
        string fingerprint = ResourceStorageSourceScopeKey.BuildFingerprint(storage);
        var scope = new FileToolsSemanticScope(
            FileToolsSemanticScopeKind.ResourceSource,
            ResourceStorageSourceScopeKey.Create(storage.Id, fingerprint),
            storage.Name);
        return new ResourceFileSourceDescriptor(
            ResourceFileSourceKey.ForStorage(storage.Id),
            MapSourceClass(storage.ProviderKind),
            storage.Name,
            BuildDetail(storage),
            scope,
            storage.Id,
            storage.ProviderKind,
            storage.IsReadOnly,
            storage.HealthStatus);
    }

    private static bool IsBrowsable(
        StorageCatalogSnapshot storage,
        IReadOnlySet<StorageProviderKind> registeredKinds)
        => storage.IsEnabled &&
           storage.CapabilityMask.HasFlag(StorageCapability.Read) &&
           registeredKinds.Contains(storage.ProviderKind);

    private static ResourceFileSourceClass MapSourceClass(StorageProviderKind providerKind)
        => providerKind switch
        {
            StorageProviderKind.FileSystem => ResourceFileSourceClass.FileSystem,
            StorageProviderKind.Ipfs => ResourceFileSourceClass.Ipfs,
            StorageProviderKind.Ftp => ResourceFileSourceClass.Ftp,
            _ => throw new ArgumentOutOfRangeException(nameof(providerKind))
        };

    private static string BuildDetail(StorageCatalogSnapshot storage)
    {
        string access = storage.IsReadOnly ? "Read only" : "Read enabled";
        return $"{storage.ProviderKind} · {access} · {storage.HealthStatus}";
    }

    private static string BuildFingerprint(IEnumerable<ResourceFileSourceDescriptor> sources)
    {
        string canonical = string.Join(
            '\n',
            sources.Select(source => $"{source.Key.Value}|{source.Scope.Id.Value}"));
        return StableContentHash.ComputeSha256Hex(canonical);
    }
}

internal static class ResourceStorageSourceScopeKey
{
    private const string Prefix = "resource:v1:";
    private const int FingerprintLength = 64;

    public static FileToolsSemanticScopeId Create(Guid storageId, string fingerprint)
    {
        if (storageId == Guid.Empty)
        {
            throw new ArgumentException("A storage identifier is required.", nameof(storageId));
        }

        if (!IsFingerprint(fingerprint))
        {
            throw new ArgumentException("A valid storage fingerprint is required.", nameof(fingerprint));
        }

        return new FileToolsSemanticScopeId($"{Prefix}{storageId:N}:{fingerprint}");
    }

    public static bool TryParse(FileToolsSemanticScopeId scopeId, out Guid storageId, out string fingerprint)
    {
        storageId = Guid.Empty;
        fingerprint = string.Empty;
        string value = scopeId.Value ?? string.Empty;
        int identifierStart = Prefix.Length;
        int fingerprintStart = identifierStart + 33;
        if (!value.StartsWith(Prefix, StringComparison.Ordinal) ||
            value.Length != fingerprintStart + FingerprintLength ||
            value[identifierStart + 32] != ':' ||
            !Guid.TryParseExact(value.AsSpan(identifierStart, 32), "N", out storageId) ||
            storageId == Guid.Empty)
        {
            return false;
        }

        fingerprint = value[fingerprintStart..];
        return IsFingerprint(fingerprint);
    }

    public static string BuildFingerprint(StorageCatalogSnapshot storage) {
        ArgumentNullException.ThrowIfNull(storage);
        return storage.SourceFingerprint;
    }

    private static bool IsFingerprint(string? value)
        => value is { Length: FingerprintLength } && value.All(Uri.IsHexDigit);
}
