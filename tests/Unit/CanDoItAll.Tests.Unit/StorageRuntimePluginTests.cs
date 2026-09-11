using CanDoItAll.Agents.Storage;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Tests.Unit.Storage;

public sealed class StorageRuntimePluginTests
{
    [Fact]
    public async Task BrowseStorage_AllowedCatalog_MapsBoundedDriverPage()
    {
        var storage = CreateStorage();
        var driver = new RecordingBrowseDriver();
        var sut = CreatePlugin(
            storage,
            driver,
            new AgentWorkspaceToolAccessSettings
            {
                CanReadStorage = true,
                AllowAllStorageCatalogs = true
            });

        var result = await sut.BrowseStorage(
            storage.Id,
            containerKey: "docs",
            pageSize: 25,
            cursor: "cursor-1");

        Assert.Equal(storage.Id, result.StorageId);
        Assert.Equal("docs", result.ContainerKey);
        var entry = Assert.Single(result.Entries);
        Assert.Equal("docs/readme.md", entry.EntryId);
        Assert.Equal("readme.md", entry.Name);
        Assert.Equal(AgentStorageBrowseEntryKind.File, entry.Kind);
        Assert.Equal(AgentStorageBrowseEntryCapability.Read, entry.Capabilities);
        Assert.Equal(AgentStorageBrowseCompleteness.Complete, result.Completeness);
        Assert.Equal("next-cursor", result.NextCursor);
        Assert.Equal(25, driver.LastRequest?.PageSize);
        Assert.Equal("cursor-1", driver.LastRequest?.Cursor?.Token);
        Assert.Equal(StorageBrowseMetadataField.None, driver.LastRequest?.Metadata);
    }

