using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace CanDoItAll.Infrastructure.Storage;

public sealed class IpfsStorageDriver(
    ILogger<IpfsStorageDriver> logger,
    IStorageSecretResolver secretResolver) : IStorageDriver
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

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
            using var client = CreateClient(secretValue);
            using var response = await client.GetAsync(BuildApiUri(storage, "version"), cancellationToken);
            response.EnsureSuccessStatusCode();

            return new StorageConnectionTestResult(
                true,
                "IPFS API responded successfully.",
                StorageHealthStatus.Healthy,
                SupportedCapabilities,
                DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "IPFS connection test failed for {Endpoint}.", storage.EndpointOrRoot);
            return new StorageConnectionTestResult(
                false,
                $"IPFS storage is unavailable: {ex.Message}",
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

        var secretValue = await secretResolver.ResolveCredentialAsync(storage.CredentialSecretId, cancellationToken);
        using var client = CreateClient(secretValue);
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(request.Content);
        content.Add(fileContent, "file", string.IsNullOrWhiteSpace(request.FileName) ? "artifact.bin" : request.FileName);

        using var addResponse = await client.PostAsync(BuildApiUri(storage, "add"), content, cancellationToken);
        addResponse.EnsureSuccessStatusCode();

        var addPayload = await addResponse.Content.ReadFromJsonAsync<IpfsAddResponse>(
            SerializerOptions,
            cancellationToken);
        if (addPayload is null || string.IsNullOrWhiteSpace(addPayload.Hash))
        {
            throw new InvalidOperationException("The IPFS add response did not return a CID.");
        }

        var configuration = StorageJson.ParseProviderConfiguration(storage.ConfigJson);
        if (configuration.PinOnUpload)
        {
            using var pinResponse = await client.PostAsync(
                BuildApiUri(storage, "pin/add", addPayload.Hash),
                content: null,
                cancellationToken);
            pinResponse.EnsureSuccessStatusCode();
        }

        var directUrl = ResolveDirectUrl(storage, addPayload.Hash);
        var reference = new StorageObjectReference(
            storage.Id,
            ProviderKind,
            StorageLocatorKind.ContentAddress,
            addPayload.Hash,
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
                string.IsNullOrWhiteSpace(request.FileName) ? addPayload.Hash : request.FileName,
                reference.ContentType,
                reference.ContentLength,
                string.Empty));
    }

    public async Task<Stream> OpenReadAsync(
        StorageCatalogRecord storage,
        StorageObjectReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(reference);

        var secretValue = await secretResolver.ResolveCredentialAsync(storage.CredentialSecretId, cancellationToken);
        using var client = CreateClient(secretValue);
        using var response = string.IsNullOrWhiteSpace(reference.Route)
            ? await client.GetAsync(BuildApiUri(storage, "cat", reference.Locator), cancellationToken)
            : await client.GetAsync(reference.Route, cancellationToken);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return new MemoryStream(bytes, writable: false);
    }

    public Task DeleteAsync(
        StorageCatalogRecord storage,
        StorageObjectReference reference,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("IPFS delete is not supported in the initial storage driver.");
    }

    private HttpClient CreateClient(string? secretValue)
    {
        var client = new HttpClient();
        if (!string.IsNullOrWhiteSpace(secretValue))
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", secretValue);
        }

        return client;
    }

    private static Uri BuildApiUri(StorageCatalogRecord storage, string action, string? arg = null)
    {
        if (string.IsNullOrWhiteSpace(storage.EndpointOrRoot))
        {
            throw new InvalidOperationException("IPFS storage requires an API base URL.");
        }

        var baseUri = new Uri(storage.EndpointOrRoot.EndsWith("/", StringComparison.Ordinal)
            ? storage.EndpointOrRoot
            : storage.EndpointOrRoot + "/");
        var apiRoot = baseUri.AbsolutePath.TrimEnd('/').EndsWith("/api/v0", StringComparison.OrdinalIgnoreCase)
            ? baseUri
            : new Uri(baseUri, "api/v0/");
        var endpoint = new Uri(apiRoot, action);
        if (string.IsNullOrWhiteSpace(arg))
        {
            return endpoint;
        }

        var builder = new UriBuilder(endpoint)
        {
            Query = $"arg={Uri.EscapeDataString(arg)}"
        };
        return builder.Uri;
    }

    private static string ResolveDirectUrl(StorageCatalogRecord storage, string cid)
    {
        var configuration = StorageJson.ParseProviderConfiguration(storage.ConfigJson);
        var gatewayBaseUrl = !string.IsNullOrWhiteSpace(configuration.GatewayBaseUrl)
            ? configuration.GatewayBaseUrl
            : DeriveGatewayBaseUrl(storage.EndpointOrRoot);

        if (string.IsNullOrWhiteSpace(gatewayBaseUrl))
        {
            return string.Empty;
        }

        var normalizedGateway = gatewayBaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? gatewayBaseUrl
            : gatewayBaseUrl + "/";
        return new Uri(new Uri(normalizedGateway), cid).ToString();
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
        var rootUri = new Uri(apiBaseUri, "../../");
        return new Uri(rootUri, "ipfs/").ToString();
    }

    private sealed record IpfsAddResponse(string Hash, string Name, string Size);
}
