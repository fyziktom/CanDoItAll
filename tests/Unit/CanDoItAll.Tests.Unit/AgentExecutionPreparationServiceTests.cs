using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentExecutionPreparationServiceTests
{
    private static readonly Guid DatabaseProfileId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid AgentId =
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid ProviderId =
        Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly WorkspaceScopeDescriptor Scope =
        WorkspaceScopeDescriptor.Project("project-42");

    [Fact]
    public async Task Warm_acquisition_revalidates_canonical_provider_and_reuses_blueprint()
    {
        var catalog = CreateCatalog(CatalogDataRevision.Initial);
        var store = new MutableCatalogStore(catalog);
        var registry = new CountingProviderRegistry(catalog.Providers.Single());
        using var cache = new AgentExecutionPreparationCache(
            new AgentExecutionPreparationCachePolicy(4));
        var service = CreateService(store, registry, cache);

        var cold = await service.AcquireAsync(AgentId);
        var warm = await service.AcquireAsync(AgentId);

        Assert.Equal(AgentExecutionPreparationSource.Refreshed, cold.Source);
        Assert.Equal(AgentExecutionPreparationSource.Reused, warm.Source);
        Assert.Equal(3, store.SnapshotLoadCount);
        Assert.Equal(3, registry.GetProviderCallCount);
        Assert.Same(cold.Blueprint, warm.Blueprint);
        Assert.Equal(catalog.CatalogDataRevision, warm.CatalogSnapshot.Revision);
    }

    [Fact]
    public async Task Atomic_consumer_uses_one_catalog_read_for_cold_and_warm_acquisition()
    {
        var catalog = CreateCatalog(CatalogDataRevision.Initial);
        var store = new MutableCatalogStore(catalog);
        var registry = new CountingProviderRegistry(catalog.Providers.Single());
        using var cache = new AgentExecutionPreparationCache(
            new AgentExecutionPreparationCachePolicy(4));
        var service = CreateService(store, registry, cache);

        var cold = await service.AcquireForAtomicConsumerAsync(AgentId);
        var warm = await service.AcquireForAtomicConsumerAsync(AgentId);

        Assert.Equal(AgentExecutionPreparationSource.Refreshed, cold.Source);
        Assert.Equal(AgentExecutionPreparationSource.Reused, warm.Source);
        Assert.Equal(2, store.SnapshotLoadCount);
        Assert.Equal(2, registry.GetProviderCallCount);
        Assert.Same(cold.Blueprint, warm.Blueprint);
    }

    [Fact]
    public async Task Use_time_validation_returns_typed_stale_reasons()
    {
        var catalog = CreateCatalog(CatalogDataRevision.Initial);
        var store = new MutableCatalogStore(catalog);
        var registry = new CountingProviderRegistry(catalog.Providers.Single());
        var generations = new MutableProfileGenerationSource();
        using var cache = new AgentExecutionPreparationCache(
            new AgentExecutionPreparationCachePolicy(4));
        var service = CreateService(store, registry, cache, generations);
        var prepared = await service.AcquireForAtomicConsumerAsync(AgentId);

        Assert.Equal(
            AgentExecutionPreparationUseValidation.Current,
            service.ValidateForUse(
                prepared.Blueprint,
                prepared.CatalogSnapshot));

        generations.Advance();
        Assert.Equal(
            AgentExecutionPreparationUseValidation
                .DatabaseProfileGenerationChanged,
            service.ValidateForUse(
                prepared.Blueprint,
                prepared.CatalogSnapshot));

        var currentGenerationService = CreateService(
            store,
            registry,
            cache,
            new FixedAgentExecutionProfileGenerationSource(default));
        var changedRevision = CreateCatalog(
            CatalogDataRevision.Initial.Next());
        Assert.Equal(
            AgentExecutionPreparationUseValidation.CatalogRevisionChanged,
            currentGenerationService.ValidateForUse(
                prepared.Blueprint,
                new SandboxWorkspaceCatalogSnapshot(
                    changedRevision,
                    changedRevision.CatalogDataRevision)));

        var changedShadowProvider = catalog.Providers.Single() with
        {
            DefaultModel = "changed-model"
        };
        var changedShadowCatalog = catalog with
        {
            Providers = [changedShadowProvider]
        };
        Assert.Equal(
            AgentExecutionPreparationUseValidation.Current,
            currentGenerationService.ValidateForUse(
                prepared.Blueprint,
                new SandboxWorkspaceCatalogSnapshot(
                    changedShadowCatalog,
                    changedShadowCatalog.CatalogDataRevision)));
        registry.Replace(catalog.Providers.Single() with
        {
            DefaultModel = "canonical-provider-change"
        });
        Assert.Equal(
            AgentExecutionPreparationUseValidation
                .ProviderConfigurationChanged,
            currentGenerationService.ValidateForUse(
                prepared.Blueprint,
                prepared.CatalogSnapshot));
        var refreshedProvider = await currentGenerationService
            .AcquireForAtomicConsumerAsync(AgentId);

        Assert.Equal(
            AgentExecutionPreparationSource.Refreshed,
            refreshedProvider.Source);
        Assert.Equal(
            "canonical-provider-change",
            refreshedProvider.Blueprint.Provider.DefaultModel);

        var changedProviderId = Guid.NewGuid();
        var rewiredCatalog = catalog with
        {
            Agents =
            [
                catalog.Agents.Single() with
                {
                    ProviderProfileId = changedProviderId
                }
            ],
            Providers =
            [
                catalog.Providers.Single(),
                catalog.Providers.Single() with
                {
                    Id = changedProviderId
                }
            ]
        };
        Assert.Equal(
            AgentExecutionPreparationUseValidation.AgentProviderChanged,
            currentGenerationService.ValidateForUse(
                prepared.Blueprint,
                new SandboxWorkspaceCatalogSnapshot(
                    rewiredCatalog,
                    rewiredCatalog.CatalogDataRevision)));
    }

    [Fact]
    public async Task
        Unrelated_provider_update_does_not_invalidate_selected_provider_blueprint()
    {
        var catalog = CreateCatalog(CatalogDataRevision.Initial);
        var store = new MutableCatalogStore(catalog);
        var registry = new CountingProviderRegistry(
            catalog.Providers.Single());
        using var cache = new AgentExecutionPreparationCache(
            new AgentExecutionPreparationCachePolicy(4));
        var service = CreateService(store, registry, cache);
        var first = await service.AcquireForAtomicConsumerAsync(AgentId);

        registry.ReplaceUnrelated(
            catalog.Providers.Single() with
            {
                Id = Guid.NewGuid(),
                DefaultModel = "unrelated-change"
            });
        var second = await service.AcquireForAtomicConsumerAsync(AgentId);

        Assert.Equal(AgentExecutionPreparationSource.Reused, second.Source);
        Assert.Same(first.Blueprint, second.Blueprint);
        Assert.Equal(
            AgentExecutionPreparationUseValidation.Current,
            service.ValidateForUse(
                first.Blueprint,
                first.CatalogSnapshot));
    }

    [Fact]
    public async Task Deleted_selected_provider_returns_typed_configuration_change()
    {
        var catalog = CreateCatalog(CatalogDataRevision.Initial);
        var store = new MutableCatalogStore(catalog);
        var registry = new CountingProviderRegistry(
            catalog.Providers.Single());
        using var cache = new AgentExecutionPreparationCache(
            new AgentExecutionPreparationCachePolicy(4));
        var service = CreateService(store, registry, cache);
        var prepared = await service.AcquireForAtomicConsumerAsync(AgentId);

        registry.RemoveSelected();

        Assert.Equal(
            AgentExecutionPreparationUseValidation
                .ProviderConfigurationChanged,
            service.ValidateForUse(
                prepared.Blueprint,
                prepared.CatalogSnapshot));
    }

    [Fact]
    public async Task Catalog_revision_change_refreshes_only_after_current_data_is_loaded()
    {
        var initialCatalog = CreateCatalog(CatalogDataRevision.Initial);
        var store = new MutableCatalogStore(initialCatalog);
        var registry = new CountingProviderRegistry(
            initialCatalog.Providers.Single());
        using var cache = new AgentExecutionPreparationCache(
            new AgentExecutionPreparationCachePolicy(4));
        var service = CreateService(store, registry, cache);

        await service.AcquireAsync(AgentId);
        var changedCatalog = CreateCatalog(
            CatalogDataRevision.Initial.Next(),
            instructions: "Changed instructions");
        store.Replace(changedCatalog);

        var refreshed = await service.AcquireAsync(AgentId);

        Assert.Equal(AgentExecutionPreparationSource.Refreshed, refreshed.Source);
        Assert.Equal(
            changedCatalog.CatalogDataRevision,
            refreshed.Blueprint.Request.Version.CatalogRevision);
        Assert.Equal("Changed instructions", refreshed.Blueprint.Agent.Instructions);
        Assert.Equal(4, registry.GetProviderCallCount);
    }

    [Fact]
    public async Task Database_profile_generation_change_refreshes_same_catalog_revision()
    {
        var catalog = CreateCatalog(CatalogDataRevision.Initial);
        var store = new MutableCatalogStore(catalog);
        var registry = new CountingProviderRegistry(catalog.Providers.Single());
        var generations = new MutableProfileGenerationSource();
        using var cache = new AgentExecutionPreparationCache(
            new AgentExecutionPreparationCachePolicy(4));
        var service = CreateService(store, registry, cache, generations);

        await service.AcquireAsync(AgentId);
        generations.Advance();

        var refreshed = await service.AcquireAsync(AgentId);

        Assert.Equal(AgentExecutionPreparationSource.Refreshed, refreshed.Source);
        Assert.Equal(
            new DatabaseProfileGeneration(1),
            refreshed.Blueprint.Request.Version.DatabaseProfileGeneration);
        Assert.Equal(4, registry.GetProviderCallCount);
    }

    [Fact]
    public async Task Revision_change_during_cold_load_discards_old_blueprint_and_retries()
    {
        var initialCatalog = CreateCatalog(CatalogDataRevision.Initial);
        var store = new MutableCatalogStore(initialCatalog);
        var providerEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseProvider = new ManualResetEventSlim();
        var registry = new CountingProviderRegistry(
            initialCatalog.Providers.Single(),
            () =>
            {
                providerEntered.TrySetResult();
                releaseProvider.Wait();
            });
        using var cache = new AgentExecutionPreparationCache(
            new AgentExecutionPreparationCachePolicy(4));
        var service = CreateService(store, registry, cache);

        var acquisitionTask = Task.Run(
            () => service.AcquireAsync(AgentId));
        await providerEntered.Task;
        store.Replace(CreateCatalog(
            CatalogDataRevision.Initial.Next(),
            instructions: "Current instructions"));
        releaseProvider.Set();

        var snapshot = await acquisitionTask;

        Assert.Equal(
            CatalogDataRevision.Initial.Next(),
            snapshot.CatalogSnapshot.Revision);
        Assert.Equal("Current instructions", snapshot.Blueprint.Agent.Instructions);
        Assert.Equal(3, registry.GetProviderCallCount);
    }

    [Fact]
    public async Task Canonical_provider_configuration_overrides_divergent_catalog_shadow()
    {
        var catalog = CreateCatalog(CatalogDataRevision.Initial);
        var canonicalProvider = catalog.Providers.Single() with
        {
            DefaultModel = "different-model"
        };
        var store = new MutableCatalogStore(catalog);
        var registry = new CountingProviderRegistry(canonicalProvider);
        using var cache = new AgentExecutionPreparationCache(
            new AgentExecutionPreparationCachePolicy(4));
        var service = CreateService(store, registry, cache);

        var prepared = await service.AcquireAsync(AgentId);

        Assert.Equal("different-model", prepared.Blueprint.Provider.DefaultModel);
        Assert.Equal(
            ProviderConfigurationFingerprintFactory.Create(canonicalProvider),
            prepared.Blueprint.Request.Version.ProviderFingerprint);
        Assert.NotEqual(
            ProviderConfigurationFingerprintFactory.Create(
                catalog.Providers.Single()),
            prepared.Blueprint.Request.Version.ProviderFingerprint);
    }

    private static AgentExecutionPreparationService CreateService(
        ISandboxWorkspaceCatalogStore store,
        IProviderRuntimeProfileSnapshotSource registry,
        IAgentExecutionPreparationCache cache,
        IAgentExecutionProfileGenerationSource? generationSource = null)
    {
        return new AgentExecutionPreparationService(
            store,
            registry,
            cache,
            generationSource ??
            new FixedAgentExecutionProfileGenerationSource(default),
            new AgentExecutionActivityWorkspaceIdentity(
                DatabaseProfileId,
                Scope,
                new DatabaseProfileGeneration(0)));
    }

    private static SandboxWorkspaceCatalog CreateCatalog(
        CatalogDataRevision revision,
        string instructions = "Instructions")
    {
        var now = DateTimeOffset.UtcNow;
        var capability = new CapabilityCatalogItem(
            Guid.NewGuid(),
            CapabilityKind.Tool,
            "workspace",
            "Workspace",
            "Workspace capability",
            "workspace",
            "{}",
            CapabilityProofStatus.Verified,
            "verified",
            now,
            true);
        var provider = new ProviderProfile(
            ProviderId,
            "OpenAI",
            ProviderKind.OpenAi,
            "https://api.openai.com",
            "OPENAI_API_KEY",
            "gpt-5.4-mini",
            ProviderTransportKind.Responses,
            true,
            true,
            true,
            true,
            true,
            "{}",
            "notes",
            "healthy",
            now,
            ["gpt-5.4-mini"]);
        var agent = new AgentDefinition(
            AgentId,
            "Prepared agent",
            "Specialist",
            "Summary",
            instructions,
            AgentLifecycleStatus.Active,
            ProviderId,
            "gpt-5.4-mini",
            AgentWorkloadKind.General,
            AgentChatHistoryMode.FrameworkManaged,
            0.2,
            false,
            false,
            "{}",
            false,
            string.Empty,
            AgentPermissionsPolicy.Default,
            [
                new AgentCapabilityAssignment(
                    capability.Id,
                    capability.Key,
                    capability.Kind,
                    capability.ProofStatus,
                    capability.LastVerifiedAtUtc,
                    capability.ProofNotes)
            ],
            ["test"],
            now,
            now);
        var memory = new AgentMemoryRecord(
            Guid.NewGuid(),
            AgentId,
            MemoryKind.Context,
            "Context",
            "Prepared memory",
            "test",
            5,
            "{}",
            now);

        return new SandboxWorkspaceCatalog(
            "1.0",
            [agent],
            [provider],
            [capability],
            [memory])
        {
            CatalogDataRevision = revision
        };
    }

    private sealed class MutableCatalogStore(
        SandboxWorkspaceCatalog catalog) :
        ISandboxWorkspaceCatalogStore
    {
        private readonly object gate = new();
        private SandboxWorkspaceCatalog catalog = catalog;
        private int snapshotLoadCount;

        public int SnapshotLoadCount => Volatile.Read(ref snapshotLoadCount);

        public void Replace(SandboxWorkspaceCatalog replacement)
        {
            lock (gate)
            {
                catalog = replacement;
            }
        }

        public async Task<SandboxWorkspaceCatalog> LoadCatalogAsync(
            CancellationToken cancellationToken = default)
        {
            return (await LoadCatalogSnapshotAsync(cancellationToken)).Catalog;
        }

        public Task<SandboxWorkspaceCatalogSnapshot> LoadCatalogSnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref snapshotLoadCount);
            lock (gate)
            {
                return Task.FromResult(
                    new SandboxWorkspaceCatalogSnapshot(
                        catalog,
                        catalog.CatalogDataRevision));
            }
        }

        public Task<SandboxWorkspaceCatalog> SaveCatalogAsync(
            SandboxWorkspaceCatalog catalog,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(
            Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(
            Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
            long expectedRevision,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class CountingProviderRegistry(
        ProviderProfile provider,
        Action? beforeCapture = null) :
        IProviderProfileRegistry,
        IProviderRuntimeProfileSource,
        IProviderRuntimeProfileSnapshotSource
    {
        private ProviderProfile provider = provider;
        private ProviderProfile? unrelatedProvider;
        private bool selectedRemoved;
        private int getProviderCallCount;

        public int GetProviderCallCount =>
            Volatile.Read(ref getProviderCallCount);

        public void Replace(ProviderProfile replacement)
        {
            ArgumentNullException.ThrowIfNull(replacement);
            Volatile.Write(ref provider, replacement);
            Volatile.Write(ref selectedRemoved, false);
        }

        public void ReplaceUnrelated(ProviderProfile replacement)
        {
            ArgumentNullException.ThrowIfNull(replacement);
            Volatile.Write(ref unrelatedProvider, replacement);
        }

        public void RemoveSelected()
        {
            Volatile.Write(ref selectedRemoved, true);
        }

        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ProviderProfile>>(
                Volatile.Read(ref unrelatedProvider) is { } unrelated
                    ? [Volatile.Read(ref provider), unrelated]
                    : [Volatile.Read(ref provider)]);
        }

        public async Task<ProviderProfile?> GetProviderAsync(
            Guid providerId,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref getProviderCallCount);
            var current = Volatile.Read(ref provider);
            return Volatile.Read(ref selectedRemoved) ||
                   current.Id != providerId
                ? null
                : current;
        }

        public ProviderRuntimeProfileSnapshotLease? CaptureProvider(
            Guid providerId,
            SandboxWorkspaceCatalogSnapshot catalogSnapshot)
        {
            Interlocked.Increment(ref getProviderCallCount);
            beforeCapture?.Invoke();
            var current = Volatile.Read(ref provider);
            if (Volatile.Read(ref selectedRemoved) ||
                current.Id != providerId)
            {
                return null;
            }

            return new ProviderRuntimeProfileSnapshotLease(
                current,
                ProviderConfigurationFingerprintFactory.Create(current));
        }

        public Task<ProviderProfileEditorModel> GetProviderEditorAsync(
            Guid? providerId = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Guid> SaveProviderAsync(
            ProviderProfileEditorModel model,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteProviderAsync(
            Guid providerId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ProviderProfile> UpdateProviderAsync(
            Guid providerId,
            Func<ProviderProfile, ProviderProfile> update,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class MutableProfileGenerationSource :
        IAgentExecutionProfileGenerationSource
    {
        private long generation;

        public DatabaseProfileGeneration GetGeneration()
        {
            return new DatabaseProfileGeneration(
                Volatile.Read(ref generation));
        }

        public void Advance()
        {
            Interlocked.Increment(ref generation);
        }
    }
}
