using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Web.Dashboard;

namespace CanDoItAll.Tests.Components.Shell;

public sealed class DashboardSnapshotLoaderTests
{
    private static readonly DateTimeOffset UpdatedAtUtc = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Loads_each_bounded_source_and_maps_usage_without_double_counting()
    {
        var projectId = Guid.NewGuid();
        var projects = new StubProjectActivityQueryService
        {
            Result =
            [
                new RecentProjectActivityItem(
                    projectId,
                    "Dashboard project",
                    ProjectStatus.Active,
                    "Implementation",
                    UpdatedAtUtc)
            ]
        };
        var workflows = new StubWorkflowActivityQueryService();
        var processes = new StubProcessActivityQueryService();
        var usage = new StubUsageTotalsQueryService
        {
            Result = new AgentUsageTotals(12_345L, 7.89m, 2, UpdatedAtUtc)
        };
        var loader = new DashboardSnapshotLoader(projects, workflows, processes, usage);

        var result = await loader.LoadAsync();

        Assert.Equal(6, projects.RequestedItemCount);
        Assert.Equal(5, workflows.RequestedItemCount);
        Assert.Equal(5, processes.RequestedItemCount);
        var project = Assert.Single(result.Projects);
        Assert.Equal(projectId, project.ProjectId);
        Assert.Equal("Active", project.Status.Label);
        Assert.Equal(12_345L, result.Usage.ObservedTokens);
        Assert.Equal(7.89m, result.Usage.KnownCostUsd);
        Assert.Equal(2, result.Usage.UnknownUsageObservationCount);

        var mutableView = (IList<DashboardProjectItem>)result.Projects;
        Assert.Throws<NotSupportedException>(() =>
            mutableView[0] = project with { Name = "Mutated across cache readers" });
        Assert.Equal("Dashboard project", result.Projects[0].Name);
    }

    [Fact]
    public async Task Rejects_a_project_source_that_breaks_the_six_row_contract()
    {
        var projects = new StubProjectActivityQueryService
        {
            Result = Enumerable.Range(0, 7)
                .Select(index => new RecentProjectActivityItem(
                    Guid.NewGuid(),
                    $"Project {index}",
                    ProjectStatus.Draft,
                    "Planning",
                    UpdatedAtUtc))
                .ToArray()
        };
        var loader = new DashboardSnapshotLoader(
            projects,
            new StubWorkflowActivityQueryService(),
            new StubProcessActivityQueryService(),
            new StubUsageTotalsQueryService());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => loader.LoadAsync());

        Assert.Contains("maximum is 6", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Propagates_source_failure_instead_of_substituting_zero_data()
    {
        var expected = new InvalidOperationException("Usage projection unavailable");
        var loader = new DashboardSnapshotLoader(
            new StubProjectActivityQueryService(),
            new StubWorkflowActivityQueryService(),
            new StubProcessActivityQueryService(),
            new StubUsageTotalsQueryService { Exception = expected });

        var actual = await Assert.ThrowsAsync<DashboardSnapshotSourceException>(() => loader.LoadAsync());

        Assert.Equal(DashboardSnapshotSource.AgentUsage, actual.SnapshotSource);
        Assert.Same(expected, actual.InnerException);
    }

    private sealed class StubProjectActivityQueryService : IRecentProjectActivityQueryService
    {
        public IReadOnlyList<RecentProjectActivityItem> Result { get; init; } = [];

        public int RequestedItemCount { get; private set; }

        public Task<IReadOnlyList<RecentProjectActivityItem>> ListAsync(
            int itemCount,
            CancellationToken cancellationToken = default)
        {
            RequestedItemCount = itemCount;
            return Task.FromResult(Result);
        }
    }

    private sealed class StubWorkflowActivityQueryService : IWorkflowDashboardActivityQueryService
    {
        public int RequestedItemCount { get; private set; }

        public Task<WorkflowDashboardActivityResult> QueryAsync(
            WorkflowDashboardActivityQuery query,
            CancellationToken cancellationToken = default)
        {
            RequestedItemCount = query.Take;
            return Task.FromResult(new WorkflowDashboardActivityResult(
                WorkflowDashboardActivityMode.RecentFallback,
                []));
        }
    }

    private sealed class StubProcessActivityQueryService : IProcessDashboardActivityQueryService
    {
        public int RequestedItemCount { get; private set; }

        public Task<ProcessDashboardActivityResult> QueryAsync(
            ProcessDashboardActivityQuery query,
            CancellationToken cancellationToken = default)
        {
            RequestedItemCount = query.Take;
            return Task.FromResult(new ProcessDashboardActivityResult(
                ProcessDashboardActivityMode.RecentFallback,
                []));
        }
    }

    private sealed class StubUsageTotalsQueryService : IAgentUsageTotalsQueryService
    {
        public AgentUsageTotals Result { get; init; } = new(0, 0m, 0, UpdatedAtUtc);

        public Exception? Exception { get; init; }

        public Task<AgentUsageTotals> GetTotalsAsync(CancellationToken cancellationToken = default)
        {
            return Exception is null
                ? Task.FromResult(Result)
                : Task.FromException<AgentUsageTotals>(Exception);
        }
    }
}
