using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

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
            fixture.Projects,
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
        string originalFingerprint = ResourceStorageSourceScopeKey.BuildFingerprint(storage.ToSnapshot());
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
                ResourceStorageSourceScopeKey.BuildFingerprint(storage.ToSnapshot())),
            storage.Name);

        IReadOnlyList<FileToolsStorageBinding> bindings = await bindingSource.ResolveAsync(scope);

        FileToolsStorageBinding binding = Assert.Single(bindings);
        Assert.Equal(storage.Id, binding.StorageId);
        Assert.True(binding.Root.IsStorageRoot);
        Assert.Equal(FileToolsHostBrowseCacheMode.UseStoragePolicy, binding.HostCacheMode);
        Assert.Equal(1, drivers.ResolveCount);
    }

    [Theory]
    [InlineData("aabbccddeeff00112233445566778899")]
    [InlineData("AABBCCDDEEFF00112233445566778899")]
    [InlineData("AaBbCcDdEeFf00112233445566778899")]
    public void Parsed_project_and_storage_keys_equal_the_generated_canonical_keys(string digits)
    {
        Guid id = Guid.ParseExact(digits, "N");

        Assert.True(ResourceFileSourceKey.TryParse($"project:{digits}", out ResourceFileSourceKey projectKey));
        Assert.True(ResourceFileSourceKey.TryParse($"  storage:{digits}  ", out ResourceFileSourceKey storageKey));

        Assert.Equal(ResourceFileSourceKey.ForProject(id), projectKey);
        Assert.Equal(ResourceFileSourceKey.ForStorage(id), storageKey);
        Assert.Equal($"project:{id:N}", projectKey.Value);
        Assert.Equal($"storage:{id:N}", storageKey.Value);
        Assert.True(projectKey.TryGetProjectId(out Guid projectId));
        Assert.True(storageKey.TryGetStorageId(out Guid storageId));
        Assert.Equal(id, projectId);
        Assert.Equal(id, storageId);
        Assert.False(projectKey.TryGetStorageId(out _));
        Assert.False(storageKey.TryGetProjectId(out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("project:00000000000000000000000000000000")]
    [InlineData("storage:00000000000000000000000000000000")]
    [InlineData("Project:aabbccddeeff00112233445566778899")]
    [InlineData("STORAGE:aabbccddeeff00112233445566778899")]
    [InlineData("resource:aabbccddeeff00112233445566778899")]
    [InlineData("project:aabbccddeeff0011223344556677889")]
    [InlineData("project:aabbccddeeff001122334455667788990")]
    [InlineData("project:aabbccdd-eeff-0011-2233-445566778899")]
    [InlineData("project:zzbbccddeeff00112233445566778899")]
    [InlineData("aabbccddeeff00112233445566778899")]
    public void Invalid_keys_and_foreign_source_kinds_are_rejected(string? value)
    {
        Assert.False(ResourceFileSourceKey.TryParse(value, out ResourceFileSourceKey key));
        Assert.Equal(default, key);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Catalog_resolves_a_key_whose_guid_digits_use_another_case(bool storageSource)
    {
        await using var fixture = await ResourceSourceFixture.CreateAsync();
        StorageCatalogRecord fileSystem = Storage("Filesystem", StorageProviderKind.FileSystem);
        var catalog = new ResourceFileSourceCatalog(
            fixture.Projects,
            new MutableStorageCatalog(fileSystem),
            new FakeBrowseDriverRegistry(StorageProviderKind.FileSystem));
        ResourceFileSourceCatalogSnapshot snapshot = await catalog.LoadAsync();
        ResourceFileSourceDescriptor expected = snapshot.Sources.Single(source =>
            storageSource
                ? source.SourceClass == ResourceFileSourceClass.FileSystem
                : source.SourceClass == ResourceFileSourceClass.Project);
        string storedSpelling = expected.Key.Value[..8] + expected.Key.Value[8..].ToUpperInvariant();
        Assert.NotEqual(expected.Key.Value, storedSpelling);

        Assert.True(ResourceFileSourceKey.TryParse(storedSpelling, out ResourceFileSourceKey parsed));
        ResourceFileSourceDescriptor resolved = await catalog.ResolveAsync(parsed);

        Assert.Equal(expected.Key, resolved.Key);
        Assert.Equal(expected.SourceClass, resolved.SourceClass);
        Assert.Equal(expected.Scope, resolved.Scope);
    }

    [Fact]
    public async Task Catalog_keeps_denying_unknown_disabled_and_unregistered_sources()
    {
        await using var fixture = await ResourceSourceFixture.CreateAsync();
        StorageCatalogRecord disabled = Storage("Disabled", StorageProviderKind.FileSystem);
        disabled.IsEnabled = false;
        StorageCatalogRecord unregistered = Storage("FTP without driver", StorageProviderKind.Ftp);
        var catalog = new ResourceFileSourceCatalog(
            fixture.Projects,
            new MutableStorageCatalog(disabled, unregistered),
            new FakeBrowseDriverRegistry(StorageProviderKind.FileSystem));

        foreach (Guid storageId in new[] { disabled.Id, unregistered.Id, Guid.NewGuid() })
        {
            Assert.True(ResourceFileSourceKey.TryParse(
                $"storage:{storageId.ToString("N").ToUpperInvariant()}",
                out ResourceFileSourceKey key));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await catalog.ResolveAsync(key));
        }
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

    private sealed class ResourceSourceFixture(ProjectWriteSelectionQuery projects) : IAsyncDisposable {
        public ProjectWriteSelectionQuery Projects { get; } = projects;

        public static async Task<ResourceSourceFixture> CreateAsync() {
            var databaseName = $"resource-sources-{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<ProjectsDbContext>()
                .UseInMemoryDatabase(databaseName).Options;
            var factory = new PooledDbContextFactory<ProjectsDbContext>(options);
            await using var dbContext = factory.CreateDbContext();
            dbContext.Set<Project>().Add(new Project {
                Id = Guid.NewGuid(),
                Name = "Project files"
            });
            await dbContext.SaveChangesAsync();
            var profile = new ResolvedDatabaseProfile(new() { ProviderKind = DatabaseProviderKind.InMemory },
                DatabaseProfileResolutionSource.ExplicitOverride, databaseName);
            return new(new ProjectWriteSelectionQuery(factory, new CanonicalDatabase(profile)));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CanonicalDatabase(ResolvedDatabaseProfile profile) : ICanonicalRuntimeDatabase {
        public ResolvedDatabaseProfile Profile { get; } = profile;
        public long Generation => 1;
    }

    private sealed class MutableStorageCatalog(params StorageCatalogRecord[] storages) : IStorageCatalogService
    {
        private readonly IReadOnlyList<StorageCatalogRecord> storages = storages;

        private Task<IReadOnlyList<StorageCatalogRecord>> ReadRecordsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(storages);

        private Task<StorageCatalogRecord?> ReadRecordAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(storages.SingleOrDefault(storage => storage.Id == id));

        private Task<StorageCatalogRecord> ReadBootstrapRecordAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        private Task<StorageCatalogRecord> SaveRecordAsync(StorageCatalogRecord record, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        internal Task<IReadOnlyList<StorageRoutingRule>> ReadRoutingRecordsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StorageRoutingRule>>([]);

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
            StorageDriverInput storage,
            StorageBrowseRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
