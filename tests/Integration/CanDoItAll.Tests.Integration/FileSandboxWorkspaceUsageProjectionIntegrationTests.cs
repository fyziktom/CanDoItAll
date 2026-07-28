using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Integration;

public sealed class FileSandboxWorkspaceUsageProjectionIntegrationTests
{
    [Fact]
    public async Task Usage_projection_delta_matches_canonical_rebuild_after_append()
    {
        await using var scenario = UsageProjectionScenario.Create();
        var runId = Guid.NewGuid();
        var agentId = await scenario.GetAgentIdAsync();
        var createdAtUtc = DateTimeOffset.UtcNow;
        var firstObservation = CreateUsageObservation(
            runId,
            agentId,
            createdAtUtc,
            "OpenAI default",
            ProviderKind.OpenAi,
            "gpt-5.4-mini",
            inputTokens: 120,
            outputTokens: 30);
        await scenario.Store.SaveExecutionRunDetailAsync(
            CreateRunDetail(
                runId,
                agentId,
                createdAtUtc,
                "OpenAI default",
                "gpt-5.4-mini",
                [firstObservation]));
        var secondObservation = CreateUsageObservation(
            runId,
            agentId,
            createdAtUtc.AddSeconds(1),
            "OpenAI default",
            ProviderKind.OpenAi,
            "gpt-5.4-mini",
            inputTokens: 80,
            outputTokens: 20);

        await ((ISandboxWorkspaceExecutionRunMutationStore)scenario.Store)
            .UpdateExecutionRunDetailAsync(
                runId,
                detail => detail with
                {
                    Run = detail.Run with
                    {
                        Revision = detail.Run.Revision + 1,
                        UpdatedAtUtc = createdAtUtc.AddSeconds(2)
                    },
                    UsageObservations =
                    [
                        .. detail.UsageObservations,
                        secondObservation
                    ]
                });
        var incremental = await scenario.Store.LoadUsageProjectionAsync();

        File.Delete(scenario.UsageIndexPath);
        var rebuilt = await new FileSandboxWorkspaceStore(
                scenario.WorkspaceRoot,
                scenario.Scope)
            .LoadUsageProjectionAsync();

        Assert.Equal(incremental.Version, rebuilt.Version);
        Assert.Equal(incremental.Revision, rebuilt.Revision);
        Assert.Equal(incremental.UpdatedAtUtc, rebuilt.UpdatedAtUtc);
        Assert.Equal(incremental.Agents, rebuilt.Agents);
        Assert.Equal(incremental.Providers, rebuilt.Providers);
        Assert.Equal(incremental.Models, rebuilt.Models);
    }

    [Fact]
    public async Task Usage_projection_keeps_delimiter_bearing_model_keys_distinct()
    {
        await using var scenario = UsageProjectionScenario.Create();
        var agentId = await scenario.GetAgentIdAsync();
        var createdAtUtc = DateTimeOffset.UtcNow;
        var firstRunId = Guid.NewGuid();
        var secondRunId = Guid.NewGuid();

        await scenario.Store.SaveExecutionRunDetailAsync(
            CreateRunDetail(
                firstRunId,
                agentId,
                createdAtUtc,
                "alpha:beta",
                "gamma",
                [
                    CreateUsageObservation(
                        firstRunId,
                        agentId,
                        createdAtUtc,
                        "alpha:beta",
                        ProviderKind.OpenAi,
                        "gamma",
                        inputTokens: 10,
                        outputTokens: 5)
                ]));
        await scenario.Store.SaveExecutionRunDetailAsync(
            CreateRunDetail(
                secondRunId,
                agentId,
                createdAtUtc.AddSeconds(1),
                "alpha",
                "beta:gamma",
                [
                    CreateUsageObservation(
                        secondRunId,
                        agentId,
                        createdAtUtc.AddSeconds(1),
                        "alpha",
                        ProviderKind.OpenAi,
                        "beta:gamma",
                        inputTokens: 20,
                        outputTokens: 5)
                ]));

        var projection = await scenario.Store.LoadUsageProjectionAsync();

        Assert.Equal(2, projection.Providers.Count);
        Assert.Equal(2, projection.Models.Count);
        Assert.Contains(
            projection.Models,
            row => row.ProviderName == "alpha:beta" &&
                   row.Model == "gamma" &&
                   row.UsageObservationCount == 1);
        Assert.Contains(
            projection.Models,
            row => row.ProviderName == "alpha" &&
                   row.Model == "beta:gamma" &&
                   row.UsageObservationCount == 1);
    }

    [Fact]
    public async Task Existing_run_update_rejects_usage_observation_removal()
    {
        await using var scenario = UsageProjectionScenario.Create();
        var runId = Guid.NewGuid();
        var agentId = await scenario.GetAgentIdAsync();
        var createdAtUtc = DateTimeOffset.UtcNow;
        await scenario.Store.SaveExecutionRunDetailAsync(
            CreateRunDetail(
                runId,
                agentId,
                createdAtUtc,
                "OpenAI default",
                "gpt-5.4-mini",
                [
                    CreateUsageObservation(
                        runId,
                        agentId,
                        createdAtUtc,
                        "OpenAI default",
                        ProviderKind.OpenAi,
                        "gpt-5.4-mini",
                        inputTokens: 10,
                        outputTokens: 5)
                ]));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => ((ISandboxWorkspaceExecutionRunMutationStore)scenario.Store)
                .UpdateExecutionRunDetailAsync(
                    runId,
                    detail => detail with
                    {
                        Run = NextRevision(detail.Run),
                        UsageObservations = []
                    }));

