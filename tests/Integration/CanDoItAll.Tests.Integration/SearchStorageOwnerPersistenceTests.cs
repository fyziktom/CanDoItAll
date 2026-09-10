using System.Data;
using System.Data.Common;
using System.Text.Json;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class SearchStorageOwnerPersistenceTests {
    [Fact]
    public async Task Runtime_models_match_the_complete_postgresql_schema_without_foreign_entities() {
        await using var application = await TestApplication.CreateAsync();
        await using var schema = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        await using var search = await application.Services.GetRequiredService<IDbContextFactory<SearchDbContext>>().CreateDbContextAsync();
        await using var storage = await application.Services.GetRequiredService<IDbContextFactory<StorageDbContext>>().CreateDbContextAsync();
        AssertModel(schema, search, [typeof(SearchDocument)]);
        AssertModel(schema, storage, [typeof(StorageCatalogRecord), typeof(StorageRoutingRule)]);
        Assert.Throws<InvalidOperationException>(() => search.Set<StorageRoutingRule>().ToQueryString());
        Assert.Throws<InvalidOperationException>(() => storage.Set<SearchDocument>().ToQueryString());
        await using var scope = application.Services.CreateAsyncScope();
        Assert.Same(scope.ServiceProvider.GetRequiredService<SearchIndexService>(),
            scope.ServiceProvider.GetRequiredService<ISearchIndexService>());
        Assert.Same(application.Services.GetRequiredService<StorageCatalogService>(),
            application.Services.GetRequiredService<IStorageCatalogService>());
    }

    [Fact]
    public async Task Legacy_search_routing_and_storage_records_survive_restart_and_remain_profile_bound() {
        await using var environment = CanDoItAllTestEnvironment.Create("search-storage-owner-restart");
        var profile = environment.CreatePostgreSqlProfile("original");
        var otherProfile = environment.CreatePostgreSqlProfile("other");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        var prefix = $"legacy-{Guid.NewGuid():N}";
        var timestamp = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var catalog = new StorageCatalogRecord {
            Id = Guid.NewGuid(), Name = prefix, ProviderKind = StorageProviderKind.Ftp,
            IsEnabled = false, IsReadOnly = true, DisplayOrder = 37, ConnectionMode = StorageConnectionMode.Remote,
            EndpointOrRoot = "ftps://storage.example.test/root", RootBindingFormatVersion = 2,
            RootHostBindingId = "historical-host-binding", RootPathState = HostBoundPathState.NeedsRebind,
            RootLastValidatedAtUtc = timestamp, ConfigJson = "{\"port\":2121,\"basePath\":\"historical\",\"metadataJson\":\"{\\\"preserve\\\":true}\"}",
            CapabilityMask = StorageCapability.Read | StorageCapability.Download,
            HealthStatus = StorageHealthStatus.Degraded, LastTestedAtUtc = timestamp,
            LastHealthMessage = "Retained historical status", CredentialSecretId = Guid.NewGuid(),
            CreatedAtUtc = timestamp, UpdatedAtUtc = timestamp
        };
        var rule = new StorageRoutingRule {
            Id = Guid.NewGuid(), Name = prefix + "-rule", IsEnabled = false, Priority = 71,
            ScopeKind = StorageRoutingScopeKind.Node, ProjectId = Guid.NewGuid(), NodeKey = "task:historical",
            UsagePurpose = StorageUsagePurpose.Evidence, ContentKind = StorageContentKind.Pdf,
            MimePattern = "application/pdf", MinimumContentLength = 123, MaximumContentLength = 456,
            EditIntent = true, PreviewRequired = true, PublishIntent = true,
            RequiredCapabilities = StorageCapability.Read | StorageCapability.Download,
            PreferredStorageId = catalog.Id, AlternativeStorageIdsJson = $"[\"{Guid.NewGuid():D}\"]",
            Reason = "Historical routing decision", CreatedAtUtc = timestamp, UpdatedAtUtc = timestamp
        };
        var document = new SearchDocument {
            Id = Guid.NewGuid(), SourceType = prefix, SourceKey = "historical", ProjectId = rule.ProjectId,
            Category = "Evidence", Title = prefix + " retained title", Summary = "Stored summary",
            Body = "Stored body and origin", Route = "/historical?key=retained", UpdatedAtUtc = timestamp
        };
        await using (var original = await TestApplication.CreateAsync(harness)) {
            await using var schema = await original.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            schema.AddRange(catalog, rule, document);
            await schema.SaveChangesAsync();
        }

        await using var restarted = await TestApplication.CreateAsync(harness);
        await using var scope = restarted.Services.CreateAsyncScope();
        var storageService = scope.ServiceProvider.GetRequiredService<StorageCatalogService>();
        var searchService = scope.ServiceProvider.GetRequiredService<SearchIndexService>();
        var reloaded = await storageService.GetAsync(catalog.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(JsonSerializer.Serialize(catalog), JsonSerializer.Serialize(reloaded));
        Assert.Equal(JsonSerializer.Serialize(rule), JsonSerializer.Serialize(
            Assert.Single(await storageService.ListRulesAsync(), item => item.Id == rule.Id)));
        Assert.Equal(document.Id, Assert.Single(await searchService.SearchAsync(prefix)).Id);
        await using (var search = await restarted.Services.GetRequiredService<IDbContextFactory<SearchDbContext>>().CreateDbContextAsync()) {
            Assert.Equal(JsonSerializer.Serialize(document), JsonSerializer.Serialize(
                await search.Set<SearchDocument>().SingleAsync(item => item.Id == document.Id)));
        }
        await searchService.UpsertAsync(new(document.SourceType, document.SourceKey, document.Category,
            "Updated through Search", document.Summary, document.Body, document.Route, document.ProjectId));
        reloaded.Name = prefix + "-updated";
        await storageService.SaveAsync(reloaded);
        await using (var schema = await restarted.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync()) {
            var savedStorage = await schema.Set<StorageCatalogRecord>().SingleAsync(item => item.Id == catalog.Id);
            Assert.Equal(reloaded.Name, savedStorage.Name);
            Assert.Equal(catalog.CreatedAtUtc, savedStorage.CreatedAtUtc);
            Assert.Equal(catalog.ConfigJson, savedStorage.ConfigJson);
            Assert.Equal(catalog.CredentialSecretId, savedStorage.CredentialSecretId);
            Assert.Equal("Updated through Search", (await schema.Set<SearchDocument>().SingleAsync(item => item.Id == document.Id)).Title);
        }

        await using var other = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = environment, ActiveProfile = otherProfile
        });
        await using var otherScope = other.Services.CreateAsyncScope();
        Assert.Null(await otherScope.ServiceProvider.GetRequiredService<StorageCatalogService>().GetAsync(catalog.Id));
        Assert.DoesNotContain(await otherScope.ServiceProvider.GetRequiredService<StorageCatalogService>().ListRulesAsync(), item => item.Id == rule.Id);
        Assert.Empty(await otherScope.ServiceProvider.GetRequiredService<SearchIndexService>().SearchAsync("Updated through Search"));
        Assert.Equal(reloaded.Name, (await storageService.GetAsync(catalog.Id))!.Name);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Staged_deletion_and_planning_share_the_exact_transaction_while_normal_reads_stay_independent(bool commit) {
        await using var application = await TestApplication.CreateAsync();
        var profile = application.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var coordinator = CoordinatedDatabaseTransaction.ForProfile(profile);
        var probe = new CommandProbe();
        var searchOptions = Options<SearchDbContext>(profile, probe);
        var storageOptions = Options<StorageDbContext>(profile, probe);
        var searchService = new SearchIndexService(new Factory<SearchDbContext>(searchOptions, static options => new(options)),
            new FixedClock(), searchOptions, coordinator);
        var storageService = new StorageCatalogService(new Factory<StorageDbContext>(storageOptions, static options => new(options)),
            new RejectingPaths(), new FixedClock(), storageOptions, coordinator);
        var projectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        var prefix = $"transaction-{Guid.NewGuid():N}";
        var catalog = new StorageCatalogRecord { Id = Guid.NewGuid(), Name = prefix, ProviderKind = StorageProviderKind.Ftp };
        SearchDocument[] documents = [
            Document("resource", prefix + "-owned-a", projectId),
            Document("resource", prefix + "-owned-b", projectId),
            Document(SearchDocument.ProjectSourceType, projectId.ToString(), null),
            Document(SearchDocument.ProjectSourceType, projectId.ToString("N"), null),
            Document("unrelated", projectId.ToString(), null),
            Document("other", prefix + "-other", otherProjectId)
        ];
        StorageRoutingRule[] rules = [
            Rule(StorageRoutingScopeKind.Project, projectId),
            Rule(StorageRoutingScopeKind.Node, projectId),
            Rule(StorageRoutingScopeKind.Workspace, projectId),
            Rule(StorageRoutingScopeKind.Project, otherProjectId),
            Rule(StorageRoutingScopeKind.Workspace, null)
        ];
        await using var owner = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        owner.Add(catalog);
        owner.AddRange(documents);
        owner.AddRange(rules);
        await owner.SaveChangesAsync();
        var documentIds = documents.Select(item => item.Id).ToArray();
        var ruleIds = rules.Select(item => item.Id).ToArray();
        await Assert.ThrowsAsync<InvalidOperationException>(() => searchService.DeleteProjectSearchForMutationAsync(projectId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => storageService.DeleteProjectRoutingForMutationAsync(projectId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => storageService.ListCatalogPlanningFactsForMutationAsync([]));

        await using var transaction = await owner.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        catalog.Name = prefix + "-uncommitted";
        await owner.SaveChangesAsync();
        probe.Commands.Clear();
        using (coordinator.Enter(owner)) {
            Assert.Equal(3, await searchService.DeleteProjectSearchForMutationAsync(projectId));
            Assert.Equal(3, await storageService.DeleteProjectRoutingForMutationAsync(projectId));
            var facts = await storageService.ListCatalogPlanningFactsForMutationAsync([]);
            Assert.Equal(catalog.Name, Assert.Single(facts, fact => fact.Id == catalog.Id).Name);
            Assert.NotEmpty(probe.Commands);
            Assert.Contains(probe.Commands, command => command.ContextType == typeof(SearchDbContext));
            Assert.Contains(probe.Commands, command => command.ContextType == typeof(StorageDbContext));
            Assert.All(probe.Commands, command => {
                Assert.Same(owner.Database.GetDbConnection(), command.Connection);
                Assert.Same(transaction.GetDbTransaction(), command.Transaction);
            });

            probe.Commands.Clear();
            var independentFacts = await storageService.ListCatalogPlanningFactsAsync([]);
            Assert.Equal(prefix, Assert.Single(independentFacts, fact => fact.Id == catalog.Id).Name);
            Assert.Equal(6, (await searchService.SearchAsync(prefix, 50)).Count);
            Assert.All(probe.Commands, command => {
                Assert.NotSame(owner.Database.GetDbConnection(), command.Connection);
                Assert.Null(command.Transaction);
            });
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => storageService.ListCatalogPlanningFactsForMutationAsync([]));
        if (commit) {
            await transaction.CommitAsync();
        } else {
            await transaction.RollbackAsync();
        }
        await using var searchReadback = new SearchDbContext(searchOptions);
        await using var storageReadback = new StorageDbContext(storageOptions);
        Assert.Equal(commit ? 3 : 6, await searchReadback.Set<SearchDocument>().CountAsync(item => documentIds.Contains(item.Id)));
        Assert.Equal(commit ? 2 : 5, await storageReadback.Set<StorageRoutingRule>().CountAsync(item => ruleIds.Contains(item.Id)));
        Assert.Equal(commit ? catalog.Name : prefix,
            (await storageReadback.Set<StorageCatalogRecord>().SingleAsync(item => item.Id == catalog.Id)).Name);

        SearchDocument Document(string sourceType, string sourceKey, Guid? attributedProject) => new() {
            SourceType = sourceType, SourceKey = sourceKey, ProjectId = attributedProject,
            Title = prefix, Category = "Owner transaction", Route = "/owner-transaction"
        };
        StorageRoutingRule Rule(StorageRoutingScopeKind kind, Guid? attributedProject) => new() {
            Name = prefix, ScopeKind = kind, ProjectId = attributedProject, PreferredStorageId = catalog.Id
        };
    }

    [Fact]
    public async Task Catalog_facts_preserve_all_host_rows_and_only_parse_referenced_ftp_configuration() {
        await using var application = await TestApplication.CreateAsync();
        var profile = application.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var coordinator = CoordinatedDatabaseTransaction.ForProfile(profile);
        var options = Options<StorageDbContext>(profile);
        var service = new StorageCatalogService(new Factory<StorageDbContext>(options, static value => new(value)),
            new RejectingPaths(), new FixedClock(), options, coordinator);
        var prefix = $"planning-{Guid.NewGuid():N}";
        var good = new StorageCatalogRecord {
            Id = Guid.NewGuid(), Name = prefix + "-good", ProviderKind = StorageProviderKind.Ftp,
            EndpointOrRoot = "ftps://storage.example.test/root", ConfigJson = "{\"port\":2121,\"basePath\":\"archive/subfolder\"}"
        };
        var malformed = new StorageCatalogRecord {
            Id = Guid.NewGuid(), Name = prefix + "-malformed", ProviderKind = StorageProviderKind.Ftp, ConfigJson = "{"
        };
        var host = new StorageCatalogRecord {
            Id = Guid.NewGuid(), Name = prefix + "-host", ProviderKind = StorageProviderKind.FileSystem,
            IsEnabled = false, IsSystemDefault = false, IsReadOnly = true, DisplayOrder = 19,
            EndpointOrRoot = "retained-host-root", RootBindingFormatVersion = 2,
            RootPlatformFamily = HostPlatformFamily.Linux, RootPathSyntax = PhysicalPathSyntax.UnixAbsolute,
            RootHostBindingId = "retained-host-id", RootPathState = HostBoundPathState.NeedsRebind,
            RootLastValidatedAtUtc = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            CapabilityMask = StorageCapability.Read, ConfigJson = "{"
        };
        await using var owner = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        owner.AddRange(good, malformed, host);
        await owner.SaveChangesAsync();
        var ids = new[] { good.Id, host.Id };
        var normal = await service.ListCatalogPlanningFactsAsync(ids);
        Assert.Equal(await owner.Set<StorageCatalogRecord>().OrderBy(item => item.Id).Select(item => item.Id).ToArrayAsync(),
            normal.OrderBy(fact => fact.Id).Select(fact => fact.Id).ToArray());
        Assert.Equal(new StorageFtpAddressingFact(2121, "archive/subfolder"), Assert.Single(normal, fact => fact.Id == good.Id).FtpAddressing);
        Assert.Null(Assert.Single(normal, fact => fact.Id == malformed.Id).FtpAddressing);
        var hostFact = Assert.Single(normal, fact => fact.Id == host.Id);
        Assert.Equal(host.EndpointOrRoot, hostFact.EndpointOrRoot);
        Assert.Equal(host.RootBindingFormatVersion, hostFact.RootBindingFormatVersion);
        Assert.Equal(host.RootPlatformFamily, hostFact.RootPlatformFamily);
        Assert.Equal(host.RootPathSyntax, hostFact.RootPathSyntax);
        Assert.Equal(host.ConnectionMode, hostFact.ConnectionMode);
        Assert.Equal(host.RootHostBindingId, hostFact.RootHostBindingId);
        Assert.Equal(host.RootPathState, hostFact.RootPathState);
        Assert.Equal(host.RootLastValidatedAtUtc, hostFact.RootLastValidatedAtUtc);
        Assert.Equal(host.DisplayOrder, hostFact.DisplayOrder);
        Assert.Equal(host.IsSystemDefault, hostFact.IsSystemDefault);
        Assert.Equal(host.CapabilityMask, hostFact.CapabilityMask);
        Assert.Equal(host.CreatedAtUtc, hostFact.CreatedAtUtc);
        Assert.Equal(host.UpdatedAtUtc, hostFact.UpdatedAtUtc);
        Assert.False(hostFact.IsEnabled);
        Assert.True(hostFact.IsReadOnly);
        Assert.Null(hostFact.FtpAddressing);
        await Assert.ThrowsAsync<JsonException>(() => service.ListCatalogPlanningFactsAsync([malformed.Id]));
        await using var transaction = await owner.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        using (coordinator.Enter(owner)) {
            var enlisted = await service.ListCatalogPlanningFactsForMutationAsync(ids);
            Assert.Equal(normal.OrderBy(fact => fact.Id).ToArray(), enlisted.OrderBy(fact => fact.Id).ToArray());
            await Assert.ThrowsAsync<JsonException>(() => service.ListCatalogPlanningFactsForMutationAsync([malformed.Id]));
        }
        await transaction.RollbackAsync();
        await using var readback = new StorageDbContext(options);
        Assert.Equal("{", (await readback.Set<StorageCatalogRecord>().SingleAsync(item => item.Id == malformed.Id)).ConfigJson);
        Assert.Equal(host.RootHostBindingId, (await readback.Set<StorageCatalogRecord>().SingleAsync(item => item.Id == host.Id)).RootHostBindingId);
    }

    [Fact]
    public async Task Search_owner_keeps_case_matching_title_order_limits_and_upsert_identity() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<SearchIndexService>();
        var term = $"ownersearch{Guid.NewGuid():N}";
        await using var schema = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        schema.AddRange(Enumerable.Range(0, 60).Reverse().Select(index => new SearchDocument {
            SourceType = term, SourceKey = index.ToString(), Title = $"{term} {index:D2}",
            Category = "Search proof", Summary = "summary", Body = "body", Route = "/search-proof"
        }));
        await schema.SaveChangesAsync();
        Assert.Empty(await service.SearchAsync("  "));
        var limited = await service.SearchAsync(term.ToUpperInvariant(), 3);
        Assert.Equal(Enumerable.Range(0, 3).Select(index => $"{term} {index:D2}"), limited.Select(item => item.Title));
        Assert.Equal(50, (await service.SearchAsync(term, 500)).Count);
        Assert.Single(await service.SearchAsync(term, 0));
        var original = await schema.Set<SearchDocument>().AsNoTracking().SingleAsync(item => item.SourceType == term && item.SourceKey == "0");
        await service.UpsertAsync(new(term, "0", " Updated category ", " Updated title ", " summary ", " body ", "/updated"));
        await using var owner = await application.Services.GetRequiredService<IDbContextFactory<SearchDbContext>>().CreateDbContextAsync();
        var updated = await owner.Set<SearchDocument>().SingleAsync(item => item.SourceType == term && item.SourceKey == "0");
        Assert.Equal(original.Id, updated.Id);
        Assert.Equal("Updated title", updated.Title);
        Assert.Equal("Updated category", updated.Category);
        await service.DeleteAsync(term, "0");
        Assert.False(await owner.Set<SearchDocument>().AsNoTracking().AnyAsync(item => item.Id == original.Id));
    }

    private static void AssertModel(AppDbContext schema, DbContext owner, Type[] expectedTypes) {
        var entities = owner.GetService<IDesignTimeModel>().Model.GetEntityTypes().ToArray();
        Assert.Equal(expectedTypes.OrderBy(type => type.Name), entities.Select(entity => entity.ClrType).OrderBy(type => type.Name));
        foreach (var entity in entities) {
            var complete = Assert.IsAssignableFrom<IEntityType>(schema.GetService<IDesignTimeModel>().Model.FindEntityType(entity.ClrType));
            Assert.Equal(complete.ToDebugString(MetadataDebugStringOptions.LongDefault), entity.ToDebugString(MetadataDebugStringOptions.LongDefault));
        }
    }

    private static DbContextOptions<T> Options<T>(ResolvedDatabaseProfile profile, CommandProbe? probe = null) where T : DbContext {
        var options = new DbContextOptionsBuilder<T>();
        AppDbContextOptionsConfigurator.Configure(options, profile);
        if (probe is not null) {
            options.AddInterceptors(probe);
        }
        return options.Options;
    }

    private sealed class Factory<T>(DbContextOptions<T> options, Func<DbContextOptions<T>, T> create) : IDbContextFactory<T> where T : DbContext {
        public T CreateDbContext() => create(options);

        public Task<T> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(create(options));
        }
    }

    private sealed class FixedClock : IClock {
        public DateTimeOffset GetUtcNow() => new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
    }

    private sealed class RejectingPaths : IWorkspacePathResolver {
        public string ResolveWorkspaceRoot() => throw new InvalidOperationException("A database-only operation requested a workspace path.");
        public string ResolveManagedFilesRoot() => throw new InvalidOperationException("A database-only operation requested a managed path.");
        public string ResolveExportsRoot() => throw new InvalidOperationException("A database-only operation requested an export path.");
        public string ResolveEvidenceRoot() => throw new InvalidOperationException("A database-only operation requested an evidence path.");
        public string ResolveManagerArtifactsRoot() => throw new InvalidOperationException("A database-only operation requested a manager path.");
    }

    private sealed class CommandProbe : DbCommandInterceptor {
        public List<(Type? ContextType, DbConnection? Connection, DbTransaction? Transaction)> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Add((eventData.Context?.GetType(), command.Connection, command.Transaction));
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            Commands.Add((eventData.Context?.GetType(), command.Connection, command.Transaction));
            return ValueTask.FromResult(result);
        }
    }
}
