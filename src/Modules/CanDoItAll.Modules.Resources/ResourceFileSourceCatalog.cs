using System.Security.Cryptography;
using System.Text;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Resources;

internal enum ResourceFileSourceClass
{
    Project,
    FileSystem,
    Ipfs,
    Ftp
}

internal readonly record struct ResourceFileSourceKey
{
    private const string ProjectPrefix = "project:";
    private const string StoragePrefix = "storage:";

    private ResourceFileSourceKey(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static ResourceFileSourceKey ForProject(Guid projectId)
        => projectId == Guid.Empty
            ? throw new ArgumentException("A project source identifier is required.", nameof(projectId))
            : new ResourceFileSourceKey($"{ProjectPrefix}{projectId:N}");

    public static ResourceFileSourceKey ForStorage(Guid storageId)
        => storageId == Guid.Empty
            ? throw new ArgumentException("A storage source identifier is required.", nameof(storageId))
            : new ResourceFileSourceKey($"{StoragePrefix}{storageId:N}");

    public static bool TryParse(string? value, out ResourceFileSourceKey key)
    {
        string normalized = value?.Trim() ?? string.Empty;
        bool valid = TryParseIdentifier(normalized, ProjectPrefix, out _) ||
                     TryParseIdentifier(normalized, StoragePrefix, out _);
        key = valid ? new ResourceFileSourceKey(normalized) : default;
        return valid;
    }

    public bool TryGetProjectId(out Guid projectId)
        => TryParseIdentifier(Value, ProjectPrefix, out projectId);

    public bool TryGetStorageId(out Guid storageId)
        => TryParseIdentifier(Value, StoragePrefix, out storageId);

    public override string ToString() => Value ?? string.Empty;

    private static bool TryParseIdentifier(string? value, string prefix, out Guid id)
    {
        id = Guid.Empty;
        return value is not null &&
               value.StartsWith(prefix, StringComparison.Ordinal) &&
               value.Length == prefix.Length + 32 &&
               Guid.TryParseExact(value[prefix.Length..], "N", out id) &&
               id != Guid.Empty;
    }
}

internal sealed record ResourcePromotionProject(Guid Id, string Name);

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
    IDbContextFactory<AppDbContext> dbContextFactory,
    IStorageCatalogService storageCatalog,
    IStorageBrowseDriverRegistry browseDrivers) : IResourceFileSourceCatalog
{
    internal const int MaximumSourceCount = 512;

    public async Task<ResourceFileSourceCatalogSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        ResourcePromotionProject[] projects = await dbContext.Set<Project>()
            .AsNoTracking()
            .OrderBy(project => project.Name)
            .ThenBy(project => project.Id)
            .Take(MaximumSourceCount + 1)
            .Select(project => new ResourcePromotionProject(project.Id, project.Name))
            .ToArrayAsync(cancellationToken);
        if (projects.Length > MaximumSourceCount)
        {
            throw new InvalidOperationException(
                $"The Resources browse catalog contains more than {MaximumSourceCount} project sources.");
        }

        IReadOnlyList<StorageCatalogRecord> storages = await storageCatalog.ListAsync(cancellationToken);
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

    private static ResourceFileSourceDescriptor CreateStorageSource(StorageCatalogRecord storage)
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
        StorageCatalogRecord storage,
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

    private static string BuildDetail(StorageCatalogRecord storage)
    {
        string access = storage.IsReadOnly ? "Read only" : "Read enabled";
        return $"{storage.ProviderKind} · {access} · {storage.HealthStatus}";
    }

    private static string BuildFingerprint(IEnumerable<ResourceFileSourceDescriptor> sources)
    {
        string canonical = string.Join(
            '\n',
            sources.Select(source => $"{source.Key.Value}|{source.Scope.Id.Value}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
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

    public static string BuildFingerprint(StorageCatalogRecord storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        string canonical = string.Join(
            '|',
            storage.Id.ToString("N"),
            storage.ProviderKind,
            storage.IsEnabled,
            storage.IsReadOnly,
            (int)storage.CapabilityMask,
            storage.ConnectionMode,
            storage.EndpointOrRoot,
            storage.ConfigJson,
            storage.CredentialSecretId?.ToString("N") ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static bool IsFingerprint(string? value)
        => value is { Length: FingerprintLength } && value.All(Uri.IsHexDigit);
}
