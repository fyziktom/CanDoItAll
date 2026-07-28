using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workspace;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Unit;

using WorkspaceProviderProfile =
    CanDoItAll.Modules.Workspace.ProviderProfile;
using AgentFrameworkProviderProfileEditorModel =
    CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class ProviderCatalogProjectionFailureTests
{
    [Fact]
    public async Task GetProviderAsync_does_not_resurrect_stale_catalog_entry_after_committed_delete()
    {
        var fixture =
            await CreateCommittedDeleteProjectionFailureAsync();
        fixture.Store.ResetCatalogLoadCount();

        var provider = await fixture.Registry.GetProviderAsync(
            fixture.ProviderId);

        Assert.Null(provider);
        Assert.True(fixture.Store.ContainsCatalogProvider(
            fixture.ProviderId));
        Assert.Equal(0, fixture.Store.CatalogLoadCount);
    }

    [Fact]
    public async Task ListProvidersAsync_does_not_resurrect_stale_catalog_entry_after_committed_delete()
    {
        var fixture =
            await CreateCommittedDeleteProjectionFailureAsync();
        fixture.Store.ResetCatalogLoadCount();

        var providers = await fixture.Registry.ListProvidersAsync();

        var provider = Assert.Single(providers);
        Assert.Equal(
            WorkspaceAgentProviderProfileMapper
                .RuntimeFallbackOllamaProviderId,
            provider.Id);
        Assert.DoesNotContain(
            providers,
            item => item.Id == fixture.ProviderId);
        Assert.True(fixture.Store.ContainsCatalogProvider(
            fixture.ProviderId));
        Assert.Equal(0, fixture.Store.CatalogLoadCount);
    }

    [Fact]
    public async Task Projection_failures_report_committed_upsert_and_delete_with_repair_actions()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(
        [
            typeof(WorkspaceModuleAssemblyMarker).Assembly
        ]);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(
                $"provider-catalog-projection-{Guid.NewGuid():N}")
            .Options;
        var dbContextFactory = new TestDbContextFactory(options);
        var providerRegistry = new ProviderRegistry(
            [new ScenarioHarnessProviderAdapter()]);
        IProviderProfileService providerProfileService =
            new ProviderProfileService();
        var providerMapper = new WorkspaceAgentProviderProfileMapper(
            providerRegistry,
            providerProfileService);
        var observer = new RecordingCommitObserver();
        var logger = new RecordingLogger<
            WorkspaceBackedAgentProviderProfileRegistry>();
        var blockedWorkspaceRoot = Path.GetTempFileName();
        var providerId = Guid.NewGuid();

        try
        {
            var registry =
                new WorkspaceBackedAgentProviderProfileRegistry(
                    dbContextFactory,
                    new FileSandboxWorkspaceStore(blockedWorkspaceRoot),
                    providerRegistry,
                    providerProfileService,
                    providerMapper,
                    [observer],
                    logger);
            var model = new AgentFrameworkProviderProfileEditorModel
            {
                Id = providerId,
                Name = "Canonical scenario provider",
                BaseUrl = ScenarioHarnessProviderAdapter.BaseUrl,
                DefaultModel = ScenarioHarnessProviderAdapter.DefaultModel,
                Transport = ProviderTransportKind.Responses,
                IsEnabled = true,
                SupportsStreaming = true,
                SupportsTools = true
            };

            var upsertException = await Assert.ThrowsAsync<
                ProviderCatalogProjectionException>(
                () => registry.SaveProviderAsync(model));

            AssertProjectionFailure(
                upsertException,
                providerId,
                ProviderCatalogProjectionOperationKind.Upsert,
                "SaveProviderAsync");
            await using (var dbContext =
                await dbContextFactory.CreateDbContextAsync())
            {
                var committedProvider = await dbContext
                    .Set<WorkspaceProviderProfile>()
                    .AsNoTracking()
                    .SingleAsync(item => item.Id == providerId);
                Assert.NotEqual(
                    Guid.Empty,
                    committedProvider.ConcurrencyToken);
            }

            Assert.Equal(providerId, observer.SavedProviderId);
            Assert.Same(upsertException, logger.LastException);
            Assert.Contains(
                "CanonicalCommitSucceeded=True",
                logger.LastMessage,
                StringComparison.Ordinal);

            var deleteException = await Assert.ThrowsAsync<
                ProviderCatalogProjectionException>(
                () => registry.DeleteProviderAsync(providerId));

            AssertProjectionFailure(
                deleteException,
                providerId,
                ProviderCatalogProjectionOperationKind.Delete,
                "DeleteProviderAsync");
            await using (var dbContext =
                await dbContextFactory.CreateDbContextAsync())
            {
                Assert.False(
                    await dbContext.Set<WorkspaceProviderProfile>()
                        .AnyAsync(item => item.Id == providerId));
            }

            Assert.Equal(providerId, observer.DeletedProviderId);
            Assert.Same(deleteException, logger.LastException);
        }
        finally
        {
            File.Delete(blockedWorkspaceRoot);
        }
    }

    private static async Task<ProjectionFailureFixture>
        CreateCommittedDeleteProjectionFailureAsync()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(
        [
            typeof(WorkspaceModuleAssemblyMarker).Assembly
        ]);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(
                $"provider-catalog-stale-projection-{Guid.NewGuid():N}")
            .Options;
        var dbContextFactory = new TestDbContextFactory(options);
        var providerRegistry = new ProviderRegistry(
            [new ScenarioHarnessProviderAdapter()]);
        IProviderProfileService providerProfileService =
            new ProviderProfileService();
        var providerMapper = new WorkspaceAgentProviderProfileMapper(
            providerRegistry,
            providerProfileService);
        var store = new FailingCatalogProjectionStore();
        var registry =
            new WorkspaceBackedAgentProviderProfileRegistry(
                dbContextFactory,
                store,
                providerRegistry,
                providerProfileService,
                providerMapper,
                [new RecordingCommitObserver()],
                new RecordingLogger<
                    WorkspaceBackedAgentProviderProfileRegistry>());
        var providerId = Guid.NewGuid();
        var model = new AgentFrameworkProviderProfileEditorModel
        {
            Id = providerId,
            Name = "Stale projected provider",
            BaseUrl = ScenarioHarnessProviderAdapter.BaseUrl,
            DefaultModel = ScenarioHarnessProviderAdapter.DefaultModel,
            Transport = ProviderTransportKind.Responses,
            IsEnabled = true,
            SupportsStreaming = true,
            SupportsTools = true
        };

        await registry.SaveProviderAsync(model);
        Assert.True(store.ContainsCatalogProvider(providerId));
        store.FailCatalogUpdates = true;

        var exception = await Assert.ThrowsAsync<
            ProviderCatalogProjectionException>(
            () => registry.DeleteProviderAsync(providerId));
        Assert.Equal(
            ProviderCatalogProjectionOperationKind.Delete,
            exception.OperationKind);
        await using (var dbContext =
            await dbContextFactory.CreateDbContextAsync())
        {
            Assert.False(
                await dbContext.Set<WorkspaceProviderProfile>()
                    .AnyAsync(item => item.Id == providerId));
        }

        return new ProjectionFailureFixture(
            registry,
            store,
            providerId);
    }

    private static void AssertProjectionFailure(
        ProviderCatalogProjectionException exception,
        Guid providerId,
        ProviderCatalogProjectionOperationKind operationKind,
        string repairOperation)
    {
        Assert.Equal(providerId, exception.ProviderId);
        Assert.Equal(operationKind, exception.OperationKind);
        Assert.True(exception.CanonicalCommitSucceeded);
        Assert.Contains(
            repairOperation,
            exception.RepairAction,
            StringComparison.Ordinal);
        Assert.Contains(
            providerId.ToString("D"),
            exception.RepairAction,
            StringComparison.Ordinal);
        Assert.NotNull(exception.InnerException);
    }

    private sealed class TestDbContextFactory(
        DbContextOptions<AppDbContext> options) :
        IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
        {
            return new AppDbContext(options);
        }

        public Task<AppDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }

    private sealed class RecordingCommitObserver :
        IWorkspaceProviderProfileCommitObserver
    {
        public Guid? SavedProviderId { get; private set; }

        public Guid? DeletedProviderId { get; private set; }

        public Task ProviderSavedAsync(
            Guid providerId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SavedProviderId = providerId;
            return Task.CompletedTask;
        }

        public Task ProviderDeletedAsync(
            Guid providerId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeletedProviderId = providerId;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public Exception? LastException { get; private set; }

        public string LastMessage { get; private set; } = string.Empty;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LastException = exception;
            LastMessage = formatter(state, exception);
        }
    }

    private sealed record ProjectionFailureFixture(
        WorkspaceBackedAgentProviderProfileRegistry Registry,
        FailingCatalogProjectionStore Store,
        Guid ProviderId);

    private sealed class FailingCatalogProjectionStore :
        ISandboxWorkspaceStore
    {
        private SandboxWorkspaceDocument document =
            SandboxWorkspaceDocument.Empty;
        private int catalogLoadCount;

        public bool FailCatalogUpdates { get; set; }

        public int CatalogLoadCount =>
            Volatile.Read(ref catalogLoadCount);

        public bool ContainsCatalogProvider(Guid providerId)
        {
            return document.Providers.Any(item => item.Id == providerId);
        }

        public void ResetCatalogLoadCount()
        {
            Interlocked.Exchange(ref catalogLoadCount, 0);
        }

        public Task<SandboxWorkspaceDocument> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(document);
        }

        public Task<SandboxWorkspaceDocumentSnapshot> LoadSnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                new SandboxWorkspaceDocumentSnapshot(document, 0));
        }

        public Task<SandboxWorkspaceDocument> SaveAsync(
            SandboxWorkspaceDocument next,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            document = next;
            return Task.FromResult(document);
        }

        public Task<SandboxWorkspaceDocument> UpdateWorkspaceAsync(
            Func<SandboxWorkspaceDocument, SandboxWorkspaceDocument> update,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            document = update(document);
            return Task.FromResult(document);
        }

        public Task<SandboxWorkspaceDocument> UpdateWorkspaceAsync(
            Func<SandboxWorkspaceDocument, SandboxWorkspaceDocument> update,
            long expectedRevision,
            CancellationToken cancellationToken = default)
        {
            return UpdateWorkspaceAsync(update, cancellationToken);
        }

        public Task<SandboxWorkspaceCatalog> LoadCatalogAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref catalogLoadCount);
            return Task.FromResult(document.ToCatalog());
        }

        public async Task<SandboxWorkspaceCatalogSnapshot>
            LoadCatalogSnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            var catalog = await LoadCatalogAsync(cancellationToken);
            return new SandboxWorkspaceCatalogSnapshot(
                catalog,
                catalog.CatalogDataRevision);
        }

        public Task<SandboxWorkspaceCatalog> SaveCatalogAsync(
            SandboxWorkspaceCatalog catalog,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            document = SandboxWorkspaceDocument.Combine(
                catalog,
                document.ToExecutionState());
            return Task.FromResult(catalog);
        }

        public Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(
            Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FailCatalogUpdates)
            {
                throw new IOException(
                    "Catalog projection is unavailable.");
            }

            var catalog = update(document.ToCatalog());
            document = SandboxWorkspaceDocument.Combine(
                catalog,
                document.ToExecutionState());
            return Task.FromResult(catalog);
        }

        public Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(
            Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
            long expectedRevision,
            CancellationToken cancellationToken = default)
        {
            return UpdateCatalogAsync(update, cancellationToken);
        }

        public Task<SandboxWorkspaceExecutionState> LoadExecutionAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(document.ToExecutionState());
        }

        public Task<SandboxWorkspaceExecutionSummary>
            LoadExecutionSummaryAsync(
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AgentUsageProjection> LoadUsageProjectionAsync(
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SaveExecutionAsync(
            SandboxWorkspaceExecutionState executionState,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            document = SandboxWorkspaceDocument.Combine(
                document.ToCatalog(),
                executionState);
            return Task.CompletedTask;
        }

        public Task<SandboxWorkspaceExecutionState> UpdateExecutionAsync(
            Func<
                SandboxWorkspaceExecutionState,
                SandboxWorkspaceExecutionState> update,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var executionState = update(document.ToExecutionState());
            document = SandboxWorkspaceDocument.Combine(
                document.ToCatalog(),
                executionState);
            return Task.FromResult(executionState);
        }

        public Task<SandboxWorkspaceExecutionState> UpdateExecutionAsync(
            Func<
                SandboxWorkspaceExecutionState,
                SandboxWorkspaceExecutionState> update,
            long expectedRevision,
            CancellationToken cancellationToken = default)
        {
            return UpdateExecutionAsync(update, cancellationToken);
        }
    }
}
