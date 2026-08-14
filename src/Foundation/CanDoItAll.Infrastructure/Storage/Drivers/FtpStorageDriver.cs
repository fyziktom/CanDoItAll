using Microsoft.Extensions.Logging;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Infrastructure.Storage;

public sealed class FtpStorageDriver(
    IStorageSecretResolver secretResolver,
    IFtpStorageTransport transport,
    ILogger<FtpStorageDriver> logger) : IStorageDriver
{
    public StorageProviderKind ProviderKind => StorageProviderKind.Ftp;

    public StorageCapability SupportedCapabilities =>
        StorageCapability.Read |
        StorageCapability.Write |
        StorageCapability.Delete |
        StorageCapability.Download |
        StorageCapability.BatchFolderUpload |
        StorageCapability.BatchTransfer |
        StorageCapability.ConnectionTest;

    public async Task<StorageConnectionTestResult> TestConnectionAsync(
        StorageCatalogRecord storage,
        string? secretValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storage);
        try
        {
            await transport.TestConnectionAsync(storage, secretValue, cancellationToken);
            return new StorageConnectionTestResult(
                true,
                "FTP server responded successfully.",
                StorageHealthStatus.Healthy,
                SupportedCapabilities,
                DateTimeOffset.UtcNow);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                "FTP connection test failed for storage {StorageId} with {FailureType}.",
                storage.Id,
                exception.GetType().Name);
            return new StorageConnectionTestResult(
                false,
                "FTP storage is unavailable.",
                StorageHealthStatus.Unavailable,
                SupportedCapabilities & ~StorageCapability.ConnectionTest,
                DateTimeOffset.UtcNow);
        }
    }

    public async Task<StorageWriteResult> SaveAsync(
        StorageCatalogRecord storage,
        StorageWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            string remotePath = NormalizeRemotePath(request.RelativePathHint, request.FileName);
            string? password = await secretResolver.ResolveCredentialAsync(
                storage.CredentialSecretId,
                cancellationToken);
            await transport.UploadAsync(storage, password, remotePath, request.Content, cancellationToken);
            var reference = new StorageObjectReference(
                storage.Id,
                ProviderKind,
                StorageLocatorKind.RemotePath,
                remotePath,
                request.FileName,
                string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType,
                request.Content.LongLength);

            return new StorageWriteResult(
                reference,
                new StorageAccessDescriptor(
                    string.Empty,
                    StorageJson.BuildDownloadUrl(reference),
                    null,
                    false,
                    true,
                    false,
                    string.IsNullOrWhiteSpace(request.FileName) ? remotePath : request.FileName,
                    reference.ContentType,
                    reference.ContentLength,
                    "FTP storage supports download but not inline preview or local open."));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw CreateFailure(storage, "write", exception);
        }
    }

    public async Task<Stream> OpenReadAsync(
        StorageCatalogRecord storage,
        StorageObjectReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(reference);
        try
        {
            string? password = await secretResolver.ResolveCredentialAsync(
                storage.CredentialSecretId,
                cancellationToken);
            return await transport.OpenReadAsync(
                storage,
                password,
                reference.Locator,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw CreateFailure(storage, "read", exception);
        }
    }

    public async Task DeleteAsync(
        StorageCatalogRecord storage,
        StorageObjectReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(reference);
        try
        {
            string? password = await secretResolver.ResolveCredentialAsync(
                storage.CredentialSecretId,
                cancellationToken);
            await transport.DeleteAsync(storage, password, reference.Locator, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw CreateFailure(storage, "delete", exception);
        }
    }

    private static string NormalizeRemotePath(string? relativePathHint, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(relativePathHint))
        {
            return StorageJson.NormalizeLogicalLocator(relativePathHint);
        }

        return PortablePhysicalFileNamePolicy.Encode(fileName).PhysicalName;
    }

    private InvalidOperationException CreateFailure(
        StorageCatalogRecord storage,
        string operation,
        Exception exception)
    {
        logger.LogWarning(
            "FTP {Operation} failed for storage {StorageId} with {FailureType}.",
            operation,
            storage.Id,
            exception.GetType().Name);
        return new InvalidOperationException($"The FTP {operation} operation failed.");
    }
}
