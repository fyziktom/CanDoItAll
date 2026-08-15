using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Unit.Storage;

public sealed class FileToolsIntegrationBoundaryTests
{
    [Fact]
    public async Task Adapter_preserves_native_ordering_budgets_completeness_and_cursor()
    {
        var native = new FakeBrowseDriver(StorageProviderKind.FileSystem)
        {
            Page = CreateNativePage(StorageBrowseCompleteness.PartialInspectionLimit)
        };
        StorageFileBrowserProvider adapter = CreateAdapter(native);
        FileBrowserItem root = await adapter.GetRootAsync(FileBrowserMetadataRequest.Standard);

        FileBrowserPage page = await adapter.BrowseAsync(new FileBrowserBrowseRequest(
            root.Key,
            pageSize: 5,
            sort: new FileBrowserSortDescriptor(
                FileBrowserSortField.ProviderNative,
                FileBrowserSortDirection.Ascending,
                FoldersFirst: false)));

        StorageBrowseRequest request = Assert.IsType<StorageBrowseRequest>(native.LastRequest);
        Assert.Equal(StorageBrowseSort.ProviderOrder, request.Sort);
        Assert.Equal(10, request.Budget.MaximumReturnedItems);
        Assert.Equal(20, request.Budget.MaximumInspectedItems);
        Assert.Equal(10, request.Budget.MaximumMetadataProbes);
        Assert.Equal(1, request.Budget.MaximumConcurrentMetadataProbes);
        Assert.Equal(TimeSpan.FromSeconds(2), request.Budget.MaximumDuration);
        Assert.Equal(FileBrowserCompleteness.Partial, page.Completeness);
        Assert.Equal("next", page.NextContinuationToken);
        Assert.Equal("revision", page.ConsistencyToken);
        Assert.Single(page.Warnings);
        Assert.Equal(FileBrowserItemKind.Container, Assert.Single(page.Items).Kind);
    }

    [Fact]
    public async Task Adapter_unsupported_global_ordering_fails_before_native_io()
    {
        var native = new FakeBrowseDriver(StorageProviderKind.FileSystem);
        StorageFileBrowserProvider adapter = CreateAdapter(native);
        FileBrowserItem root = await adapter.GetRootAsync(FileBrowserMetadataRequest.Standard);

        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(() =>
            adapter.BrowseAsync(new FileBrowserBrowseRequest(
                root.Key,
                sort: new FileBrowserSortDescriptor(FileBrowserSortField.Name))).AsTask());

        Assert.Equal(FileBrowserErrorCode.Unsupported, exception.Error.Code);
        Assert.Equal(0, native.CallCount);
    }

