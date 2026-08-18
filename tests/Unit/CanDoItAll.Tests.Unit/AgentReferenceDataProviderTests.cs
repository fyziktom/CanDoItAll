using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentReferenceDataProviderTests
{
    [Fact]
    public async Task GetAsync_with_agents_only_does_not_load_providers()
    {
        var activeAgent = CreateAgent("Active agent", AgentLifecycleStatus.Active);
        var inactiveAgent = CreateAgent("Inactive agent", AgentLifecycleStatus.Suspended);
        var templateAgent = CreateAgent("Template agent", AgentLifecycleStatus.Active, isTemplate: true);
        var workspace = new CountingWorkspaceService(
            [inactiveAgent, templateAgent, activeAgent],
            [CreateProvider("Chat provider", ProviderProfilePurpose.Chat)]);
        var provider = new WorkspaceBackedAgentReferenceDataProvider(workspace, new AgentReferenceDataCache());

        var snapshot = await provider.GetAsync(new AgentReferenceDataRequest(
            AgentReferenceDataSections.Agents,
            ActiveAgentsOnly: true));

        var agent = Assert.Single(snapshot.Agents);
        Assert.Equal(activeAgent.Id, agent.Id);
        Assert.Empty(snapshot.Providers);
        Assert.Equal(1, workspace.ListAgentsCallCount);
        Assert.Equal(0, workspace.ListProvidersCallCount);
    }

    [Fact]
    public async Task GetAsync_with_provider_filter_does_not_load_agents()
    {
        var chatProvider = CreateProvider("Chat provider", ProviderProfilePurpose.Chat);
        var disabledImageProvider = CreateProvider("Disabled image provider", ProviderProfilePurpose.ImageGeneration, isEnabled: false);
        var enabledImageProvider = CreateProvider("Enabled image provider", ProviderProfilePurpose.ImageGeneration);
        var workspace = new CountingWorkspaceService(
            [CreateAgent("Agent", AgentLifecycleStatus.Active)],
            [chatProvider, disabledImageProvider, enabledImageProvider]);
        var provider = new WorkspaceBackedAgentReferenceDataProvider(workspace, new AgentReferenceDataCache());

        var snapshot = await provider.GetAsync(new AgentReferenceDataRequest(
            AgentReferenceDataSections.Providers,
            EnabledProvidersOnly: true,
            ProviderPurpose: ProviderProfilePurpose.ImageGeneration));

        var result = Assert.Single(snapshot.Providers);
        Assert.Equal(enabledImageProvider.Id, result.Id);
        Assert.Empty(snapshot.Agents);
        Assert.Equal(0, workspace.ListAgentsCallCount);
        Assert.Equal(1, workspace.ListProvidersCallCount);
    }

    [Fact]
    public async Task GetAsync_reuses_cached_request_until_invalidated()
    {
        var cache = new AgentReferenceDataCache();
        var workspace = new CountingWorkspaceService(
            [CreateAgent("Agent", AgentLifecycleStatus.Active)],
            [CreateProvider("Provider", ProviderProfilePurpose.Chat)]);
        var provider = new WorkspaceBackedAgentReferenceDataProvider(workspace, cache);
        var request = AgentReferenceDataRequest.AgentsAndProviders();

        await provider.GetAsync(request);
        await provider.GetAsync(request);

        Assert.Equal(1, workspace.ListAgentsCallCount);
        Assert.Equal(1, workspace.ListProvidersCallCount);

        cache.Invalidate();
        await provider.GetAsync(request);

        Assert.Equal(2, workspace.ListAgentsCallCount);
        Assert.Equal(2, workspace.ListProvidersCallCount);
    }

    [Fact]
    public async Task Shared_invalidation_clears_reference_data_caches_across_scopes()
    {
        var invalidationHub = new AgentReferenceDataInvalidationHub();
        using var firstCache = new AgentReferenceDataCache(invalidationHub);
        using var secondCache = new AgentReferenceDataCache(invalidationHub);
        var firstWorkspace = new CountingWorkspaceService(
            [CreateAgent("First agent", AgentLifecycleStatus.Active)],
            [CreateProvider("First provider", ProviderProfilePurpose.Chat)]);
        var secondWorkspace = new CountingWorkspaceService(
            [CreateAgent("Second agent", AgentLifecycleStatus.Active)],
            [CreateProvider("Second provider", ProviderProfilePurpose.Chat)]);
        var firstProvider = new WorkspaceBackedAgentReferenceDataProvider(firstWorkspace, firstCache);
        var secondProvider = new WorkspaceBackedAgentReferenceDataProvider(secondWorkspace, secondCache);
        var request = AgentReferenceDataRequest.AgentsAndProviders();

        await firstProvider.GetAsync(request);
        await secondProvider.GetAsync(request);
        await firstProvider.GetAsync(request);
        await secondProvider.GetAsync(request);

        Assert.Equal(1, firstWorkspace.ListAgentsCallCount);
        Assert.Equal(1, secondWorkspace.ListAgentsCallCount);

        invalidationHub.Invalidate();
        await firstProvider.GetAsync(request);
        await secondProvider.GetAsync(request);

        Assert.Equal(2, firstWorkspace.ListAgentsCallCount);
        Assert.Equal(2, firstWorkspace.ListProvidersCallCount);
        Assert.Equal(2, secondWorkspace.ListAgentsCallCount);
        Assert.Equal(2, secondWorkspace.ListProvidersCallCount);
    }

    [Fact]
    public async Task Shared_invalidation_clears_later_scopes_when_an_earlier_subscriber_throws()
    {
        var invalidationHub = new AgentReferenceDataInvalidationHub();
        invalidationHub.Invalidated += (_, _) => throw new InvalidOperationException("Expected test failure.");
        using var firstCache = new AgentReferenceDataCache(invalidationHub);
        using var secondCache = new AgentReferenceDataCache(invalidationHub);
        var firstWorkspace = new CountingWorkspaceService(
            [CreateAgent("First agent", AgentLifecycleStatus.Active)],
            [CreateProvider("First provider", ProviderProfilePurpose.Chat)]);
        var secondWorkspace = new CountingWorkspaceService(
            [CreateAgent("Second agent", AgentLifecycleStatus.Active)],
            [CreateProvider("Second provider", ProviderProfilePurpose.Chat)]);
        var firstProvider = new WorkspaceBackedAgentReferenceDataProvider(firstWorkspace, firstCache);
        var secondProvider = new WorkspaceBackedAgentReferenceDataProvider(secondWorkspace, secondCache);
        var request = AgentReferenceDataRequest.AgentsAndProviders();

        await firstProvider.GetAsync(request);
        await secondProvider.GetAsync(request);

        Assert.Throws<InvalidOperationException>(invalidationHub.Invalidate);
        await firstProvider.GetAsync(request);
        await secondProvider.GetAsync(request);

        Assert.Equal(2, firstWorkspace.ListAgentsCallCount);
        Assert.Equal(2, firstWorkspace.ListProvidersCallCount);
        Assert.Equal(2, secondWorkspace.ListAgentsCallCount);
        Assert.Equal(2, secondWorkspace.ListProvidersCallCount);
    }

    [Fact]
    public void Invalidate_notifies_current_subscribers_only()
    {
        var cache = new AgentReferenceDataCache();
        var notificationCount = 0;
        EventHandler handler = (_, _) => notificationCount++;
        cache.Invalidated += handler;

        cache.Invalidate();
        cache.Invalidated -= handler;
        cache.Invalidate();

        Assert.Equal(1, notificationCount);
    }

    [Fact]
    public void Invalidate_notifies_later_subscribers_when_an_earlier_subscriber_throws()
    {
        var cache = new AgentReferenceDataCache();
        var notificationCount = 0;
        cache.Invalidated += (_, _) => throw new InvalidOperationException("Expected test failure.");
        cache.Invalidated += (_, _) => notificationCount++;

        Assert.Throws<InvalidOperationException>(cache.Invalidate);

        Assert.Equal(1, notificationCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Shared_failure_or_cancellation_removes_cache_entry(
        bool cancelSharedLoad)
    {
        using var cache = new AgentReferenceDataCache();
        var request = new AgentReferenceDataRequest(
            AgentReferenceDataSections.Agents);
        var attempts = 0;

        Task<AgentReferenceDataSnapshot> LoadAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Interlocked.Increment(ref attempts) == 1)
            {
                return cancelSharedLoad
                    ? Task.FromCanceled<AgentReferenceDataSnapshot>(
                        new CancellationToken(canceled: true))
                    : Task.FromException<AgentReferenceDataSnapshot>(
                        new InvalidOperationException("Expected test failure."));
            }

            return Task.FromResult(new AgentReferenceDataSnapshot(
                AgentReferenceDataSections.Agents,
                [],
                [],
                new Dictionary<Guid, ProviderProfile>(),
                DateTimeOffset.UtcNow,
                TimeSpan.Zero));
        }

        if (cancelSharedLoad)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => cache.GetOrCreateAsync(
                    request,
                    DateTimeOffset.UtcNow,
                    TimeSpan.FromMinutes(1),
                    LoadAsync));
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => cache.GetOrCreateAsync(
                    request,
                    DateTimeOffset.UtcNow,
                    TimeSpan.FromMinutes(1),
                    LoadAsync));
        }

        await cache.GetOrCreateAsync(
            request,
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(1),
            LoadAsync);

        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task First_waiter_cancellation_does_not_cancel_or_remove_shared_load()
    {
        var loadStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLoad = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var observedTokens = new List<CancellationToken>();
        var agent = CreateAgent("Agent", AgentLifecycleStatus.Active);
        var workspace = new CountingWorkspaceService(
            [agent],
            [],
            async cancellationToken =>
            {
                lock (observedTokens)
                {
                    observedTokens.Add(cancellationToken);
                }

                loadStarted.TrySetResult();
                await releaseLoad.Task.WaitAsync(cancellationToken);
                return [agent];
            });
        using var cache = new AgentReferenceDataCache();
        var provider = new WorkspaceBackedAgentReferenceDataProvider(
            workspace,
            cache);
        var request = new AgentReferenceDataRequest(
            AgentReferenceDataSections.Agents);
        using var firstWaiterCancellation = new CancellationTokenSource();
        var firstWaiter = provider.GetAsync(
            request,
            firstWaiterCancellation.Token);

        await loadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        firstWaiterCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => firstWaiter);

        var laterWaiter = provider.GetAsync(request);
        releaseLoad.TrySetResult();
        var snapshot = await laterWaiter.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(agent.Id, Assert.Single(snapshot.Agents).Id);
        Assert.Equal(1, workspace.ListAgentsCallCount);
        var factoryToken = Assert.Single(observedTokens);
        Assert.NotEqual(firstWaiterCancellation.Token, factoryToken);
    }

    [Fact]
    public async Task Later_waiter_cancellation_cancels_only_its_wait()
    {
        var loadStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLoad = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = CreateAgent("Agent", AgentLifecycleStatus.Active);
        var workspace = new CountingWorkspaceService(
            [agent],
            [],
            async cancellationToken =>
            {
                loadStarted.TrySetResult();
                await releaseLoad.Task.WaitAsync(cancellationToken);
                return [agent];
            });
        using var cache = new AgentReferenceDataCache();
        var provider = new WorkspaceBackedAgentReferenceDataProvider(
            workspace,
            cache);
        var request = new AgentReferenceDataRequest(
            AgentReferenceDataSections.Agents);
        var firstWaiter = provider.GetAsync(request);
        await loadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var laterWaiterCancellation = new CancellationTokenSource();
        var laterWaiter = provider.GetAsync(
            request,
            laterWaiterCancellation.Token);

        try
        {
            laterWaiterCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => laterWaiter.WaitAsync(TimeSpan.FromMilliseconds(500)));
            Assert.False(firstWaiter.IsCompleted);
        }
        finally
        {
            releaseLoad.TrySetResult();
        }

        var snapshot = await firstWaiter.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(agent.Id, Assert.Single(snapshot.Agents).Id);
        Assert.Equal(1, workspace.ListAgentsCallCount);
    }

    [Fact]
    public async Task Cache_disposal_cancels_shared_load_unsubscribes_and_rejects_new_calls()
    {
        var invalidationHub = new AgentReferenceDataInvalidationHub();
        var cache = new AgentReferenceDataCache(invalidationHub);
        var loadStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLoad = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var factoryCancellationObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var invalidationCount = 0;
        cache.Invalidated += (_, _) => invalidationCount++;
        var agent = CreateAgent("Agent", AgentLifecycleStatus.Active);
        var workspace = new CountingWorkspaceService(
            [agent],
            [],
            async cancellationToken =>
            {
                var waitForRelease = releaseLoad.Task.WaitAsync(cancellationToken);
                using var registration = cancellationToken.Register(
                    factoryCancellationObserved.SetResult);
                loadStarted.TrySetResult();
                await waitForRelease;
                return [agent];
            });
        var provider = new WorkspaceBackedAgentReferenceDataProvider(
            workspace,
            cache);
        var request = new AgentReferenceDataRequest(
            AgentReferenceDataSections.Agents);
        var pendingLoad = provider.GetAsync(request);

        try
        {
            await loadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cache.Dispose();
            cache.Dispose();

            await factoryCancellationObserved.Task.WaitAsync(
                TimeSpan.FromSeconds(5));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => pendingLoad);
            invalidationHub.Invalidate();
            Assert.Equal(0, invalidationCount);
            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => provider.GetAsync(request));
        }
        finally
        {
            releaseLoad.TrySetResult();
            provider.Dispose();
            cache.Dispose();
        }
    }

    [Fact]
    public async Task Provider_disposal_cancels_active_load_and_rejects_new_calls()
    {
        var loadStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLoad = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = CreateAgent("Agent", AgentLifecycleStatus.Active);
        var workspace = new CountingWorkspaceService(
            [agent],
            [],
            async cancellationToken =>
            {
                var waitForRelease = releaseLoad.Task.WaitAsync(cancellationToken);
                using var registration = cancellationToken.Register(
                    cancellationObserved.SetResult);
                loadStarted.TrySetResult();
                await waitForRelease;
                return [agent];
            });
        using var cache = new AgentReferenceDataCache();
        var provider = new WorkspaceBackedAgentReferenceDataProvider(
            workspace,
            cache);
        var request = new AgentReferenceDataRequest(
            AgentReferenceDataSections.Agents);
        var pendingLoad = provider.GetAsync(request);

        try
        {
            await loadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            provider.Dispose();
            provider.Dispose();

            await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => pendingLoad);
            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => provider.GetAsync(request));
        }
        finally
        {
            releaseLoad.TrySetResult();
            provider.Dispose();
        }
    }

    [Fact]
    public async Task Different_reference_data_requests_never_overlap_on_workspace_service()
    {
        var agentCallEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseAgentCall = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var providerCallEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var activeCalls = 0;
        var maximumConcurrentCalls = 0;
        var agent = CreateAgent("Agent", AgentLifecycleStatus.Active);
        var providerProfile = CreateProvider(
            "Provider",
            ProviderProfilePurpose.Chat);
        var workspace = new CountingWorkspaceService(
            [agent],
            [providerProfile],
            async cancellationToken =>
            {
                EnterWorkspaceCall();
                try
                {
                    agentCallEntered.TrySetResult();
                    await releaseAgentCall.Task.WaitAsync(cancellationToken);
                    return [agent];
                }
                finally
                {
                    Interlocked.Decrement(ref activeCalls);
                }
            },
            cancellationToken =>
            {
                EnterWorkspaceCall();
                try
                {
                    providerCallEntered.TrySetResult();
                    return Task.FromResult<IReadOnlyList<ProviderProfile>>(
                        [providerProfile]);
                }
                finally
                {
                    Interlocked.Decrement(ref activeCalls);
                }
            });
        using var cache = new AgentReferenceDataCache();
        var provider = new WorkspaceBackedAgentReferenceDataProvider(
            workspace,
            cache);
        var agentsLoad = provider.GetAsync(new AgentReferenceDataRequest(
            AgentReferenceDataSections.Agents));
        await agentCallEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var providersLoad = provider.GetAsync(new AgentReferenceDataRequest(
            AgentReferenceDataSections.Providers));
        try
        {
            Assert.False(providerCallEntered.Task.IsCompleted);
        }
        finally
        {
            releaseAgentCall.TrySetResult();
        }

        await Task.WhenAll(agentsLoad, providersLoad).WaitAsync(
            TimeSpan.FromSeconds(5));
        Assert.Equal(1, Volatile.Read(ref maximumConcurrentCalls));

        void EnterWorkspaceCall()
        {
            var concurrentCalls = Interlocked.Increment(ref activeCalls);
            var observedMaximum = Volatile.Read(ref maximumConcurrentCalls);
            while (concurrentCalls > observedMaximum)
            {
                var previousMaximum = Interlocked.CompareExchange(
                    ref maximumConcurrentCalls,
                    concurrentCalls,
                    observedMaximum);
                if (previousMaximum == observedMaximum)
                {
                    return;
                }

                observedMaximum = previousMaximum;
            }
        }
    }

    [Fact]
    public async Task Snapshot_defensively_copies_top_level_and_nested_collections()
    {
        var capabilityAssignments = new List<AgentCapabilityAssignment>
        {
            new(
                Guid.NewGuid(),
                "test.capability",
                CapabilityKind.Tool,
                CapabilityProofStatus.Verified,
                DateTimeOffset.UtcNow,
                "Verified.")
        };
        var agentTags = new List<string> { "agent-tag" };
        var allowedSecrets = new List<AgentAllowedSecretReference>
        {
            new(Guid.NewGuid(), "Secret", "test")
        };
        var agent = CreateAgent(
            "Agent",
            AgentLifecycleStatus.Active) with
        {
            Capabilities = capabilityAssignments,
            Tags = agentTags,
            Permissions = AgentPermissionsPolicy.Default with
            {
                AllowedSecrets = allowedSecrets
            }
        };
        var suggestedModels = new List<string> { "test-model" };
        var providerTags = new List<string> { "provider-tag" };
        var modelPrices = new List<ProviderModelTokenPrice>
        {
            new("test-model", 1m, 1m, 1m)
        };
        var providerProfile = CreateProvider(
            "Provider",
            ProviderProfilePurpose.Chat) with
        {
            SuggestedModels = suggestedModels,
            Tags = providerTags,
            ModelPrices = modelPrices
        };
        var agents = new List<AgentDefinition> { agent };
        var providers = new List<ProviderProfile> { providerProfile };
        using var cache = new AgentReferenceDataCache();
        var provider = new WorkspaceBackedAgentReferenceDataProvider(
            new CountingWorkspaceService(agents, providers),
            cache);

        var snapshot = await provider.GetAsync(
            AgentReferenceDataRequest.AgentsAndProviders());

        agents.Clear();
        providers.Clear();
        capabilityAssignments.Clear();
        agentTags.Clear();
        allowedSecrets.Clear();
        suggestedModels.Clear();
        providerTags.Clear();
        modelPrices.Clear();

        var snapshotAgent = Assert.Single(snapshot.Agents);
        Assert.Single(snapshotAgent.Capabilities);
        Assert.Single(snapshotAgent.Tags);
        Assert.Single(snapshotAgent.Permissions.NormalizedAllowedSecrets);
        var snapshotProvider = Assert.Single(snapshot.Providers);
        Assert.Single(snapshotProvider.SuggestedModels);
        Assert.Single(snapshotProvider.Tags);
        Assert.Single(snapshotProvider.ModelPrices);
        Assert.Single(snapshot.ProviderById);
        Assert.Single(
            snapshot.ProviderById[providerProfile.Id].SuggestedModels);

        Assert.Throws<NotSupportedException>(
            () => Assert.IsAssignableFrom<IList<AgentDefinition>>(
                snapshot.Agents).Clear());
        Assert.Throws<NotSupportedException>(
            () => Assert.IsAssignableFrom<IList<AgentCapabilityAssignment>>(
                snapshotAgent.Capabilities).Clear());
        Assert.Throws<NotSupportedException>(
            () => Assert.IsAssignableFrom<IList<ProviderProfile>>(
                snapshot.Providers).Clear());
        Assert.Throws<NotSupportedException>(
            () => Assert.IsAssignableFrom<IDictionary<Guid, ProviderProfile>>(
                snapshot.ProviderById).Clear());
    }

    private static AgentDefinition CreateAgent(
        string name,
        AgentLifecycleStatus status,
        bool isTemplate = false)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            name,
            "Test role",
            "Test summary.",
            "Test instructions.",
            status,
            ProviderProfileId: null,
            Model: "test-model",
            AgentWorkloadKind.General,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: isTemplate,
            TemplateKey: isTemplate ? $"{name}-template" : string.Empty,
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }

    private static ProviderProfile CreateProvider(
        string name,
        ProviderProfilePurpose purpose,
        bool isEnabled = true)
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            name,
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "OPENAI_API_KEY",
            "gpt-5-mini",
            ProviderTransportKind.Responses,
            isEnabled,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: true,
            ConfigurationJson: "{}",
            Notes: "Test provider.",
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: ["gpt-5-mini"],
            purpose);
    }

    private sealed class CountingWorkspaceService(
        IReadOnlyList<AgentDefinition> agents,
        IReadOnlyList<ProviderProfile> providers,
        Func<CancellationToken, Task<IReadOnlyList<AgentDefinition>>>?
            agentsLoader = null,
        Func<CancellationToken, Task<IReadOnlyList<ProviderProfile>>>?
            providersLoader = null) : IAgentFrameworkWorkspaceService
    {
        public event EventHandler<ExecutionLogEntry>? ExecutionUpdated
        {
            add { }
            remove { }
        }

        public int ListAgentsCallCount { get; private set; }

        public int ListProvidersCallCount { get; private set; }

        public Task<IReadOnlyList<AgentDefinition>> ListAgentsAsync(
            bool includeTemplates = true,
            CancellationToken cancellationToken = default)
        {
            ListAgentsCallCount++;
            return agentsLoader?.Invoke(cancellationToken) ??
                Task.FromResult(agents);
        }

        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
        {
            ListProvidersCallCount++;
            return providersLoader?.Invoke(cancellationToken) ??
                Task.FromResult(providers);
        }

        public Task<SandboxDashboardSnapshot> GetDashboardAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentOverviewSnapshot> GetAgentOverviewAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentUsageDetailSnapshot> GetAgentUsageDetailsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderUsageDetailSnapshot> GetProviderUsageDetailsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<ModelUsageDetailSnapshot> GetModelUsageDetailsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentEditorModel> GetAgentEditorAsync(Guid? agentId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveAgentAsync(AgentEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteAgentAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentTeamDefinition>> ListAgentTeamsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentTeamEditorModel> GetAgentTeamEditorAsync(Guid? teamId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveAgentTeamAsync(AgentTeamEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentTeamDefinition> UpdateAgentTeamMembersAsync(Guid teamId, IReadOnlyList<Guid> agentIds, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteAgentTeamAsync(Guid teamId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> CloneAgentAsync(Guid agentId, string cloneName, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> ConvertToTemplateAsync(Guid agentId, string templateKey, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentExportResult> ExportAgentAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> ImportAgentAsync(string packagePath, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderProfileEditorModel> GetProviderEditorAsync(Guid? providerId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveProviderAsync(ProviderProfileEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteProviderAsync(Guid providerId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderHealthResult> TestProviderAsync(Guid providerId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(Guid providerId, ProviderTestChatRequest request, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(Guid providerId, ProviderModelMaintenanceEditorRequest request, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<CapabilityCatalogItem>> ListCapabilitiesAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<CapabilityEditorModel> GetCapabilityEditorAsync(Guid? capabilityId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveCapabilityAsync(CapabilityEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteCapabilityAsync(Guid capabilityId, CancellationToken cancellationToken = default) => throw Unused();

        public Task VerifyCapabilityAsync(Guid agentId, Guid capabilityId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ChatSessionRecord>> ListChatSessionsAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatPageBootstrapSnapshot> GetChatPageBootstrapAsync(bool includeTemplates = false, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatAgentWorkspaceSnapshot> GetChatAgentWorkspaceAsync(Guid agentId, Guid? preferredSessionId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatSessionRecord> GetOrCreateChatSessionAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatSessionRecord> RenameChatSessionAsync(Guid agentId, Guid chatSessionId, string title, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ExecutionRunResult> ExecuteRunAsync(ExecutionRunRequest request, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ExecutionRunResult> ContinueExecutionRunAsync(Guid executionRunId, AgentExecutionOperationId activityOperationId, bool approved, bool autoApprovePendingToolCalls = false, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ExecutionRunResult> ContinueExecutionRunAsync(Guid executionRunId, AgentExecutionOperationId activityOperationId, IReadOnlyList<PendingToolApprovalDecision> decisions, bool autoApprovePendingToolCalls = false, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentChatRunResult> SendMessageAsync(
            Guid agentId,
            Guid? chatSessionId,
            string prompt,
            AgentChatRunOptions options,
            CancellationToken cancellationToken = default,
            IReadOnlyList<string>? attachmentPaths = null) => throw Unused();

        public Task<AgentChatRunResult> RespondToPendingApprovalsAsync(
            Guid agentId,
            Guid chatSessionId,
            AgentExecutionOperationId activityOperationId,
            bool approved,
            bool autoApprovePendingToolCalls = false,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentChatRunResult> RespondToPendingApprovalsAsync(
            Guid agentId,
            Guid chatSessionId,
            AgentExecutionOperationId activityOperationId,
            IReadOnlyList<PendingToolApprovalDecision> decisions,
            bool autoApprovePendingToolCalls = false,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionLogEntry>> ListExecutionLogAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatRuntimeSnapshot> GetChatRuntimeSnapshotAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentRunMetric>> ListMetricsAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentMemoryRecord>> ListMemoryAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveMemoryAsync(MemoryEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteMemoryAsync(Guid memoryId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(ExecutionRunQuery query, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentExecutionReportPage> QueryExecutionReportAsync(
            AgentExecutionReportQuery query,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<ExecutionRunDetail> GetExecutionRunDetailAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionArtifactRecord>> ListExecutionArtifactsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionWorkflowCheckpointRecord>> ListExecutionWorkflowCheckpointsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ToolExecutionReceiptRecord>> ListToolExecutionReceiptsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        private static InvalidOperationException Unused()
        {
            return new InvalidOperationException("This fake member is not used by agent reference data tests.");
        }
    }
}
