using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration;

public sealed class WorkflowUsagePersistenceIntegrationTests
{
    private static readonly DateTimeOffset RecordedAtUtc = new(2026, 7, 12, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PostgreSqlPersistsImmutableUsageFactsAndExecutesDatabaseAggregates()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("workflowusageanalytics");
        var options = database.CreateAppDbContextOptions();
        await using (var dbContext = new AppDbContext(options))
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        var factory = new WorkflowUsagePostgresDbContextFactory(options);
        var runStore = new PersistentWorkflowRunStore(factory);
        var usageStore = new PersistentWorkflowUsageObservationStore(factory);
        var runId = WorkflowRunId.New();
        var workflowId = WorkflowId.New();
        var versionId = WorkflowVersionId.New();
        var origin = new WorkflowLaunchOrigin.ProcessAssignment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new WorkflowLaunchCorrelationId("postgres-workflow-usage"));
        var run = new WorkflowRunSnapshot(
            runId,
            workflowId,
            versionId,
            WorkflowRunState.Completed,
            WorkflowRuntimeBackendKind.InProcess,
            "postgres-usage-run",
            "Completed",
            RecordedAtUtc.AddSeconds(-5),
            RecordedAtUtc)
        {
            TerminalAtUtc = RecordedAtUtc,
            Origin = origin
        };
        var known = CreateObservation(
            WorkflowUsageObservationId.New(),
            runId,
            workflowId,
            versionId,
            "model-a",
            WorkflowPricingStatus.Known,
            CostUsd: 0m,
            inputTokens: 10) with
        {
            Origin = origin
        };
        var unknown = CreateObservation(
            WorkflowUsageObservationId.New(),
            runId,
            workflowId,
            versionId,
            "model-b",
            WorkflowPricingStatus.Unknown,
            CostUsd: null,
            inputTokens: 4) with
        {
            Origin = origin
        };

        await runStore.SaveRunAsync(run);
        await usageStore.AppendRangeAsync([known, unknown, known]);

        var persistedRun = await runStore.GetRunAsync(runId);
        var persistedFacts = await usageStore.ListAsync(new WorkflowUsageObservationQuery
        {
            RunIds = [runId],
            OriginProcessRunIds = [new WorkflowProcessRunId(origin.ProcessRunId)]
        });
        var aggregate = await usageStore.AggregateAsync(new WorkflowUsageAnalyticsStoreQuery([runId]));

        Assert.NotNull(persistedRun);
        Assert.Equal(RecordedAtUtc, persistedRun.TerminalAtUtc);
        Assert.Equal(origin, persistedRun.Origin);
        Assert.Equal(2, persistedFacts.Count);
        Assert.Equal(2, aggregate.Usage.ObservationCount);
        Assert.Equal(14, aggregate.Usage.InputTokens);
        Assert.Equal(1, aggregate.Usage.PricingKnownObservationCount);
        Assert.Equal(1, aggregate.Usage.PricingUnknownObservationCount);
        Assert.Equal(0m, aggregate.Usage.KnownCostUsd);
        Assert.Equal(2, aggregate.ProviderModels.Count);
        await Assert.ThrowsAsync<WorkflowUsageObservationCorrelationException>(() => usageStore.AppendAsync(
            known with { Id = WorkflowUsageObservationId.New(), RunId = null }));
        await Assert.ThrowsAsync<WorkflowUsageObservationConflictException>(() => usageStore.AppendAsync(
            known with { ProviderRequestId = "conflicting-request" }));
    }

    private static WorkflowUsageObservation CreateObservation(
        WorkflowUsageObservationId id,
        WorkflowRunId runId,
        WorkflowId workflowId,
        WorkflowVersionId versionId,
        string model,
        WorkflowPricingStatus pricingStatus,
        decimal? CostUsd,
        int inputTokens)
        => new(
            id,
            runId,
            workflowId,
            versionId,
            new WorkflowNodeId("postgres-usage-node"),
            new WorkflowExecutorId("postgres-usage-executor"),
            ComponentId: null,
            WorkflowUsageProducerKind.Executor,
            Guid.NewGuid(),
            Attempt: 1,
            ProviderProfileId: null,
            "postgres-usage-provider",
            ProviderKind.OpenAi,
            ProviderTransportKind.ChatCompletions,
            model,
            "postgres-integration",
            WorkflowUsageStatus.Observed,
            pricingStatus,
            pricingStatus == WorkflowPricingStatus.Known
                ? WorkflowUsagePricingProvenance.ProviderReported
                : WorkflowUsagePricingProvenance.Unavailable,
            inputTokens,
            CachedInputTokens: 0,
            OutputTokens: 2,
            ReasoningTokens: 0,
            TotalTokens: inputTokens + 2,
            ToolCallCount: 0,
            CostUsd,
            PricingProfileHash: "postgres-profile",
            PricingVersion: "v1",
            ProviderRequestId: string.Empty,
            ProviderResponseId: string.Empty,
            RecordedAtUtc.AddSeconds(-1),
            RecordedAtUtc,
            RecordedAtUtc,
            Origin: null);
}

internal sealed class WorkflowUsagePostgresDbContextFactory(
    DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext() => new(options);

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());
}
