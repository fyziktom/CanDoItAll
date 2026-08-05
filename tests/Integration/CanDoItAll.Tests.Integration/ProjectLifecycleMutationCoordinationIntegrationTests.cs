using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectLifecycleMutationCoordinationIntegrationTests
{
    [Fact]
    public async Task Explicit_project_save_waiting_behind_delete_returns_typed_not_found_without_recreation()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var setupScope = application.Services.CreateAsyncScope();
        var projects = setupScope.ServiceProvider.GetRequiredService<ProjectsService>();
        var dbContextFactory = setupScope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectId = await CreateProjectAsync(projects, "Stale save barrier");
        var staleEditor = await projects.GetAsync(projectId);
        staleEditor.Name = "Stale replacement must fail";

        await using var saveScope = application.Services.CreateAsyncScope();
        var savingProjects = saveScope.ServiceProvider
            .GetRequiredService<ProjectsService>();
        Task<CanDoItAll.SharedKernel.Result<Guid>> saveTask;
        await using (var deletionContext = await dbContextFactory.CreateDbContextAsync())
        await using (var deletionScope = await SerializableMutationScope.BeginAsync(
                         deletionContext,
                         ProjectMutationScopeKeys.ForProject(projectId),
                         CancellationToken.None))
        {
            deletionContext.Remove(await deletionContext.Set<Project>()
                .SingleAsync(project => project.Id == projectId));
            await deletionContext.SaveChangesAsync();

            saveTask = savingProjects.SaveAsync(staleEditor);
            await AssertRemainsBlockedAsync(
                saveTask,
                TimeSpan.FromMilliseconds(300));
            await deletionScope.CommitAsync(CancellationToken.None);
        }

        var result = await saveTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Errors,
            error => error.Code == ProjectErrorCodes.NotFound);
        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        Assert.False(await verificationContext.Set<Project>()
            .AnyAsync(project => project.Id == projectId));
        Assert.False(await verificationContext.Set<Project>()
            .AnyAsync(project => project.Name == staleEditor.Name));
    }

    [Fact]
    public async Task Subproject_attach_waiting_behind_child_delete_returns_domain_failure_without_link_leak()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var setupScope = application.Services.CreateAsyncScope();
        var projects = setupScope.ServiceProvider.GetRequiredService<ProjectsService>();
        var dbContextFactory = setupScope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var parentProjectId = await CreateProjectAsync(projects, "Hierarchy parent");
        var childProjectId = await CreateProjectAsync(projects, "Hierarchy child");

        await using var hierarchyScope = application.Services.CreateAsyncScope();
        var hierarchyProjects = hierarchyScope.ServiceProvider
            .GetRequiredService<ProjectsService>();
        Task<CanDoItAll.SharedKernel.Result> attachTask;
        await using (var deletionContext = await dbContextFactory.CreateDbContextAsync())
        await using (var deletionScope = await SerializableMutationScope.BeginAsync(
                         deletionContext,
                         ProjectMutationScopeKeys.ForProject(childProjectId),
                         CancellationToken.None))
        {
            deletionContext.Remove(await deletionContext.Set<Project>()
                .SingleAsync(project => project.Id == childProjectId));
            await deletionContext.SaveChangesAsync();

            attachTask = hierarchyProjects.AddSubprojectAsync(
                parentProjectId,
                childProjectId);
            await AssertRemainsBlockedAsync(
                attachTask,
                TimeSpan.FromMilliseconds(300));
            await deletionScope.CommitAsync(CancellationToken.None);
        }

        var result = await attachTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Errors,
            error => error.Message.Contains(
                "subproject could not be found",
                StringComparison.OrdinalIgnoreCase));
        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        Assert.False(await verificationContext.Set<ProjectHierarchyLink>()
            .AnyAsync(link =>
                link.ParentProjectId == parentProjectId &&
                link.ChildProjectId == childProjectId));
    }

    [Fact]
    public async Task Concurrent_disjoint_hierarchy_edges_are_globally_serialized_and_cannot_jointly_close_a_cycle()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var setupScope = application.Services.CreateAsyncScope();
        var projects = setupScope.ServiceProvider.GetRequiredService<ProjectsService>();
        var dbContextFactory = setupScope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectA = await CreateProjectAsync(projects, "Cycle A");
        var projectB = await CreateProjectAsync(projects, "Cycle B");
        var projectC = await CreateProjectAsync(projects, "Cycle C");
        var projectD = await CreateProjectAsync(projects, "Cycle D");
        Assert.True((await projects.AddSubprojectAsync(projectB, projectC)).IsSuccess);
        Assert.True((await projects.AddSubprojectAsync(projectD, projectA)).IsSuccess);

        await using var firstScope = application.Services.CreateAsyncScope();
        await using var secondScope = application.Services.CreateAsyncScope();
        Task<CanDoItAll.SharedKernel.Result> firstEdge;
        Task<CanDoItAll.SharedKernel.Result> secondEdge;
        await using (var lockContext = await dbContextFactory.CreateDbContextAsync())
        await using (var hierarchyScope = await SerializableMutationScope.BeginAsync(
                         lockContext,
                         ProjectMutationScopeKeys.Hierarchy,
                         CancellationToken.None))
        {
            firstEdge = firstScope.ServiceProvider
                .GetRequiredService<ProjectsService>()
                .AddSubprojectAsync(projectA, projectB);
            secondEdge = secondScope.ServiceProvider
                .GetRequiredService<ProjectsService>()
                .AddSubprojectAsync(projectC, projectD);
            await AssertAllRemainBlockedAsync(
                [firstEdge, secondEdge],
                TimeSpan.FromMilliseconds(300));
            await hierarchyScope.CommitAsync(CancellationToken.None);
        }

        var results = await Task.WhenAll(firstEdge, secondEdge)
            .WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Single(results, result => result.IsSuccess);
        var rejected = Assert.Single(results, result => !result.IsSuccess);
        Assert.Contains(
            rejected.Errors,
            error => error.Message.Contains("cycle", StringComparison.OrdinalIgnoreCase));

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var links = await verificationContext.Set<ProjectHierarchyLink>()
            .AsNoTracking()
            .ToListAsync();
        Assert.Equal(3, links.Count);
        Assert.False(
            links.Any(link =>
                link.ParentProjectId == projectA &&
                link.ChildProjectId == projectB) &&
            links.Any(link =>
                link.ParentProjectId == projectC &&
                link.ChildProjectId == projectD));
    }

    [Fact]
    public async Task Attached_subproject_remains_successful_when_post_commit_activity_write_fails()
    {
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            ConfigureServices = services =>
            {
                services.RemoveAll<IActivityStream>();
                services.AddSingleton<ThrowingActivityStream>();
                services.AddSingleton<IActivityStream>(serviceProvider =>
                    serviceProvider.GetRequiredService<ThrowingActivityStream>());
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var parentProjectId = await CreateProjectAsync(projects, "Activity parent");
        var childProjectId = await CreateProjectAsync(projects, "Activity child");

        var result = await projects.AddSubprojectAsync(
            parentProjectId,
            childProjectId);

        Assert.True(result.IsSuccess);
        Assert.True(scope.ServiceProvider
            .GetRequiredService<ThrowingActivityStream>()
            .InvocationCount >= 3);
        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        Assert.True(await verificationContext.Set<ProjectHierarchyLink>()
            .AnyAsync(link =>
                link.ParentProjectId == parentProjectId &&
                link.ChildProjectId == childProjectId));
    }

    private static async Task<Guid> CreateProjectAsync(
        ProjectsService projects,
        string name)
    {
        var result = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Objective = "Validate project lifecycle serialization.",
            CurrentPhase = "Validation"
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task AssertRemainsBlockedAsync(
        Task task,
        TimeSpan observationWindow)
    {
        var deadline = TimeProvider.System.GetTimestamp() +
            (long)(observationWindow.TotalSeconds *
                   TimeProvider.System.TimestampFrequency);
        do
        {
            var delay = Task.Delay(TimeSpan.FromMilliseconds(25));
            var completed = await Task.WhenAny(task, delay);
            Assert.Same(delay, completed);
        }
        while (TimeProvider.System.GetTimestamp() < deadline);
    }

    private static async Task AssertAllRemainBlockedAsync(
        IReadOnlyCollection<Task> tasks,
        TimeSpan observationWindow)
    {
        var deadline = TimeProvider.System.GetTimestamp() +
            (long)(observationWindow.TotalSeconds *
                   TimeProvider.System.TimestampFrequency);
        do
        {
            await Task.Delay(TimeSpan.FromMilliseconds(25));
            Assert.All(tasks, task => Assert.False(task.IsCompleted));
        }
        while (TimeProvider.System.GetTimestamp() < deadline);
    }

    private sealed class ThrowingActivityStream : IActivityStream
    {
        private int invocationCount;

        public int InvocationCount => Volatile.Read(ref invocationCount);

        public Task RecordAsync(
            ActivityWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref invocationCount);
            return Task.FromException(
                new IOException("Injected post-commit activity failure."));
        }
    }
}
