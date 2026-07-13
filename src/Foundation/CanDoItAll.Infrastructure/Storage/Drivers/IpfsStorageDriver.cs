using Microsoft.Extensions.Logging;

namespace CanDoItAll.Infrastructure.Storage;

public sealed class IpfsStorageDriver(
    ILogger<IpfsStorageDriver> logger,
    IStorageSecretResolver secretResolver,
    IIpfsStorageTransport transport) : IStorageDriver
{
    public StorageProviderKind ProviderKind => StorageProviderKind.Ipfs;

    public StorageCapability SupportedCapabilities =>
        StorageCapability.Read |
        StorageCapability.Write |
        StorageCapability.InlinePreview |
        StorageCapability.Download |
        StorageCapability.DirectUrl |
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
                "IPFS API responded successfully.",
                StorageHealthStatus.Healthy,
                SupportedCapabilities,
                DateTimeOffset.UtcNow);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                "IPFS connection test failed for storage {StorageId} with {FailureType}.",
                storage.Id,
                exception.GetType().Name);
            return new StorageConnectionTestResult(
                false,
                "IPFS storage is unavailable.",
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
            string? secretValue = await secretResolver.ResolveCredentialAsync(
                storage.CredentialSecretId,
                cancellationToken);
            string fileName = string.IsNullOrWhiteSpace(request.FileName) ? "artifact.bin" : request.FileName;
            IpfsAddResult add = await transport.AddAsync(
                storage,
                secretValue,
                fileName,
                request.Content,
                cancellationToken);

            StorageProviderConfiguration configuration = StorageJson.ParseProviderConfiguration(storage.ConfigJson);
            if (configuration.PinOnUpload)
            {
                await transport.PinAsync(storage, secretValue, add.ContentId, cancellationToken);
            }

            string directUrl = ResolveDirectUrl(storage, add.ContentId);
            var reference = new StorageObjectReference(
                storage.Id,
                ProviderKind,
                StorageLocatorKind.ContentAddress,
                add.ContentId,
                request.FileName,
                string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType,
                request.Content.LongLength,
                directUrl);

            return new StorageWriteResult(
                reference,
                new StorageAccessDescriptor(
                    StorageJson.BuildPreviewUrl(reference),
                    StorageJson.BuildDownloadUrl(reference),
                    directUrl,
                    true,
                    true,
                    false,
                    string.IsNullOrWhiteSpace(request.FileName) ? add.ContentId : request.FileName,
                    reference.ContentType,
                    reference.ContentLength,
                    string.Empty));
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
            string? secretValue = await secretResolver.ResolveCredentialAsync(
                storage.CredentialSecretId,
                cancellationToken);
            return await transport.OpenReadAsync(
                storage,
                secretValue,
                reference.Locator,
                reference.Route,
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

    public Task DeleteAsync(
        StorageCatalogRecord storage,
        StorageObjectReference reference,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("IPFS delete is not supported by this storage driver.");

    internal static string ResolveDirectUrl(StorageCatalogRecord storage, string contentId)
    {
        StorageProviderConfiguration configuration = StorageJson.ParseProviderConfiguration(storage.ConfigJson);
        string gatewayBaseUrl = !string.IsNullOrWhiteSpace(configuration.GatewayBaseUrl)
            ? configuration.GatewayBaseUrl
            : DeriveGatewayBaseUrl(storage.EndpointOrRoot);
        if (string.IsNullOrWhiteSpace(gatewayBaseUrl))
        {
            return string.Empty;
        }

        string normalizedGateway = gatewayBaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? gatewayBaseUrl
            : gatewayBaseUrl + "/";
        return new Uri(new Uri(normalizedGateway), contentId).ToString();
    }

    private static string DeriveGatewayBaseUrl(string apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            return string.Empty;
        }

        var apiBaseUri = new Uri(apiBaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? apiBaseUrl
            : apiBaseUrl + "/");
        return new Uri(new Uri(apiBaseUri, "../../"), "ipfs/").ToString();
    }

    private InvalidOperationException CreateFailure(
        StorageCatalogRecord storage,
        string operation,
        Exception exception)
    {
        logger.LogWarning(
            "IPFS {Operation} failed for storage {StorageId} with {FailureType}.",
            operation,
            storage.Id,
            exception.GetType().Name);
        return new InvalidOperationException($"The IPFS {operation} operation failed.");
    }
}
