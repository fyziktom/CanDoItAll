using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowOverviewQueryServiceTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 19, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Query_builds_typed_dashboard_snapshot_from_bounded_store_projection()
    {
        var activeId = new WorkflowId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var draftId = new WorkflowId(Guid.Parse("10000000-0000-0000-0000-000000000002"));
        var deletedId = new WorkflowId(Guid.Parse("10000000-0000-0000-0000-000000000003"));
        IReadOnlyList<WorkflowCatalogItem> definitions =
        [
            CreateCatalogItem(activeId, "Active flow", WorkflowLifecycleStatus.Active, FixedUtcNow.AddMinutes(-5)),
            CreateCatalogItem(draftId, "Draft flow", WorkflowLifecycleStatus.Draft, FixedUtcNow.AddMinutes(-1))
        ];
        var recentRun = CreateRun(activeId, WorkflowRunState.Completed, FixedUtcNow.AddMinutes(-2));
        var store = new RecordingOverviewStore(new WorkflowOverviewStoreSnapshot(
            new Dictionary<WorkflowRunState, int>
            {
                [WorkflowRunState.Running] = 1,
                [WorkflowRunState.WaitingForInput] = 1,
                [WorkflowRunState.Completed] = 8,
                [WorkflowRunState.Failed] = 2
            },
            new Dictionary<WorkflowRuntimeBackendKind, int>
            {
                [WorkflowRuntimeBackendKind.InProcess] = 9,
                [WorkflowRuntimeBackendKind.DurableTask] = 3
            },
            [
                new WorkflowOverviewStoreWorkflowRow(activeId, 7, 1, FixedUtcNow.AddMinutes(-2)),
                new WorkflowOverviewStoreWorkflowRow(deletedId, 5, 1, FixedUtcNow.AddMinutes(-3))
            ],
            [recentRun]));
        var catalog = new RecordingWorkflowCatalogService(definitions);
        var service = new WorkflowOverviewQueryService(catalog, store, new FixedTimeProvider(FixedUtcNow));

        var result = await service.QueryAsync(new WorkflowOverviewQuery());

        Assert.Equal(FixedUtcNow, result.AsOfUtc);
        Assert.Equal(2, result.DefinitionCount);
        Assert.Equal(1, result.ActiveDefinitionCount);
        Assert.Equal(12, result.RunCount);
        Assert.Equal(1, result.RunningRunCount);
        Assert.Equal(1, result.WaitingForInputRunCount);
        Assert.Equal(8, result.CompletedRunCount);
        Assert.Equal(2, result.FailedRunCount);
        Assert.Equal(80m, result.SuccessRatePercent);
        Assert.Equal("Draft flow", result.RecentlyUpdatedDefinitions[0].Name);
        Assert.Equal("Active flow", result.TopWorkflows[0].Name);
        Assert.Null(result.TopWorkflows[1].Status);
        Assert.StartsWith("Deleted workflow", result.TopWorkflows[1].Name, StringComparison.Ordinal);
        Assert.Equal("Active flow", Assert.Single(result.RecentRuns).WorkflowName);
        Assert.Equal(1, catalog.ListRequestCount);
        var request = Assert.Single(store.Queries);
        Assert.Equal(6, request.RecentTake);
        Assert.Equal(5, request.TopWorkflowTake);
    }

    [Fact]
    public async Task Query_fetches_fresh_definitions_for_every_snapshot()
    {
        var workflowId = new WorkflowId(Guid.Parse("10000000-0000-0000-0000-000000000011"));
        var addedWorkflowId = new WorkflowId(Guid.Parse("10000000-0000-0000-0000-000000000012"));
        var catalog = new RecordingWorkflowCatalogService(
        [
            CreateCatalogItem(workflowId, "Original flow", WorkflowLifecycleStatus.Draft, FixedUtcNow.AddMinutes(-2))
        ]);
        var store = new RecordingOverviewStore(new WorkflowOverviewStoreSnapshot(
            new Dictionary<WorkflowRunState, int>(),
            new Dictionary<WorkflowRuntimeBackendKind, int>(),
            [new WorkflowOverviewStoreWorkflowRow(workflowId, 1, 0, FixedUtcNow.AddMinutes(-1))],
            []));
        var service = new WorkflowOverviewQueryService(catalog, store, new FixedTimeProvider(FixedUtcNow));

        var initial = await service.QueryAsync(new WorkflowOverviewQuery());
        catalog.Definitions =
        [
            CreateCatalogItem(workflowId, "Renamed active flow", WorkflowLifecycleStatus.Active, FixedUtcNow),
            CreateCatalogItem(addedWorkflowId, "Added flow", WorkflowLifecycleStatus.Active, FixedUtcNow.AddMinutes(1))
        ];
        var refreshed = await service.QueryAsync(new WorkflowOverviewQuery());

        Assert.Equal(1, initial.DefinitionCount);
        Assert.Equal(0, initial.ActiveDefinitionCount);
        Assert.Equal("Original flow", Assert.Single(initial.TopWorkflows).Name);
        Assert.Equal(2, refreshed.DefinitionCount);
        Assert.Equal(2, refreshed.ActiveDefinitionCount);
        Assert.Equal("Renamed active flow", Assert.Single(refreshed.TopWorkflows).Name);
        Assert.Contains(refreshed.RecentlyUpdatedDefinitions, definition => definition.Name == "Added flow");
        Assert.Equal(2, catalog.ListRequestCount);
    }

    [Fact]
    public async Task In_memory_overview_store_returns_exact_aggregates_with_bounded_windows()
    {
        var firstWorkflowId = new WorkflowId(Guid.Parse("20000000-0000-0000-0000-000000000001"));
        var secondWorkflowId = new WorkflowId(Guid.Parse("20000000-0000-0000-0000-000000000002"));
        var thirdWorkflowId = new WorkflowId(Guid.Parse("20000000-0000-0000-0000-000000000003"));
        var store = new InMemoryWorkflowRunStore();
        var states = new[]
        {
            WorkflowRunState.Completed,
            WorkflowRunState.Completed,
            WorkflowRunState.Failed,
            WorkflowRunState.Running,
            WorkflowRunState.WaitingForInput,
            WorkflowRunState.Cancelled
        };
        var workflowIds = new[]
        {
            firstWorkflowId,
            firstWorkflowId,
            firstWorkflowId,
            secondWorkflowId,
            secondWorkflowId,
            thirdWorkflowId
        };

        for (var index = 0; index < states.Length; index++)
        {
            await store.SaveRunAsync(CreateRun(
                workflowIds[index],
                states[index],
                FixedUtcNow.AddMinutes(index)));
        }

        var result = await store.QueryOverviewAsync(new WorkflowOverviewStoreQuery(
            RecentTake: 3,
            TopWorkflowTake: 2));

        Assert.Equal(2, result.RunsByState[WorkflowRunState.Completed]);
        Assert.Equal(1, result.RunsByState[WorkflowRunState.Failed]);
        Assert.Equal(states.Length, result.RunsByBackend[WorkflowRuntimeBackendKind.InProcess]);
        Assert.Equal(2, result.TopWorkflows.Count);
        Assert.Equal(firstWorkflowId, result.TopWorkflows[0].WorkflowId);
        Assert.Equal(3, result.TopWorkflows[0].RunCount);
        Assert.Equal(3, result.RecentRuns.Count);
        Assert.Equal(thirdWorkflowId, result.RecentRuns[0].WorkflowId);
    }

    [Fact]
    public async Task Query_rejects_unbounded_windows_before_calling_store()
    {
        var store = new RecordingOverviewStore(new WorkflowOverviewStoreSnapshot(
            new Dictionary<WorkflowRunState, int>(),
            new Dictionary<WorkflowRuntimeBackendKind, int>(),
            [],
            []));
        var catalog = new RecordingWorkflowCatalogService([]);
        var service = new WorkflowOverviewQueryService(catalog, store, new FixedTimeProvider(FixedUtcNow));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.QueryAsync(
            new WorkflowOverviewQuery(RecentTake: 13)));

        Assert.Equal(0, catalog.ListRequestCount);
        Assert.Empty(store.Queries);
    }

    [Fact]
    public void Workflow_core_registers_overview_query_service_as_scoped()
    {
        var services = new ServiceCollection();

        services.AddWorkflowCoreServices();

        var descriptor = Assert.Single(
            services,
            service => service.ServiceType == typeof(IWorkflowOverviewQueryService));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(WorkflowOverviewQueryService), descriptor.ImplementationType);
    }

    private static WorkflowCatalogItem CreateCatalogItem(
        WorkflowId workflowId,
        string name,
        WorkflowLifecycleStatus status,
        DateTimeOffset updatedAtUtc)
        => new(
            workflowId,
            WorkflowVersionId.New(),
            name,
            $"{name} description",
            status,
            WorkflowRuntimeBackendKind.InProcess,
            updatedAtUtc);

    private static WorkflowRunSnapshot CreateRun(
        WorkflowId workflowId,
        WorkflowRunState state,
        DateTimeOffset updatedAtUtc)
        => new(
            WorkflowRunId.New(),
            workflowId,
            WorkflowVersionId.New(),
            state,
            WorkflowRuntimeBackendKind.InProcess,
            $"backend-{Guid.NewGuid():N}",
            $"{state} run",
            updatedAtUtc.AddMinutes(-1),
            updatedAtUtc);

    private sealed class RecordingOverviewStore(WorkflowOverviewStoreSnapshot snapshot) : IWorkflowOverviewStore
    {
        public List<WorkflowOverviewStoreQuery> Queries { get; } = [];

        public Task<WorkflowOverviewStoreSnapshot> QueryOverviewAsync(
            WorkflowOverviewStoreQuery query,
            CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            return Task.FromResult(snapshot);
        }
    }

    private sealed class RecordingWorkflowCatalogService(
        IReadOnlyList<WorkflowCatalogItem> definitions) : IWorkflowCatalogService
    {
        public IReadOnlyList<WorkflowCatalogItem> Definitions { get; set; } = definitions;

        public int ListRequestCount { get; private set; }

        public Task<IReadOnlyList<WorkflowCatalogItem>> ListDefinitionsAsync(
            CancellationToken cancellationToken = default)
        {
            ListRequestCount++;
            return Task.FromResult(Definitions);
        }

        public Task<WorkflowDefinitionDetail?> GetDefinitionAsync(
            WorkflowId workflowId,
            WorkflowVersionId? versionId = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDefinitionDetail?> GetLatestDefinitionByStatusAsync(
            WorkflowId workflowId,
            WorkflowLifecycleStatus status,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDefinition> SaveDefinitionAsync(
            WorkflowDefinitionSaveRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDefinition> ChangeDefinitionStatusAsync(
            WorkflowDefinitionStatusChangeRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDefinitionExportEnvelope?> ExportDefinitionAsync(
            WorkflowId workflowId,
            WorkflowVersionId? versionId = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDefinition> ImportDefinitionAsync(
            WorkflowDefinitionImportRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteDefinitionAsync(
            WorkflowId workflowId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowValidationResult> ValidateDefinitionAsync(
            WorkflowDefinition definition,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
