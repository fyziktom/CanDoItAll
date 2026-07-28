using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Prompts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class WorkflowDashboardActivityQueryTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 16, 0, 0, TimeSpan.Zero);
    private static readonly WorkflowId WorkflowId = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));

    [Fact]
    public async Task In_memory_store_returns_only_active_states_in_deterministic_bounded_order()
    {
        var store = new InMemoryWorkflowRunStore();
        await store.SaveRunAsync(CreateRun(1, WorkflowRunState.Completed, Now.AddMinutes(10)));
        await store.SaveRunAsync(CreateRun(2, WorkflowRunState.Failed, Now.AddMinutes(9)));
        await store.SaveRunAsync(CreateRun(3, WorkflowRunState.Running, Now.AddMinutes(2)));
        await store.SaveRunAsync(CreateRun(4, WorkflowRunState.WaitingForInput, Now.AddMinutes(4)));
        await store.SaveRunAsync(CreateRun(5, WorkflowRunState.Running, Now.AddMinutes(3)));
        await store.SaveRunAsync(CreateRun(6, WorkflowRunState.Running, Now.AddMinutes(1)));
        await store.SaveRunAsync(CreateRun(7, WorkflowRunState.WaitingForInput, Now));
        await store.SaveRunAsync(CreateRun(8, WorkflowRunState.Running, Now.AddMinutes(4)));

        var result = await store.QueryActivityAsync(new WorkflowDashboardActivityQuery());

        Assert.Equal(WorkflowDashboardActivityMode.Active, result.Mode);
        Assert.Equal(5, result.Runs.Count);
        Assert.All(result.Runs, run => Assert.True(WorkflowRunActivityPolicy.IsActive(run.State)));
        Assert.Equal(
            [8, 4, 5, 3, 6],
            result.Runs.Select(run => RunSequence(run.RunId)));
    }

    [Fact]
    public async Task In_memory_store_falls_back_to_latest_terminal_runs_when_none_are_active()
    {
        var store = new InMemoryWorkflowRunStore();
        await store.SaveRunAsync(CreateRun(1, WorkflowRunState.Completed, Now));
        await store.SaveRunAsync(CreateRun(2, WorkflowRunState.Failed, Now.AddMinutes(2)));
        await store.SaveRunAsync(CreateRun(3, WorkflowRunState.Cancelled, Now.AddMinutes(1)));

        var result = await store.QueryActivityAsync(new WorkflowDashboardActivityQuery(2));

        Assert.Equal(WorkflowDashboardActivityMode.RecentFallback, result.Mode);
        Assert.Equal([2, 3], result.Runs.Select(run => RunSequence(run.RunId)));
    }

    [Fact]
    public async Task Not_started_and_idle_runs_are_recent_fallback_candidates_not_active_runs()
    {
        var store = new InMemoryWorkflowRunStore();
        await store.SaveRunAsync(CreateRun(1, WorkflowRunState.Completed, Now));
        await store.SaveRunAsync(CreateRun(2, WorkflowRunState.NotStarted, Now.AddMinutes(2)));
        await store.SaveRunAsync(CreateRun(3, WorkflowRunState.Idle, Now.AddMinutes(1)));

        var result = await store.QueryActivityAsync(new WorkflowDashboardActivityQuery());

        Assert.Equal(WorkflowDashboardActivityMode.RecentFallback, result.Mode);
        Assert.Equal([2, 3, 1], result.Runs.Select(run => RunSequence(run.RunId)));
    }

    [Fact]
    public void Workflow_activity_policy_is_exhaustive_for_every_run_state()
    {
        var expectedActiveStates = new HashSet<WorkflowRunState>
        {
            WorkflowRunState.Running,
            WorkflowRunState.WaitingForInput
        };

        Assert.All(
            Enum.GetValues<WorkflowRunState>(),
            state => Assert.Equal(expectedActiveStates.Contains(state), WorkflowRunActivityPolicy.IsActive(state)));
    }

    [Fact]
    public async Task Persistent_store_matches_active_policy_is_bounded_and_does_not_track_rows()
    {
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(AgentFrameworkModuleAssemblyMarker).Assembly]);
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"workflow-dashboard-activity-{Guid.NewGuid():N}")
            .Options;
        var factory = new TrackingAppDbContextFactory(options);
        await using (var dbContext = factory.CreateDbContext())
        {
            var activeRecords = Enumerable.Range(1, 7)
                .Select(sequence => WorkflowRunRecordEntity.FromSnapshot(CreateRun(
                    sequence,
                    sequence % 2 == 0 ? WorkflowRunState.WaitingForInput : WorkflowRunState.Running,
                    Now.AddMinutes(sequence))))
                .ToArray();
            activeRecords[0].OriginJson = "{ malformed dashboard-irrelevant origin";
            dbContext.Set<WorkflowRunRecordEntity>().AddRange(activeRecords);
            dbContext.Set<WorkflowRunRecordEntity>().Add(
                WorkflowRunRecordEntity.FromSnapshot(CreateRun(20, WorkflowRunState.Completed, Now.AddDays(1))));
            await dbContext.SaveChangesAsync();
        }

        factory.ResetTrackedEntityCount();
        var store = new PersistentWorkflowRunStore(factory);

        var result = await store.QueryActivityAsync(new WorkflowDashboardActivityQuery());

        Assert.Equal(WorkflowDashboardActivityMode.Active, result.Mode);
        Assert.Equal([7, 6, 5, 4, 3], result.Runs.Select(run => RunSequence(run.RunId)));
        Assert.Equal(0, factory.TrackedEntityCount);
    }

    [Fact]
    public async Task Persistent_store_matches_recent_fallback_when_no_run_is_active()
    {
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(AgentFrameworkModuleAssemblyMarker).Assembly]);
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"workflow-dashboard-fallback-{Guid.NewGuid():N}")
            .Options;
        var factory = new TrackingAppDbContextFactory(options);
        await using (var dbContext = factory.CreateDbContext())
        {
            dbContext.Set<WorkflowRunRecordEntity>().AddRange(
                WorkflowRunRecordEntity.FromSnapshot(CreateRun(1, WorkflowRunState.Completed, Now)),
                WorkflowRunRecordEntity.FromSnapshot(CreateRun(2, WorkflowRunState.Failed, Now.AddMinutes(2))),
                WorkflowRunRecordEntity.FromSnapshot(CreateRun(3, WorkflowRunState.Cancelled, Now.AddMinutes(1))),
                WorkflowRunRecordEntity.FromSnapshot(CreateRun(4, WorkflowRunState.NotStarted, Now.AddMinutes(4))),
                WorkflowRunRecordEntity.FromSnapshot(CreateRun(5, WorkflowRunState.Idle, Now.AddMinutes(3))));
            await dbContext.SaveChangesAsync();
        }

        factory.ResetTrackedEntityCount();
        var store = new PersistentWorkflowRunStore(factory);

        var result = await store.QueryActivityAsync(new WorkflowDashboardActivityQuery(4));

        Assert.Equal(WorkflowDashboardActivityMode.RecentFallback, result.Mode);
        Assert.Equal([4, 5, 2, 3], result.Runs.Select(run => RunSequence(run.RunId)));
        Assert.Equal(0, factory.TrackedEntityCount);
    }

    [Fact]
    public async Task Persistent_catalog_lookup_returns_only_requested_write_heads_without_tracking()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(
            [typeof(AgentFrameworkModuleAssemblyMarker).Assembly, typeof(PromptsModuleAssemblyMarker).Assembly]);
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"workflow-dashboard-catalog-{Guid.NewGuid():N}")
            .Options;
        var factory = new TrackingAppDbContextFactory(options);
        var requestedWorkflowId = new WorkflowId(Guid.Parse("40000000-0000-0000-0000-000000000001"));
        var unrelatedWorkflowId = new WorkflowId(Guid.Parse("40000000-0000-0000-0000-000000000002"));
        var missingWorkflowId = new WorkflowId(Guid.Parse("40000000-0000-0000-0000-000000000003"));
        var oldVersionId = WorkflowVersionId.New();
        var currentVersionId = WorkflowVersionId.New();
        var unrelatedVersionId = WorkflowVersionId.New();
        await using (var dbContext = factory.CreateDbContext())
        {
            dbContext.Set<WorkflowDefinitionRecord>().AddRange(
                CreateDefinitionRecord(requestedWorkflowId, oldVersionId, 1, "Requested old", Now),
                CreateDefinitionRecord(requestedWorkflowId, currentVersionId, 2, "Requested current", Now.AddMinutes(1)),
                CreateDefinitionRecord(unrelatedWorkflowId, unrelatedVersionId, 1, "Unrelated", Now.AddMinutes(2)));
            dbContext.Set<WorkflowDefinitionHeadRecord>().AddRange(
                new WorkflowDefinitionHeadRecord
                {
                    WorkflowId = requestedWorkflowId.Value,
                    VersionId = currentVersionId.Value
                },
                new WorkflowDefinitionHeadRecord
                {
                    WorkflowId = unrelatedWorkflowId.Value,
                    VersionId = unrelatedVersionId.Value
                });
            await dbContext.SaveChangesAsync();
        }

        factory.ResetTrackedEntityCount();
        var promptGallery = PromptGalleryTestSupport.CreateService(factory);
        var catalog = new PersistentWorkflowCatalogService(
            factory,
            new WorkflowDefinitionValidator(),
            promptGallery,
            promptGallery);

        var result = await catalog.LookupDefinitionsAsync(new WorkflowCatalogLookupQuery(
            [requestedWorkflowId, missingWorkflowId]));

        var item = Assert.Single(result);
        Assert.Equal(requestedWorkflowId, item.Id);
        Assert.Equal(currentVersionId, item.VersionId);
        Assert.Equal("Requested current", item.Name);
        Assert.Equal(0, factory.TrackedEntityCount);
    }

    [Fact]
    public async Task In_memory_catalog_lookup_returns_only_requested_latest_definitions()
    {
        var catalog = new InMemoryWorkflowCatalogService(new WorkflowDefinitionValidator());
        var oldDefinition = await catalog.SaveDefinitionAsync(CreateDefinitionSaveRequest("Requested old"));
        var currentDefinition = await catalog.SaveDefinitionAsync(CreateDefinitionSaveRequest(
            "Requested current",
            oldDefinition.Id,
            oldDefinition.VersionId));
        await catalog.SaveDefinitionAsync(CreateDefinitionSaveRequest("Unrelated"));
        var missingWorkflowId = new WorkflowId(Guid.Parse("50000000-0000-0000-0000-000000000001"));

        var result = await catalog.LookupDefinitionsAsync(new WorkflowCatalogLookupQuery(
            [oldDefinition.Id, missingWorkflowId]));

        var item = Assert.Single(result);
        Assert.Equal(oldDefinition.Id, item.Id);
        Assert.Equal(currentDefinition.VersionId, item.VersionId);
        Assert.Equal("Requested current", item.Name);
    }

    [Fact]
    public async Task Query_service_uses_bounded_id_lookup_and_labels_deleted_workflows_honestly()
    {
        var knownWorkflowId = new WorkflowId(Guid.Parse("20000000-0000-0000-0000-000000000001"));
        var deletedWorkflowId = new WorkflowId(Guid.Parse("20000000-0000-0000-0000-000000000002"));
        var knownRun = CreateDashboardRun(1, WorkflowRunState.Running, Now) with { WorkflowId = knownWorkflowId };
        var deletedRun = CreateDashboardRun(2, WorkflowRunState.WaitingForInput, Now.AddMinutes(-1)) with
        {
            WorkflowId = deletedWorkflowId
        };
        var activityStore = new StubWorkflowActivityStore(new WorkflowDashboardActivityStoreResult(
            WorkflowDashboardActivityMode.Active,
            [knownRun, deletedRun]));
        var catalog = new StubWorkflowCatalogLookupService(
        [
            CreateCatalogItem(knownWorkflowId, "Known workflow")
        ]);
        var service = new WorkflowDashboardActivityQueryService(activityStore, catalog);

        var result = await service.QueryAsync(new WorkflowDashboardActivityQuery());

        Assert.Equal(WorkflowDashboardActivityMode.Active, result.Mode);
        Assert.Equal("Known workflow", result.Items[0].WorkflowName);
        Assert.Equal($"Deleted workflow {deletedWorkflowId.Value:D}", result.Items[1].WorkflowName);
        var lookup = Assert.Single(catalog.Queries);
        Assert.Equal([knownWorkflowId, deletedWorkflowId], lookup.WorkflowIds);
        Assert.Equal(WorkflowDashboardActivityQuery.MaximumTake, activityStore.Queries[0].Take);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(WorkflowDashboardActivityQuery.MaximumTake + 1)]
    public void Query_rejects_unbounded_take(int take)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowDashboardActivityQuery(take));
    }

    [Fact]
    public void Catalog_lookup_normalizes_duplicates_before_enforcing_its_independent_bound()
    {
        var workflowIds = Enumerable.Range(1, WorkflowCatalogLookupQuery.MaximumWorkflowCount + 1)
            .Select(sequence => new WorkflowId(new Guid($"30000000-0000-0000-0000-{sequence:D12}")))
            .ToArray();
        var duplicateWorkflowId = workflowIds[0];

        var normalized = new WorkflowCatalogLookupQuery(
            Enumerable.Repeat(duplicateWorkflowId, WorkflowCatalogLookupQuery.MaximumWorkflowCount + 1).ToArray());

        Assert.Equal([duplicateWorkflowId], normalized.WorkflowIds);
        Assert.NotEqual(WorkflowDashboardActivityQuery.MaximumTake, WorkflowCatalogLookupQuery.MaximumWorkflowCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowCatalogLookupQuery(workflowIds));
    }

    [Fact]
    public void Workflow_activity_services_and_store_aliases_are_registered()
    {
        var coreServices = new ServiceCollection();
        var inMemoryCatalogServices = new ServiceCollection();
        var inMemoryRuntimeServices = new ServiceCollection();
        var moduleServices = new ServiceCollection();

        coreServices.AddWorkflowCoreServices();
        inMemoryCatalogServices.AddInMemoryWorkflowCatalogServices();
        inMemoryRuntimeServices.AddInMemoryWorkflowRuntimeStores(Path.GetTempPath());
        moduleServices.AddAgentFrameworkModule(new ConfigurationBuilder().Build());

        AssertRegistration<IWorkflowDashboardActivityQueryService, WorkflowDashboardActivityQueryService>(
            coreServices,
            ServiceLifetime.Scoped);
        AssertAlias<IWorkflowCatalogLookupService, InMemoryWorkflowCatalogService>(
            inMemoryCatalogServices,
            ServiceLifetime.Scoped);
        AssertAlias<IWorkflowDashboardActivityStore, InMemoryWorkflowRunStore>(
            inMemoryRuntimeServices,
            ServiceLifetime.Singleton);
        AssertAlias<IWorkflowCatalogLookupService, PersistentWorkflowCatalogService>(
            moduleServices,
            ServiceLifetime.Scoped);
        AssertAlias<IWorkflowDashboardActivityStore, PersistentWorkflowRunStore>(
            moduleServices,
            ServiceLifetime.Scoped);
    }

    private static WorkflowRunSnapshot CreateRun(
        int sequence,
        WorkflowRunState state,
        DateTimeOffset updatedAtUtc)
        => new(
            new WorkflowRunId(new Guid($"00000000-0000-0000-0000-{sequence:D12}")),
            WorkflowId,
            WorkflowVersionId.New(),
            state,
            WorkflowRuntimeBackendKind.InProcess,
            $"backend-{sequence}",
            $"Run {sequence}",
            updatedAtUtc.AddMinutes(-1),
            updatedAtUtc);

    private static WorkflowDashboardActivityRun CreateDashboardRun(
        int sequence,
        WorkflowRunState state,
        DateTimeOffset updatedAtUtc)
        => new(
            new WorkflowRunId(new Guid($"00000000-0000-0000-0000-{sequence:D12}")),
            WorkflowId,
            state,
            $"Run {sequence}",
            updatedAtUtc);

    private static int RunSequence(WorkflowRunId runId)
        => int.Parse(runId.Value.ToString("N")[^12..]);

    private static WorkflowCatalogItem CreateCatalogItem(WorkflowId workflowId, string name)
        => new(
            workflowId,
            WorkflowVersionId.New(),
            name,
            string.Empty,
            WorkflowLifecycleStatus.Active,
            WorkflowRuntimeBackendKind.InProcess,
            Now);

    private static WorkflowDefinitionRecord CreateDefinitionRecord(
        WorkflowId workflowId,
        WorkflowVersionId versionId,
        long revision,
        string name,
        DateTimeOffset updatedAtUtc)
        => new()
        {
            WorkflowId = workflowId.Value,
            VersionId = versionId.Value,
            Revision = revision,
            Name = name,
            Description = string.Empty,
            Status = WorkflowLifecycleStatus.Active,
            PreferredBackend = WorkflowRuntimeBackendKind.InProcess,
            DefinitionJson = "{}",
            InstructionSnapshotSchemaVersion = 3,
            CreatedAtUtc = Now,
            UpdatedAtUtc = updatedAtUtc
        };

    private static WorkflowDefinitionSaveRequest CreateDefinitionSaveRequest(
        string name,
        WorkflowId? workflowId = null,
        WorkflowVersionId? expectedVersionId = null)
    {
        var start = CreateNode("start", WorkflowNodeKind.Start);
        var end = CreateNode("end", WorkflowNodeKind.End);
        return new WorkflowDefinitionSaveRequest(
            workflowId,
            expectedVersionId,
            name,
            string.Empty,
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(
                start.Id,
                [start, end],
                [
                    new WorkflowEdge(
                        new WorkflowEdgeId("start-to-end"),
                        start.Id,
                        SourcePortId: null,
                        end.Id,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty)
                ]),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false));
    }

    private static WorkflowNode CreateNode(string id, WorkflowNodeKind kind)
        => new(
            new WorkflowNodeId(id),
            kind,
            id,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text));

    private static void AssertRegistration<TService, TImplementation>(
        IServiceCollection services,
        ServiceLifetime lifetime)
    {
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(TService) &&
                descriptor.ImplementationType == typeof(TImplementation) &&
                descriptor.Lifetime == lifetime);
    }

    private static void AssertAlias<TService, TImplementation>(
        IServiceCollection services,
        ServiceLifetime lifetime)
    {
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(TService) &&
                descriptor.ImplementationFactory is not null &&
                descriptor.Lifetime == lifetime);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(TImplementation));
    }

    private sealed class StubWorkflowActivityStore(
        WorkflowDashboardActivityStoreResult result) : IWorkflowDashboardActivityStore
    {
        public List<WorkflowDashboardActivityQuery> Queries { get; } = [];

        public Task<WorkflowDashboardActivityStoreResult> QueryActivityAsync(
            WorkflowDashboardActivityQuery query,
            CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            return Task.FromResult(result);
        }
    }

    private sealed class StubWorkflowCatalogLookupService(
        IReadOnlyList<WorkflowCatalogItem> definitions) : IWorkflowCatalogLookupService
    {
        public List<WorkflowCatalogLookupQuery> Queries { get; } = [];

        public Task<IReadOnlyList<WorkflowCatalogItem>> LookupDefinitionsAsync(
            WorkflowCatalogLookupQuery query,
            CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            return Task.FromResult(definitions);
        }
    }

    private sealed class TrackingAppDbContextFactory(
        DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public int TrackedEntityCount { get; private set; }

        public AppDbContext CreateDbContext()
        {
            var dbContext = new AppDbContext(options);
            dbContext.ChangeTracker.Tracked += (_, _) => TrackedEntityCount++;
            return dbContext;
        }

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }

        public void ResetTrackedEntityCount()
        {
            TrackedEntityCount = 0;
        }
    }
}
