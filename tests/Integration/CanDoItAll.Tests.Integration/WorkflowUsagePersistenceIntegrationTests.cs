using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration.AgentFramework;

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
        var usageStore = new PersistentWorkflowUsageObservationStore(factory, new(new CanDoItAll.AgentFramework.ProviderHistory.Persistence.HistoryOutboxWriter(TimeProvider.System)));
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
            Origin = origin,
            StartedAtUtc = RecordedAtUtc.AddSeconds(-1).AddTicks(7),
            CompletedAtUtc = RecordedAtUtc.AddTicks(7),
            RecordedAtUtc = RecordedAtUtc.AddTicks(7)
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
        await usageStore.AppendAsync(known);

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
        var persistedKnown = Assert.Single(persistedFacts, fact => fact.Id == known.Id);
        Assert.Equal(RecordedAtUtc.AddSeconds(-1), persistedKnown.StartedAtUtc);
        Assert.Equal(RecordedAtUtc, persistedKnown.CompletedAtUtc);
        Assert.Equal(RecordedAtUtc, persistedKnown.RecordedAtUtc);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_workflow_append_commits_or_rolls_back_exact_attempt_outbox(bool rollback) {
        await using var history = await HistoryPersistenceTestDatabase.CreateAsync();
        var interceptor = new FailAfterWorkflowHistorySave(rollback);
        var factory = history.Factory.WithInterceptor(interceptor);
        var store = new PersistentWorkflowUsageObservationStore(factory, new(history.Outbox));
        var start = history.Start();
        var exact = HistoryAttemptEvidence.Create(start, history.Completion());
        var runId = WorkflowRunId.New();
        var observation = CreateObservation(WorkflowUsageObservationId.New(), runId, WorkflowId.New(),
            WorkflowVersionId.New(), "model", WorkflowPricingStatus.Unknown, null, 1000) with {
                HistoryEvidence = new(start.RequestId, true, [exact])
            };

        if (rollback) {
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.AppendAsync(observation));
            Assert.True(interceptor.Failed);
            await using var rolledBack = history.Factory.CreateDbContext();
            Assert.Empty(await rolledBack.Set<WorkflowUsageObservationRecordEntity>().ToListAsync());
            Assert.Empty(await rolledBack.Set<HistoryOutboxRow>().ToListAsync());
            return;
        }

        await store.AppendAsync(observation);
        await store.AppendAsync(observation);
        Assert.Equal(1, await history.Processor.ProcessAsync(history.Partition, 20, default));
        await using var db = history.Factory.CreateDbContext();
        var entry = await db.Set<HistoryEntryRow>().SingleAsync();
        Assert.Equal(start.EntryId.Value, entry.Id);
        Assert.Equal(10, entry.InputTokens);
        Assert.Equal(0.01m, entry.Amount);
        Assert.Equal(HistoryRetentionAuthority.CanonicalOwner, entry.RetentionAuthority);
        Assert.Empty(await db.Set<HistoryDetailRow>().ToListAsync());
        var restored = Assert.Single(await store.ListAsync(new() { RunIds = [runId] }));
        Assert.Equal(observation.HistoryEvidence, restored.HistoryEvidence);
        var adapter = new WorkflowHistorySource(history.Factory, history.Outbox);
        var source = new CanonicalEvidenceReference(history.Partition, HistorySourceKind.Workflow,
            new(runId.Value.ToString("N")), new(observation.Id.Value.ToString("N")));
        var linked = await adapter.ReadAsync(source, default);
        Assert.Equal(exact.Id, Assert.Single(linked!.Attempts).Id);
        var progress = await adapter.ProcessAsync(history.Maintenance, null, 1, default);
        Assert.False(progress.BackfillComplete);
        var resumed = await new WorkflowHistorySource(history.Factory, history.Outbox)
            .ProcessAsync(history.Maintenance, progress.Cursor, 1, default);
        Assert.True(resumed.BackfillComplete);
        Assert.Equal(1, await history.Processor.ProcessAsync(history.Partition, 10, default));
        Assert.Single(await db.Set<HistoryEntryRow>().ToArrayAsync());
        Assert.Null(await adapter.ReadAsync(source with { Owner = new(Guid.NewGuid().ToString("N")) }, default));
        Assert.Equal(1000, restored.InputTokens);
    }

    private sealed class FailAfterWorkflowHistorySave(bool enabled) : SaveChangesInterceptor {
        public bool Failed { get; private set; }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData,
            int result, CancellationToken cancellationToken = default) {
            if (enabled && eventData.Context is { } db &&
                db.ChangeTracker.Entries<WorkflowUsageObservationRecordEntity>().Any() &&
                db.ChangeTracker.Entries<HistoryOutboxRow>().Any() && db.Database.CurrentTransaction is not null) {
                Failed = true;
                throw new InvalidOperationException("Injected failure after workflow source and outbox flush.");
            }
            return ValueTask.FromResult(result);
        }
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
