using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit.Infrastructure;

public sealed class ResourceFileSourceCatalogTests
{
    [Fact]
    public async Task Load_exposes_truthful_project_filesystem_ipfs_and_ftp_sources_only()
    {
        await using var fixture = await ResourceSourceFixture.CreateAsync();
        StorageCatalogRecord fileSystem = Storage("Filesystem", StorageProviderKind.FileSystem);
        StorageCatalogRecord ipfs = Storage("IPFS", StorageProviderKind.Ipfs);
        StorageCatalogRecord ftp = Storage("FTP", StorageProviderKind.Ftp);
        StorageCatalogRecord disabled = Storage("Disabled", StorageProviderKind.FileSystem);
        disabled.IsEnabled = false;
        StorageCatalogRecord writeOnly = Storage("Write only", StorageProviderKind.Ftp);
        writeOnly.CapabilityMask = StorageCapability.Write;
        var storageCatalog = new MutableStorageCatalog(fileSystem, ipfs, ftp, disabled, writeOnly);
        var catalog = new ResourceFileSourceCatalog(
            fixture.Factory,
            storageCatalog,
            new FakeBrowseDriverRegistry(
                StorageProviderKind.FileSystem,
                StorageProviderKind.Ipfs,
                StorageProviderKind.Ftp));

        ResourceFileSourceCatalogSnapshot snapshot = await catalog.LoadAsync();

        Assert.Collection(
            snapshot.Sources.OrderBy(source => source.SourceClass),
            source => Assert.Equal(ResourceFileSourceClass.Project, source.SourceClass),
            source => Assert.Equal(ResourceFileSourceClass.FileSystem, source.SourceClass),
            source => Assert.Equal(ResourceFileSourceClass.Ipfs, source.SourceClass),
            source => Assert.Equal(ResourceFileSourceClass.Ftp, source.SourceClass));
        Assert.DoesNotContain(snapshot.Sources, source => source.DisplayName is "Disabled" or "Write only");
        Assert.All(
            snapshot.Sources.Where(source => source.SourceClass != ResourceFileSourceClass.Project),
            source => Assert.Equal(FileToolsSemanticScopeKind.ResourceSource, source.Scope.Kind));
        Assert.Equal(FileToolsSemanticScopeKind.Project, snapshot.Sources.Single(source => source.SourceClass == ResourceFileSourceClass.Project).Scope.Kind);
        Assert.Equal(64, snapshot.Fingerprint.Length);
    }

    [Fact]
    public async Task Binding_rejects_stale_storage_configuration_before_browse_driver_use()
    {
        StorageCatalogRecord storage = Storage("Filesystem", StorageProviderKind.FileSystem);
        var storageCatalog = new MutableStorageCatalog(storage);
        var drivers = new FakeBrowseDriverRegistry(StorageProviderKind.FileSystem);
        var bindingSource = new ResourceFileToolsStorageBindingSource(storageCatalog, drivers);
        string originalFingerprint = ResourceStorageSourceScopeKey.BuildFingerprint(storage);
        var staleScope = new FileToolsSemanticScope(
            FileToolsSemanticScopeKind.ResourceSource,
            ResourceStorageSourceScopeKey.Create(storage.Id, originalFingerprint),
            storage.Name);
        storage.EndpointOrRoot = "changed-root";

        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(
            async () => await bindingSource.ResolveAsync(staleScope));

        Assert.Equal(FileBrowserErrorCode.Conflict, exception.Error.Code);
        Assert.Equal(0, drivers.ResolveCount);
    }

    [Fact]
    public async Task Binding_returns_one_storage_root_with_host_cache_policy_for_current_scope()
    {
        StorageCatalogRecord storage = Storage("IPFS", StorageProviderKind.Ipfs);
        var storageCatalog = new MutableStorageCatalog(storage);
        var drivers = new FakeBrowseDriverRegistry(StorageProviderKind.Ipfs);
        var bindingSource = new ResourceFileToolsStorageBindingSource(storageCatalog, drivers);
        var scope = new FileToolsSemanticScope(
            FileToolsSemanticScopeKind.ResourceSource,
            ResourceStorageSourceScopeKey.Create(
                storage.Id,
                ResourceStorageSourceScopeKey.BuildFingerprint(storage)),
            storage.Name);

        IReadOnlyList<FileToolsStorageBinding> bindings = await bindingSource.ResolveAsync(scope);

        FileToolsStorageBinding binding = Assert.Single(bindings);
        Assert.Equal(storage.Id, binding.StorageId);
        Assert.True(binding.Root.IsStorageRoot);
        Assert.Equal(FileToolsHostBrowseCacheMode.UseStoragePolicy, binding.HostCacheMode);
        Assert.Equal(1, drivers.ResolveCount);
    }