    [Fact]
    public async Task BrowseStorage_DisallowedCatalog_DoesNotInvokeDriver()
    {
        var storage = CreateStorage();
        var driver = new RecordingBrowseDriver();
        var sut = CreatePlugin(
            storage,
            driver,
            new AgentWorkspaceToolAccessSettings
            {
                CanReadStorage = true
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.BrowseStorage(storage.Id));

        Assert.Contains("not allowed", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, driver.InvocationCount);
    }

    [Fact]
    public async Task BrowseStorage_MetadataRequestedFromUnsupportedDriver_FailsBeforeBrowse()
    {
        var storage = CreateStorage();
        var driver = new RecordingBrowseDriver(includeMetadataCapability: false);
        var sut = CreatePlugin(
            storage,
            driver,
            new AgentWorkspaceToolAccessSettings
            {
                CanReadStorage = true,
                AllowAllStorageCatalogs = true
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.BrowseStorage(storage.Id, includeMetadata: true));

        Assert.Contains("includeMetadata=false", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, driver.InvocationCount);
    }

    [Fact]
    public async Task BrowseStorage_CatalogWithoutReadCapability_DoesNotInvokeDriver()
    {
        var storage = CreateStorage(StorageCapability.Write);
        var driver = new RecordingBrowseDriver();
        var sut = CreatePlugin(
            storage,
            driver,
            new AgentWorkspaceToolAccessSettings
            {
                CanReadStorage = true,
                AllowAllStorageCatalogs = true
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.BrowseStorage(storage.Id));

        Assert.Contains("required capability 'Read'", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, driver.InvocationCount);
    }

    [Fact]
    public async Task BrowseStorage_ContentDriverWithoutReadCapability_DoesNotInvokeBrowseDriver()
    {
        var storage = CreateStorage();
        var browseDriver = new RecordingBrowseDriver();
        var contentDriver = new RecordingStorageDriver(storage.ProviderKind, StorageCapability.Write);
        var sut = CreatePlugin(
            storage,
            browseDriver,
            new AgentWorkspaceToolAccessSettings
            {
                CanReadStorage = true,
                AllowAllStorageCatalogs = true
            },
            contentDriver);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.BrowseStorage(storage.Id));

        Assert.Contains("required capability 'Read'", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, browseDriver.InvocationCount);
    }

    [Theory]
    [InlineData(StorageProviderKind.FileSystem, "docs/report..md", StorageLocatorKind.RelativePath, "docs/report..md")]
    [InlineData(StorageProviderKind.Ftp, "docs/readme.md", StorageLocatorKind.RemotePath, "docs/readme.md")]
    [InlineData(StorageProviderKind.Ipfs, "cid:bafy-readme", StorageLocatorKind.ContentAddress, "bafy-readme")]
    [InlineData(StorageProviderKind.Ipfs, "mfs:/docs/readme.md", StorageLocatorKind.RemotePath, "mfs:/docs/readme.md")]
    public async Task ReadStorageTextFile_BrowseEntryId_RoundTripsToProviderLocator(
        StorageProviderKind providerKind,
        string entryId,
        StorageLocatorKind expectedLocatorKind,
        string expectedLocator)
    {
        var storage = CreateStorage(providerKind: providerKind);
        var browseDriver = new RecordingBrowseDriver(providerKind: providerKind, entryId: entryId);
        var contentDriver = new RecordingStorageDriver(providerKind, StorageCapability.Read);
        var sut = CreatePlugin(
            storage,
            browseDriver,
            new AgentWorkspaceToolAccessSettings
            {
                CanReadStorage = true,
                AllowAllStorageCatalogs = true
            },
            contentDriver);

        var browse = await sut.BrowseStorage(storage.Id);
        var entry = Assert.Single(browse.Entries);
        var read = await sut.ReadStorageTextFile(storage.Id, entry.EntryId);

        Assert.Equal("content", read.Content);
        Assert.Equal(expectedLocatorKind, contentDriver.LastReadReference?.LocatorKind);
        Assert.Equal(expectedLocator, contentDriver.LastReadReference?.Locator);
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("docs/../escape.txt")]
    [InlineData("docs/./readme.txt")]
    public async Task ReadStorageTextFile_DotPathSegment_IsRejectedBeforeDriverRead(string locator)
    {
        var storage = CreateStorage();
        var contentDriver = new RecordingStorageDriver(storage.ProviderKind, StorageCapability.Read);
        var sut = CreatePlugin(
            storage,
            new RecordingBrowseDriver(),
            new AgentWorkspaceToolAccessSettings
            {
                CanReadStorage = true,
                AllowAllStorageCatalogs = true
            },
            contentDriver);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ReadStorageTextFile(storage.Id, locator));

        Assert.Contains("'.' or '..' segments", exception.Message, StringComparison.Ordinal);
        Assert.Null(contentDriver.LastReadReference);
    }

    [Fact]
    public async Task BrowseStorage_ReadOnlyAgent_DoesNotAdvertiseMutationCapabilities()
    {
        var storage = CreateStorage(StorageCapability.Read | StorageCapability.Write | StorageCapability.Delete);
        var browseDriver = new RecordingBrowseDriver(
            entryCapabilities: StorageBrowseEntryCapability.Read |
                               StorageBrowseEntryCapability.Write |
                               StorageBrowseEntryCapability.Delete);
        var contentDriver = new RecordingStorageDriver(
            storage.ProviderKind,
            StorageCapability.Read | StorageCapability.Write | StorageCapability.Delete);
        var sut = CreatePlugin(
            storage,
            browseDriver,
            new AgentWorkspaceToolAccessSettings
            {
                CanReadStorage = true,
                AllowAllStorageCatalogs = true
            },
            contentDriver);

        var result = await sut.BrowseStorage(storage.Id);

        var entry = Assert.Single(result.Entries);
        Assert.Equal(AgentStorageBrowseEntryCapability.Read, entry.Capabilities);
    }

    private static StorageRuntimePlugin CreatePlugin(
        StorageCatalogRecord storage,
        IStorageBrowseDriver browseDriver,
        AgentWorkspaceToolAccessSettings settings,
        IStorageDriver? contentDriver = null)
    {
        return new StorageRuntimePlugin(
            new StaticStorageCatalogService(storage),
            new StorageDriverRegistry([
                contentDriver ?? new RecordingStorageDriver(storage.ProviderKind, StorageCapability.Read)
            ]),
            new StorageBrowseDriverRegistry([browseDriver]),
            settings);
    }

    private static StorageCatalogRecord CreateStorage(
        StorageCapability capabilityMask = StorageCapability.Read,
        StorageProviderKind providerKind = StorageProviderKind.FileSystem)
    {
        return new StorageCatalogRecord
        {
            Id = Guid.NewGuid(),
            Name = "Documents",
            ProviderKind = providerKind,
            IsEnabled = true,
            CapabilityMask = capabilityMask
        };
    }

    private sealed class RecordingBrowseDriver(
        bool includeMetadataCapability = true,
        StorageProviderKind providerKind = StorageProviderKind.FileSystem,
        string entryId = "docs/readme.md",
        StorageBrowseEntryCapability entryCapabilities = StorageBrowseEntryCapability.Read) : IStorageBrowseDriver
    {
        public StorageProviderKind ProviderKind => providerKind;

        public StorageBrowseCapability Capabilities =>
            StorageBrowseCapability.Browse |
            StorageBrowseCapability.ProviderNativeOrdering |
            (includeMetadataCapability ? StorageBrowseCapability.Metadata : StorageBrowseCapability.None);

        public StorageBrowseWorkBudget MaximumBudget { get; } = StorageBrowseWorkBudget.Default;

        public int InvocationCount { get; private set; }

        public StorageBrowseRequest? LastRequest { get; private set; }

        public Task<StorageBrowsePage> BrowseAsync(
            StorageDriverInput storage,
            StorageBrowseRequest request,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            LastRequest = request;
            var entry = new StorageBrowseEntry(
                new StorageBrowseEntryId(entryId),
                request.Container,
                Path.GetFileName(entryId),
                entryId,
                StorageBrowseEntryKind.File,
                entryCapabilities,
                mediaType: "text/markdown");
            var page = new StorageBrowsePage(
                request.Container,
                [new StorageBrowsePathSegment(storage.Name, StorageBrowseContainer.Root)],
                [entry],
                request.Sort,
                StorageBrowseCompleteness.Complete,
                new StorageBrowseOperationMetrics(1, 1, 0, 0, TimeSpan.FromMilliseconds(1)),
                new StorageBrowseCursor("next-cursor"));
            return Task.FromResult(page);
        }
    }

    private sealed class RecordingStorageDriver(
        StorageProviderKind providerKind,
        StorageCapability supportedCapabilities) : IStorageDriver
    {
        public StorageProviderKind ProviderKind => providerKind;

        public StorageCapability SupportedCapabilities => supportedCapabilities;

        public StorageObjectReference? LastReadReference { get; private set; }

        public Task<StorageConnectionTestResult> TestConnectionAsync(
            StorageDriverInput storage,
            string? secretValue,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StorageWriteResult> SaveAsync(
            StorageDriverInput storage,
            StorageWriteRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(
            StorageDriverInput storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
        {
            LastReadReference = reference;
            return Task.FromResult<Stream>(new MemoryStream("content"u8.ToArray()));
        }

        public Task DeleteAsync(
            StorageDriverInput storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class StaticStorageCatalogService(StorageCatalogRecord storage) : IStorageCatalogService
    {
        private Task<IReadOnlyList<StorageCatalogRecord>> ReadRecordsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StorageCatalogRecord>>([storage]);

        private Task<StorageCatalogRecord?> ReadRecordAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == storage.Id ? storage : null);

        private Task<StorageCatalogRecord> ReadBootstrapRecordAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        private Task<StorageCatalogRecord> SaveRecordAsync(StorageCatalogRecord record, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        internal Task<IReadOnlyList<StorageRoutingRule>> ReadRoutingRecordsAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        private Task<StorageRoutingRule> SaveRoutingRecordAsync(StorageRoutingRule rule, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public async Task<IReadOnlyList<StorageCatalogSnapshot>> ListAsync(CancellationToken cancellationToken = default) =>
            (await ReadRecordsAsync(cancellationToken)).Select(StorageCatalogMapping.ToSnapshot).ToArray();

        public async Task<StorageCatalogSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            (await ReadRecordAsync(id, cancellationToken))?.ToSnapshot();

        public async Task<StorageDriverInput?> GetDriverAsync(Guid id, CancellationToken cancellationToken = default) =>
            (await ReadRecordAsync(id, cancellationToken))?.ToDriverInput();

        public async Task<StorageCatalogEditorSnapshot?> GetEditorAsync(Guid id, CancellationToken cancellationToken = default) {
            var row = await ReadRecordAsync(id, cancellationToken);
            return row is null ? null : new(row.ToSnapshot(), StorageJson.ParseProviderConfiguration(row.ConfigJson));
        }

        public async Task<StorageDriverInput> EnsureBootstrapFileSystemStorageAsync(CancellationToken cancellationToken = default) =>
            (await ReadBootstrapRecordAsync(cancellationToken)).ToDriverInput();

        public async Task<StorageCatalogSnapshot> SaveAsync(StorageCatalogSaveRequest request, CancellationToken cancellationToken = default) =>
            (await SaveRecordAsync(StorageCatalogMapping.CreateDraft(request), cancellationToken)).ToSnapshot();

        public async Task<IReadOnlyList<StorageRoutingRuleSnapshot>> ListRulesAsync(CancellationToken cancellationToken = default) =>
            (await ReadRoutingRecordsAsync(cancellationToken)).Select(StorageCatalogMapping.ToSnapshot).ToArray();

        public async Task<StorageRoutingRuleSnapshot> SaveRuleAsync(StorageRoutingRuleSaveRequest request, CancellationToken cancellationToken = default) =>
            (await SaveRoutingRecordAsync(StorageCatalogMapping.CreateDraft(request), cancellationToken)).ToSnapshot();

        public Task ApplyDefaultPurposesAsync(Guid storageId, IReadOnlyCollection<StorageUsagePurpose> defaultPurposes,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

    }
}
