using CanDoItAll.FileTools.Integration;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class StorageBrowseCacheTests
{
    [Fact]
    public async Task Disabled_cache_performs_zero_cache_interaction()
    {
        var inner = new RecordingBrowseDriver();
        var driver = new CachingStorageBrowseDriver(
            inner,
            CreateContext(),
            new StorageBrowseCachePolicy(false, new StorageBrowseCacheSettings()),
            new ThrowingCacheStore(),
            new ProcessLocalFileCatalogRevisionService(),
            new MutableRuntimeState());

        await driver.BrowseAsync(CreateStorage(), CreateRequest());
        await driver.BrowseAsync(CreateStorage(), CreateRequest());

        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public async Task Memory_cache_coalesces_and_caches_same_revisioned_key()
    {
        await using CacheHarness cache = CreateCache();
        var inner = new RecordingBrowseDriver { Block = true };
        CachingStorageBrowseDriver driver = CreateDriver(inner, cache);

        Task<StorageBrowsePage> first = driver.BrowseAsync(CreateStorage(), CreateRequest());
        await inner.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task<StorageBrowsePage> second = driver.BrowseAsync(CreateStorage(), CreateRequest());
        await Task.Delay(50);
        Assert.Equal(1, inner.CallCount);
        inner.Release.TrySetResult();
        await Task.WhenAll(first, second);
        await driver.BrowseAsync(CreateStorage(), CreateRequest());

        Assert.Equal(1, inner.CallCount);
        StorageBrowseCacheMetricsSnapshot metrics = cache.Metrics.GetSnapshot();
        Assert.Equal(1, metrics.Misses);
        Assert.Equal(2, metrics.Hits);
        Assert.Equal(1, metrics.RetainedEntries);
    }

    [Fact]
    public async Task Key_isolates_scope_runtime_query_and_revision()
    {
        await using CacheHarness cache = CreateCache();
        var inner = new RecordingBrowseDriver();
        var runtime = new MutableRuntimeState();
        var revisions = new ProcessLocalFileCatalogRevisionService();
        FileToolsSemanticScope firstScope = CreateScope("project-a");
        FileToolsSemanticScope secondScope = CreateScope("project-b");
        CachingStorageBrowseDriver first = CreateDriver(inner, cache, runtime, revisions, firstScope);
        CachingStorageBrowseDriver second = CreateDriver(inner, cache, runtime, revisions, secondScope);

        await first.BrowseAsync(CreateStorage(), CreateRequest("one"));
        await first.BrowseAsync(CreateStorage(), CreateRequest("one"));
        await first.BrowseAsync(CreateStorage(), CreateRequest("two"));
        await second.BrowseAsync(CreateStorage(), CreateRequest("one"));
        runtime.Generation = 2;
        await second.BrowseAsync(CreateStorage(), CreateRequest("one"));
        revisions.PublishScopeChanged(secondScope, StorageId);
        await second.BrowseAsync(CreateStorage(), CreateRequest("one"));
        StorageBrowseCacheContext changedSource = CreateContext(secondScope) with
        {
            SourceSetFingerprint = "changed-source-set"
        };
        var sourceChanged = new CachingStorageBrowseDriver(
            inner,
            changedSource,
            new StorageBrowseCachePolicy(true, CreateSettings()),
            cache.Store,
            revisions,
            runtime);
        await sourceChanged.BrowseAsync(CreateStorage(), CreateRequest("one"));

        Assert.Equal(6, inner.CallCount);
    }

    [Fact]
    public async Task Bounds_evict_oldest_entry_and_expose_metrics()
    {
        var settings = CreateSettings(maximumEntries: 2, maximumItems: 2);
        await using CacheHarness cache = CreateCache();
        var inner = new RecordingBrowseDriver();
        CachingStorageBrowseDriver driver = CreateDriver(inner, cache, settings: settings);

        await driver.BrowseAsync(CreateStorage(), CreateRequest("one"));
        cache.Time.Advance(TimeSpan.FromSeconds(1));
        await driver.BrowseAsync(CreateStorage(), CreateRequest("two"));
        cache.Time.Advance(TimeSpan.FromSeconds(1));
        await driver.BrowseAsync(CreateStorage(), CreateRequest("three"));
        await driver.BrowseAsync(CreateStorage(), CreateRequest("one"));

        StorageBrowseCacheMetricsSnapshot metrics = cache.Metrics.GetSnapshot();
        Assert.Equal(4, inner.CallCount);
        Assert.True(metrics.Evictions >= 1);
        Assert.InRange(metrics.RetainedEntries, 0, 2);
        Assert.InRange(metrics.RetainedItems, 0, 2);
        Assert.InRange(metrics.RetainedBytes, 0, settings.MaximumRetainedBytes);
    }

    [Fact]
    public async Task Cancelled_and_failed_loads_are_not_retained()
    {
        await using CacheHarness cache = CreateCache();
        var inner = new RecordingBrowseDriver { Block = true };
        CachingStorageBrowseDriver driver = CreateDriver(inner, cache);
        using var cancellation = new CancellationTokenSource();
        Task<StorageBrowsePage> cancelled = driver.BrowseAsync(
            CreateStorage(),
            CreateRequest(),
            cancellation.Token);
        await inner.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        Assert.Equal(0, cache.Metrics.GetSnapshot().RetainedEntries);

        inner.Block = false;
        inner.Failure = new IOException("provider failed");
        await Assert.ThrowsAsync<IOException>(() => driver.BrowseAsync(CreateStorage(), CreateRequest()));
        inner.Failure = null;
        await driver.BrowseAsync(CreateStorage(), CreateRequest());

        Assert.Equal(3, inner.CallCount);
        Assert.Equal(1, cache.Metrics.GetSnapshot().RetainedEntries);
    }

    [Fact]
    public void Mutable_root_cannot_claim_provider_immutable_policy()
    {
        StorageBrowseCacheSettings settings = CreateSettings();
        settings.ImmutableVersionPolicy = StorageBrowseImmutableVersionPolicy.RequireProviderVerifiedVersion;
        StorageCatalogRecord storage = CreateStorage(StorageProviderKind.Ipfs, settings);
        var binding = new FileToolsStorageBinding(
            StorageId,
            "MFS",
            new FileToolsBrowseWorkLimits(),
            new FileToolsStorageRoot("mfs:/mutable"));
        var driver = new RecordingBrowseDriver(StorageProviderKind.Ipfs)
        {
            AdvertiseImmutableVersion = true
        };

        StorageBrowseException exception = Assert.Throws<StorageBrowseException>(() =>
            StorageBrowseCachePolicy.Resolve(storage, binding, driver));

        Assert.Equal(StorageBrowseErrorCode.InvalidConfiguration, exception.Error.Code);
    }

    [Fact]
    public async Task Cached_listing_never_authorizes_a_stale_occurrence()
    {
        await using CacheHarness cache = CreateCache();
        var inner = new RecordingBrowseDriver();
        FileToolsSemanticScope scope = CreateScope("project-a");
        StorageCatalogRecord storage = CreateStorage();
        FileToolsStorageBinding binding = CreateContext(scope).Binding;
        var listing = CreateDriver(inner, cache, scope: scope);
        var provider = new StorageFileBrowserProvider(scope, binding, storage, listing, inner);
        FileBrowserItem root = await provider.GetRootAsync(FileBrowserMetadataRequest.Standard);
        FileBrowserPage page = await provider.BrowseAsync(new FileBrowserBrowseRequest(
            root.Key,
            pageSize: 1,
            sort: new FileBrowserSortDescriptor(
                FileBrowserSortField.ProviderNative,
                FoldersFirst: false)));
        FileBrowserItem listed = Assert.Single(page.Items);
        inner.IncludeEntry = false;

        await Assert.ThrowsAsync<FileAccessDeniedException>(() => provider.AuthorizeItemAsync(
            listed.Key,
            new FileAccessContext(
                new FileAccessActorId("actor"),
                new FileAccessSessionId("session"),
                Guid.NewGuid(),
                runtimeGeneration: 1,
                authorizationRevision: 0,
                new FileAccessCorrelationId("correlation")),
            scope,
            FileAccessOperation.View,
            new ThrowingAuthorizationCoordinator()).AsTask());

        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public async Task Ttl_expiry_reloads_from_provider()
    {
        StorageBrowseCacheSettings settings = CreateSettings();
        settings.TimeToLive = TimeSpan.FromSeconds(1);
        settings.MaximumLifetime = TimeSpan.FromSeconds(1);
        await using CacheHarness cache = CreateCache();
        var inner = new RecordingBrowseDriver();
        CachingStorageBrowseDriver driver = CreateDriver(inner, cache, settings: settings);

        await driver.BrowseAsync(CreateStorage(), CreateRequest());
        await Task.Delay(TimeSpan.FromMilliseconds(1_200));
        await driver.BrowseAsync(CreateStorage(), CreateRequest());

        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public async Task Successful_semantic_revision_selects_new_listing()
    {
        await using CacheHarness cache = CreateCache();
        var inner = new RecordingBrowseDriver { EntryName = "before.txt" };
        var revisions = new ProcessLocalFileCatalogRevisionService();
        FileToolsSemanticScope scope = CreateScope("aggregate");
        CachingStorageBrowseDriver driver = CreateDriver(inner, cache, revisions: revisions, scope: scope);

        StorageBrowsePage before = await driver.BrowseAsync(CreateStorage(), CreateRequest());
        inner.EntryName = "after.txt";
        StorageBrowsePage stillCached = await driver.BrowseAsync(CreateStorage(), CreateRequest());
        revisions.PublishScopeChanged(scope, StorageId);
        StorageBrowsePage after = await driver.BrowseAsync(CreateStorage(), CreateRequest());

        Assert.Equal("before.txt", Assert.Single(before.Entries).Name);
        Assert.Equal("before.txt", Assert.Single(stillCached.Entries).Name);
        Assert.Equal("after.txt", Assert.Single(after.Entries).Name);
        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public async Task Oversized_payload_bypasses_retention()
    {
        StorageBrowseCacheSettings settings = CreateSettings();
        settings.MaximumPayloadBytes = 4096;
        await using CacheHarness cache = CreateCache();
        var inner = new RecordingBrowseDriver
        {
            EntryName = new string('x', 3000)
        };
        CachingStorageBrowseDriver driver = CreateDriver(inner, cache, settings: settings);

        await driver.BrowseAsync(CreateStorage(), CreateRequest());
        await driver.BrowseAsync(CreateStorage(), CreateRequest());

        Assert.Equal(2, inner.CallCount);
        Assert.Equal(2, cache.Metrics.GetSnapshot().Bypasses);
        Assert.Equal(0, cache.Metrics.GetSnapshot().RetainedEntries);
    }

    [Fact]
    public async Task Continuation_retention_limit_bypasses_continuation_page()
    {
        StorageBrowseCacheSettings settings = CreateSettings();
        settings.MaximumContinuations = 0;
        await using CacheHarness cache = CreateCache();
        var inner = new RecordingBrowseDriver { IncludeContinuation = true };
        CachingStorageBrowseDriver driver = CreateDriver(inner, cache, settings: settings);

        await driver.BrowseAsync(CreateStorage(), CreateRequest());
        await driver.BrowseAsync(CreateStorage(), CreateRequest());

        Assert.Equal(2, inner.CallCount);
        Assert.Equal(2, cache.Metrics.GetSnapshot().Bypasses);
        Assert.Equal(0, cache.Metrics.GetSnapshot().RetainedContinuations);
    }

    private static readonly Guid StorageId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static CachingStorageBrowseDriver CreateDriver(
        RecordingBrowseDriver inner,
        CacheHarness cache,
        MutableRuntimeState? runtime = null,
        ProcessLocalFileCatalogRevisionService? revisions = null,
        FileToolsSemanticScope? scope = null,
        StorageBrowseCacheSettings? settings = null)
        => new(
            inner,
            CreateContext(scope),
            new StorageBrowseCachePolicy(true, settings ?? CreateSettings()),
            cache.Store,
            revisions ?? new ProcessLocalFileCatalogRevisionService(),
            runtime ?? new MutableRuntimeState());

    private static StorageBrowseCacheContext CreateContext(FileToolsSemanticScope? scope = null)
    {
        var binding = new FileToolsStorageBinding(
            StorageId,
            "Project files",
            new FileToolsBrowseWorkLimits(),
            FileToolsStorageRoot.StorageRoot);
        return new StorageBrowseCacheContext(
            scope ?? CreateScope("project-a"),
            binding,
            CreateStorage(),
            StorageBrowseCacheKeyBuilder.BuildSourceSetFingerprint([binding]),
            StorageBrowseCacheKeyBuilder.BuildStorageFingerprint(CreateStorage(), binding));
    }

    private static FileToolsSemanticScope CreateScope(string id)
        => new(FileToolsSemanticScopeKind.Project, new FileToolsSemanticScopeId(id), id);

    private static StorageCatalogRecord CreateStorage(
        StorageProviderKind providerKind = StorageProviderKind.FileSystem,
        StorageBrowseCacheSettings? settings = null)
        => new()
        {
            Id = StorageId,
            Name = "Storage",
            ProviderKind = providerKind,
            ConfigJson = settings is null
                ? "{}"
                : StorageJson.SerializeProviderConfiguration(new StorageProviderConfiguration
                {
                    BrowseCache = settings
                })
        };

    private static StorageBrowseRequest CreateRequest(string container = "root")
        => new(
            new StorageBrowseContainer(container),
            pageSize: 1,
            metadata: StorageBrowseMetadataField.Size,
            budget: new StorageBrowseWorkBudget(
                maximumReturnedItems: 1,
                maximumInspectedItems: 2,
                maximumMetadataProbes: 1,
                maximumConcurrentMetadataProbes: 1));

    private static StorageBrowseCacheSettings CreateSettings(
        int maximumEntries = 16,
        int maximumItems = 32)
        => new()
        {
            Enabled = true,
            Mode = StorageBrowseCacheMode.Memory,
            TimeToLive = TimeSpan.FromMinutes(5),
            MaximumLifetime = TimeSpan.FromHours(1),
            MaximumPageSize = 1,
            MaximumItems = maximumItems,
            MaximumEntries = maximumEntries,
            MaximumContinuations = maximumEntries,
            MaximumPayloadBytes = 64 * 1024,
            MaximumRetainedBytes = 256 * 1024
        };

    private static CacheHarness CreateCache()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache(options =>
        {
            options.MaximumKeyLength = 128;
            options.MaximumPayloadBytes = StorageBrowseCacheSettings.AbsoluteMaximumPayloadBytes;
        });
        ServiceProvider provider = services.BuildServiceProvider();
        var metrics = new StorageBrowseCacheMetrics();
        var time = new MutableTimeProvider();
        var store = new HybridStorageBrowseCacheStore(
            provider.GetRequiredService<HybridCache>(),
            time,
            metrics,
            NullLogger<HybridStorageBrowseCacheStore>.Instance);
        return new CacheHarness(provider, store, metrics, time);
    }

    private sealed class RecordingBrowseDriver(StorageProviderKind providerKind = StorageProviderKind.FileSystem)
        : IStorageBrowseDriver
    {
        public StorageProviderKind ProviderKind => providerKind;

        public StorageBrowseCapability Capabilities =>
            StorageBrowseCapability.Browse |
            StorageBrowseCapability.ProviderNativeOrdering |
            StorageBrowseCapability.Metadata |
            (AdvertiseImmutableVersion ? StorageBrowseCapability.ImmutableVersion : StorageBrowseCapability.None);

        public StorageBrowseWorkBudget MaximumBudget { get; } = new(
            maximumReturnedItems: 10,
            maximumInspectedItems: 20,
            maximumMetadataProbes: 10,
            maximumConcurrentMetadataProbes: 2);

        public bool AdvertiseImmutableVersion { get; set; }

        public bool Block { get; set; }

        public Exception? Failure { get; set; }

        public bool IncludeEntry { get; set; } = true;

        public bool IncludeContinuation { get; set; }

        public string EntryName { get; set; } = "item.txt";

        public int CallCount { get; private set; }

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<StorageBrowsePage> BrowseAsync(
            StorageCatalogRecord storage,
            StorageBrowseRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Started.TrySetResult();
            if (Block)
            {
                await Release.Task.WaitAsync(cancellationToken);
            }

            if (Failure is not null)
            {
                throw Failure;
            }

            StorageBrowseEntry[] entries = IncludeEntry
                ?
                [
                    new StorageBrowseEntry(
                        new StorageBrowseEntryId($"{request.Container.Key}-item"),
                        request.Container,
                        EntryName,
                        EntryName,
                        StorageBrowseEntryKind.File,
                        StorageBrowseEntryCapability.Read,
                        size: 4)
                ]
                : [];
            return new StorageBrowsePage(
                request.Container,
                [],
                entries,
                StorageBrowseSort.ProviderOrder,
                StorageBrowseCompleteness.Complete,
                new StorageBrowseOperationMetrics(
                    entries.Length,
                    entries.Length,
                    entries.Length,
                    0,
                    TimeSpan.Zero),
                IncludeContinuation ? new StorageBrowseCursor("next") : null);
        }
    }

    private sealed class ThrowingAuthorizationCoordinator : IStorageFileAccessAuthorizationCoordinator
    {
        public ValueTask<FileReference> GrantAsync(
            FileAccessGrantRequest request,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("A missing current occurrence must fail before grant.");

        public ValueTask<AuthorizedStorageFile> ResolveAsync(
            FileReference file,
            FileAccessContext context,
            FileAccessOperation operation,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask RevokeAsync(FileReference file, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask RevokeAllAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class ThrowingCacheStore : IStorageBrowseCacheStore
    {
        public ValueTask<StorageBrowsePage> GetOrCreateAsync(
            string key,
            Guid partitionId,
            StorageBrowseCacheSettings settings,
            Func<CancellationToken, ValueTask<StorageBrowsePage>> factory,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("Disabled caching touched the cache store.");

        public void RecordBypass()
            => throw new InvalidOperationException("Disabled caching recorded a bypass.");
    }

    private sealed class MutableRuntimeState : IDatabaseRuntimeState
    {
        public long Generation { get; set; } = 1;

        public DatabaseRuntimeSnapshot GetSnapshot()
            => new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "runtime", Generation);

        public void MarkCurrentProfile(ResolvedDatabaseProfile profile)
            => throw new NotSupportedException();
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = DateTimeOffset.Parse("2026-07-13T00:00:00Z");

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }

    private sealed class CacheHarness(
        ServiceProvider services,
        HybridStorageBrowseCacheStore store,
        StorageBrowseCacheMetrics metrics,
        MutableTimeProvider time) : IAsyncDisposable
    {
        public HybridStorageBrowseCacheStore Store { get; } = store;

        public StorageBrowseCacheMetrics Metrics { get; } = metrics;

        public MutableTimeProvider Time { get; } = time;

        public async ValueTask DisposeAsync() => await services.DisposeAsync();
    }
}