    [Fact]
    public async Task Adapter_stale_native_cursor_maps_to_safe_typed_error()
    {
        var native = new FakeBrowseDriver(StorageProviderKind.FileSystem)
        {
            Failure = new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.SourceChanged,
                "secret-native-detail"))
        };
        StorageFileBrowserProvider adapter = CreateAdapter(native);
        FileBrowserItem root = await adapter.GetRootAsync(FileBrowserMetadataRequest.Standard);

        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(() =>
            adapter.BrowseAsync(new FileBrowserBrowseRequest(
                root.Key,
                pageSize: 5,
                sort: new FileBrowserSortDescriptor(
                    FileBrowserSortField.ProviderNative,
                    FoldersFirst: false))).AsTask());

        Assert.Equal(FileBrowserErrorCode.StaleCursor, exception.Error.Code);
        Assert.DoesNotContain("secret-native-detail", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Session_factory_rejects_duplicate_binding()
    {
        StorageCatalogRecord storage = CreateStorage();
        FileToolsStorageBinding binding = CreateBinding(storage.Id);
        var factory = CreateFactory(storage, [binding, binding]);

        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(() =>
            factory.CreateAsync(CreateScope()).AsTask());

        Assert.Equal(FileBrowserErrorCode.CorruptProviderResponse, exception.Error.Code);
    }

    [Fact]
    public async Task Session_factory_rejects_unbounded_source_set_before_catalog_access()
    {
        StorageCatalogRecord storage = CreateStorage();
        FileToolsStorageBinding binding = CreateBinding(storage.Id);
        StorageFileToolsBrowseSessionFactory factory = CreateFactory(
            storage,
            Enumerable.Repeat(binding, StorageBrowseCacheKeyBuilder.MaximumSourceCount + 1).ToArray());

        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(() =>
            factory.CreateAsync(CreateScope()).AsTask());

        Assert.Equal(FileBrowserErrorCode.InvalidOperation, exception.Error.Code);
    }

    [Fact]
    public async Task Composition_registration_resolves_and_creates_session_declaratively()
    {
        StorageCatalogRecord storage = CreateStorage();
        var services = new ServiceCollection();
        services.AddSingleton<IFileToolsStorageBindingProvider>(new FakeBindingProvider([CreateBinding(storage.Id)]));
        services.AddSingleton<IStorageCatalogService>(new FakeStorageCatalog(storage));
        services.AddSingleton<IStorageBrowseDriver>(new FakeBrowseDriver(StorageProviderKind.FileSystem));
        services.AddSingleton<IStorageBrowseDriverRegistry, StorageBrowseDriverRegistry>();
        AddSourceCapabilityDependencies(services);
        services.AddSingleton<IDatabaseRuntimeState, StaticDatabaseRuntimeState>();
        services.AddCanDoItAllFileToolsIntegration();
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();

        IFileToolsBrowseSessionFactory factory = scope.ServiceProvider.GetRequiredService<IFileToolsBrowseSessionFactory>();
        FileToolsBrowseSession session = await factory.CreateAsync(CreateScope());

        Assert.Single(session.Providers);
        Assert.Equal(FileBrowserSortField.ProviderNative, session.DefaultSort.Field);
        Assert.False(session.DefaultSort.FoldersFirst);
        Assert.Equal(FileBrowserSortField.ProviderNative, Assert.Single(session.Providers).Descriptor.SupportedSortFields.Single());
    }

    [Fact]
    public void Composition_current_binding_graph_is_scoped()
    {
        var services = new ServiceCollection();

        services.AddCanDoItAllFileToolsIntegration();

        Assert.Equal(
            ServiceLifetime.Scoped,
            Assert.Single(services, descriptor =>
                descriptor.ServiceType == typeof(IFileToolsStorageBindingProvider)).Lifetime);
        Assert.Equal(
            ServiceLifetime.Scoped,
            Assert.Single(services, descriptor =>
                descriptor.ServiceType == typeof(IStorageFileAccessAuthorizationCoordinator)).Lifetime);
        Assert.Equal(
            ServiceLifetime.Scoped,
            Assert.Single(services, descriptor =>
                descriptor.ServiceType == typeof(IFileToolsBrowseSessionFactory)).Lifetime);
    }

    [Fact]
    public void Composition_does_not_override_host_hybrid_cache_bounds()
    {
        var services = new ServiceCollection();
        services.AddHybridCache(options =>
        {
            options.MaximumKeyLength = 512;
            options.MaximumPayloadBytes = 32 * 1024 * 1024;
        });
        services.AddCanDoItAllFileToolsIntegration();
        using ServiceProvider provider = services.BuildServiceProvider();

        HybridCacheOptions options = provider.GetRequiredService<IOptions<HybridCacheOptions>>().Value;

        Assert.Equal(512, options.MaximumKeyLength);
        Assert.Equal(32 * 1024 * 1024, options.MaximumPayloadBytes);
    }

    [Fact]
    public async Task Composition_duplicate_semantic_scope_owners_fail_closed()
    {
        StorageCatalogRecord storage = CreateStorage();
        var services = new ServiceCollection();
        services.AddSingleton<IFileToolsStorageBindingSource>(
            new FakeBindingSource(FileToolsSemanticScopeKind.Project, [CreateBinding(storage.Id)]));
        services.AddSingleton<IFileToolsStorageBindingSource>(
            new FakeBindingSource(FileToolsSemanticScopeKind.Project, [CreateBinding(storage.Id)]));
        services.AddSingleton<IStorageCatalogService>(new FakeStorageCatalog(storage));
        services.AddSingleton<IStorageBrowseDriver>(new FakeBrowseDriver(StorageProviderKind.FileSystem));
        services.AddSingleton<IStorageBrowseDriverRegistry, StorageBrowseDriverRegistry>();
        AddSourceCapabilityDependencies(services);
        services.AddSingleton<IDatabaseRuntimeState, StaticDatabaseRuntimeState>();
        services.AddCanDoItAllFileToolsIntegration();
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();

        IFileToolsBrowseSessionFactory factory = scope.ServiceProvider.GetRequiredService<IFileToolsBrowseSessionFactory>();
        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(
            () => factory.CreateAsync(CreateScope()).AsTask());

        Assert.Equal(FileBrowserErrorCode.CorruptProviderResponse, exception.Error.Code);
    }

    [Fact]
    public async Task Composition_independent_scope_source_extends_without_coordinator_change()
    {
        Guid projectStorageId = Guid.NewGuid();
        Guid processStorageId = Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddSingleton<IFileToolsStorageBindingSource>(
            new FakeBindingSource(FileToolsSemanticScopeKind.Project, [CreateBinding(projectStorageId)]));
        services.AddSingleton<IFileToolsStorageBindingSource>(
            new FakeBindingSource(FileToolsSemanticScopeKind.ProcessRun, [CreateBinding(processStorageId)]));
        services.AddCanDoItAllFileToolsIntegration();
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();
        IFileToolsStorageBindingProvider bindings = scope.ServiceProvider.GetRequiredService<IFileToolsStorageBindingProvider>();

        IReadOnlyList<FileToolsStorageBinding> projectBindings = await bindings.ResolveAsync(CreateScope());
        IReadOnlyList<FileToolsStorageBinding> processBindings = await bindings.ResolveAsync(new FileToolsSemanticScope(
            FileToolsSemanticScopeKind.ProcessRun,
            new FileToolsSemanticScopeId("run-1"),
            "Run one"));

        Assert.Equal(projectStorageId, Assert.Single(projectBindings).StorageId);
        Assert.Equal(processStorageId, Assert.Single(processBindings).StorageId);
    }

    [Fact]
    public async Task Session_factory_rejects_unknown_storage()
    {
        StorageCatalogRecord storage = CreateStorage();
        var factory = CreateFactory(storage, [CreateBinding(Guid.NewGuid())]);

        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(() =>
            factory.CreateAsync(CreateScope()).AsTask());

        Assert.Equal(FileBrowserErrorCode.NotFound, exception.Error.Code);
    }

    [Fact]
    public void Adapter_rejects_mismatched_provider()
    {
        StorageCatalogRecord storage = CreateStorage();
        var driver = new FakeBrowseDriver(StorageProviderKind.Ftp);

        FileBrowserProviderException exception = Assert.Throws<FileBrowserProviderException>(() =>
            new StorageFileBrowserProvider(CreateScope(), CreateBinding(storage.Id), storage, driver));

        Assert.Equal(FileBrowserErrorCode.CorruptProviderResponse, exception.Error.Code);
    }

    [Fact]
    public async Task Adapter_does_not_grant_content_authority()
    {
        var native = new FakeBrowseDriver(StorageProviderKind.FileSystem)
        {
            Page = CreateNativeFilePage()
        };
        StorageFileBrowserProvider adapter = CreateAdapter(native);
        FileBrowserItem root = await adapter.GetRootAsync(FileBrowserMetadataRequest.Standard);

        FileBrowserPage page = await adapter.BrowseAsync(new FileBrowserBrowseRequest(
            root.Key,
            pageSize: 5,
            sort: new FileBrowserSortDescriptor(
                FileBrowserSortField.ProviderNative,
                FoldersFirst: false)));

        Assert.False(Assert.Single(page.Items).Capabilities.HasFlag(FileBrowserItemCapabilities.Open));
    }

    private static StorageFileBrowserProvider CreateAdapter(FakeBrowseDriver driver)
    {
        StorageCatalogRecord storage = CreateStorage();
        return new StorageFileBrowserProvider(CreateScope(), CreateBinding(storage.Id), storage, driver);
    }

    private static StorageFileToolsBrowseSessionFactory CreateFactory(
        StorageCatalogRecord storage,
        IReadOnlyList<FileToolsStorageBinding> bindings)
    {
        var driver = new FakeBrowseDriver(storage.ProviderKind);
        return new StorageFileToolsBrowseSessionFactory(
            new FakeBindingProvider(bindings),
            new FakeStorageCatalog(storage),
            new StorageBrowseDriverRegistry([driver]),
            new StorageDriverRegistry([]),
            new FileSystemStoragePathPolicy(new StaticWorkspacePathResolver()),
            new DisabledCacheStore(),
            new ProcessLocalFileCatalogRevisionService(),
            new StaticDatabaseRuntimeState());
    }

    private static void AddSourceCapabilityDependencies(IServiceCollection services)
    {
        services.AddSingleton<IStorageDriverRegistry>(new StorageDriverRegistry([]));
        services.AddSingleton<IWorkspacePathResolver, StaticWorkspacePathResolver>();
        services.AddSingleton<FileSystemStoragePathPolicy>();
    }

    private static FileToolsSemanticScope CreateScope()
        => new(FileToolsSemanticScopeKind.Project, new FileToolsSemanticScopeId("project-1"), "Project one");

    private static FileToolsStorageBinding CreateBinding(Guid storageId)
        => new(
            storageId,
            "Project files",
            new FileToolsBrowseWorkLimits(
                maximumReturnedItems: 10,
                maximumInspectedItems: 20,
                maximumMetadataProbes: 10,
                maximumConcurrentMetadataProbes: 1,
                maximumDuration: TimeSpan.FromSeconds(2)));

    private static StorageCatalogRecord CreateStorage()
        => new()
        {
            Id = Guid.NewGuid(),
            Name = "Files",
            ProviderKind = StorageProviderKind.FileSystem,
            EndpointOrRoot = "test-root"
        };

    private static StorageBrowsePage CreateNativePage(StorageBrowseCompleteness completeness)
    {
        StorageBrowseEntry[] entries =
        [
            new(
                new StorageBrowseEntryId("folder"),
                StorageBrowseContainer.Root,
                "Folder",
                "Folder",
                StorageBrowseEntryKind.Container,
                StorageBrowseEntryCapability.Browse,
                size: 0)
        ];
        return new StorageBrowsePage(
            StorageBrowseContainer.Root,
            [],
            entries,
            StorageBrowseSort.ProviderOrder,
            completeness,
            new StorageBrowseOperationMetrics(1, 2, 1, 4, TimeSpan.FromMilliseconds(1)),
            new StorageBrowseCursor("next"),
            new StorageBrowseConsistencyToken("revision"));
    }

    private static StorageBrowsePage CreateNativeFilePage()
        => new(
            StorageBrowseContainer.Root,
            [],
            [
                new StorageBrowseEntry(
                    new StorageBrowseEntryId("file"),
                    StorageBrowseContainer.Root,
                    "readme.md",
                    "readme.md",
                    StorageBrowseEntryKind.File,
                    StorageBrowseEntryCapability.Read,
                    size: 10,
                    mediaType: "text/markdown")
            ],
            StorageBrowseSort.ProviderOrder,
            StorageBrowseCompleteness.Complete,
            new StorageBrowseOperationMetrics(1, 1, 0, 0, TimeSpan.Zero));

    private sealed class FakeBrowseDriver(StorageProviderKind providerKind) : IStorageBrowseDriver
    {
        public StorageProviderKind ProviderKind => providerKind;

        public StorageBrowseCapability Capabilities =>
            StorageBrowseCapability.Browse |
            StorageBrowseCapability.ProviderNativeOrdering |
            StorageBrowseCapability.Metadata;

        public StorageBrowseWorkBudget MaximumBudget { get; } = new(
            maximumReturnedItems: 50,
            maximumInspectedItems: 100,
            maximumMetadataProbes: 50,
            maximumConcurrentMetadataProbes: 2,
            maximumDuration: TimeSpan.FromSeconds(10));

        public StorageBrowsePage Page { get; set; } = new(
            StorageBrowseContainer.Root,
            [],
            [],
            StorageBrowseSort.ProviderOrder,
            StorageBrowseCompleteness.Complete,
            new StorageBrowseOperationMetrics(0, 0, 0, 0, TimeSpan.Zero));

        public StorageBrowseException? Failure { get; set; }

        public StorageBrowseRequest? LastRequest { get; private set; }

        public int CallCount { get; private set; }

        public Task<StorageBrowsePage> BrowseAsync(
            StorageCatalogRecord storage,
            StorageBrowseRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            return Failure is null ? Task.FromResult(Page) : Task.FromException<StorageBrowsePage>(Failure);
        }
    }

    private sealed class FakeBindingProvider(IReadOnlyList<FileToolsStorageBinding> bindings)
        : IFileToolsStorageBindingProvider
    {
        public ValueTask<IReadOnlyList<FileToolsStorageBinding>> ResolveAsync(
            FileToolsSemanticScope scope,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(bindings);
    }

    private sealed class FakeBindingSource(
        FileToolsSemanticScopeKind scopeKind,
        IReadOnlyList<FileToolsStorageBinding> bindings) : IFileToolsStorageBindingSource
    {
        public FileToolsSemanticScopeKind ScopeKind => scopeKind;

        public ValueTask<IReadOnlyList<FileToolsStorageBinding>> ResolveAsync(
            FileToolsSemanticScope scope,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(bindings);
    }

    private sealed class FakeStorageCatalog(StorageCatalogRecord storage) : IStorageCatalogService
    {
        public Task<IReadOnlyList<StorageCatalogRecord>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StorageCatalogRecord>>([storage]);

        public Task<StorageCatalogRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<StorageCatalogRecord?>(id == storage.Id ? storage : null);

        public Task<StorageCatalogRecord> EnsureBootstrapFileSystemStorageAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(storage);

        public Task<StorageCatalogRecord> SaveAsync(
            StorageCatalogRecord record,
            CancellationToken cancellationToken = default) => Task.FromResult(record);

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<StorageRoutingRule>> ListRulesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StorageRoutingRule>>([]);

        public Task<StorageRoutingRule> SaveRuleAsync(
            StorageRoutingRule rule,
            CancellationToken cancellationToken = default) => Task.FromResult(rule);
    }

    private sealed class DisabledCacheStore : IStorageBrowseCacheStore
    {
        public ValueTask<StorageBrowsePage> GetOrCreateAsync(
            string key,
            Guid partitionId,
            StorageBrowseCacheSettings settings,
            Func<CancellationToken, ValueTask<StorageBrowsePage>> factory,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("Disabled caching must not call the cache store.");

        public void RecordBypass()
            => throw new InvalidOperationException("Disabled caching must not record a cache bypass.");
    }

    private sealed class StaticDatabaseRuntimeState : IDatabaseRuntimeState
    {
        public DatabaseRuntimeSnapshot GetSnapshot() => new(null, null, 0);

        public void MarkCurrentProfile(CanDoItAll.Infrastructure.ControlPlane.ResolvedDatabaseProfile profile)
            => throw new NotSupportedException();
    }

    private sealed class StaticWorkspacePathResolver : IWorkspacePathResolver
    {
        private readonly string root = Directory.GetCurrentDirectory();

        public string ResolveWorkspaceRoot() => root;

        public string ResolveManagedFilesRoot() => Path.Combine(root, "managed-files");

        public string ResolveExportsRoot() => Path.Combine(root, "exports");

        public string ResolveEvidenceRoot() => Path.Combine(root, "evidence");

        public string ResolveManagerArtifactsRoot() => Path.Combine(root, "manager-artifacts");
    }
}
