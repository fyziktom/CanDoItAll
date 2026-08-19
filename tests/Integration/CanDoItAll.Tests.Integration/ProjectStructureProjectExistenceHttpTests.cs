using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed class ProjectStructureProjectExistenceHttpTests
{
    [Fact]
    public async Task Project_create_waiting_behind_deletion_is_rejected_without_orphan_rows()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        Guid projectId;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var created = await scope.ServiceProvider.GetRequiredService<ProjectsService>()
                .SaveAsync(new ProjectEditorModel
                {
                    Name = "Deletion barrier",
                    Objective = "Reject a late structure writer.",
                    CurrentPhase = "Validation"
                });
            Assert.True(created.IsSuccess);
            projectId = created.Value;
        }

        await using var writerScope = host.App.Services.CreateAsyncScope();
        var writer = writerScope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = writerScope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        Guid[] baselineBindingIds;
        await using (var baselineContext = await dbContextFactory.CreateDbContextAsync())
        {
            baselineBindingIds = await baselineContext
                .Set<ProjectNodeBindingRecord>()
                .AsNoTracking()
                .Select(binding => binding.Id)
                .OrderBy(id => id)
                .ToArrayAsync();
        }

        Task<ProjectStructureNode> createTask;
        await using (var deletionContext = await dbContextFactory.CreateDbContextAsync())
        await using (var deletionScope = await SerializableMutationScope.BeginAsync(
                         deletionContext,
                         ProjectMutationScopeKeys.ForProject(projectId),
                         CancellationToken.None))
        {
            var project = await deletionContext.Set<Project>()
                .SingleAsync(item => item.Id == projectId);
            deletionContext.Remove(project);
            await deletionContext.SaveChangesAsync();

            createTask = writer.CreateObjectAsync(
                projectId,
                new ProjectObjectCreateRequest(
                    ProjectObjectType.Note,
                    "Late note",
                    string.Empty,
                    "Must not become an orphan.",
                    $"project:{projectId:D}",
                    100,
                    100));
            await AssertRemainsBlockedAsync(createTask, TimeSpan.FromMilliseconds(300));
            await deletionScope.CommitAsync(CancellationToken.None);
        }

        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(
            () => createTask);
        Assert.Equal(404, exception.StatusCode);
        Assert.Equal("ProjectNotFound", exception.ErrorCode);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        Assert.False(await verificationContext.Set<ProjectObjectRecord>()
            .AnyAsync(record => record.ProjectId == projectId));
        var finalBindingIds = await verificationContext
            .Set<ProjectNodeBindingRecord>()
            .AsNoTracking()
            .Select(binding => binding.Id)
            .OrderBy(id => id)
            .ToArrayAsync();
        Assert.Equal(baselineBindingIds, finalBindingIds);
    }

    [Fact]
    public async Task Missing_project_reads_assets_and_lease_mutations_return_typed_not_found()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        var projectId = Guid.NewGuid();

        await AssertMissingProjectBoundaryAsync(
            host.Client,
            projectId,
            Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public async Task Deleted_project_reads_assets_and_lease_mutations_return_typed_not_found_while_release_is_idempotent()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        Guid projectId;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
            var created = await projects.SaveAsync(new ProjectEditorModel
            {
                Name = "Deleted project HTTP boundary",
                Objective = "Reject late project structure work.",
                CurrentPhase = "Validation"
            });
            Assert.True(created.IsSuccess);
            projectId = created.Value;
        }

        using var acquireResponse = await host.Client.PostAsJsonAsync(
            "/api/project-structure/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                "Validate deleted project boundary"));
        acquireResponse.EnsureSuccessStatusCode();
        using var lease = JsonDocument.Parse(await acquireResponse.Content.ReadAsStringAsync());
        var leaseToken = lease.RootElement.GetProperty("leaseToken").GetString()!;

        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ProjectsService>()
                .DeleteAsync(projectId);
        }

        await AssertMissingProjectBoundaryAsync(host.Client, projectId, leaseToken);
    }

    private static async Task AssertMissingProjectBoundaryAsync(
        HttpClient client,
        Guid projectId,
        string leaseToken)
    {
        using var structureResponse = await client.PostAsJsonAsync(
            $"/api/project-structure/projects/{projectId:D}/structure/read",
            new ProjectStructureReadRequest());
        await AssertProjectNotFoundAsync(structureResponse);

        using var assetResponse = await client.GetAsync(
            $"/api/project-structure/projects/{projectId:D}/assets/custom:missing");
        await AssertProjectNotFoundAsync(assetResponse);

        using var assetContentResponse = await client.GetAsync(
            $"/api/project-structure/projects/{projectId:D}/assets/custom:missing/content");
        await AssertProjectNotFoundAsync(assetContentResponse);

        using var acquireResponse = await client.PostAsJsonAsync(
            "/api/project-structure/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                "Late project lease"));
        await AssertProjectNotFoundAsync(acquireResponse);

        using var renewResponse = await client.PostAsJsonAsync(
            "/api/project-structure/leases/renew",
            new ProjectStructureLeaseRenewRequest(
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                leaseToken));
        await AssertProjectNotFoundAsync(renewResponse);

        using var releaseResponse = await client.PostAsJsonAsync(
            "/api/project-structure/leases/release",
            new ProjectStructureLeaseReleaseRequest(
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                leaseToken));
        releaseResponse.EnsureSuccessStatusCode();
    }

    private static async Task AssertProjectNotFoundAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "ProjectNotFound",
            body.RootElement.GetProperty("error").GetProperty("errorCode").GetString());
    }

    private static async Task AssertRemainsBlockedAsync(
        Task task,
        TimeSpan observationWindow)
    {
        var deadline = TimeProvider.System.GetTimestamp() +
            (long)(observationWindow.TotalSeconds * TimeProvider.System.TimestampFrequency);
        do
        {
            var delay = Task.Delay(TimeSpan.FromMilliseconds(25));
            var completed = await Task.WhenAny(task, delay);
            Assert.Same(delay, completed);
        }
        while (TimeProvider.System.GetTimestamp() < deadline);
    }
}
