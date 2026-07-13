using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Infrastructure.Storage;

public sealed class IpfsStorageBrowseDriver : IStorageBrowseDriver
{
    private const long MaximumResponseBytes = 2L * 1024 * 1024;
    private readonly IStorageSecretResolver _secretResolver;
    private readonly IIpfsStorageTransport _transport;
    private readonly RemoteStorageBrowseCursorCodec _cursorCodec;
    private readonly ILogger<IpfsStorageBrowseDriver> _logger;

    public IpfsStorageBrowseDriver(
        IStorageSecretResolver secretResolver,
        IIpfsStorageTransport transport,
        ILogger<IpfsStorageBrowseDriver> logger)
        : this(secretResolver, transport, new RemoteStorageBrowseCursorCodec(), logger)
    {
    }

    internal IpfsStorageBrowseDriver(
        IStorageSecretResolver secretResolver,
        IIpfsStorageTransport transport,
        RemoteStorageBrowseCursorCodec cursorCodec,
        ILogger<IpfsStorageBrowseDriver> logger)
    {
        _secretResolver = secretResolver ?? throw new ArgumentNullException(nameof(secretResolver));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _cursorCodec = cursorCodec ?? throw new ArgumentNullException(nameof(cursorCodec));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public StorageProviderKind ProviderKind => StorageProviderKind.Ipfs;

    public StorageBrowseCapability Capabilities =>
        StorageBrowseCapability.Browse |
        StorageBrowseCapability.ProviderNativeOrdering |
        StorageBrowseCapability.ConsistentContinuation |
        StorageBrowseCapability.Metadata |
        StorageBrowseCapability.ImmutableVersion;

    public StorageBrowseWorkBudget MaximumBudget { get; } = new(
        maximumReturnedItems: 500,
        maximumInspectedItems: 10_000,
        maximumMetadataProbes: 10_000,
        maximumConcurrentMetadataProbes: 1,
        maximumDuration: TimeSpan.FromSeconds(30));

    public async Task<StorageBrowsePage> BrowseAsync(
        StorageCatalogRecord storage,
        StorageBrowseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(request);
        Validate(storage, request);
        IpfsBrowseAddress address = ParseAddress(storage, request.Container);
        RemoteStorageBrowseCursorState? cursor = ResolveCursor(storage, request);
        int offset = cursor?.Offset ?? 0;
        string? secret = await _secretResolver.ResolveCredentialAsync(
            storage.CredentialSecretId,
            cancellationToken);
        var transportRequest = new RemoteBrowseTransportRequest(
            offset,
            request.PageSize,
            request.Budget.MaximumInspectedItems,
            MaximumResponseBytes,
            request.Budget.MaximumDuration);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            RemoteBrowseTransportPage transportPage = await _transport.BrowseAsync(
                storage,
                secret,
                address,
                transportRequest,
                cancellationToken);
            ValidateTransportPage(request, transportPage);
            if (cursor?.SourceRevision is not null &&
                !string.Equals(cursor.SourceRevision, transportPage.SourceRevision, StringComparison.Ordinal))
            {
                throw new StorageBrowseException(new StorageBrowseError(
                    StorageBrowseErrorCode.SourceChanged,
                    "The IPFS browse source changed after the cursor was issued."));
            }

            StorageBrowseEntry[] entries = transportPage.Entries
                .Select(entry => MapEntry(request.Container, entry, request.Metadata))
                .ToArray();
            StorageBrowseCursor? nextCursor = transportPage.HasMore
                ? _cursorCodec.Encode(new RemoteStorageBrowseCursorState(
                    ProviderKind,
                    storage.Id,
                    RemoteStorageBrowseCursorCodec.Fingerprint(request.Container),
                    offset + entries.Length,
                    request.PageSize,
                    request.Metadata,
                    transportPage.SourceRevision))
                : null;
            stopwatch.Stop();
            int metadataProbes = request.Metadata == StorageBrowseMetadataField.None ? 0 : entries.Length;
            var metrics = new StorageBrowseOperationMetrics(
                entries.Length,
                transportPage.InspectedItems,
                metadataProbes,
                nextCursor?.Token.Length ?? 0,
                stopwatch.Elapsed);
            StorageBrowseConsistencyToken? consistency = address.Kind == IpfsBrowseAddressKind.ContentAddress
                ? new StorageBrowseConsistencyToken(address.Value)
                : transportPage.SourceRevision is null
                    ? null
                    : new StorageBrowseConsistencyToken(transportPage.SourceRevision);
            var page = new StorageBrowsePage(
                request.Container,
                CreatePath(address),
                entries,
                request.Sort,
                StorageBrowseCompleteness.Complete,
                metrics,
                nextCursor,
                consistency);
            _logger.LogInformation(
                "IPFS browse completed for storage {StorageId}, address kind {AddressKind}, returned {Returned}, inspected {Inspected}, response bytes {ResponseBytes}, requests {RequestCount}.",
                storage.Id,
                address.Kind,
                entries.Length,
                transportPage.InspectedItems,
                transportPage.ResponseBytes,
                transportPage.RequestCount);
            return page;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("IPFS browse cancelled for storage {StorageId}.", storage.Id);
            throw;
        }
        catch (StorageBrowseException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "IPFS browse failed for storage {StorageId} with {FailureType}.",
                storage.Id,
                exception.GetType().Name);
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.ProviderUnavailable,
                "The IPFS source could not complete the browse operation.",
                isRetryable: true));
        }
    }

    private static IpfsBrowseAddress ParseAddress(
        StorageCatalogRecord storage,
        StorageBrowseContainer container)
    {
        if (container.IsRoot)
        {
            StorageProviderConfiguration configuration = StorageJson.ParseProviderConfiguration(storage.ConfigJson);
            string root = string.IsNullOrWhiteSpace(configuration.BasePath) ? "/" : configuration.BasePath;
            return new IpfsBrowseAddress(IpfsBrowseAddressKind.MutableFileSystem, NormalizeMfsPath(root));
        }

        if (container.Key.StartsWith("cid:", StringComparison.Ordinal))
        {
            string contentId = container.Key[4..].Trim();
            if (contentId.Length > 0)
            {
                return new IpfsBrowseAddress(IpfsBrowseAddressKind.ContentAddress, contentId);
            }
        }

        if (container.Key.StartsWith("mfs:", StringComparison.Ordinal))
        {
            return new IpfsBrowseAddress(
                IpfsBrowseAddressKind.MutableFileSystem,
                NormalizeMfsPath(container.Key[4..]));
        }

        throw new StorageBrowseException(new StorageBrowseError(
            StorageBrowseErrorCode.InvalidRequest,
            "An IPFS container must use an explicit cid: or mfs: identifier."));
    }

    private RemoteStorageBrowseCursorState? ResolveCursor(
        StorageCatalogRecord storage,
        StorageBrowseRequest request)
    {
        if (request.Cursor is null)
        {
            return null;
        }

        RemoteStorageBrowseCursorState cursor = _cursorCodec.Decode(request.Cursor);
        bool matches = cursor.ProviderKind == ProviderKind &&
                       cursor.StorageId == storage.Id &&
                       cursor.ContainerFingerprint == RemoteStorageBrowseCursorCodec.Fingerprint(request.Container) &&
                       cursor.PageSize == request.PageSize &&
                       cursor.Metadata == request.Metadata;
        if (!matches)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.InvalidCursor,
                "The IPFS browse cursor does not match the current request."));
        }

        return cursor;
    }

    private static StorageBrowseEntry MapEntry(
        StorageBrowseContainer parent,
        RemoteBrowseTransportEntry entry,
        StorageBrowseMetadataField metadata)
    {
        bool isContainer = entry.Kind == StorageBrowseEntryKind.Container;
        StorageBrowseEntryCapability capabilities = isContainer
            ? StorageBrowseEntryCapability.Browse
            : StorageBrowseEntryCapability.Read;
        return new StorageBrowseEntry(
            new StorageBrowseEntryId(entry.Locator),
            parent,
            entry.Name,
            entry.Name,
            entry.Kind,
            capabilities,
            metadata.HasFlag(StorageBrowseMetadataField.Size) ? entry.Size : null,
            modifiedAtUtc: metadata.HasFlag(StorageBrowseMetadataField.ModifiedAtUtc) ? entry.ModifiedAtUtc : null);
    }

    private static IReadOnlyList<StorageBrowsePathSegment> CreatePath(IpfsBrowseAddress address)
        => [new StorageBrowsePathSegment(
            address.Kind == IpfsBrowseAddressKind.ContentAddress ? "IPFS content" : "IPFS files",
            new StorageBrowseContainer(
                address.Kind == IpfsBrowseAddressKind.ContentAddress
                    ? $"cid:{address.Value}"
                    : $"mfs:{address.Value}"))];

    private static string NormalizeMfsPath(string value)
    {
        string normalized = value.Trim().Replace('\\', '/');
        if (normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or ".."))
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.AccessDenied,
                "The IPFS mutable path is not permitted."));
        }

        return "/" + normalized.Trim('/');
    }

    private void Validate(StorageCatalogRecord storage, StorageBrowseRequest request)
    {
        if (storage.ProviderKind != ProviderKind)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.InvalidRequest,
                "The IPFS browse driver received a different provider kind."));
        }

        if (request.Sort != StorageBrowseSort.ProviderOrder)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.UnsupportedOperation,
                "IPFS browsing supports only provider-native ordering."));
        }

        if (request.Budget.MaximumInspectedItems > MaximumBudget.MaximumInspectedItems ||
            request.Budget.MaximumDuration > MaximumBudget.MaximumDuration)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.BudgetExceeded,
                "The IPFS browse request exceeds provider limits."));
        }
    }

    private static void ValidateTransportPage(
        StorageBrowseRequest request,
        RemoteBrowseTransportPage page)
    {
        if (page.Entries.Count > request.PageSize ||
            (page.HasMore && page.Entries.Count == 0) ||
            page.InspectedItems < page.Entries.Count ||
            page.InspectedItems > request.Budget.MaximumInspectedItems ||
            page.ResponseBytes < 0 ||
            page.ResponseBytes > MaximumResponseBytes ||
            page.RequestCount < 1)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.ProviderUnavailable,
                "The IPFS transport returned inconsistent bounded browse facts."));
        }
    }
}