    private static StorageCatalogRecord Storage(string name, StorageProviderKind providerKind)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            ProviderKind = providerKind,
            IsEnabled = true,
            CapabilityMask = StorageCapability.Read,
            HealthStatus = StorageHealthStatus.Healthy,
            ConfigJson = "{}",
            UpdatedAtUtc = DateTimeOffset.Parse("2026-07-13T00:00:00Z")
        };

    private sealed class ResourceSourceFixture : IAsyncDisposable
    {
        private ResourceSourceFixture(TestDbContextFactory factory)
        {
            Factory = factory;
        }

        public TestDbContextFactory Factory { get; }

        public static async Task<ResourceSourceFixture> CreateAsync()
        {
            AppDbContextModelRegistry.ConfigureAssemblies(
                [typeof(Project).Assembly, typeof(ProjectResource).Assembly]);
            var options = AppDbContextTestOptionsBuilder.Create()
                .UseInMemoryDatabase($"resource-sources-{Guid.NewGuid():N}")
                .Options;
            var factory = new TestDbContextFactory(options);
            await using AppDbContext dbContext = factory.CreateDbContext();
            dbContext.Set<Project>().Add(new Project
            {
                Id = Guid.NewGuid(),
                Name = "Project files"
            });
            await dbContext.SaveChangesAsync();
            return new ResourceSourceFixture(factory);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class MutableStorageCatalog(params StorageCatalogRecord[] storages) : IStorageCatalogService
    {
        private readonly IReadOnlyList<StorageCatalogRecord> storages = storages;

        public Task<IReadOnlyList<StorageCatalogRecord>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(storages);

        public Task<StorageCatalogRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(storages.SingleOrDefault(storage => storage.Id == id));

        public Task<StorageCatalogRecord> EnsureBootstrapFileSystemStorageAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StorageCatalogRecord> SaveAsync(StorageCatalogRecord record, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<StorageRoutingRule>> ListRulesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StorageRoutingRule>>([]);

        public Task<StorageRoutingRule> SaveRuleAsync(StorageRoutingRule rule, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeBrowseDriverRegistry(params StorageProviderKind[] registeredKinds) : IStorageBrowseDriverRegistry
    {
        private readonly IReadOnlyDictionary<StorageProviderKind, IStorageBrowseDriver> drivers = registeredKinds
            .Distinct()
            .ToDictionary(kind => kind, kind => (IStorageBrowseDriver)new UnusedBrowseDriver(kind));

        public int ResolveCount { get; private set; }

        public IReadOnlyCollection<StorageProviderKind> RegisteredKinds => drivers.Keys.ToArray();

        public bool TryResolve(StorageProviderKind providerKind, out IStorageBrowseDriver driver)
        {
            ResolveCount++;
            return drivers.TryGetValue(providerKind, out driver!);
        }

        public IStorageBrowseDriver Resolve(StorageProviderKind providerKind)
            => throw new NotSupportedException();

        public IStorageBrowseSearchDriver ResolveSearch(StorageProviderKind providerKind)
            => throw new NotSupportedException();

        public IStorageBrowseStatDriver ResolveStat(StorageProviderKind providerKind)
            => throw new NotSupportedException();
    }

    private sealed class UnusedBrowseDriver(StorageProviderKind providerKind) : IStorageBrowseDriver
    {
        public StorageProviderKind ProviderKind { get; } = providerKind;

        public StorageBrowseCapability Capabilities => StorageBrowseCapability.None;

        public StorageBrowseWorkBudget MaximumBudget { get; } = StorageBrowseWorkBudget.Default;

        public Task<StorageBrowsePage> BrowseAsync(
            StorageCatalogRecord storage,
            StorageBrowseRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
