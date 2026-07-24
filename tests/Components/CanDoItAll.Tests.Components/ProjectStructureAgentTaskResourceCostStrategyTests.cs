using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureAgentTaskResourceCostStrategyTests
{
    private static readonly Guid ProjectId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid PartyId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid TechnicalAgentId = Guid.Parse("30000000-0000-0000-0000-000000000003");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-17T10:00:00Z");

    [Fact]
    public async Task Quote_maps_the_CRM_party_to_technical_agent_and_averages_complete_run_history()
    {
        var agent = CreateAgent(TechnicalAgentId);
        var provider = CreateProvider();
        var firstRun = CreateRun(TechnicalAgentId, Now.AddMinutes(-2));
        var secondRun = CreateRun(TechnicalAgentId, Now.AddMinutes(-1));
        var usageAnalytics = new HrAgentUsageAnalyticsService(
            new StubExecutionStore(SandboxWorkspaceExecutionState.Empty with
            {
                ExecutionRuns = [firstRun, secondRun],
                ProviderUsageObservations =
                [
                    CreateObservation(firstRun.Id, TechnicalAgentId, provider, 2m),
                    CreateObservation(secondRun.Id, TechnicalAgentId, provider, 4m)
                ]
            }),
            new StubReferenceDataProvider([agent], [provider]));
        var bridge = new StubTechnicalAgentBridge(CreateBoundSummary());
        var strategy = new ProjectStructureAgentTaskResourceCostStrategy(
            bridge,
            usageAnalytics,
            new FixedTimeProvider(Now));

        var quote = await strategy.GetQuoteAsync(CreateRequest());

        Assert.True(quote.IsAvailable);
        Assert.Equal(3m, quote.Amount);
        Assert.Equal("USD", quote.CurrencyCode);
        Assert.Equal("Agent run history", quote.Source);
        Assert.Equal(ProjectStructureTaskResourceCostSource.AgentRunHistory, quote.SourceKind);
        Assert.Equal(Now, quote.CalculatedAtUtc);
        Assert.Equal([PartyId], bridge.RequestedPartyIds);
    }

    [Fact]
    public async Task Quote_is_unavailable_when_the_CRM_party_has_no_bound_technical_agent()
    {
        var usageAnalytics = new HrAgentUsageAnalyticsService(
            new StubExecutionStore(SandboxWorkspaceExecutionState.Empty),
            new StubReferenceDataProvider([], []));
        var bridge = new StubTechnicalAgentBridge(new AiTechnicalAgentDirectorySummary(
            TechnicalAgentId: null,
            AiResourceBindingStatus.Unbound,
            "Not bound",
            ExecutionMode: null,
            ProviderName: string.Empty,
            DefaultModel: string.Empty,
            CapabilityCount: 0,
            HasTechnicalProfile: false,
            AgentsRoute: string.Empty));
        var strategy = new ProjectStructureAgentTaskResourceCostStrategy(
            bridge,
            usageAnalytics,
            new FixedTimeProvider(Now));

        var quote = await strategy.GetQuoteAsync(CreateRequest());

        Assert.False(quote.IsAvailable);
        Assert.Null(quote.Amount);
        Assert.Equal(ProjectStructureTaskResourceCostSource.AgentRunHistory, quote.SourceKind);
        Assert.Contains("not bound", quote.Summary, StringComparison.OrdinalIgnoreCase);
    }

    private static ProjectStructureTaskResourceCostRequest CreateRequest()
    {
        return new ProjectStructureTaskResourceCostRequest(
            ProjectId,
            new ProjectStructureTaskResourceSelection(
                ProjectStructureTaskResourceKind.Agent,
                PartyId),
            new ProjectTaskEstimate(8m, ProjectWorkItemEffortUnit.Hours, null, string.Empty));
    }

    private static AiTechnicalAgentDirectorySummary CreateBoundSummary()
    {
        return new AiTechnicalAgentDirectorySummary(
            TechnicalAgentId,
            AiResourceBindingStatus.Bound,
            "Bound",
            AiExecutionMode.Remote,
            "Provider",
            "priced-model",
            CapabilityCount: 0,
            HasTechnicalProfile: true,
            AgentsRoute: $"/agents/{TechnicalAgentId:D}");
    }

    private static AgentDefinition CreateAgent(Guid id)
    {
        return new AgentDefinition(
            id,
            "Cost estimator",
            "Specialist",
            string.Empty,
            "Estimate cost.",
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
            CreatedAtUtc: Now,
            UpdatedAtUtc: Now);
    }

    private static ProviderProfile CreateProvider()
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            "Provider",
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
            ProviderProfilePurpose.Chat);
    }

    private static ExecutionRunRecord CreateRun(Guid agentId, DateTimeOffset createdAtUtc)
    {
        var id = Guid.NewGuid();
        return new ExecutionRunRecord(
            id,
            agentId,
            ChatSessionId: null,
            "Historical agent run",
            "test",
            string.Empty,
            id.ToString("D"),
            string.Empty,
            "test",
            "test",
            "{}",
            string.Empty,
            string.Empty,
            "Provider",
            "priced-model",
            ExecutionState.Completed,
            RunOutcome.Succeeded,
            createdAtUtc,
            createdAtUtc,
            createdAtUtc,
            createdAtUtc,
            string.Empty,
            null,
            []);
    }

    private static ProviderUsageObservation CreateObservation(
        Guid executionRunId,
        Guid agentId,
        ProviderProfile provider,
        decimal cost)
    {
        return new ProviderUsageObservation(
            Guid.NewGuid(),
            Now,
            provider.Name,
            provider.Kind,
            provider.DefaultModel,
            provider.Transport,
            ProviderUsageSourcePhases.AgentRuntime,
            ProviderUsageObservationStatus.Observed,
            InputTokens: 100,
            CachedInputTokens: 0,
            OutputTokens: 50,
            ReasoningTokens: 0,
            TotalTokens: 150,
            ToolCallCount: 0)
        {
            AgentId = agentId,
            ExecutionRunId = executionRunId,
            ProviderCostUsd = cost
        };
    }

    private sealed class StubTechnicalAgentBridge(
        AiTechnicalAgentDirectorySummary summary) : IAiTechnicalAgentBridge
    {
        public IReadOnlyList<Guid> RequestedPartyIds { get; private set; } = [];

        public Task<IReadOnlyDictionary<Guid, AiTechnicalAgentDirectorySummary>> GetDirectorySummariesAsync(
            IReadOnlyList<Guid> partyIds,
            CancellationToken cancellationToken = default)
        {
            RequestedPartyIds = partyIds.ToArray();
            return Task.FromResult<IReadOnlyDictionary<Guid, AiTechnicalAgentDirectorySummary>>(
                new Dictionary<Guid, AiTechnicalAgentDirectorySummary>
                {
                    [PartyId] = summary
                });
        }

        public Task SynchronizeDirectoryProjectionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AiTechnicalAgentWorkspaceModel> GetWorkspaceAsync(
            Guid partyId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyDictionary<Guid, AiAgentStaffingFactModel>> GetStaffingFactsAsync(
            IReadOnlyList<Guid> partyIds,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result<AiTechnicalAgentSaveResult>> SaveAsync(
            AiAgentProfileEditorModel model,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
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
                Now,
                TimeSpan.Zero));
        }
    }

    private sealed class StubExecutionStore(
        SandboxWorkspaceExecutionState state) : ISandboxWorkspaceExecutionStore
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
            return new NotSupportedException("This member is not used by the cost strategy tests.");
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
