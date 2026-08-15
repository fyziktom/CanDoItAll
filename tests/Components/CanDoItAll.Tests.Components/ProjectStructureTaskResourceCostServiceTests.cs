using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectStructureTaskResourceCostServiceTests
{
    private static readonly Guid ProjectId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid PartyId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-16T16:00:00Z");

    [Theory]
    [InlineData(ProjectResourceRateUnit.Hour, 25, 200)]
    [InlineData(ProjectResourceRateUnit.ManDay, 300, 300)]
    public async Task Person_strategy_uses_pure_effort_and_the_typed_CRM_rate(
        ProjectResourceRateUnit unit,
        decimal rate,
        decimal expectedAmount)
    {
        var service = new ProjectStructureTaskResourceCostService(
        [
            new ProjectStructurePersonTaskResourceCostStrategy(
                new StaticPartyCostRateBridge(new ProjectPartyCostRate(PartyId, rate, unit, "EUR")),
                new FixedTimeProvider(Now))
        ]);

        var quote = await service.GetQuoteAsync(CreateRequest(
            ProjectStructureTaskResourceKind.Person,
            PartyId,
            new ProjectTaskEstimate(8m, ProjectWorkItemEffortUnit.Hours, null, string.Empty)));

        Assert.True(quote.IsAvailable);
        Assert.Equal(expectedAmount, quote.Amount);
        Assert.Equal("EUR", quote.CurrencyCode);
        Assert.Equal("CRM workforce rate", quote.Source);
        Assert.Equal(ProjectStructureTaskResourceCostSource.CrmWorkforceRate, quote.SourceKind);
        Assert.Equal(Now, quote.CalculatedAtUtc);
    }

    [Fact]
    public async Task Person_strategy_does_not_invent_a_price_when_CRM_has_no_rate()
    {
        var service = new ProjectStructureTaskResourceCostService(
        [
            new ProjectStructurePersonTaskResourceCostStrategy(
                new StaticPartyCostRateBridge(null),
                new FixedTimeProvider(Now))
        ]);

        var quote = await service.GetQuoteAsync(CreateRequest(
            ProjectStructureTaskResourceKind.Person,
            PartyId,
            new ProjectTaskEstimate(8m, ProjectWorkItemEffortUnit.Hours, 123m, "USD")));

        Assert.False(quote.IsAvailable);
        Assert.Null(quote.Amount);
        Assert.Equal(ProjectStructureTaskResourceCostSource.CrmWorkforceRate, quote.SourceKind);
        Assert.Contains("no internal cost rate", quote.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Workflow_strategy_uses_bounded_version_filtered_recent_history()
    {
        var workflowId = new WorkflowId(Guid.Parse("30000000-0000-0000-0000-000000000003"));
        var selectedVersionId = new WorkflowVersionId(Guid.Parse("40000000-0000-0000-0000-000000000004"));
        var otherVersionId = new WorkflowVersionId(Guid.Parse("50000000-0000-0000-0000-000000000005"));
        var runStore = new InMemoryWorkflowRunStore();
        var selectedVersionRunIds = new HashSet<WorkflowRunId>();
        for (var index = 0; index < 7; index++)
        {
            var runId = new WorkflowRunId(Guid.NewGuid());
            selectedVersionRunIds.Add(runId);
            await runStore.SaveRunAsync(CreateRun(
                runId,
                workflowId,
                selectedVersionId,
                Now.AddMinutes(index)));
        }

        for (var index = 0; index < 3; index++)
        {
            await runStore.SaveRunAsync(CreateRun(
                new WorkflowRunId(Guid.NewGuid()),
                workflowId,
                otherVersionId,
                Now.AddHours(index + 1)));
        }

        var usageStore = new RecordingWorkflowUsageAnalyticsStore();
        var service = new ProjectStructureTaskResourceCostService(
        [
            new ProjectStructureWorkflowTaskResourceCostStrategy(
                runStore,
                usageStore,
                new FixedTimeProvider(Now))
        ]);

        var quote = await service.GetQuoteAsync(new ProjectStructureTaskResourceCostRequest(
            ProjectId,
            new ProjectStructureTaskResourceSelection(
                ProjectStructureTaskResourceKind.Workflow,
                workflowId.Value,
                selectedVersionId.Value),
            ProjectTaskEstimate.Empty()));

        Assert.True(quote.IsAvailable);
        Assert.Equal(10m, quote.Amount);
        Assert.Equal(ProjectStructureTaskResourceCostSource.WorkflowRunHistory, quote.SourceKind);
        Assert.Equal(5, usageStore.RequestedRunIds.Count);
        Assert.All(usageStore.RequestedRunIds, runId => Assert.Contains(runId, selectedVersionRunIds));
    }

    [Fact]
    public async Task Workflow_strategy_rejects_recent_history_with_incomplete_usage_pricing()
    {
        var workflowId = new WorkflowId(Guid.Parse("30000000-0000-0000-0000-000000000013"));
        var versionId = new WorkflowVersionId(Guid.Parse("40000000-0000-0000-0000-000000000014"));
        var pricedRunId = new WorkflowRunId(Guid.Parse("50000000-0000-0000-0000-000000000015"));
        var unpricedRunId = new WorkflowRunId(Guid.Parse("60000000-0000-0000-0000-000000000016"));
        var runStore = new InMemoryWorkflowRunStore();
        await runStore.SaveRunAsync(CreateRun(
            pricedRunId,
            workflowId,
            versionId,
            Now.AddMinutes(-1)));
        await runStore.SaveRunAsync(CreateRun(
            unpricedRunId,
            workflowId,
            versionId,
            Now));
        var usageStore = new IncompleteWorkflowUsageAnalyticsStore(unpricedRunId);
        var service = new ProjectStructureTaskResourceCostService(
        [
            new ProjectStructureWorkflowTaskResourceCostStrategy(
                runStore,
                usageStore,
                new FixedTimeProvider(Now))
        ]);

        var quote = await service.GetQuoteAsync(new ProjectStructureTaskResourceCostRequest(
            ProjectId,
            new ProjectStructureTaskResourceSelection(
                ProjectStructureTaskResourceKind.Workflow,
                workflowId.Value,
                versionId.Value),
            ProjectTaskEstimate.Empty()));

        Assert.False(quote.IsAvailable);
        Assert.Null(quote.Amount);
        Assert.Equal(ProjectStructureTaskResourceCostSource.WorkflowRunHistory, quote.SourceKind);
        Assert.Contains("missing or unresolved", quote.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Process_strategy_uses_the_existing_historical_estimate_and_rounding()
    {
        const string definitionKey = "architecture-decision-governance";
        var definitionId = ProcessDefinitionCatalogProjectionService.CreateDefinitionId(
            new ProcessDefinitionCatalogItemKey(definitionKey));
        var historyReader = new StaticProcessHistoricalRunCostReader(
            new ProcessHistoricalRunCostEstimate(
                definitionId,
                definitionKey,
                CompletedRunCount: 4,
                PricedRunCount: 3,
                AverageActualCostUsd: 1.235m,
                Samples: []));
        var service = new ProjectStructureTaskResourceCostService(
        [
            new ProjectStructureProcessTaskResourceCostStrategy(
                new ProcessDefinitionCatalogProjectionService(new SystemProcessProjectionClock()),
                historyReader,
                new FixedTimeProvider(Now))
        ]);

        var quote = await service.GetQuoteAsync(CreateRequest(
            ProjectStructureTaskResourceKind.Process,
            definitionId.Value,
            ProjectTaskEstimate.Empty()));

        Assert.True(quote.IsAvailable);
        Assert.Equal(1.24m, quote.Amount);
        Assert.Equal("USD", quote.CurrencyCode);
        Assert.Equal(ProjectStructureTaskResourceCostSource.ProcessRunHistory, quote.SourceKind);
        Assert.Equal(definitionId, historyReader.Query?.DefinitionId);
        Assert.Equal(definitionKey, historyReader.Query?.DefinitionKey);
        Assert.Equal(Now, historyReader.Query?.ObservedAtUtc);
    }

    [Fact]
    public async Task Dispatcher_selects_only_the_exact_typed_strategy()
    {
        var person = new RecordingStrategy(ProjectStructureTaskResourceKind.Person);
        var agent = new RecordingStrategy(ProjectStructureTaskResourceKind.Agent);
        var workflow = new RecordingStrategy(ProjectStructureTaskResourceKind.Workflow);
        var process = new RecordingStrategy(ProjectStructureTaskResourceKind.Process);
        var service = new ProjectStructureTaskResourceCostService([person, agent, workflow, process]);

        var quote = await service.GetQuoteAsync(CreateRequest(
            ProjectStructureTaskResourceKind.Agent,
            PartyId,
            ProjectTaskEstimatePolicy.Create(
                2m,
                ProjectWorkItemEffortUnit.ManDays,
                null,
                string.Empty)));

        Assert.True(quote.IsAvailable);
        Assert.Equal(0, person.CallCount);
        Assert.Equal(1, agent.CallCount);
        Assert.Equal(0, workflow.CallCount);
        Assert.Equal(0, process.CallCount);
        Assert.Equal(16m, agent.Request?.Estimate.ExpectedEffortHours);
    }

    [Fact]
    public async Task Dispatcher_fails_explicitly_when_the_requested_kind_is_missing()
    {
        var service = new ProjectStructureTaskResourceCostService(
        [
            new RecordingStrategy(ProjectStructureTaskResourceKind.Person)
        ]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetQuoteAsync(
            CreateRequest(
                ProjectStructureTaskResourceKind.Agent,
                PartyId,
                ProjectTaskEstimate.Empty())));

        Assert.Contains("No task resource cost strategy", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ProjectStructureTaskResourceKind.Agent), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dispatcher_rejects_a_quote_with_a_source_for_another_resource_kind()
    {
        var service = new ProjectStructureTaskResourceCostService(
        [
            new RecordingStrategy(
                ProjectStructureTaskResourceKind.Person,
                ProjectStructureTaskResourceCostSource.AgentRunHistory)
        ]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetQuoteAsync(CreateRequest(
                ProjectStructureTaskResourceKind.Person,
                PartyId,
                ProjectTaskEstimate.Empty())));

        Assert.Contains("requires cost source", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            nameof(ProjectStructureTaskResourceCostSource.CrmWorkforceRate),
            exception.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ProjectStructureTaskResourceKind.Person)]
    [InlineData(ProjectStructureTaskResourceKind.Agent)]
    [InlineData(ProjectStructureTaskResourceKind.Process)]
    public async Task Dispatcher_rejects_a_version_for_non_workflow_resource_before_strategy_invocation(
        ProjectStructureTaskResourceKind kind)
    {
        var strategy = new RecordingStrategy(kind);
        var service = new ProjectStructureTaskResourceCostService([strategy]);
        var request = new ProjectStructureTaskResourceCostRequest(
            ProjectId,
            new ProjectStructureTaskResourceSelection(
                kind,
                PartyId,
                Guid.Parse("60000000-0000-0000-0000-000000000006")),
            ProjectTaskEstimate.Empty());

        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            service.GetQuoteAsync(request));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("TaskResourceVersionNotSupported", exception.ErrorCode);
        Assert.Equal(0, strategy.CallCount);
    }

    [Fact]
    public async Task Dispatcher_requires_an_exact_workflow_version_before_strategy_invocation()
    {
        var strategy = new RecordingStrategy(ProjectStructureTaskResourceKind.Workflow);
        var service = new ProjectStructureTaskResourceCostService([strategy]);

        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            service.GetQuoteAsync(CreateRequest(
                ProjectStructureTaskResourceKind.Workflow,
                PartyId,
                ProjectTaskEstimate.Empty())));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("TaskWorkflowVersionRequired", exception.ErrorCode);
        Assert.Equal(0, strategy.CallCount);
    }

    [Fact]
    public void Dispatcher_fails_eagerly_when_a_kind_is_registered_more_than_once()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ProjectStructureTaskResourceCostService(
            [
                new RecordingStrategy(ProjectStructureTaskResourceKind.Person),
                new RecordingStrategy(ProjectStructureTaskResourceKind.Person)
            ]));

        Assert.Contains("Multiple task resource cost strategies", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ProjectStructureTaskResourceKind.Person), exception.Message, StringComparison.Ordinal);
    }

    private static ProjectStructureTaskResourceCostRequest CreateRequest(
        ProjectStructureTaskResourceKind kind,
        Guid resourceId,
        ProjectTaskEstimate estimate)
    {
        return new ProjectStructureTaskResourceCostRequest(
            ProjectId,
            new ProjectStructureTaskResourceSelection(kind, resourceId),
            estimate);
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

    private sealed class IncompleteWorkflowUsageAnalyticsStore(WorkflowRunId unpricedRunId)
        : IWorkflowUsageAnalyticsStore
    {
        public Task<WorkflowUsageAnalyticsStoreSnapshot> AggregateAsync(
            WorkflowUsageAnalyticsStoreQuery query,
            CancellationToken cancellationToken = default)
        {
            var runUsage = query.RunIds.ToDictionary(
                static runId => runId,
                runId => runId == unpricedRunId
                    ? WorkflowUsageAnalyticsTotals.Empty with
                    {
                        ObservationCount = 1,
                        PricingUnknownObservationCount = 1
                    }
                    : WorkflowUsageAnalyticsTotals.Empty with
                    {
                        ObservationCount = 1,
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

    private sealed class StaticProcessHistoricalRunCostReader(
        ProcessHistoricalRunCostEstimate estimate) : IProcessHistoricalRunCostReader
    {
        public ProcessHistoricalRunCostQuery? Query { get; private set; }

        public ValueTask<ProcessHistoricalRunCostEstimate> ReadAsync(
            ProcessHistoricalRunCostQuery query,
            CancellationToken cancellationToken = default)
        {
            Query = query;
            return ValueTask.FromResult(estimate);
        }
    }

    private sealed class RecordingStrategy(
        ProjectStructureTaskResourceKind kind,
        ProjectStructureTaskResourceCostSource? sourceKind = null)
        : IProjectStructureTaskResourceCostStrategy
    {
        public ProjectStructureTaskResourceKind Kind => kind;

        public int CallCount { get; private set; }

        public ProjectStructureTaskResourceCostRequest? Request { get; private set; }

        public Task<ProjectStructureTaskResourceCostQuote> GetQuoteAsync(
            ProjectStructureTaskResourceCostRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Request = request;
            return Task.FromResult(new ProjectStructureTaskResourceCostQuote(
                ProjectStructureTaskResourceCostQuoteStatus.Available,
                1m,
                "USD",
                "Test",
                "Selected fake strategy.",
                Now,
                sourceKind ?? ProjectStructureTaskResourceCostSourcePolicy.RequireFor(kind)));
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
