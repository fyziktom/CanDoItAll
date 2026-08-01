using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.FileTools.Integration;

internal sealed record StorageBrowseCacheContext(
    FileToolsSemanticScope Scope,
    FileToolsStorageBinding Binding,
    StorageCatalogRecord Storage,
    string SourceSetFingerprint,
    string StorageFingerprint);

internal sealed record StorageBrowseSourceRevisionPart(
    string SourceId,
    string StorageFingerprint,
    FileCatalogRevision CatalogRevision);

internal static class StorageBrowseCacheKeyBuilder
{
    private const string Schema = "filetools-list-v1";
    public const int MaximumCacheKeyLength = 128;
    public const int MaximumSourceCount = 256;

    public static string Build(
        StorageBrowseCacheContext context,
        StorageBrowseRequest request,
        DatabaseRuntimeSnapshot runtime,
        FileCatalogRevision revision)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        if ((runtime.ActiveFingerprint?.Length ?? 0) > 4096 ||
            context.SourceSetFingerprint.Length > 128 ||
            context.StorageFingerprint.Length > 128)
        {
            throw InvalidConfiguration("A storage browse cache fingerprint exceeds the bounded key contract.");
        }

        string canonical = string.Join('\n',
            Schema,
            runtime.ActiveProfileId?.ToString("N") ?? "none",
            runtime.Generation.ToString(CultureInfo.InvariantCulture),
            context.Storage.Id.ToString("N"),
            ((int)context.Storage.ProviderKind).ToString(CultureInfo.InvariantCulture),
            ((int)context.Scope.Kind).ToString(CultureInfo.InvariantCulture),
            context.Scope.Id.Value,
            context.Binding.Root.Value,
            context.SourceSetFingerprint,
            context.StorageFingerprint,
            runtime.ActiveFingerprint ?? "none",
            revision.Storage.ToString(CultureInfo.InvariantCulture),
            revision.Scope.ToString(CultureInfo.InvariantCulture),
            request.Container.Key,
            request.Cursor?.Token ?? string.Empty,
            request.PageSize.ToString(CultureInfo.InvariantCulture),
            ((int)request.Sort.Field).ToString(CultureInfo.InvariantCulture),
            ((int)request.Sort.Direction).ToString(CultureInfo.InvariantCulture),
            ((int)request.Metadata).ToString(CultureInfo.InvariantCulture),
            request.Budget.MaximumReturnedItems.ToString(CultureInfo.InvariantCulture),
            request.Budget.MaximumInspectedItems.ToString(CultureInfo.InvariantCulture),
            request.Budget.MaximumMetadataProbes.ToString(CultureInfo.InvariantCulture),
            request.Budget.MaximumConcurrentMetadataProbes.ToString(CultureInfo.InvariantCulture),
            request.Budget.MaximumDuration.Ticks.ToString(CultureInfo.InvariantCulture));
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        string key = $"{Schema}:{Convert.ToHexStringLower(hash)}";
        if (key.Length > MaximumCacheKeyLength)
        {
            throw InvalidConfiguration("The storage browse cache key exceeds its bounded contract.");
        }

        return key;
    }

    public static string BuildSourceSetFingerprint(IReadOnlyList<FileToolsStorageBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        if (bindings.Count > MaximumSourceCount)
        {
            throw InvalidConfiguration("The semantic file scope contains too many storage sources.");
        }

        string canonical = string.Join('\n', bindings
            .OrderBy(binding => binding.StorageId)
            .ThenBy(binding => binding.Root.Value, StringComparer.Ordinal)
            .Select(binding => string.Join(':',
                binding.StorageId.ToString("N"),
                binding.Root.Value,
                Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(binding.DisplayName))),
                ((int)binding.HostCacheMode).ToString(CultureInfo.InvariantCulture),
                binding.WorkLimits.MaximumReturnedItems.ToString(CultureInfo.InvariantCulture),
                binding.WorkLimits.MaximumInspectedItems.ToString(CultureInfo.InvariantCulture),
                binding.WorkLimits.MaximumMetadataProbes.ToString(CultureInfo.InvariantCulture),
                binding.WorkLimits.MaximumConcurrentMetadataProbes.ToString(CultureInfo.InvariantCulture),
                binding.WorkLimits.MaximumDuration.Ticks.ToString(CultureInfo.InvariantCulture))));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static string BuildStorageFingerprint(
        StorageCatalogRecord storage,
        FileToolsStorageBinding binding)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(binding);
        string endpoint = storage.EndpointOrRoot ?? string.Empty;
        string configuration = storage.ConfigJson ?? "{}";
        if (endpoint.Length > StorageBrowseContainer.MaximumKeyLength ||
            configuration.Length > StorageJson.MaximumProviderConfigurationJsonLength)
        {
            throw InvalidConfiguration("The storage source configuration exceeds the bounded fingerprint contract.");
        }

        string canonical = string.Join('\n',
            storage.Id.ToString("N"),
            ((int)storage.ProviderKind).ToString(CultureInfo.InvariantCulture),
            endpoint,
            configuration,
            ((int)storage.CapabilityMask).ToString(CultureInfo.InvariantCulture),
            storage.IsEnabled ? "1" : "0",
            storage.IsReadOnly ? "1" : "0",
            storage.CredentialSecretId?.ToString("N") ?? "none",
            binding.Root.Value);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static FileToolsBrowseSessionRevision BuildSessionRevision(
        FileToolsSemanticScope scope,
        string sourceSetFingerprint,
        IReadOnlyList<StorageBrowseSourceRevisionPart> sources,
        DatabaseRuntimeSnapshot runtime)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSetFingerprint);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(runtime);
        if (sources.Count > MaximumSourceCount ||
            sourceSetFingerprint.Length > 128 ||
            (runtime.ActiveFingerprint?.Length ?? 0) > 4096)
        {
            throw InvalidConfiguration("A browse-session revision input exceeds its bounded contract.");
        }

        IEnumerable<string> sourceParts = sources
            .OrderBy(source => source.SourceId, StringComparer.Ordinal)
            .Select(source => string.Join(':',
                source.SourceId,
                source.StorageFingerprint,
                source.CatalogRevision.Storage.ToString(CultureInfo.InvariantCulture),
                source.CatalogRevision.Scope.ToString(CultureInfo.InvariantCulture)));
        string canonical = string.Join('\n',
            [
                "filetools-session-v1",
                ((int)scope.Kind).ToString(CultureInfo.InvariantCulture),
                scope.Id.Value,
                runtime.ActiveProfileId?.ToString("N") ?? "none",
                runtime.Generation.ToString(CultureInfo.InvariantCulture),
                runtime.ActiveFingerprint ?? "none",
                sourceSetFingerprint,
                .. sourceParts
            ]);
        return new FileToolsBrowseSessionRevision(
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))));
    }

    private static StorageBrowseException InvalidConfiguration(string message)
        => new(new StorageBrowseError(StorageBrowseErrorCode.InvalidConfiguration, message));
}
