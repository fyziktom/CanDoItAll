using CanDoItAll.Modules.Projects.Pages;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectsPageLoadGenerationTests
{
    [Fact]
    public async Task Late_route_A_cannot_replace_a_newer_preview_B()
    {
        var generation = new ProjectsPageLoadGeneration();
        var routeCompletion = CreateCompletionSource();
        var previewCompletion = CreateCompletionSource();
        var committedProjects = new List<Guid>();
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();

        var routeStamp = generation.Begin(new ProjectsPageLoadKey(
            ProjectsPageLoadKind.Route,
            projectA));
        var routeLoad = ApplyWhenCompletedAsync(routeStamp, routeCompletion.Task, projectA);
        var previewStamp = generation.Begin(new ProjectsPageLoadKey(
            ProjectsPageLoadKind.Preview,
            projectB));
        var previewLoad = ApplyWhenCompletedAsync(previewStamp, previewCompletion.Task, projectA);

        previewCompletion.SetResult(projectB);
        await previewLoad;
        routeCompletion.SetResult(projectA);
        await routeLoad;

        Assert.Equal([projectB], committedProjects);

        async Task ApplyWhenCompletedAsync(
            ProjectsPageLoadStamp stamp,
            Task<Guid> projectTask,
            Guid? currentRouteProjectId)
        {
            var loadedProjectId = await projectTask;
            generation.TryCommit(
                stamp,
                currentRouteProjectId,
                () => committedProjects.Add(loadedProjectId));
        }
    }

    [Fact]
    public async Task Late_completion_refresh_cannot_overwrite_newer_route_B_or_publish_failure()
    {
        var generation = new ProjectsPageLoadGeneration();
        var refreshCompletion = CreateCompletionSource();
        var routeCompletion = CreateCompletionSource();
        var state = "loading";
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();

        var refreshStamp = generation.Begin(new ProjectsPageLoadKey(
            ProjectsPageLoadKind.CompletionRefresh,
            projectA));
        var refreshLoad = CompleteRefreshAsync();
        var routeStamp = generation.Begin(new ProjectsPageLoadKey(
            ProjectsPageLoadKind.Route,
            projectB));
        var routeLoad = CompleteRouteAsync();

        routeCompletion.SetResult(projectB);
        await routeLoad;
        refreshCompletion.SetException(new InvalidOperationException("stale refresh failed"));
        await refreshLoad;

        Assert.Equal("ready-b", state);

        async Task CompleteRefreshAsync()
        {
            try
            {
                await refreshCompletion.Task;
                generation.TryCommit(refreshStamp, projectB, () => state = "ready-a");
            }
            catch (InvalidOperationException)
            {
                generation.TryCommit(refreshStamp, projectB, () => state = "failed-a");
            }
        }

        async Task CompleteRouteAsync()
        {
            await routeCompletion.Task;
            generation.TryCommit(routeStamp, projectB, () => state = "ready-b");
        }
    }

    [Fact]
    public void Route_stamp_is_rejected_when_the_query_identity_changes()
    {
        var generation = new ProjectsPageLoadGeneration();
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();
        var routeStamp = generation.Begin(new ProjectsPageLoadKey(
            ProjectsPageLoadKind.Route,
            projectA));

        Assert.False(generation.IsCurrent(routeStamp, projectB));
        Assert.False(generation.TryCommit(routeStamp, projectB, () => { }));
    }

    private static TaskCompletionSource<Guid> CreateCompletionSource()
    {
        return new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