        Assert.Contains("cannot remove provider usage observation", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Existing_run_update_rejects_usage_observation_identity_change()
    {
        await using var scenario = UsageProjectionScenario.Create();
        var runId = Guid.NewGuid();
        var agentId = await scenario.GetAgentIdAsync();
        var createdAtUtc = DateTimeOffset.UtcNow;
        await scenario.Store.SaveExecutionRunDetailAsync(
            CreateRunDetail(
                runId,
                agentId,
                createdAtUtc,
                "OpenAI default",
                "gpt-5.4-mini",
                [
                    CreateUsageObservation(
                        runId,
                        agentId,
                        createdAtUtc,
                        "OpenAI default",
                        ProviderKind.OpenAi,
                        "gpt-5.4-mini",
                        inputTokens: 10,
                        outputTokens: 5)
                ]));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => ((ISandboxWorkspaceExecutionRunMutationStore)scenario.Store)
                .UpdateExecutionRunDetailAsync(
                    runId,
                    detail => detail with
                    {
                        Run = NextRevision(detail.Run),
                        UsageObservations = detail.UsageObservations
                            .Select(observation => observation with
                            {
                                Model = "gpt-5.4-mini:changed"
                            })
                            .ToArray()
                    }));

        Assert.Contains("cannot change the identity", exception.Message, StringComparison.Ordinal);
    }

    private static ExecutionRunRecord NextRevision(ExecutionRunRecord run)
    {
        return run with
        {
            Revision = run.Revision + 1,
            UpdatedAtUtc = run.UpdatedAtUtc.AddSeconds(1)
        };
    }

    private static ExecutionRunDetail CreateRunDetail(
        Guid runId,
        Guid agentId,
        DateTimeOffset createdAtUtc,
        string providerName,
        string model,
        IReadOnlyList<ProviderUsageObservation> usageObservations)
    {
        return new ExecutionRunDetail(
            new ExecutionRunRecord(
                Id: runId,
                AgentId: agentId,
                ChatSessionId: null,
                Title: "Usage projection test",
                SourceKind: "integration-test",
                SourceId: $"run-{runId:N}",
                CorrelationId: $"corr-{runId:N}",
                CausationId: string.Empty,
                RequestedBy: "test",
                RequestedByKind: "system",
                MetadataJson: "{}",
                InputSummary: "summary",
                ResultSummary: "completed",
                ProviderName: providerName,
                Model: model,
                State: ExecutionState.Completed,
                Outcome: RunOutcome.Succeeded,
                CreatedAtUtc: createdAtUtc,
                UpdatedAtUtc: createdAtUtc,
                StartedAtUtc: createdAtUtc,
                CompletedAtUtc: createdAtUtc,
                RuntimeSessionKey: string.Empty,
                SerializedSessionStateJson: string.Empty,
                PendingApprovals: []),
            ChatSession: null,
            ExecutionLog: [],
            Metrics: [])
        {
            UsageObservations = usageObservations
        };
    }

    private static ProviderUsageObservation CreateUsageObservation(
        Guid executionRunId,
        Guid agentId,
        DateTimeOffset createdAtUtc,
        string providerName,
        ProviderKind providerKind,
        string model,
        int inputTokens,
        int outputTokens)
    {
        return new ProviderUsageObservation(
            Id: Guid.NewGuid(),
            CreatedAtUtc: createdAtUtc,
            ProviderName: providerName,
            ProviderKind: providerKind,
            Model: model,
            TransportKind: ProviderTransportKind.Responses,
            SourcePhase: ProviderUsageSourcePhases.AgentRuntime,
            UsageStatus: ProviderUsageObservationStatus.Observed,
            InputTokens: inputTokens,
            CachedInputTokens: 0,
            OutputTokens: outputTokens,
            ReasoningTokens: 0,
            TotalTokens: inputTokens + outputTokens,
            ToolCallCount: 0)
        {
            ExecutionRunId = executionRunId,
            AgentId = agentId,
            CalculatedCostUsd = 0.001m
        };
    }

    private sealed class UsageProjectionScenario(
        string workspaceRoot,
        WorkspaceScopeDescriptor scope) : IAsyncDisposable
    {
        public string WorkspaceRoot { get; } = workspaceRoot;

        public WorkspaceScopeDescriptor Scope { get; } = scope;

        public FileSandboxWorkspaceStore Store { get; } = new(
            workspaceRoot,
            scope);

        public string UsageIndexPath { get; } = Path.Combine(
            scope.ResolveDataRoot(workspaceRoot),
            "execution",
            "usage-index.json");

        public static UsageProjectionScenario Create()
        {
            var workspaceRoot = Path.Combine(
                Path.GetTempPath(),
                $"candoitall-usage-projection-{Guid.NewGuid():N}");
            return new UsageProjectionScenario(
                workspaceRoot,
                WorkspaceScopeDescriptor.Organization(
                    "usage-projection-test"));
        }

        public async Task<Guid> GetAgentIdAsync()
        {
            var snapshot = await Store.LoadCatalogSnapshotAsync();
            return snapshot.Catalog.Agents
                .First(agent => !agent.IsTemplate)
                .Id;
        }

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(WorkspaceRoot))
            {
                Directory.Delete(WorkspaceRoot, recursive: true);
            }

            return ValueTask.CompletedTask;
        }
    }
}
