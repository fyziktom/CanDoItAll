using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

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
        IReadOnlyList<ProviderProfile> providers) : IAgentFrameworkWorkspaceService
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
            return Task.FromResult(agents);
        }

        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
        {
            ListProvidersCallCount++;
            return Task.FromResult(providers);
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

        public Task<ExecutionRunResult> ContinueExecutionRunAsync(Guid executionRunId, bool approved, bool autoApprovePendingToolCalls = false, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentChatRunResult> SendMessageAsync(
            Guid agentId,
            Guid? chatSessionId,
            string prompt,
            CancellationToken cancellationToken = default,
            IReadOnlyList<string>? attachmentPaths = null,
            AgentChatRunOptions? options = null) => throw Unused();

        public Task<AgentChatRunResult> RespondToPendingApprovalsAsync(
            Guid agentId,
            Guid chatSessionId,
            bool approved,
            bool autoApprovePendingToolCalls = false,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionLogEntry>> ListExecutionLogAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatRuntimeSnapshot> GetChatRuntimeSnapshotAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentRunMetric>> ListMetricsAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentMemoryRecord>> ListMemoryAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveMemoryAsync(MemoryEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteMemoryAsync(Guid memoryId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(ExecutionRunQuery query, CancellationToken cancellationToken = default) => throw Unused();

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
