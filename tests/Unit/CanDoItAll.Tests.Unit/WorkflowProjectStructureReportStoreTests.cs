using System.Linq.Expressions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Tests.Unit.AgentFramework;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class WorkflowProjectStructureReportStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 16, 0, 0, TimeSpan.Zero);
    private static readonly Guid WorkflowId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid VersionId = Guid.Parse("20000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Persistent_store_filters_and_aggregates_the_requested_project_page()
    {
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(AgentFrameworkModuleAssemblyMarker).Assembly]);
        var queryInterceptor = new QueryCompilationCountingInterceptor();
        var databaseRoot = new InMemoryDatabaseRoot();
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase(
                $"workflow-project-report-{Guid.NewGuid():N}",
                databaseRoot)
            .EnableServiceProviderCaching(false)
            .AddInterceptors(queryInterceptor)
            .Options;
        var factory = new TrackingAppDbContextFactory(options);
        var projectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        var first = CreateRun(
            sequence: 1,
            projectId,
            WorkflowLaunchOriginKind.ProjectStructureNode,
            WorkflowRunState.Completed,
            createdAtUtc: Now.AddHours(-2),
            endedAtUtc: Now.AddHours(-1));
        var second = CreateRun(
            sequence: 2,
            projectId,
            WorkflowLaunchOriginKind.ProjectStructureNode,
            WorkflowRunState.Failed,
            createdAtUtc: Now.AddDays(-1).AddHours(-2),
            endedAtUtc: Now.AddDays(-1));
        var third = CreateRun(
            sequence: 3,
            projectId,
            WorkflowLaunchOriginKind.ProjectStructureNode,
            WorkflowRunState.Completed,
            createdAtUtc: Now.AddDays(-5).AddMinutes(-30),
            endedAtUtc: Now.AddDays(-5));
        var excludedRuns = new[]
        {
            CreateRun(
                sequence: 4,
                otherProjectId,
                WorkflowLaunchOriginKind.ProjectStructureNode,
                WorkflowRunState.Completed,
                Now.AddHours(-2),
                Now.AddHours(-1)),
            CreateRun(
                sequence: 5,
                projectId,
                WorkflowLaunchOriginKind.ProcessAssignment,
                WorkflowRunState.Completed,
                Now.AddHours(-2),
                Now.AddHours(-1)),
            CreateRun(
                sequence: 6,
                projectId,
                WorkflowLaunchOriginKind.ProjectStructureNode,
                WorkflowRunState.Running,
                Now.AddHours(-2),
                Now.AddHours(-1)),
            CreateRun(
                sequence: 7,
                projectId,
                WorkflowLaunchOriginKind.ProjectStructureNode,
                WorkflowRunState.Completed,
                Now.AddDays(-12).AddHours(-1),
                Now.AddDays(-12)),
            CreateRun(
                sequence: 8,
                projectId,
                WorkflowLaunchOriginKind.ProjectStructureNode,
                WorkflowRunState.Completed,
                Now,
                Now.AddMinutes(1))
        };

        await using (var dbContext = factory.CreateDbContext())
        {
            dbContext.Set<WorkflowRunRecordEntity>().AddRange([first, second, third, .. excludedRuns]);
            dbContext.Set<WorkflowUsageObservationRecordEntity>().AddRange(
                CreateUsage(sequence: 1, first, WorkflowPricingStatus.Known, 1.25m),
                CreateUsage(sequence: 2, second, WorkflowPricingStatus.Known, 2.50m),
                CreateUsage(sequence: 3, second, WorkflowPricingStatus.Unknown, costUsd: null),
                CreateUsage(sequence: 4, excludedRuns[0], WorkflowPricingStatus.Known, 100m),
                CreateUsage(sequence: 5, excludedRuns[1], WorkflowPricingStatus.Known, 100m),
                CreateUsage(sequence: 6, excludedRuns[2], WorkflowPricingStatus.Known, 100m),
                CreateUsage(sequence: 7, excludedRuns[3], WorkflowPricingStatus.Known, 100m),
                CreateUsage(sequence: 8, excludedRuns[4], WorkflowPricingStatus.Known, 100m));
            await dbContext.SaveChangesAsync();
        }

        factory.ResetTrackedEntityCount();
        var store = new PersistentWorkflowRunStore(factory);
        var firstPage = await store.QueryProjectStructureReportAsync(CreateQuery(projectId, pageIndex: 0));
        Assert.Equal(3, queryInterceptor.CompilationCount);
        queryInterceptor.Reset();
        var secondPage = await store.QueryProjectStructureReportAsync(
            CreateQuery(projectId, pageIndex: 1, includeAggregate: false));
        Assert.Equal(1, queryInterceptor.CompilationCount);
        queryInterceptor.Reset();
        var pagePastEnd = await store.QueryProjectStructureReportAsync(CreateQuery(projectId, pageIndex: 2));
        Assert.Equal(3, queryInterceptor.CompilationCount);

        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(3.75m, firstPage.KnownCostUsd);
        Assert.Equal(2, firstPage.UnknownCostRunCount);
        Assert.Equal((long)TimeSpan.FromHours(3.5).TotalMilliseconds, firstPage.TotalDurationMilliseconds);
        Assert.Equal([first.RunId, second.RunId], firstPage.Runs.Select(run => run.RunId.Value));
        Assert.Equal([1.25m, 2.50m], firstPage.Runs.Select(run => run.KnownCostUsd));
        Assert.Equal([false, true], firstPage.Runs.Select(run => run.HasUnknownCost));
        Assert.Equal(
            [
                new WorkflowProjectStructureDailyCost(
                    DateOnly.FromDateTime(Now.AddDays(-1).UtcDateTime),
                    2.50m),
                new WorkflowProjectStructureDailyCost(
                    DateOnly.FromDateTime(Now.UtcDateTime),
                    1.25m)
            ],
            firstPage.DailyCost);

        var secondPageRun = Assert.Single(secondPage.Runs);
        Assert.Equal(third.RunId, secondPageRun.RunId.Value);
        Assert.True(secondPageRun.HasUnknownCost);
        Assert.Equal(0, secondPage.TotalCount);
        Assert.Equal(0m, secondPage.KnownCostUsd);
        Assert.Equal(0, secondPage.UnknownCostRunCount);
        Assert.Equal(0L, secondPage.TotalDurationMilliseconds);
        Assert.Empty(secondPage.DailyCost);
        Assert.Empty(pagePastEnd.Runs);
        Assert.Equal(3, pagePastEnd.TotalCount);
        Assert.Equal(0, factory.TrackedEntityCount);
    }

    [Fact]
    public async Task Persistent_store_uses_terminal_time_as_the_canonical_activity_time()
    {
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(AgentFrameworkModuleAssemblyMarker).Assembly]);
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"workflow-project-activity-{Guid.NewGuid():N}")
            .Options;
        var factory = new TrackingAppDbContextFactory(options);
        var projectId = Guid.NewGuid();
        var terminalInsideWindow = CreateRun(
            sequence: 1,
            projectId,
            WorkflowLaunchOriginKind.ProjectStructureNode,
            WorkflowRunState.Completed,
            createdAtUtc: Now.AddHours(-3),
            endedAtUtc: Now.AddHours(-1));
        terminalInsideWindow.UpdatedAtUtc = Now.AddMinutes(1);
        var terminalOutsideWindow = CreateRun(
            sequence: 2,
            projectId,
            WorkflowLaunchOriginKind.ProjectStructureNode,
            WorkflowRunState.Completed,
            createdAtUtc: Now.AddHours(-4),
            endedAtUtc: Now.AddHours(-3));
        terminalOutsideWindow.UpdatedAtUtc = Now.AddMinutes(-30);
        var activeInsideWindow = CreateRun(
            sequence: 3,
            projectId,
            WorkflowLaunchOriginKind.ProjectStructureNode,
            WorkflowRunState.Running,
            createdAtUtc: Now.AddHours(-2),
            endedAtUtc: Now.AddMinutes(-30));
        var activeOutsideWindow = CreateRun(
            sequence: 4,
            projectId,
            WorkflowLaunchOriginKind.ProjectStructureNode,
            WorkflowRunState.Running,
            createdAtUtc: Now.AddHours(-2),
            endedAtUtc: Now.AddMinutes(1));

        await using (var dbContext = factory.CreateDbContext())
        {
            dbContext.Set<WorkflowRunRecordEntity>().AddRange(
                terminalInsideWindow,
                terminalOutsideWindow,
                activeInsideWindow,
                activeOutsideWindow);
            await dbContext.SaveChangesAsync();
        }

        factory.ResetTrackedEntityCount();
        var store = new PersistentWorkflowRunStore(factory);
        var report = await store.QueryProjectStructureReportAsync(
            new WorkflowProjectStructureReportQuery(
                [projectId],
                Now.AddHours(-2),
                Now,
                Now.AddHours(-2),
                [WorkflowRunState.Completed, WorkflowRunState.Running],
                pageSize: 10));

        Assert.Equal(2, report.TotalCount);
        Assert.Equal(
            [activeInsideWindow.RunId, terminalInsideWindow.RunId],
            report.Runs.Select(run => run.RunId.Value));
        Assert.Equal(
            [activeInsideWindow.UpdatedAtUtc, terminalInsideWindow.TerminalAtUtc!.Value],
            report.Runs.Select(run => run.ActivityAtUtc));
        Assert.Equal(0m, report.KnownCostUsd);
        Assert.Equal(2, report.UnknownCostRunCount);
        Assert.Equal(
            (long)(TimeSpan.FromMinutes(90) + TimeSpan.FromHours(2)).TotalMilliseconds,
            report.TotalDurationMilliseconds);
        Assert.Equal(
            [
                new WorkflowProjectStructureDailyCost(
                    DateOnly.FromDateTime(Now.UtcDateTime),
                    0m)
            ],
            report.DailyCost);
        Assert.Equal(0, factory.TrackedEntityCount);
    }

    [Fact]
    public void Query_rejects_unbounded_or_inverted_windows_and_pages()
    {
        var projectId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => new WorkflowProjectStructureReportQuery(
            [],
            activityFromUtc: null,
            Now,
            Now.AddDays(-1),
            []));
        Assert.Throws<ArgumentException>(() => new WorkflowProjectStructureReportQuery(
            [projectId],
            Now,
            Now.AddDays(-1),
            Now.AddDays(-2),
            []));
        Assert.Throws<ArgumentException>(() => new WorkflowProjectStructureReportQuery(
            [projectId],
            activityFromUtc: null,
            Now,
            Now.AddMinutes(1),
            []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowProjectStructureReportQuery(
            [projectId],
            activityFromUtc: null,
            Now,
            Now.AddDays(-1),
            [],
            pageSize: WorkflowProjectStructureReportQuery.MaximumPageSize + 1));
    }

    private static WorkflowProjectStructureReportQuery CreateQuery(
        Guid projectId,
        int pageIndex,
        bool includeAggregate = true)
        => new(
            [projectId],
            Now.AddDays(-10),
            Now,
            Now.AddDays(-2),
            [WorkflowRunState.Completed, WorkflowRunState.Failed],
            pageIndex,
            pageSize: 2,
            includeAggregate: includeAggregate);

    private static WorkflowRunRecordEntity CreateRun(
        int sequence,
        Guid projectId,
        WorkflowLaunchOriginKind originKind,
        WorkflowRunState state,
        DateTimeOffset createdAtUtc,
        DateTimeOffset endedAtUtc)
        => new()
        {
            RunId = SequenceGuid(sequence),
            WorkflowId = WorkflowId,
            VersionId = VersionId,
            State = state,
            Backend = WorkflowRuntimeBackendKind.InProcess,
            BackendRunId = $"backend-{sequence}",
            Summary = $"Workflow run {sequence}",
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = endedAtUtc,
            TerminalAtUtc = state is WorkflowRunState.Completed or WorkflowRunState.Failed
                ? endedAtUtc
                : null,
            OriginJson = "{ malformed and deliberately unused by reporting",
            OriginKind = originKind,
            OriginProjectId = projectId
        };

    private static WorkflowUsageObservationRecordEntity CreateUsage(
        int sequence,
        WorkflowRunRecordEntity run,
        WorkflowPricingStatus pricingStatus,
        decimal? costUsd)
        => new()
        {
            Id = SequenceGuid(sequence + 100),
            RunId = run.RunId,
            WorkflowId = run.WorkflowId,
            VersionId = run.VersionId,
            NodeId = "node",
            ProducerKind = WorkflowUsageProducerKind.LlmComponent,
            InvocationId = SequenceGuid(sequence + 200),
            Attempt = 1,
            ProviderName = "Test provider",
            ProviderNameKey = "TEST PROVIDER",
            Model = "test-model",
            ModelKey = "TEST-MODEL",
            SourcePhase = "test",
            UsageStatus = WorkflowUsageStatus.Observed,
            PricingStatus = pricingStatus,
            PricingProvenance = pricingStatus == WorkflowPricingStatus.Known
                ? WorkflowUsagePricingProvenance.PricingProfileSnapshot
                : WorkflowUsagePricingProvenance.Unavailable,
            CostUsd = costUsd,
            PricingProfileHash = "test",
            PricingVersion = "test",
            ProviderRequestId = string.Empty,
            ProviderResponseId = string.Empty,
            RecordedAtUtc = run.UpdatedAtUtc,
            OriginJson = string.Empty
        };

    private static Guid SequenceGuid(int sequence)
        => new($"00000000-0000-0000-0000-{sequence:D12}");

    private sealed class QueryCompilationCountingInterceptor : IQueryExpressionInterceptor
    {
        public int CompilationCount { get; private set; }

        public Expression QueryCompilationStarting(
            Expression queryExpression,
            QueryExpressionEventData eventData)
        {
            CompilationCount++;
            return queryExpression;
        }

        public void Reset()
        {
            CompilationCount = 0;
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
