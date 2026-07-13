namespace CanDoItAll.Infrastructure.Storage;

public enum IpfsBrowseAddressKind
{
    ContentAddress,
    MutableFileSystem
}

public sealed record IpfsBrowseAddress(IpfsBrowseAddressKind Kind, string Value);

public sealed record RemoteBrowseTransportRequest(
    int Offset,
    int Limit,
    int MaximumInspectedItems,
    long MaximumResponseBytes,
    TimeSpan MaximumDuration);

public sealed record RemoteBrowseTransportEntry(
    string Name,
    string Locator,
    StorageBrowseEntryKind Kind,
    long? Size = null,
    DateTimeOffset? ModifiedAtUtc = null,
    string? ContentVersion = null);

public sealed record RemoteBrowseTransportPage(
    IReadOnlyList<RemoteBrowseTransportEntry> Entries,
    int InspectedItems,
    bool HasMore,
    long ResponseBytes,
    int RequestCount,
    string? SourceRevision = null,
    bool ClassificationReliable = true);

public sealed record IpfsAddResult(string ContentId);

public interface IIpfsStorageTransport
{
    Task TestConnectionAsync(
        StorageCatalogRecord storage,
        string? bearerToken,
        CancellationToken cancellationToken);

    Task<IpfsAddResult> AddAsync(
        StorageCatalogRecord storage,
        string? bearerToken,
        string fileName,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken);

    Task PinAsync(
        StorageCatalogRecord storage,
        string? bearerToken,
        string contentId,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(
        StorageCatalogRecord storage,
        string? bearerToken,
        string locator,
        string route,
        CancellationToken cancellationToken);

    Task<RemoteBrowseTransportPage> BrowseAsync(
        StorageCatalogRecord storage,
        string? bearerToken,
        IpfsBrowseAddress address,
        RemoteBrowseTransportRequest request,
        CancellationToken cancellationToken);
}

public interface IFtpStorageTransport
{
    Task<string?> TestConnectionAsync(
        StorageCatalogRecord storage,
        string? password,
        CancellationToken cancellationToken);

    Task UploadAsync(
        StorageCatalogRecord storage,
        string? password,
        string remotePath,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(
        StorageCatalogRecord storage,
        string? password,
        string remotePath,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        StorageCatalogRecord storage,
        string? password,
        string remotePath,
        CancellationToken cancellationToken);

    Task<RemoteBrowseTransportPage> BrowseAsync(
        StorageCatalogRecord storage,
        string? password,
        string remotePath,
        RemoteBrowseTransportRequest request,
        CancellationToken cancellationToken);
}
