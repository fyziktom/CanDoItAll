namespace CanDoItAll.Infrastructure.Storage;

public sealed class FileSystemStorageDriver(IWorkspacePathResolver workspacePathResolver) : IStorageDriver
{
    public StorageProviderKind ProviderKind => StorageProviderKind.FileSystem;

    public StorageCapability SupportedCapabilities =>
        StorageCapability.Read |
        StorageCapability.Write |
        StorageCapability.Delete |
        StorageCapability.InlinePreview |
        StorageCapability.Download |
        StorageCapability.OpenLocally |
        StorageCapability.MutableUpdate |
        StorageCapability.BatchFolderUpload |
        StorageCapability.BatchTransfer |
        StorageCapability.ConnectionTest;

    public Task<StorageConnectionTestResult> TestConnectionAsync(
        StorageCatalogRecord storage,
        string? secretValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storage);

        try
        {
            var rootPath = ResolveRootPath(storage);
            Directory.CreateDirectory(rootPath);

            return Task.FromResult(new StorageConnectionTestResult(
                true,
                $"Accessible local root '{rootPath}'.",
                StorageHealthStatus.Healthy,
                SupportedCapabilities,
                DateTimeOffset.UtcNow));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new StorageConnectionTestResult(
                false,
                $"Local filesystem storage is unavailable: {ex.Message}",
                StorageHealthStatus.Unavailable,
                SupportedCapabilities & ~StorageCapability.ConnectionTest,
                DateTimeOffset.UtcNow));
        }
    }

    public async Task<StorageWriteResult> SaveAsync(
        StorageCatalogRecord storage,
        StorageWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(request);

        var relativePath = ResolveRelativePath(request.RelativePathHint, request.FileName);
        var fullPath = ResolveFullPath(storage, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(fullPath, request.Content, cancellationToken);

        var reference = new StorageObjectReference(
            storage.Id,
            ProviderKind,
            StorageLocatorKind.RelativePath,
            NormalizeRoutePath(relativePath),
            request.FileName,
            string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType,
            request.Content.LongLength,
            BuildLegacyRoute(relativePath));
        var token = StorageJson.EncodeReferenceToken(reference);

        return new StorageWriteResult(
            reference,
            new StorageAccessDescriptor(
                $"/storage/objects/preview?ref={Uri.EscapeDataString(token)}",
                $"/storage/objects/download?ref={Uri.EscapeDataString(token)}",
                null,
                true,
                true,
                IsTrustedForLocalOpen(storage),
                request.FileName,
                reference.ContentType,
                reference.ContentLength,
                string.Empty));
    }

    public Task<Stream> OpenReadAsync(
        StorageCatalogRecord storage,
        StorageObjectReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(reference);

        Stream stream = File.OpenRead(ResolveFullPath(storage, reference.Locator));
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(
        StorageCatalogRecord storage,
        StorageObjectReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(reference);

        var fullPath = ResolveFullPath(storage, reference.Locator);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    internal string ResolveFullPath(StorageCatalogRecord storage, string relativePath)
    {
        var rootPath = ResolveRootPath(storage);
        var normalizedPath = NormalizeRelativePath(relativePath);
        var fullPath = Path.GetFullPath(Path.Combine(rootPath, normalizedPath));
        var normalizedRoot = rootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The resolved path is outside the configured storage root.");
        }

        return fullPath;
    }

    internal string ResolveRootPath(StorageCatalogRecord storage)
    {
        var configuredRoot = string.IsNullOrWhiteSpace(storage.EndpointOrRoot)
            ? workspacePathResolver.ResolveWorkspaceRoot()
            : storage.EndpointOrRoot;
        return Path.GetFullPath(configuredRoot);
    }

    internal bool IsTrustedForLocalOpen(StorageCatalogRecord storage)
    {
        var workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
        var storageRoot = ResolveRootPath(storage);
        return storageRoot.StartsWith(workspaceRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveRelativePath(string? relativePathHint, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(relativePathHint))
        {
            return NormalizeRelativePath(relativePathHint);
        }

        var sanitizedFileName = string.Concat(fileName.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
        return string.IsNullOrWhiteSpace(sanitizedFileName)
            ? "artifact.bin"
            : sanitizedFileName;
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath
            .Trim()
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
    }

    private static string NormalizeRoutePath(string relativePath)
        => relativePath.Trim().Replace('\\', '/').TrimStart('/');

    private static string BuildLegacyRoute(string relativePath)
    {
        var normalized = NormalizeRoutePath(relativePath);
        return normalized.StartsWith("managed-files/", StringComparison.OrdinalIgnoreCase)
            ? "/" + normalized
            : string.Empty;
    }
}
