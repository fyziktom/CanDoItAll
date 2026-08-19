using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class FileSandboxWorkspaceGenericNewRunCommitRecoveryIntegrationTests
{
    [Theory]
    [InlineData((int)GenericNewRunCommitStage.JournalPersisted)]
    [InlineData((int)GenericNewRunCommitStage.ExecutionSlicesPersisted)]
    [InlineData((int)GenericNewRunCommitStage.ExecutionIndexPersisted)]
    [InlineData((int)GenericNewRunCommitStage.UsageIndexPersisted)]
    [InlineData((int)GenericNewRunCommitStage.WorkspaceIndexPersisted)]
    [InlineData((int)GenericNewRunCommitStage.ChatIndexPersisted)]
    public async Task ReserveExecutionRunAsync_FailureAfterCommitBoundary_RecoversEveryProjectionExactlyOnce(
        int failureStageValue)
    {
        var failureStage = (GenericNewRunCommitStage)failureStageValue;
        var failureInjected = false;
        await using var scenario = await CreateScenarioAsync(stage =>
        {
            if (!failureInjected &&
                stage == failureStage)
            {
                failureInjected = true;
                throw new InjectedCommitFailureException(stage);
            }
        });

        await Assert.ThrowsAsync<InjectedCommitFailureException>(
            () => ((ISandboxWorkspaceExecutionRunReservationStore)scenario.Store)
                .ReserveExecutionRunAsync(
                    scenario.Source,
                    scenario.Candidate));

        Assert.True(failureInjected);
        Assert.True(File.Exists(scenario.JournalPath));

        var recoveryStore = new FileSandboxWorkspaceStore(
            scenario.WorkspaceRoot,
            scenario.Scope);
        var recoveredDetail =
            await recoveryStore.GetExecutionRunDetailAsync(
                scenario.Candidate.Run.Id);
        var recoveredRuns =
            await recoveryStore.ListExecutionRunsAsync();
        var recoveredExecutionIndex =
            await ReadJsonAsync<ExecutionStorageIndex>(
                scenario.ExecutionIndexPath);
        var recoveredUsageIndex =
            await ReadJsonAsync<AgentUsageProjection>(
                scenario.UsageIndexPath);
        var recoveredWorkspaceIndex =
            await ReadJsonAsync<WorkspaceStorageIndex>(
                scenario.WorkspaceIndexPath);
        var recoveredChatIndex =
            await ReadJsonAsync<ExecutionChatIndex>(
                scenario.ChatIndexPath);

        Assert.False(File.Exists(scenario.JournalPath));
        Assert.NotNull(recoveredDetail);
        Assert.NotNull(recoveredExecutionIndex);
        Assert.NotNull(recoveredUsageIndex);
        Assert.NotNull(recoveredWorkspaceIndex);
        Assert.NotNull(recoveredChatIndex);
        Assert.Equal(
            scenario.Candidate.Run.Id,
            recoveredDetail!.Run.Id);
        Assert.Equal(
            scenario.Candidate.Run.SourceKind,
            recoveredDetail.Run.SourceKind);
        Assert.Equal(
            scenario.Candidate.Run.SourceId,
            recoveredDetail.Run.SourceId);
        Assert.Equal(
            scenario.Candidate.Run.State,
            recoveredDetail.Run.State);
        Assert.Equal(
            scenario.Candidate.Run.Outcome,
            recoveredDetail.Run.Outcome);
        Assert.Empty(recoveredDetail.Run.PendingApprovals);
        Assert.Equal(
            scenario.Candidate.ExecutionLog.ToArray(),
            recoveredDetail.ExecutionLog.ToArray());
        Assert.Equal(
            scenario.Candidate.Metrics.ToArray(),
            recoveredDetail.Metrics.ToArray());
        Assert.Equal(
            scenario.Candidate.UsageObservations.ToArray(),
            recoveredDetail.UsageObservations.ToArray());
        Assert.Equal(
            1,
            recoveredRuns.Count(run =>
                run.Id == scenario.Candidate.Run.Id));

        Assert.Equal(
            scenario.BeforeExecutionIndex.Revision + 1L,
            recoveredExecutionIndex!.Revision);
        Assert.Equal(
            scenario.BeforeExecutionIndex.RunCount + 1,
            recoveredExecutionIndex.RunCount);
        Assert.Equal(
            scenario.BeforeExecutionIndex.LogCount + 1,
            recoveredExecutionIndex.LogCount);
        Assert.Equal(
            scenario.BeforeExecutionIndex.MetricCount + 1,
            recoveredExecutionIndex.MetricCount);
        Assert.Equal(
            scenario.BeforeExecutionIndex.UsageObservationCount + 1,
            recoveredExecutionIndex.UsageObservationCount);
        Assert.Equal(
            scenario.BeforeUsageIndex.Revision + 1L,
            recoveredUsageIndex!.Revision);
        Assert.Equal(
            scenario.BeforeUsageIndex.UsageObservationCount + 1,
            recoveredUsageIndex.UsageObservationCount);
        Assert.Equal(
            scenario.BeforeUsageIndex.TotalTokens + 15,
            recoveredUsageIndex.TotalTokens);
        Assert.Equal(
            scenario.BeforeWorkspaceIndex.Revision + 1L,
            recoveredWorkspaceIndex!.Revision);
        Assert.Equal(
            recoveredExecutionIndex.Revision,
            recoveredChatIndex!.Revision);
        Assert.Equal(
            1,
            recoveredChatIndex.RunSummaries.Count(summary =>
                summary.ExecutionRunId ==
                    scenario.Candidate.Run.Id));

        var idempotencyStore = new FileSandboxWorkspaceStore(
            scenario.WorkspaceRoot,
            scenario.Scope);
        var secondRead = await idempotencyStore.GetExecutionRunDetailAsync(
            scenario.Candidate.Run.Id);
        var secondExecutionIndex =
            await ReadJsonAsync<ExecutionStorageIndex>(
                scenario.ExecutionIndexPath);
        var secondUsageIndex =
            await ReadJsonAsync<AgentUsageProjection>(
                scenario.UsageIndexPath);
        var secondWorkspaceIndex =
            await ReadJsonAsync<WorkspaceStorageIndex>(
                scenario.WorkspaceIndexPath);

        Assert.NotNull(secondRead);
        Assert.Equal(recoveredDetail.Run.Id, secondRead!.Run.Id);
        Assert.Equal(
            recoveredDetail.ExecutionLog.ToArray(),
            secondRead.ExecutionLog.ToArray());
        Assert.Equal(
            recoveredDetail.Metrics.ToArray(),
            secondRead.Metrics.ToArray());
        Assert.Equal(
            recoveredDetail.UsageObservations.ToArray(),
            secondRead.UsageObservations.ToArray());
        Assert.Equal(recoveredExecutionIndex, secondExecutionIndex);
        Assert.NotNull(secondUsageIndex);
        Assert.Equal(
            recoveredUsageIndex.Revision,
            secondUsageIndex!.Revision);
        Assert.Equal(
            recoveredUsageIndex.UpdatedAtUtc,
            secondUsageIndex.UpdatedAtUtc);
        Assert.Equal(
            recoveredUsageIndex.Agents.ToArray(),
            secondUsageIndex.Agents.ToArray());
        Assert.Equal(
            recoveredUsageIndex.Providers.ToArray(),
            secondUsageIndex.Providers.ToArray());
        Assert.Equal(
            recoveredUsageIndex.Models.ToArray(),
            secondUsageIndex.Models.ToArray());
        Assert.Equal(recoveredWorkspaceIndex, secondWorkspaceIndex);
    }

    private static async Task<Scenario> CreateScenarioAsync(
        Action<GenericNewRunCommitStage> commitBoundary)
    {
        var environment = CanDoItAllTestEnvironment.Create(
            $"generic-new-run-recovery-{Guid.NewGuid():N}");
        try
        {
            var profile =
                environment.CreateInMemoryProfile("primary");
            var scope = WorkspaceScopeDescriptor.Sandbox;
            var setupStore = new FileSandboxWorkspaceStore(
                profile.WorkspaceRootPath,
                scope);
            var catalog =
                await setupStore.LoadCatalogSnapshotAsync();
            var agent = catalog.Catalog.Agents.First(candidate =>
                candidate.ProviderProfileId.HasValue);
            var candidate = CreateCandidate(agent);
            var dataRoot = scope.ResolveDataRoot(
                profile.WorkspaceRootPath);
            var executionRoot = Path.Combine(
                dataRoot,
                "execution");
            var executionIndexPath = Path.Combine(
                executionRoot,
                "index.json");
            var usageIndexPath = Path.Combine(
                executionRoot,
                "usage-index.json");
            var workspaceIndexPath = Path.Combine(
                dataRoot,
                "workspace.index.json");
            var beforeExecutionIndex =
                await ReadJsonAsync<ExecutionStorageIndex>(
                    executionIndexPath)
                ?? throw new InvalidDataException(
                    "The execution index was not initialized.");
            var beforeUsageIndex =
                await ReadJsonAsync<AgentUsageProjection>(
                    usageIndexPath)
                ?? throw new InvalidDataException(
                    "The usage index was not initialized.");
            var beforeWorkspaceIndex =
                await ReadJsonAsync<WorkspaceStorageIndex>(
                    workspaceIndexPath)
                ?? throw new InvalidDataException(
                    "The workspace index was not initialized.");
            var store = new FileSandboxWorkspaceStore(
                profile.WorkspaceRootPath,
                scope,
                chatBackedRunCommitBoundary: null,
                existingRunDetailCommitBoundary: null,
                genericNewRunCommitBoundary: commitBoundary,
                jsonReadDiagnostics: null);

            return new Scenario(
                environment,
                profile.WorkspaceRootPath,
                scope,
                store,
                new ExecutionRunSourceKey(
                    candidate.Run.SourceKind,
                    candidate.Run.SourceId),
                candidate,
                beforeExecutionIndex,
                beforeUsageIndex,
                beforeWorkspaceIndex,
                Path.Combine(
                    executionRoot,
                    "pending-run-start.json"),
                executionIndexPath,
                usageIndexPath,
                workspaceIndexPath,
                Path.Combine(
                    executionRoot,
                    "chat-index.json"));
        }
        catch
        {
            await environment.DisposeAsync();
            throw;
        }
    }

    private static ExecutionRunDetail CreateCandidate(
        AgentDefinition agent)
    {
        var now = DateTimeOffset.UtcNow;
        var runId = Guid.NewGuid();
        const string providerName = "OpenAI integration";
        const string model = "gpt-5.4-mini";
        var run = new ExecutionRunRecord(
            Id: runId,
            AgentId: agent.Id,
            ChatSessionId: null,
            Title: "Generic reservation recovery",
            SourceKind: "integration-test",
            SourceId: $"generic-{runId:N}",
            CorrelationId: $"correlation-{runId:N}",
            CausationId: string.Empty,
            RequestedBy: "test",
            RequestedByKind: "system",
            MetadataJson: "{}",
            InputSummary: "Persist every generic run projection.",
            ResultSummary: "Completed.",
            ProviderName: providerName,
            Model: model,
            State: ExecutionState.Completed,
            Outcome: RunOutcome.Succeeded,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: now,
            CompletedAtUtc: now,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: string.Empty,
            PendingApprovals: [])
        {
            ProviderProfileId = agent.ProviderProfileId
        };
        var log = new ExecutionLogEntry(
            Guid.NewGuid(),
            agent.Id,
            ChatSessionId: null,
            now,
            ExecutionState.Completed,
            "Completed",
            "Generic run completed.")
        {
            ExecutionRunId = runId
        };
        var metric = new AgentRunMetric(
            Guid.NewGuid(),
            agent.Id,
            ChatSessionId: null,
            now,
            RunOutcome.Succeeded,
            providerName,
            model,
            DurationMs: 5,
            InputTokens: 10,
            OutputTokens: 5,
            ToolCalls: 0)
        {
            ExecutionRunId = runId,
            CostUsd = 0.001m
        };
        var usage = new ProviderUsageObservation(
            Id: Guid.NewGuid(),
            CreatedAtUtc: now,
            ProviderName: providerName,
            ProviderKind: ProviderKind.OpenAi,
            Model: model,
            TransportKind: ProviderTransportKind.Responses,
            SourcePhase: ProviderUsageSourcePhases.AgentRuntime,
            UsageStatus: ProviderUsageObservationStatus.Observed,
            InputTokens: 10,
            CachedInputTokens: 0,
            OutputTokens: 5,
            ReasoningTokens: 0,
            TotalTokens: 15,
            ToolCallCount: 0)
        {
            ExecutionRunId = runId,
            AgentId = agent.Id,
            CalculatedCostUsd = 0.001m
        };

        return new ExecutionRunDetail(
            run,
            ChatSession: null,
            ExecutionLog: [log],
            Metrics: [metric])
        {
            UsageObservations = [usage]
        };
    }

    private static Task<T?> ReadJsonAsync<T>(
        string path)
    {
        return new FileSandboxWorkspaceJsonStore()
            .ReadJsonAsync<T>(
                path,
                CancellationToken.None);
    }

    private sealed class InjectedCommitFailureException(
        GenericNewRunCommitStage stage)
        : IOException(
            $"Injected generic execution-run creation failure after '{stage}'.");

    private sealed record Scenario(
        CanDoItAllTestEnvironment Environment,
        string WorkspaceRoot,
        WorkspaceScopeDescriptor Scope,
        FileSandboxWorkspaceStore Store,
        ExecutionRunSourceKey Source,
        ExecutionRunDetail Candidate,
        ExecutionStorageIndex BeforeExecutionIndex,
        AgentUsageProjection BeforeUsageIndex,
        WorkspaceStorageIndex BeforeWorkspaceIndex,
        string JournalPath,
        string ExecutionIndexPath,
        string UsageIndexPath,
        string WorkspaceIndexPath,
        string ChatIndexPath) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            return Environment.DisposeAsync();
        }
    }
}
