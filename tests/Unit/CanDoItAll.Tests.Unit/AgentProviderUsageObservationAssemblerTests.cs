using CanDoItAll.AgentFramework.Core.Execution;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentProviderUsageObservationAssemblerTests
{
    [Fact]
    public void ExistingAgentProjectionRemainsCompatible()
    {
        var provider = CreateProvider();
        var agent = CreateAgent(provider.Id);
        var run = CreateRun(agent.Id);
        var metric = new AgentRunMetric(
            Guid.NewGuid(),
            agent.Id,
            run.ChatSessionId,
            run.CreatedAtUtc,
            RunOutcome.Succeeded,
            provider.Name,
            "model-a",
            DurationMs: 10,
            InputTokens: 1_000,
            OutputTokens: 500,
            ToolCalls: 0)
        {
            ExecutionRunId = run.Id
        };
        var response = new AgentRuntimeResponse(
            "done",
            InputTokens: 1_000,
            OutputTokens: 500,
            ToolCalls: 0,
            RuntimeSessionKey: "runtime-session",
            SerializedSessionStateJson: null,
            PendingApprovals: []);

        var observations = new AgentProviderUsageObservationAssembler().BuildUsageObservations(
            run,
            agent,
            provider,
            metric,
            response,
            "model-a");

        var observation = Assert.Single(observations);
        Assert.Equal(run.Id, observation.ExecutionRunId);
        Assert.Equal(agent.Id, observation.AgentId);
        Assert.Equal(provider.Id, observation.ProviderProfileId);
        Assert.Equal("model-a", observation.Model);
        Assert.Equal(1_500, observation.TotalTokens);
        Assert.NotNull(observation.CalculatedCostUsd);
        Assert.True(observation.CalculatedCostUsd > 0m);
    }

    [Fact]
    public void RepairUsageIsAppendedWithoutReplacingRuntimeUsage()
    {
        var runtimeObservation = CreateObservation(ProviderUsageSourcePhases.AgentRuntime);
        var repairObservation = CreateObservation(string.Empty);
        var response = new AgentRuntimeResponse(
            "invalid",
            InputTokens: 2,
            OutputTokens: 1,
            ToolCalls: 0,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: [])
        {
            UsageObservations = [runtimeObservation]
        };
        var repair = new AgentOutputRepairAttemptResult
        {
            Succeeded = true,
            RepairedRawOutput = "{}",
            RemainingErrors = [],
            FailureMessage = string.Empty,
            UsageObservations = [repairObservation]
        };

        var result = AgentProviderUsageObservationAssembler.AppendRepairUsageObservations(response, repair);

        Assert.Equal(2, result.UsageObservations.Count);
        Assert.Equal(runtimeObservation.Id, result.UsageObservations[0].Id);
        Assert.Equal(
            ProviderUsageSourcePhases.StructuredOutputRepair,
            result.UsageObservations[1].SourcePhase);
    }

    private static ProviderProfile CreateProvider()
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            "Provider",
            ProviderKind.OpenAi,
            "https://example.test",
            "API_KEY",
            "model-a",
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: false,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: ["model-a"])
        {
            ModelPrices = [new ProviderModelTokenPrice("model-a", 1m, 0.1m, 2m)]
        };
    }

    private static AgentDefinition CreateAgent(Guid providerId)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            "Agent",
            "Agent",
            "Summary",
            "Instructions",
            AgentLifecycleStatus.Active,
            providerId,
            "model-a",
            AgentWorkloadKind.General,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }

    private static ExecutionRunRecord CreateRun(Guid agentId)
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunRecord(
            Guid.NewGuid(),
            agentId,
            Guid.NewGuid(),
            "Run",
            SourceKind: "test",
            SourceId: "source",
            CorrelationId: "correlation",
            CausationId: string.Empty,
            RequestedBy: "test",
            RequestedByKind: "test",
            MetadataJson: "{}",
            InputSummary: "prompt",
            ResultSummary: string.Empty,
            ProviderName: "Provider",
            Model: "model-a",
            State: ExecutionState.Running,
            Outcome: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: now,
            CompletedAtUtc: null,
            RuntimeSessionKey: "runtime-session",
            SerializedSessionStateJson: null,
            PendingApprovals: []);
    }

    private static ProviderUsageObservation CreateObservation(string sourcePhase)
    {
        return new ProviderUsageObservation(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "Provider",
            ProviderKind.OpenAi,
            "model-a",
            ProviderTransportKind.ChatCompletions,
            sourcePhase,
            ProviderUsageObservationStatus.Observed,
            InputTokens: 1,
            CachedInputTokens: 0,
            OutputTokens: 1,
            ReasoningTokens: 0,
            TotalTokens: 2,
            ToolCallCount: 0);
    }
}
