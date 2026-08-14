using System.Security.Cryptography;
using CanDoItAll.Infrastructure;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Infrastructure.Storage;

public sealed class FileSystemStorageDriver : IStorageDriver, IStorageRevisionedContentDriver
{
    private readonly FileSystemStoragePathPolicy pathPolicy;
    private readonly DurableFileWriter durableFileWriter;

    public FileSystemStorageDriver(FileSystemStoragePathPolicy pathPolicy)
        : this(
            pathPolicy,
            new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory()))
    {
    }

    public FileSystemStorageDriver(
        FileSystemStoragePathPolicy pathPolicy,
        DurableFileWriter durableFileWriter)
    {
        this.pathPolicy = pathPolicy ?? throw new ArgumentNullException(nameof(pathPolicy));
        this.durableFileWriter = durableFileWriter ?? throw new ArgumentNullException(nameof(durableFileWriter));
    }

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
            durableFileWriter.EnsureDirectory(rootPath, rootPath, requirePrivateUnixMode: false);
            pathPolicy.ResolveRootPolicy(storage).EnsureSafePath(rootPath);

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

        bool allocateNewPhysicalName = string.IsNullOrWhiteSpace(request.RelativePathHint);
        var relativePath = ResolveRelativePath(storage, request.RelativePathHint, request.FileName);
        var fullPath = pathPolicy.ResolveFullPath(storage, relativePath);
        await durableFileWriter.WriteBytesAsync(
            pathPolicy.ResolveRootPath(storage),
            fullPath,
            request.Content,
            options: allocateNewPhysicalName
                ? DurableFileWriteOptions.CreateNew
                : DurableFileWriteOptions.Default,
            cancellationToken: cancellationToken,
            beforeCommit: allocateNewPhysicalName
                ? token => EnsureAllocatedTargetRemainsAvailableAsync(fullPath, token)
                : null);

        var reference = new StorageObjectReference(
            storage.Id,
            ProviderKind,
            StorageLocatorKind.RelativePath,
            relativePath,
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

    public async Task DeleteAsync(
        StorageCatalogRecord storage,
        StorageObjectReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(reference);
        var fullPath = pathPolicy.ResolveFullPath(storage, reference.Locator);
        await durableFileWriter.DeleteAsync(
            pathPolicy.ResolveRootPath(storage),
            fullPath,
            cancellationToken: cancellationToken).ConfigureAwait(false);
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
        await durableFileWriter.WriteBytesAsync(
            pathPolicy.ResolveRootPath(storage),
            fullPath,
            request.Content,
            cancellationToken: cancellationToken,
            beforeCommit: _ =>
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

                return ValueTask.CompletedTask;
            });

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

    internal string ResolveFullPath(StorageCatalogRecord storage, string relativePath)
        => pathPolicy.ResolveFullPath(storage, relativePath);

    internal string ResolveRootPath(StorageCatalogRecord storage)
        => pathPolicy.ResolveRootPath(storage);

    internal bool IsTrustedForLocalOpen(StorageCatalogRecord storage)
        => pathPolicy.IsTrustedForLocalOpen(storage);

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

    private static ValueTask EnsureAllocatedTargetRemainsAvailableAsync(
        string fullPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(fullPath) || Directory.Exists(fullPath))
        {
            throw new IOException(
                "The allocated storage filename became occupied before commit. Retry the save to allocate a new name.");
        }

        return ValueTask.CompletedTask;
    }

    private string ResolveRelativePath(
        StorageCatalogRecord storage,
        string? relativePathHint,
        string fileName)
    {
        if (!string.IsNullOrWhiteSpace(relativePathHint))
        {
            return FileSystemStorageKeyCodec.Canonicalize(relativePathHint);
        }

        var rootPolicy = pathPolicy.ResolveRootPolicy(storage);
        IEnumerable<string> existingNames = Directory.Exists(rootPolicy.RootPath)
            ? Directory.EnumerateFileSystemEntries(rootPolicy.RootPath)
                .Select(path => Path.GetFileName(path)!)
            : [];
        PortablePhysicalFileName encodedName = PortablePhysicalFileNamePolicy.Allocate(
            fileName,
            existingNames,
            rootPolicy.PathComparer);
        return FileSystemStorageKeyCodec.Append(string.Empty, encodedName.PhysicalName);
    }

    private static string BuildLegacyRoute(string relativePath)
    {
        return relativePath.StartsWith("managed-files/", StringComparison.Ordinal)
            ? "/" + relativePath
            : string.Empty;
    }
}
