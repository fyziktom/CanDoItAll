using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Infrastructure.Storage;

public sealed class FtpStorageBrowseDriver : IStorageBrowseDriver
{
    private const long MaximumResponseBytes = 2L * 1024 * 1024;
    private readonly IStorageSecretResolver _secretResolver;
    private readonly IFtpStorageTransport _transport;
    private readonly RemoteStorageBrowseCursorCodec _cursorCodec;
    private readonly ILogger<FtpStorageBrowseDriver> _logger;

    public FtpStorageBrowseDriver(
        IStorageSecretResolver secretResolver,
        IFtpStorageTransport transport,
        ILogger<FtpStorageBrowseDriver> logger)
        : this(secretResolver, transport, new RemoteStorageBrowseCursorCodec(), logger)
    {
    }

    internal FtpStorageBrowseDriver(
        IStorageSecretResolver secretResolver,
        IFtpStorageTransport transport,
        RemoteStorageBrowseCursorCodec cursorCodec,
        ILogger<FtpStorageBrowseDriver> logger)
    {
        _secretResolver = secretResolver ?? throw new ArgumentNullException(nameof(secretResolver));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _cursorCodec = cursorCodec ?? throw new ArgumentNullException(nameof(cursorCodec));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public StorageProviderKind ProviderKind => StorageProviderKind.Ftp;

    public StorageBrowseCapability Capabilities =>
        StorageBrowseCapability.Browse |
        StorageBrowseCapability.ProviderNativeOrdering |
        StorageBrowseCapability.Metadata;

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
        int offset = ResolveOffset(storage, request);
        string remotePath = NormalizeContainer(request.Container);
        string? password = await _secretResolver.ResolveCredentialAsync(
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
                password,
                remotePath,
                transportRequest,
                cancellationToken);
            ValidateTransportPage(request, transportPage);
            if (!transportPage.ClassificationReliable)
            {
                throw new StorageBrowseException(new StorageBrowseError(
                    StorageBrowseErrorCode.UnsupportedOperation,
                    "The FTP server did not provide reliable entry classification facts."));
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
                    SourceRevision: null))
                : null;
            stopwatch.Stop();
            int metadataProbes = request.Metadata == StorageBrowseMetadataField.None ? 0 : entries.Length;
            var page = new StorageBrowsePage(
                request.Container,
                CreatePath(request.Container),
                entries,
                request.Sort,
                StorageBrowseCompleteness.Complete,
                new StorageBrowseOperationMetrics(
                    entries.Length,
                    transportPage.InspectedItems,
                    metadataProbes,
                    nextCursor?.Token.Length ?? 0,
                    stopwatch.Elapsed),
                nextCursor);
            _logger.LogInformation(
                "FTP browse completed for storage {StorageId}, returned {Returned}, inspected {Inspected}, response bytes {ResponseBytes}, requests {RequestCount}.",
                storage.Id,
                entries.Length,
                transportPage.InspectedItems,
                transportPage.ResponseBytes,
                transportPage.RequestCount);
            return page;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("FTP browse cancelled for storage {StorageId}.", storage.Id);
            throw;
        }
        catch (StorageBrowseException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "FTP browse failed for storage {StorageId} with {FailureType}.",
                storage.Id,
                exception.GetType().Name);
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.ProviderUnavailable,
                "The FTP source could not complete the browse operation.",
                isRetryable: true));
        }
    }

    private int ResolveOffset(StorageCatalogRecord storage, StorageBrowseRequest request)
    {
        if (request.Cursor is null)
        {
            return 0;
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
                "The FTP browse cursor does not match the current request."));
        }

        return cursor.Offset;
    }

    private static string NormalizeContainer(StorageBrowseContainer container)
    {
        string normalized = container.Key.Trim().Replace('\\', '/').Trim('/');
        if (normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or ".."))
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.AccessDenied,
                "The FTP browse path is not permitted."));
        }

        return normalized;
    }

    private static StorageBrowseEntry MapEntry(
        StorageBrowseContainer parent,
        RemoteBrowseTransportEntry entry,
        StorageBrowseMetadataField metadata)
    {
        StorageBrowseEntryCapability capabilities = entry.Kind == StorageBrowseEntryKind.Container
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

    private static IReadOnlyList<StorageBrowsePathSegment> CreatePath(StorageBrowseContainer container)
        => [new StorageBrowsePathSegment(container.IsRoot ? "FTP root" : container.Key, container)];

    private void Validate(StorageCatalogRecord storage, StorageBrowseRequest request)
    {
        if (storage.ProviderKind != ProviderKind)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.InvalidRequest,
                "The FTP browse driver received a different provider kind."));
        }

        if (request.Sort != StorageBrowseSort.ProviderOrder)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.UnsupportedOperation,
                "FTP browsing supports only provider-native ordering."));
        }

        if (request.Budget.MaximumInspectedItems > MaximumBudget.MaximumInspectedItems ||
            request.Budget.MaximumDuration > MaximumBudget.MaximumDuration)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.BudgetExceeded,
                "The FTP browse request exceeds provider limits."));
        }
    }

    private static void ValidateTransportPage(StorageBrowseRequest request, RemoteBrowseTransportPage page)
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
                "The FTP transport returned inconsistent bounded browse facts."));
        }
    }
}
