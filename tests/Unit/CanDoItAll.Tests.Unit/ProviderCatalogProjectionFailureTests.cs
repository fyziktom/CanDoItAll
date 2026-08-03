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
using AgentFrameworkProviderKind =
    CanDoItAll.AgentFramework.Models.ProviderKind;

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

    [Fact]
    public async Task Provider_save_persists_kind_and_defined_thinking_capabilities_through_database_mapping()
    {
        var registry = CreateScenarioRegistry();
        var providerId = await registry.SaveProviderAsync(
            new AgentFrameworkProviderProfileEditorModel
            {
                Name = "Azure deployment provider",
                Kind = AgentFrameworkProviderKind.AzureOpenAi,
                BaseUrl = ScenarioHarnessProviderAdapter.BaseUrl,
                DefaultModel = "reasoning-deployment",
                Transport = ProviderTransportKind.ChatCompletions,
                Purpose = ProviderProfilePurpose.Chat,
                IsEnabled = true,
                SupportsStreaming = true,
                SupportsTools = true,
                ConfigurationJson =
                    """{"modelThinkingEffortCapabilities":[{"model":"reasoning-deployment","status":"supported","source":"defined","allowedEfforts":["low","medium","high"],"modelFamily":"","summary":"Deployment metadata supplied by the provider administrator."}]}"""
            });
        var reloaded = Assert.IsType<CanDoItAll.AgentFramework.Models.ProviderProfile>(
            await registry.GetProviderAsync(providerId));

        Assert.Equal(AgentFrameworkProviderKind.AzureOpenAi, reloaded.Kind);
        var reloadedCapability = Assert.Single(
            reloaded.ModelThinkingEffortCapabilities);
        Assert.Equal("reasoning-deployment", reloadedCapability.Model);
        Assert.Equal(
            AgentThinkingEffortSupportStatus.Supported,
            reloadedCapability.Status);
        Assert.Equal(
            AgentThinkingEffortCapabilitySource.Defined,
            reloadedCapability.Source);
        Assert.Equal(
            [
                AgentReasoningEffortLevel.Low,
                AgentReasoningEffortLevel.Medium,
                AgentReasoningEffortLevel.High
            ],
            reloadedCapability.AllowedEfforts);
        Assert.Equal(
            AgentThinkingEffortControlMode.EffortLevels,
            reloadedCapability.ControlMode);
        Assert.Contains(
            "modelThinkingEffortCapabilities",
            reloaded.ConfigurationJson,
            StringComparison.Ordinal);

        var editor = await registry.GetProviderEditorAsync(providerId);
        editor.ConfigurationJson =
            """{"modelThinkingEffortCapabilities":[{"model":"reasoning-deployment","status":"supported","source":"defined","allowedEfforts":["high"],"modelFamily":"","summary":"Updated deployment metadata."}]}""";
        await registry.SaveProviderAsync(editor);
        var edited = Assert.IsType<CanDoItAll.AgentFramework.Models.ProviderProfile>(
            await registry.GetProviderAsync(providerId));

        Assert.Equal(
            [AgentReasoningEffortLevel.High],
            Assert.Single(edited.ModelThinkingEffortCapabilities).AllowedEfforts);
    }

    [Fact]
    public async Task Provider_update_persists_discovered_thinking_capabilities_through_database_mapping()
    {
        var registry = CreateScenarioRegistry();
        var providerId = await registry.SaveProviderAsync(
            new AgentFrameworkProviderProfileEditorModel
            {
                Name = "Local model provider",
                Kind = AgentFrameworkProviderKind.Ollama,
                BaseUrl = ScenarioHarnessProviderAdapter.BaseUrl,
                DefaultModel = "qwen3.5:2b",
                Transport = ProviderTransportKind.ChatCompletions,
                Purpose = ProviderProfilePurpose.Chat,
                IsEnabled = true,
                SupportsStreaming = true,
                SupportsTools = true
            });
        var capability = AgentThinkingEffortPolicy.CreateDiscoveredCapability(
            "qwen3.5:2b",
            "qwen3",
            AgentThinkingEffortSupportStatus.Supported);

        await registry.UpdateProviderAsync(
            providerId,
            provider => provider with
            {
                ModelThinkingEffortCapabilities = [capability]
            });
        var reloaded = Assert.IsType<CanDoItAll.AgentFramework.Models.ProviderProfile>(
            await registry.GetProviderAsync(providerId));

        Assert.Equal(AgentFrameworkProviderKind.Ollama, reloaded.Kind);
        var reloadedCapability = Assert.Single(
            reloaded.ModelThinkingEffortCapabilities);
        Assert.Equal(capability.Model, reloadedCapability.Model);
        Assert.Equal(capability.Status, reloadedCapability.Status);
        Assert.Equal(capability.Source, reloadedCapability.Source);
        Assert.Equal(capability.AllowedEfforts, reloadedCapability.AllowedEfforts);
        Assert.Equal(capability.ControlMode, reloadedCapability.ControlMode);

        await registry.UpdateProviderAsync(
            providerId,
            provider => provider with
            {
                ModelThinkingEffortCapabilities = []
            });
        var cleared = Assert.IsType<CanDoItAll.AgentFramework.Models.ProviderProfile>(
            await registry.GetProviderAsync(providerId));

        Assert.Empty(cleared.ModelThinkingEffortCapabilities);
        Assert.DoesNotContain(
            "modelThinkingEffortCapabilities",
            cleared.ConfigurationJson,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Provider_save_drops_discovered_capabilities_when_provider_identity_changes()
    {
        var registry = CreateScenarioRegistry();
        var discoveredCapability = AgentThinkingEffortPolicy.CreateDiscoveredCapability(
            "qwen3.5:2b",
            "qwen35",
            AgentThinkingEffortSupportStatus.Supported);
        var providerId = await registry.SaveProviderAsync(
            new AgentFrameworkProviderProfileEditorModel
            {
                Name = "Changing provider identity",
                Kind = AgentFrameworkProviderKind.Ollama,
                BaseUrl = ScenarioHarnessProviderAdapter.BaseUrl,
                DefaultModel = "qwen3.5:2b",
                Transport = ProviderTransportKind.ChatCompletions,
                Purpose = ProviderProfilePurpose.Chat,
                IsEnabled = true,
                SupportsStreaming = true,
                SupportsTools = true,
                ModelThinkingEffortCapabilities = [discoveredCapability]
            });
        var editor = await registry.GetProviderEditorAsync(providerId);
        editor.Kind = AgentFrameworkProviderKind.AzureOpenAi;
        editor.BaseUrl = "https://example.openai.azure.com";
        editor.DefaultModel = "reasoning-deployment";
        editor.Transport = ProviderTransportKind.Responses;

        await registry.SaveProviderAsync(editor);
        var reloaded = Assert.IsType<CanDoItAll.AgentFramework.Models.ProviderProfile>(
            await registry.GetProviderAsync(providerId));

        Assert.Equal(AgentFrameworkProviderKind.AzureOpenAi, reloaded.Kind);
        Assert.Empty(reloaded.ModelThinkingEffortCapabilities);
        Assert.DoesNotContain(
            "modelThinkingEffortCapabilities",
            reloaded.ConfigurationJson,
            StringComparison.Ordinal);
        Assert.Equal(
            AgentThinkingEffortSupportStatus.Unknown,
            AgentThinkingEffortPolicy.ResolveCapability(
                reloaded,
                "reasoning-deployment").Status);
    }

    [Fact]
    public async Task Provider_save_rejects_malformed_configuration_without_mutating_stored_profile()
    {
        var registry = CreateScenarioRegistry();
        var providerId = await registry.SaveProviderAsync(
            new AgentFrameworkProviderProfileEditorModel
            {
                Name = "Stored provider",
                Kind = AgentFrameworkProviderKind.AzureOpenAi,
                BaseUrl = ScenarioHarnessProviderAdapter.BaseUrl,
                DefaultModel = "reasoning-deployment",
                Transport = ProviderTransportKind.Responses,
                Purpose = ProviderProfilePurpose.Chat,
                IsEnabled = true,
                SupportsStreaming = true,
                SupportsTools = true,
                ConfigurationJson = "{}"
            });
        var before = Assert.IsType<CanDoItAll.AgentFramework.Models.ProviderProfile>(
            await registry.GetProviderAsync(providerId));
        var editor = await registry.GetProviderEditorAsync(providerId);
        editor.Name = "Must not persist";
        editor.ConfigurationJson = "{not-valid-json";

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            registry.SaveProviderAsync(editor));
        var after = Assert.IsType<CanDoItAll.AgentFramework.Models.ProviderProfile>(
            await registry.GetProviderAsync(providerId));

        Assert.Contains("not valid JSON", exception.Message, StringComparison.Ordinal);
        Assert.Equal(before.Name, after.Name);
        Assert.Equal(before.ConfigurationJson, after.ConfigurationJson);
        Assert.Equal(before.ModelThinkingEffortCapabilities, after.ModelThinkingEffortCapabilities);
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

    private static WorkspaceBackedAgentProviderProfileRegistry
        CreateScenarioRegistry()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(
        [
            typeof(WorkspaceModuleAssemblyMarker).Assembly
        ]);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(
                $"provider-thinking-capability-{Guid.NewGuid():N}")
            .Options;
        var providerRegistry = new ProviderRegistry(
            [new ScenarioHarnessProviderAdapter()]);
        IProviderProfileService providerProfileService =
            new ProviderProfileService();
        return new WorkspaceBackedAgentProviderProfileRegistry(
            new TestDbContextFactory(options),
            new FailingCatalogProjectionStore(),
            providerRegistry,
            providerProfileService,
            new WorkspaceAgentProviderProfileMapper(
                providerRegistry,
                providerProfileService),
            [new RecordingCommitObserver()],
            new RecordingLogger<
                WorkspaceBackedAgentProviderProfileRegistry>());
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

        public Task<AgentExecutionReportPage> QueryExecutionReportAsync(
            AgentExecutionReportQuery query,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AgentWorkspaceDeletionResult> DeleteAgentWorkspaceDataAsync(
            Guid agentId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

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
