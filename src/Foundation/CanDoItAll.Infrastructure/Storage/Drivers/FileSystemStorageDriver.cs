using System.Security.Cryptography;

namespace CanDoItAll.Infrastructure.Storage;

public sealed class FileSystemStorageDriver(FileSystemStoragePathPolicy pathPolicy)
    : IStorageDriver, IStorageRevisionedContentDriver
{
    private const int WriteLockCount = 64;
    private static readonly SemaphoreSlim[] WriteLocks = Enumerable
        .Range(0, WriteLockCount)
        .Select(_ => new SemaphoreSlim(1, 1))
        .ToArray();

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
                "Accessible local root.",
                StorageHealthStatus.Healthy,
                SupportedCapabilities,
                DateTimeOffset.UtcNow));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new StorageConnectionTestResult(
                false,
                $"Local filesystem storage is unavailable ({ex.GetType().Name}).",
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
        var fullPath = pathPolicy.ResolveFullPath(storage, relativePath);
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

        Stream stream = new FileStream(
            pathPolicy.ResolveFullPath(storage, reference.Locator),
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 80 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(
        StorageCatalogRecord storage,
        StorageObjectReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(reference);

        var fullPath = pathPolicy.ResolveFullPath(storage, reference.Locator);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public Task<StorageContentRevision?> GetRevisionAsync(
        StorageCatalogRecord storage,
        StorageObjectReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(reference);
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath = pathPolicy.ResolveFullPath(storage, reference.Locator);
        return Task.FromResult(CreateRevision(fullPath));
    }

    public async Task<StorageRevisionedWriteResult> ReplaceAsync(
        StorageCatalogRecord storage,
        StorageRevisionedWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(request);
        string fullPath = pathPolicy.ResolveFullPath(storage, request.Reference.Locator);
        SemaphoreSlim writeLock = ResolveWriteLock(fullPath);
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            StorageContentRevision? actualRevision = CreateRevision(fullPath);
            if (request.ExpectedRevision is { } expected && expected != actualRevision)
            {
                throw new StorageContentConflictException(expected, actualRevision);
            }

            if (request.ExpectedRevision is null && !request.AllowOverwrite)
            {
                throw new StorageContentConflictException(null, actualRevision);
            }

            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temporaryPath = fullPath + "." + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)) + ".tmp";
            try
            {
                await File.WriteAllBytesAsync(temporaryPath, request.Content, cancellationToken);
                if (request.ExpectedRevision is { } expectedBeforeCommit)
                {
                    StorageContentRevision? revisionBeforeCommit = CreateRevision(fullPath);
                    if (expectedBeforeCommit != revisionBeforeCommit)
                    {
                        throw new StorageContentConflictException(expectedBeforeCommit, revisionBeforeCommit);
                    }
                }

                File.Move(temporaryPath, fullPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }

            StorageContentRevision persistedRevision = CreateRevision(fullPath)
                ?? throw new IOException("The storage object was not persisted.");
            StorageObjectReference persistedReference = request.Reference with
            {
                ContentLength = request.Content.LongLength
            };
            string token = StorageJson.EncodeReferenceToken(persistedReference);
            var result = new StorageWriteResult(
                persistedReference,
                new StorageAccessDescriptor(
                    $"/storage/objects/preview?ref={Uri.EscapeDataString(token)}",
                    $"/storage/objects/download?ref={Uri.EscapeDataString(token)}",
                    null,
                    true,
                    true,
                    IsTrustedForLocalOpen(storage),
                    persistedReference.DisplayName,
                    persistedReference.ContentType,
                    persistedReference.ContentLength,
                    string.Empty));
            return new StorageRevisionedWriteResult(result, persistedRevision);
        }
        finally
        {
            writeLock.Release();
        }
    }

    internal string ResolveFullPath(StorageCatalogRecord storage, string relativePath)
        => pathPolicy.ResolveFullPath(storage, relativePath);

    internal string ResolveRootPath(StorageCatalogRecord storage)
        => pathPolicy.ResolveRootPath(storage);

    internal bool IsTrustedForLocalOpen(StorageCatalogRecord storage)
        => pathPolicy.IsTrustedForLocalOpen(storage);

    private static SemaphoreSlim ResolveWriteLock(string fullPath)
    {
        int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(fullPath) & int.MaxValue;
        return WriteLocks[hash % WriteLockCount];
    }

    private static StorageContentRevision? CreateRevision(string fullPath)
    {
        var file = new FileInfo(fullPath);
        if (!file.Exists)
        {
            return null;
        }

        string state = FormattableString.Invariant($"{file.Length}:{file.LastWriteTimeUtc.Ticks}");
        byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(state));
        return new StorageContentRevision(Convert.ToHexStringLower(hash));
    }

    private static string ResolveRelativePath(string? relativePathHint, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(relativePathHint))
        {
            return FileSystemStoragePathPolicy.NormalizeRelativeKey(relativePathHint);
        }

        var sanitizedFileName = string.Concat(fileName.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
        return string.IsNullOrWhiteSpace(sanitizedFileName)
            ? "artifact.bin"
            : sanitizedFileName;
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
