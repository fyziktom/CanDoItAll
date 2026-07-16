using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureTaskResourceCostServiceTests
{
    private static readonly Guid ProjectId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid PartyId = Guid.Parse("20000000-0000-0000-0000-000000000002");

    [Theory]
    [InlineData(ProjectResourceRateUnit.Hour, 25, 200)]
    [InlineData(ProjectResourceRateUnit.ManDay, 300, 300)]
    public async Task Person_quote_uses_pure_effort_and_the_typed_CRM_rate(
        ProjectResourceRateUnit unit,
        decimal rate,
        decimal expectedAmount)
    {
        var service = CreateService(new ProjectPartyCostRate(PartyId, rate, unit, "EUR"));

        var quote = await service.GetQuoteAsync(new ProjectStructureTaskResourceCostRequest(
            ProjectId,
            new ProjectStructureTaskResourceSelection(ProjectStructureTaskResourceKind.Person, PartyId),
            new ProjectTaskEstimate(8m, ProjectWorkItemEffortUnit.Hours, null, string.Empty)));

        Assert.True(quote.IsAvailable);
        Assert.Equal(expectedAmount, quote.Amount);
        Assert.Equal("EUR", quote.CurrencyCode);
        Assert.Equal("CRM workforce rate", quote.Source);
    }

    [Fact]
    public async Task Missing_rate_does_not_invent_a_resource_price()
    {
        var service = CreateService(null);

        var quote = await service.GetQuoteAsync(new ProjectStructureTaskResourceCostRequest(
            ProjectId,
            new ProjectStructureTaskResourceSelection(ProjectStructureTaskResourceKind.Agent, PartyId),
            new ProjectTaskEstimate(8m, ProjectWorkItemEffortUnit.Hours, 123m, "USD")));

        Assert.False(quote.IsAvailable);
        Assert.Null(quote.Amount);
        Assert.Contains("no internal cost rate", quote.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Workflow_quote_uses_bounded_version_filtered_recent_history()
    {
        var workflowId = new WorkflowId(Guid.Parse("30000000-0000-0000-0000-000000000003"));
        var selectedVersionId = new WorkflowVersionId(Guid.Parse("40000000-0000-0000-0000-000000000004"));
        var otherVersionId = new WorkflowVersionId(Guid.Parse("50000000-0000-0000-0000-000000000005"));
        var runStore = new InMemoryWorkflowRunStore();
        var selectedVersionRunIds = new HashSet<WorkflowRunId>();
        var now = DateTimeOffset.Parse("2026-07-16T16:00:00Z");
        for (var index = 0; index < 7; index++)
        {
            var runId = new WorkflowRunId(Guid.NewGuid());
            selectedVersionRunIds.Add(runId);
            await runStore.SaveRunAsync(CreateRun(
                runId,
                workflowId,
                selectedVersionId,
                now.AddMinutes(index)));
        }

        for (var index = 0; index < 3; index++)
        {
            await runStore.SaveRunAsync(CreateRun(
                new WorkflowRunId(Guid.NewGuid()),
                workflowId,
                otherVersionId,
                now.AddHours(index + 1)));
        }

        var usageStore = new RecordingWorkflowUsageAnalyticsStore();
        var service = new ProjectStructureTaskResourceCostService(
            new StaticPartyCostRateBridge(null),
            runStore,
            usageStore,
            new ProcessDefinitionCatalogProjectionService(new SystemProcessProjectionClock()),
            new UnexpectedProcessHistoricalRunCostReader(),
            TimeProvider.System);

        var quote = await service.GetQuoteAsync(new ProjectStructureTaskResourceCostRequest(
            ProjectId,
            new ProjectStructureTaskResourceSelection(
                ProjectStructureTaskResourceKind.Workflow,
                workflowId.Value,
                selectedVersionId.Value),
            ProjectTaskEstimate.Empty()));

        Assert.True(quote.IsAvailable);
        Assert.Equal(10m, quote.Amount);
        Assert.Equal(5, usageStore.RequestedRunIds.Count);
        Assert.All(usageStore.RequestedRunIds, runId => Assert.Contains(runId, selectedVersionRunIds));
    }

    private static ProjectStructureTaskResourceCostService CreateService(ProjectPartyCostRate? rate)
    {
        return new ProjectStructureTaskResourceCostService(
            new StaticPartyCostRateBridge(rate),
            new InMemoryWorkflowRunStore(),
            new UnexpectedWorkflowUsageAnalyticsStore(),
            new ProcessDefinitionCatalogProjectionService(new SystemProcessProjectionClock()),
            new UnexpectedProcessHistoricalRunCostReader(),
            TimeProvider.System);
    }

    private sealed class StaticPartyCostRateBridge(ProjectPartyCostRate? rate) : IProjectPartyCostRateBridge
    {
        public Task<ProjectPartyCostRate?> GetInternalCostRateAsync(
            Guid partyId,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(PartyId, partyId);
            return Task.FromResult(rate);
        }
    }

    private static WorkflowRunSnapshot CreateRun(
        WorkflowRunId runId,
        WorkflowId workflowId,
        WorkflowVersionId versionId,
        DateTimeOffset updatedAtUtc)
        => new(
            runId,
            workflowId,
            versionId,
            WorkflowRunState.Completed,
            WorkflowRuntimeBackendKind.InProcess,
            runId.ToString(),
            "Completed test workflow",
            updatedAtUtc.AddMinutes(-1),
            updatedAtUtc);

    private sealed class UnexpectedWorkflowUsageAnalyticsStore : IWorkflowUsageAnalyticsStore
    {
        public Task<WorkflowUsageAnalyticsStoreSnapshot> AggregateAsync(
            WorkflowUsageAnalyticsStoreQuery query,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Workflow usage must not be queried for a party quote.");
        }
    }

    private sealed class RecordingWorkflowUsageAnalyticsStore : IWorkflowUsageAnalyticsStore
    {
        public IReadOnlyList<WorkflowRunId> RequestedRunIds { get; private set; } = [];

        public Task<WorkflowUsageAnalyticsStoreSnapshot> AggregateAsync(
            WorkflowUsageAnalyticsStoreQuery query,
            CancellationToken cancellationToken = default)
        {
            RequestedRunIds = query.RunIds.ToArray();
            var runUsage = RequestedRunIds.ToDictionary(
                runId => runId,
                _ => WorkflowUsageAnalyticsTotals.Empty with
                {
                    PricingKnownObservationCount = 1,
                    KnownCostUsd = 10m
                });
            return Task.FromResult(new WorkflowUsageAnalyticsStoreSnapshot(
                WorkflowUsageAnalyticsTotals.Empty,
                runUsage,
                [],
                []));
        }
    }

    private sealed class UnexpectedProcessHistoricalRunCostReader : IProcessHistoricalRunCostReader
    {
        public ValueTask<ProcessHistoricalRunCostEstimate> ReadAsync(
            ProcessHistoricalRunCostQuery query,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Process history must not be queried for a party quote.");
        }
    }
}
