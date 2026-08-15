using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class HrAgentUsageAnalyticsServiceTests
{
    [Fact]
    public async Task GetAsync_separates_process_scope_and_excludes_estimates_from_known_totals()
    {
        var agent = CreateAgent();
        var provider = CreateProvider();
        var processRunId = Guid.NewGuid().ToString("D");
        var observedProcess = CreateObservation(
            agent.Id,
            provider,
            ProviderUsageObservationStatus.Observed,
            inputTokens: 1_000,
            cachedInputTokens: 100,
            outputTokens: 500,
            reasoningTokens: 50,
            totalTokens: 1_500) with
        {
            ProcessRunId = processRunId,
            ProcessStepId = "process-step-1"
        };
        var estimatedProcess = CreateObservation(
            agent.Id,
            provider,
            ProviderUsageObservationStatus.EstimatedFromMetric,
            inputTokens: 50_000,
            cachedInputTokens: 0,
            outputTokens: 20_000,
            reasoningTokens: 5_000,
            totalTokens: 70_000) with
        {
            ProcessRunId = processRunId,
            ProcessStepId = "process-step-1",
            CalculatedCostUsd = 99m
        };
        var observedChat = CreateObservation(
            agent.Id,
            provider,
            ProviderUsageObservationStatus.Observed,
            inputTokens: 200,
            cachedInputTokens: 0,
            outputTokens: 100,
            reasoningTokens: 0,
            totalTokens: 300) with
        {
            ChatSessionId = Guid.NewGuid(),
            ProviderCostUsd = 0.25m
        };
        var state = SandboxWorkspaceExecutionState.Empty with
        {
            ProviderUsageObservations = [observedProcess, estimatedProcess, observedChat]
        };
        var service = new HrAgentUsageAnalyticsService(
            new StubExecutionStore(state),
            new StubReferenceDataProvider([agent], [provider]));

        var result = await service.GetAsync(
            new HrAgentUsageInput(agent.Id, HrAgentUsageScope.Process),
            CancellationToken.None);

        Assert.Equal(2, result.ObservationCount);
        Assert.Equal(1, result.KnownUsageObservationCount);
        Assert.Equal(1, result.EstimatedUsageObservationCount);
        Assert.Equal(1, result.KnownCostObservationCount);
        Assert.Equal(1, result.UnknownCostObservationCount);
        Assert.Equal(1_000, result.InputTokens);
        Assert.Equal(100, result.CachedInputTokens);
        Assert.Equal(500, result.OutputTokens);
        Assert.Equal(50, result.ReasoningTokens);
        Assert.Equal(1_500, result.TotalTokens);
        Assert.Equal(0.00195m, result.KnownCostUsd);
        Assert.False(result.IsComplete);
        Assert.Contains("include observed usage only", result.CostQualification, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsync_rejects_unknown_agent_instead_of_returning_false_zeroes()
    {
        var service = new HrAgentUsageAnalyticsService(
            new StubExecutionStore(SandboxWorkspaceExecutionState.Empty),
            new StubReferenceDataProvider([], []));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAsync(
            new HrAgentUsageInput(Guid.NewGuid()),
            CancellationToken.None));

        Assert.Contains("was not found", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsync_keeps_workflow_runs_out_of_basic_chat_and_other_scopes()
    {
        var agent = CreateAgent();
        var provider = CreateProvider();
        var executionRunId = Guid.NewGuid();
        var chatSessionId = Guid.NewGuid();
        var run = CreateExecutionRun(
            executionRunId,
            agent.Id,
            chatSessionId,
            sourceKind: "chat-session");
        var workflowObservation = CreateObservation(
            agent.Id,
            provider,
            ProviderUsageObservationStatus.Observed,
            inputTokens: 10,
            cachedInputTokens: 0,
            outputTokens: 5,
            reasoningTokens: 0,
            totalTokens: 15) with
        {
            ExecutionRunId = executionRunId,
            ChatSessionId = chatSessionId,
            WorkflowRunId = Guid.NewGuid().ToString("D")
        };
        var service = CreateService(agent, provider, [run], [workflowObservation]);

        var workflow = await service.GetAsync(
            new HrAgentUsageInput(agent.Id, HrAgentUsageScope.Workflow),
            CancellationToken.None);
        var basicChat = await service.GetAsync(
            new HrAgentUsageInput(agent.Id, HrAgentUsageScope.BasicChat),
            CancellationToken.None);
        var other = await service.GetAsync(
            new HrAgentUsageInput(agent.Id, HrAgentUsageScope.Other),
            CancellationToken.None);

        Assert.Equal(1, workflow.RunCount);
        Assert.Equal(1, workflow.ObservationCount);
        Assert.Equal(0, basicChat.RunCount);
        Assert.Equal(0, basicChat.ObservationCount);
        Assert.Equal(0, other.RunCount);
        Assert.Equal(0, other.ObservationCount);
    }

    [Fact]
    public async Task GetAsync_classifies_manager_reviews_as_other_and_honors_explicit_observation_agent()
    {
        var targetAgent = CreateAgent();
        var managerAgent = CreateAgent();
        var provider = CreateProvider();
        var managerRunId = Guid.NewGuid();
        var managerRun = CreateExecutionRun(
            managerRunId,
            managerAgent.Id,
            Guid.NewGuid(),
            HrAgentExecutionLineage.ManagerReviewSourceKind) with
        {
            ProcessRunId = Guid.NewGuid().ToString("D")
        };
        var managerObservation = CreateObservation(
            managerAgent.Id,
            provider,
            ProviderUsageObservationStatus.Observed,
            inputTokens: 10,
            cachedInputTokens: 0,
            outputTokens: 5,
            reasoningTokens: 0,
            totalTokens: 15) with
        {
            ExecutionRunId = managerRunId,
            ChatSessionId = managerRun.ChatSessionId,
            ProcessRunId = managerRun.ProcessRunId
        };
        var mismatchedObservation = managerObservation with
        {
            Id = Guid.NewGuid(),
            AgentId = targetAgent.Id,
            InputTokens = 100
        };
        var service = new HrAgentUsageAnalyticsService(
            new StubExecutionStore(SandboxWorkspaceExecutionState.Empty with
            {
                ExecutionRuns = [managerRun],
                ProviderUsageObservations = [managerObservation, mismatchedObservation]
            }),
            new StubReferenceDataProvider([targetAgent, managerAgent], [provider]));

        var managerProcess = await service.GetAsync(
            new HrAgentUsageInput(managerAgent.Id, HrAgentUsageScope.Process),
            CancellationToken.None);
        var managerOther = await service.GetAsync(
            new HrAgentUsageInput(managerAgent.Id, HrAgentUsageScope.Other),
            CancellationToken.None);
        var targetOther = await service.GetAsync(
            new HrAgentUsageInput(targetAgent.Id, HrAgentUsageScope.Other),
            CancellationToken.None);

        Assert.Equal(0, managerProcess.RunCount);
        Assert.Equal(0, managerProcess.ObservationCount);
        Assert.Equal(1, managerOther.RunCount);
        Assert.Equal(1, managerOther.ObservationCount);
        Assert.Equal(0, targetOther.RunCount);
        Assert.Equal(1, targetOther.ObservationCount);
    }

    private static ProviderUsageObservation CreateObservation(
        Guid agentId,
        ProviderProfile provider,
        ProviderUsageObservationStatus status,
        int inputTokens,
        int cachedInputTokens,
        int outputTokens,
        int reasoningTokens,
        int totalTokens)
    {
        return new ProviderUsageObservation(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            provider.Name,
            provider.Kind,
            provider.DefaultModel,
            provider.Transport,
            ProviderUsageSourcePhases.AgentRuntime,
            status,
            inputTokens,
            cachedInputTokens,
            outputTokens,
            reasoningTokens,
            totalTokens,
            ToolCallCount: 0)
        {
            AgentId = agentId
        };
    }

    private static HrAgentUsageAnalyticsService CreateService(
        AgentDefinition agent,
        ProviderProfile provider,
        IReadOnlyList<ExecutionRunRecord> runs,
        IReadOnlyList<ProviderUsageObservation> observations)
    {
        return new HrAgentUsageAnalyticsService(
            new StubExecutionStore(SandboxWorkspaceExecutionState.Empty with
            {
                ExecutionRuns = runs,
                ProviderUsageObservations = observations
            }),
            new StubReferenceDataProvider([agent], [provider]));
    }

    private static ExecutionRunRecord CreateExecutionRun(
        Guid id,
        Guid agentId,
        Guid? chatSessionId,
        string sourceKind)
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunRecord(
            id,
            agentId,
            chatSessionId,
            "Usage classification run",
            sourceKind,
            string.Empty,
            id.ToString("D"),
            string.Empty,
            "test",
            "test",
            "{}",
            string.Empty,
            string.Empty,
            "Priced provider",
            "priced-model",
            ExecutionState.Completed,
            RunOutcome.Succeeded,
            now,
            now,
            now,
            now,
            string.Empty,
            null,
            []);
    }

    private static AgentDefinition CreateAgent()
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            "Usage target",
            "Specialist",
            string.Empty,
            "Work carefully.",
            AgentLifecycleStatus.Active,
            null,
            string.Empty,
            AgentWorkloadKind.General,
            AgentChatHistoryMode.FrameworkManaged,
            0.2d,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }

    private static ProviderProfile CreateProvider()
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            "Priced provider",
            ProviderKind.OpenAi,
            "https://api.openai.com",
            "OPENAI_API_KEY",
            "priced-model",
            ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: [],
            ProviderProfilePurpose.Chat)
        {
            ModelPrices =
            [
                new ProviderModelTokenPrice(
                    "priced-model",
                    InputPerMillionTokensUsd: 1m,
                    CachedInputPerMillionTokensUsd: 0.5m,
                    OutputPerMillionTokensUsd: 2m)
            ]
        };
    }

    private sealed class StubReferenceDataProvider(
        IReadOnlyList<AgentDefinition> agents,
        IReadOnlyList<ProviderProfile> providers) : IAgentReferenceDataProvider
    {
        public Task<AgentReferenceDataSnapshot> GetAsync(
            AgentReferenceDataRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AgentReferenceDataSnapshot(
                AgentReferenceDataSections.Agents | AgentReferenceDataSections.Providers,
                agents,
                providers,
                providers.ToDictionary(provider => provider.Id),
                DateTimeOffset.UtcNow,
                TimeSpan.Zero));
        }
    }

    private sealed class StubExecutionStore(SandboxWorkspaceExecutionState state) : ISandboxWorkspaceExecutionStore
    {
        public Task<SandboxWorkspaceExecutionState> LoadExecutionAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(state);
        }

        public Task<SandboxWorkspaceExecutionSummary> LoadExecutionSummaryAsync(
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentUsageProjection> LoadUsageProjectionAsync(
            CancellationToken cancellationToken = default) => throw Unused();

        public Task SaveExecutionAsync(
            SandboxWorkspaceExecutionState executionState,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<SandboxWorkspaceExecutionState> UpdateExecutionAsync(
            Func<SandboxWorkspaceExecutionState, SandboxWorkspaceExecutionState> update,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<SandboxWorkspaceExecutionState> UpdateExecutionAsync(
            Func<SandboxWorkspaceExecutionState, SandboxWorkspaceExecutionState> update,
            long expectedRevision,
            CancellationToken cancellationToken = default) => throw Unused();

        private static NotSupportedException Unused()
        {
            return new NotSupportedException("This member is not used by HR usage analytics tests.");
        }
    }
}
