using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace CanDoItAll.Infrastructure.Storage;

public sealed class IpfsHttpStorageTransport(HttpClient httpClient) : IIpfsStorageTransport
{
    private const long MaximumContentBytes = 256L * 1024 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task TestConnectionAsync(
        StorageCatalogRecord storage,
        string? bearerToken,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Get,
            BuildApiUri(storage, "version"),
            bearerToken,
            content: null,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IpfsAddResult> AddAsync(
        StorageCatalogRecord storage,
        string? bearerToken,
        string fileName,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        if (content.Length > MaximumContentBytes)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.BudgetExceeded,
                "The IPFS upload exceeds the configured byte limit."));
        }

        if (!MemoryMarshal.TryGetArray(content, out ArraySegment<byte> segment) || segment.Array is null)
        {
            throw new InvalidOperationException("The IPFS upload content is not backed by a bounded byte array.");
        }

        using var multipart = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(segment.Array, segment.Offset, segment.Count);
        multipart.Add(fileContent, "file", fileName);
        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Post,
            BuildApiUri(storage, "add"),
            bearerToken,
            multipart,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        IpfsAddPayload? payload = await response.Content.ReadFromJsonAsync<IpfsAddPayload>(
            SerializerOptions,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(payload?.Hash))
        {
            throw new InvalidOperationException("The IPFS add response did not contain a content identifier.");
        }

        return new IpfsAddResult(payload.Hash);
    }

    public async Task PinAsync(
        StorageCatalogRecord storage,
        string? bearerToken,
        string contentId,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Post,
            BuildApiUri(storage, "pin/add", contentId),
            bearerToken,
            content: null,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Stream> OpenReadAsync(
        StorageCatalogRecord storage,
        string? bearerToken,
        string locator,
        string route,
        CancellationToken cancellationToken)
    {
        Uri uri = string.IsNullOrWhiteSpace(route)
            ? BuildApiUri(storage, "cat", locator)
            : new Uri(route, UriKind.Absolute);
        HttpResponseMessage response = await SendAsync(
            HttpMethod.Get,
            uri,
            bearerToken,
            content: null,
            cancellationToken);
        try
        {
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaximumContentBytes)
            {
                throw new StorageBrowseException(new StorageBrowseError(
                    StorageBrowseErrorCode.BudgetExceeded,
                    "The IPFS content exceeds the configured stream byte limit."));
            }

            Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return new OwnedBoundedReadStream(stream, response, MaximumContentBytes);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    public async Task<RemoteBrowseTransportPage> BrowseAsync(
        StorageCatalogRecord storage,
        string? bearerToken,
        IpfsBrowseAddress address,
        RemoteBrowseTransportRequest request,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.MaximumDuration);
        string? revisionBefore = null;
        long revisionBytes = 0;
        int requestCount = 1;
        if (address.Kind == IpfsBrowseAddressKind.MutableFileSystem)
        {
            (revisionBefore, revisionBytes) = await ReadMfsRevisionAsync(
                storage,
                bearerToken,
                address.Value,
                request.MaximumResponseBytes,
                timeout.Token);
            requestCount++;
        }

        string action = address.Kind == IpfsBrowseAddressKind.ContentAddress ? "ls" : "files/ls";
        Uri uri = BuildApiUri(storage, action, address.Value, includeLongFacts: true);
        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Post,
            uri,
            bearerToken,
            content: null,
            timeout.Token);
        response.EnsureSuccessStatusCode();
        await using Stream raw = await response.Content.ReadAsStreamAsync(timeout.Token);
        await using var bounded = new OwnedBoundedReadStream(
            raw,
            NoopDisposable.Instance,
            request.MaximumResponseBytes - revisionBytes);
        string entriesProperty = address.Kind == IpfsBrowseAddressKind.ContentAddress ? "Links" : "Entries";
        await using var entriesStream = new NestedJsonArrayReadStream(bounded, entriesProperty);
        var entries = new List<RemoteBrowseTransportEntry>(request.Limit);
        int inspected = 0;
        bool hasMore = false;
        await foreach (IpfsBrowsePayloadEntry? item in JsonSerializer.DeserializeAsyncEnumerable<IpfsBrowsePayloadEntry>(
            entriesStream,
            SerializerOptions,
            timeout.Token))
        {
            timeout.Token.ThrowIfCancellationRequested();
            if (item is null)
            {
                throw new JsonException("The IPFS browse response contained a null entry.");
            }

            if (inspected >= request.MaximumInspectedItems)
            {
                hasMore = true;
                break;
            }

            int currentIndex = inspected++;
            if (currentIndex < request.Offset)
            {
                continue;
            }

            if (entries.Count == request.Limit)
            {
                hasMore = true;
                break;
            }

            entries.Add(MapEntry(address, item));
        }
        string? revision = address.Kind == IpfsBrowseAddressKind.ContentAddress ? address.Value : revisionBefore;
        if (address.Kind == IpfsBrowseAddressKind.MutableFileSystem)
        {
            (string revisionAfter, long afterBytes) = await ReadMfsRevisionAsync(
                storage,
                bearerToken,
                address.Value,
                request.MaximumResponseBytes - revisionBytes - bounded.Position,
                timeout.Token);
            requestCount++;
            revisionBytes += afterBytes;
            if (!string.Equals(revisionBefore, revisionAfter, StringComparison.Ordinal))
            {
                throw new StorageBrowseException(new StorageBrowseError(
                    StorageBrowseErrorCode.SourceChanged,
                    "The IPFS mutable source changed during the browse operation."));
            }
        }

        return new RemoteBrowseTransportPage(
            entries,
            inspected,
            hasMore,
            bounded.Position + revisionBytes,
            requestCount,
            revision);
    }

    private async Task<(string Revision, long ResponseBytes)> ReadMfsRevisionAsync(
        StorageCatalogRecord storage,
        string? bearerToken,
        string path,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        if (maximumBytes < 1)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.BudgetExceeded,
                "The IPFS browse response exceeded its byte limit."));
        }

        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Post,
            BuildApiUri(storage, "files/stat", path),
            bearerToken,
            content: null,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream raw = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var bounded = new OwnedBoundedReadStream(raw, NoopDisposable.Instance, maximumBytes);
        using JsonDocument document = await JsonDocument.ParseAsync(bounded, cancellationToken: cancellationToken);
        string revision = document.RootElement.GetProperty("Hash").GetString()
            ?? throw new JsonException("The IPFS mutable source did not report a revision hash.");
        return (revision, bounded.Position);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        Uri uri,
        string? bearerToken,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri) { Content = content };
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
    }

    private static RemoteBrowseTransportEntry MapEntry(
        IpfsBrowseAddress address,
        IpfsBrowsePayloadEntry item)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
        {
            throw new JsonException("An IPFS browse entry did not contain a name.");
        }

        bool isContainer = item.Type == 1;
        string hash = item.Hash ?? string.Empty;
        string locator = address.Kind == IpfsBrowseAddressKind.ContentAddress
            ? $"cid:{hash}"
            : $"mfs:{CombineMfsPath(address.Value, item.Name)}";
        return new RemoteBrowseTransportEntry(
            item.Name,
            locator,
            isContainer ? StorageBrowseEntryKind.Container : StorageBrowseEntryKind.File,
            item.Size,
            ContentVersion: string.IsNullOrWhiteSpace(hash) ? null : hash);
    }

    private static Uri BuildApiUri(
        StorageCatalogRecord storage,
        string action,
        string? argument = null,
        bool includeLongFacts = false)
    {
        if (string.IsNullOrWhiteSpace(storage.EndpointOrRoot))
        {
            throw new InvalidOperationException("IPFS storage requires an API base URL.");
        }

        var baseUri = new Uri(storage.EndpointOrRoot.EndsWith("/", StringComparison.Ordinal)
            ? storage.EndpointOrRoot
            : storage.EndpointOrRoot + "/");
        Uri apiRoot = baseUri.AbsolutePath.TrimEnd('/').EndsWith("/api/v0", StringComparison.OrdinalIgnoreCase)
            ? baseUri
            : new Uri(baseUri, "api/v0/");
        var builder = new UriBuilder(new Uri(apiRoot, action));
        var query = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(argument))
        {
            query.Add($"arg={Uri.EscapeDataString(argument)}");
        }

        if (includeLongFacts)
        {
            query.Add("long=true");
        }

        builder.Query = string.Join('&', query);
        return builder.Uri;
    }

    private static string CombineMfsPath(string parent, string name)
        => $"/{string.Join('/', new[] { parent.Trim('/'), name.Trim('/') }.Where(value => value.Length > 0))}";

    private sealed record IpfsAddPayload(string Hash);

    private sealed record IpfsBrowsePayloadEntry(
        string Name,
        int Type,
        long? Size,
        string? Hash);

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
