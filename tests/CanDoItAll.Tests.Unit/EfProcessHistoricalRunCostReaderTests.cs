using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

public sealed class EfProcessHistoricalRunCostReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReadAsync_averages_actual_cost_per_completed_process_run()
    {
        await using var dbContext = CreateDbContext();
        var definitionId = ProcessDefinitionId.New();
        var otherDefinitionId = ProcessDefinitionId.New();
        var rootRunOne = ProcessRunId.New();
        var childRun = ProcessRunId.New();
        var rootRunTwo = ProcessRunId.New();
        var failedRun = ProcessRunId.New();
        AddPlan(dbContext, rootRunOne.Value, definitionId, Now.AddHours(-4));
        AddPlan(dbContext, childRun.Value, otherDefinitionId, Now.AddHours(-3));
        AddPlan(dbContext, rootRunTwo.Value, definitionId, Now.AddHours(-2));
        AddPlan(dbContext, failedRun.Value, definitionId, Now.AddHours(-1));
        AddState(dbContext, rootRunOne, rootRunOne, rootRunOne.Value, ProcessRuntimeStatus.Completed, Now.AddHours(-3));
        AddState(dbContext, childRun, rootRunOne, childRun.Value, ProcessRuntimeStatus.Completed, Now.AddHours(-2.5));
        AddState(dbContext, rootRunTwo, rootRunTwo, rootRunTwo.Value, ProcessRuntimeStatus.Completed, Now.AddHours(-1.5));
        AddState(dbContext, failedRun, failedRun, failedRun.Value, ProcessRuntimeStatus.Failed, Now.AddMinutes(-30));
        await dbContext.SaveChangesAsync();

        var usageReader = new RecordingUsageTelemetryReader(
            Usage(rootRunOne, Now.AddHours(-2.9), 0.30m),
            Usage(childRun, Now.AddHours(-2.4), 0.70m),
            Usage(rootRunTwo, Now.AddHours(-1.4), 2.00m),
            Usage(failedRun, Now.AddMinutes(-20), 9.00m));
        var reader = new EfProcessHistoricalRunCostReader(dbContext, usageReader);

        var estimate = await reader.ReadAsync(new ProcessHistoricalRunCostQuery(
            definitionId,
            "software-delivery",
            Now,
            TakeRuns: 5,
            FromUtc: Now.AddDays(-1)));

        Assert.Equal(2, estimate.CompletedRunCount);
        Assert.Equal(2, estimate.PricedRunCount);
        Assert.Equal(1.50m, estimate.AverageActualCostUsd);
        Assert.Contains(estimate.Samples, sample => sample.RunId == rootRunOne && sample.ActualCostUsd == 1.00m);
        Assert.Contains(estimate.Samples, sample => sample.RunId == rootRunTwo && sample.ActualCostUsd == 2.00m);
        Assert.NotNull(usageReader.LastQuery);
        Assert.Contains(rootRunOne, usageReader.LastQuery!.RunIds);
        Assert.Contains(childRun, usageReader.LastQuery.RunIds);
        Assert.Contains(rootRunTwo, usageReader.LastQuery.RunIds);
        Assert.DoesNotContain(failedRun, usageReader.LastQuery.RunIds);
    }

    [Fact]
    public async Task ReadAsync_returns_empty_when_no_completed_run_matches_definition()
    {
        await using var dbContext = CreateDbContext();
        var definitionId = ProcessDefinitionId.New();
        var runId = ProcessRunId.New();
        AddPlan(dbContext, runId.Value, definitionId, Now.AddHours(-2));
        AddState(dbContext, runId, runId, runId.Value, ProcessRuntimeStatus.Failed, Now.AddHours(-1));
        await dbContext.SaveChangesAsync();
        var usageReader = new RecordingUsageTelemetryReader(Usage(runId, Now.AddMinutes(-30), 1.00m));
        var reader = new EfProcessHistoricalRunCostReader(dbContext, usageReader);

        var estimate = await reader.ReadAsync(new ProcessHistoricalRunCostQuery(
            definitionId,
            "software-delivery",
            Now));

        Assert.Equal(0, estimate.CompletedRunCount);
        Assert.Equal(0, estimate.PricedRunCount);
        Assert.Equal(0m, estimate.AverageActualCostUsd);
        Assert.Null(usageReader.LastQuery);
    }

    private static ProcessPersistenceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>()
            .UseInMemoryDatabase($"process-historical-cost-{Guid.NewGuid():N}")
            .Options;
        return new ProcessPersistenceDbContext(options);
    }

    private static void AddPlan(
        ProcessPersistenceDbContext dbContext,
        Guid planId,
        ProcessDefinitionId definitionId,
        DateTimeOffset createdAtUtc)
    {
        dbContext.InstancePlans.Add(new ProcessInstancePlanEntity
        {
            PlanId = planId,
            RootPlanId = planId,
            ParentPlanId = null,
            ParentStepId = null,
            DefinitionId = definitionId.Value,
            DefinitionVersionId = Guid.NewGuid(),
            PlanHash = $"plan:{planId:N}",
            PlanSchemaVersion = "test",
            DefinitionContentHash = $"definition:{definitionId.Value:N}",
            PayloadJson = "{}",
            CreatedAtUtc = createdAtUtc
        });
    }

    private static void AddState(
        ProcessPersistenceDbContext dbContext,
        ProcessRunId runId,
        ProcessRunId rootRunId,
        Guid planId,
        ProcessRuntimeStatus status,
        DateTimeOffset updatedAtUtc)
    {
        dbContext.RuntimeStates.Add(new ProcessRuntimeStateEntity
        {
            RunId = runId.Value,
            RootRunId = rootRunId.Value,
            PlanId = planId,
            PlanHash = $"plan:{planId:N}",
            Status = status,
            UpdatedAtUtc = updatedAtUtc,
            ConcurrencyToken = Guid.NewGuid()
        });
    }

    private static ProcessRuntimeUsageObservation Usage(
        ProcessRunId runId,
        DateTimeOffset createdAtUtc,
        decimal actualCostUsd)
    {
        return new ProcessRuntimeUsageObservation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            runId,
            StepInstanceId: null,
            createdAtUtc,
            "OpenAI default",
            "gpt-test",
            "agent-runtime",
            "Observed",
            IsKnownUsage: true,
            InputTokens: 100,
            CachedInputTokens: 0,
            OutputTokens: 20,
            ReasoningTokens: 0,
            TotalTokens: 120,
            EstimatedCostUsd: 0m,
            ActualCostUsd: actualCostUsd);
    }

    private sealed class RecordingUsageTelemetryReader(params ProcessRuntimeUsageObservation[] observations) : IProcessRuntimeUsageTelemetryReader
    {
        public ProcessRuntimeUsageTelemetryQuery? LastQuery { get; private set; }

        public ValueTask<IReadOnlyList<ProcessRuntimeUsageObservation>> ListAsync(
            ProcessRuntimeUsageTelemetryQuery query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            var runIds = query.RunIds.ToHashSet();
            IReadOnlyList<ProcessRuntimeUsageObservation> result = observations
                .Where(observation =>
                    runIds.Contains(observation.RunId) &&
                    observation.CreatedAtUtc >= query.FromUtc &&
                    observation.CreatedAtUtc <= query.ToUtc)
                .OrderBy(observation => observation.CreatedAtUtc)
                .ToArray();
            return ValueTask.FromResult(result);
        }
    }
}
