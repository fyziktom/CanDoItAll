using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Tests.Unit.Storage;

public sealed class StorageBrowseContractsTests
{
    [Fact]
    public void Request_PageSizeExceedsReturnedItemBudget_ThrowsTypedValidationError()
    {
        var budget = new StorageBrowseWorkBudget(maximumReturnedItems: 20);

        StorageBrowseException exception = Assert.Throws<StorageBrowseException>(() =>
            new StorageBrowseRequest(StorageBrowseContainer.Root, pageSize: 21, budget: budget));

        Assert.Equal(StorageBrowseErrorCode.InvalidRequest, exception.Error.Code);
    }

    [Fact]
    public void Cursor_WhitespaceToken_ThrowsTypedCursorError()
    {
        StorageBrowseException exception = Assert.Throws<StorageBrowseException>(() =>
            new StorageBrowseCursor(" "));

        Assert.Equal(StorageBrowseErrorCode.InvalidCursor, exception.Error.Code);
    }

    [Fact]
    public void Page_ReturnedCountDoesNotMatchEntries_ThrowsTypedValidationError()
    {
        StorageBrowseException exception = Assert.Throws<StorageBrowseException>(() =>
            new StorageBrowsePage(
                StorageBrowseContainer.Root,
                [],
                [],
                StorageBrowseSort.ProviderOrder,
                StorageBrowseCompleteness.Complete,
                new StorageBrowseOperationMetrics(1, 1, 0, 0, TimeSpan.Zero)));

        Assert.Equal(StorageBrowseErrorCode.InvalidRequest, exception.Error.Code);
    }

    [Fact]
    public void Registry_DuplicateProviderKind_ThrowsTypedRegistrationError()
    {
        StorageBrowseException exception = Assert.Throws<StorageBrowseException>(() =>
            new StorageBrowseDriverRegistry([
                new BrowseOnlyDriver(StorageProviderKind.FileSystem),
                new BrowseOnlyDriver(StorageProviderKind.FileSystem)
            ]));

        Assert.Equal(StorageBrowseErrorCode.DuplicateProviderRegistration, exception.Error.Code);
    }

    [Fact]
    public void Registry_UnknownProvider_ThrowsTypedResolutionError()
    {
        var registry = new StorageBrowseDriverRegistry([
            new BrowseOnlyDriver(StorageProviderKind.FileSystem)
        ]);

        StorageBrowseException exception = Assert.Throws<StorageBrowseException>(() =>
            registry.Resolve(StorageProviderKind.Ftp));

        Assert.Equal(StorageBrowseErrorCode.ProviderNotRegistered, exception.Error.Code);
    }

    [Fact]
    public void Registry_BrowseOnlyProviderSearch_ThrowsUnsupportedWithoutFallback()
    {
        var registry = new StorageBrowseDriverRegistry([
            new BrowseOnlyDriver(StorageProviderKind.FileSystem),
            new SearchableDriver(StorageProviderKind.Ipfs)
        ]);

        StorageBrowseException exception = Assert.Throws<StorageBrowseException>(() =>
            registry.ResolveSearch(StorageProviderKind.FileSystem));

        Assert.Equal(StorageBrowseErrorCode.UnsupportedOperation, exception.Error.Code);
        Assert.IsType<SearchableDriver>(registry.ResolveSearch(StorageProviderKind.Ipfs));
    }

    [Fact]
    public void Registry_AdvertisedSearchWithoutSearchFacet_ThrowsConfigurationError()
    {
        StorageBrowseException exception = Assert.Throws<StorageBrowseException>(() =>
            new StorageBrowseDriverRegistry([
                new InconsistentSearchDriver()
            ]));

        Assert.Equal(StorageBrowseErrorCode.InvalidConfiguration, exception.Error.Code);
    }

    [Fact]
    public async Task DistinctProviderShapes_ExecuteThroughNativeContract()
    {
        IStorageBrowseDriver[] drivers = [
            new BrowseOnlyDriver(StorageProviderKind.FileSystem),
            new SearchableDriver(StorageProviderKind.Ipfs)
        ];
        var registry = new StorageBrowseDriverRegistry(drivers);
        var storage = new StorageCatalogRecord();

        StorageBrowsePage fileSystemPage = await registry.Resolve(StorageProviderKind.FileSystem)
            .BrowseAsync(storage, new StorageBrowseRequest(StorageBrowseContainer.Root));
        StorageBrowsePage ipfsSearchPage = await registry.ResolveSearch(StorageProviderKind.Ipfs)
            .SearchAsync(
                storage,
                new StorageBrowseSearchRequest(
                    "report",
                    new StorageBrowseRequest(StorageBrowseContainer.Root)));

        Assert.Empty(fileSystemPage.Entries);
        Assert.Empty(ipfsSearchPage.Entries);
        Assert.Equal(StorageBrowseCompleteness.Complete, fileSystemPage.Completeness);
        Assert.Equal(StorageBrowseCompleteness.Complete, ipfsSearchPage.Completeness);
    }

    private static StorageBrowsePage EmptyPage(StorageBrowseContainer container)
        => new(
            container,
            [],
            [],
            StorageBrowseSort.ProviderOrder,
            StorageBrowseCompleteness.Complete,
            new StorageBrowseOperationMetrics(0, 0, 0, 0, TimeSpan.Zero));

    private sealed class BrowseOnlyDriver(StorageProviderKind providerKind) : IStorageBrowseDriver
    {
        public StorageProviderKind ProviderKind => providerKind;

        public StorageBrowseCapability Capabilities =>
            StorageBrowseCapability.Browse |
            StorageBrowseCapability.ProviderNativeOrdering;

        public StorageBrowseWorkBudget MaximumBudget => StorageBrowseWorkBudget.Default;

        public Task<StorageBrowsePage> BrowseAsync(
            StorageCatalogRecord storage,
            StorageBrowseRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(EmptyPage(request.Container));
    }

    private sealed class SearchableDriver(StorageProviderKind providerKind) :
        IStorageBrowseDriver,
        IStorageBrowseSearchDriver,
        IStorageBrowseStatDriver
    {
        public StorageProviderKind ProviderKind => providerKind;

        public StorageBrowseCapability Capabilities =>
            StorageBrowseCapability.Browse |
            StorageBrowseCapability.Search |
            StorageBrowseCapability.Stat |
            StorageBrowseCapability.ProviderNativeOrdering;

        public StorageBrowseWorkBudget MaximumBudget => StorageBrowseWorkBudget.Default;

        public StorageBrowseSearchBudget MaximumSearchBudget => StorageBrowseSearchBudget.Default;

        public Task<StorageBrowsePage> BrowseAsync(
            StorageCatalogRecord storage,
            StorageBrowseRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(EmptyPage(request.Container));

        public Task<StorageBrowsePage> SearchAsync(
            StorageCatalogRecord storage,
            StorageBrowseSearchRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(EmptyPage(request.Browse.Container));

        public Task<StorageBrowseEntry> StatAsync(
            StorageCatalogRecord storage,
            StorageBrowseStatRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new StorageBrowseEntry(
                request.EntryId,
                request.Container,
                "entry",
                "entry",
                StorageBrowseEntryKind.File,
                StorageBrowseEntryCapability.Read));
    }

    private sealed class InconsistentSearchDriver : IStorageBrowseDriver
    {
        public StorageProviderKind ProviderKind => StorageProviderKind.Ftp;

        public StorageBrowseCapability Capabilities =>
            StorageBrowseCapability.Browse |
            StorageBrowseCapability.Search;

        public StorageBrowseWorkBudget MaximumBudget => StorageBrowseWorkBudget.Default;

        public Task<StorageBrowsePage> BrowseAsync(
            StorageCatalogRecord storage,
            StorageBrowseRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(EmptyPage(request.Container));
    }
}
