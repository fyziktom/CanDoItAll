using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectStructureChangesHttpTests {
    [Fact]
    public async Task Rolled_back_node_write_has_no_notification_or_persisted_node() {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        await using var scope = host.App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projectId = await CreateProjectAsync(services);
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var original = await workbench.GetStructureAsync(projectId);
        var path = $"/api/project-structure/projects/{projectId:D}/changes";
        var baseline = await host.Client.GetFromJsonAsync<ProjectStructureChanges>(path);
        Assert.NotNull(baseline);
        var contexts = services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>();
        var nodeId = $"note:{Guid.NewGuid():D}";
        await using (var context = await contexts.CreateDbContextAsync()) {
            await using var mutation = await services.GetRequiredService<ProjectStructureMutationScopeFactory>()
                .BeginAsync(context, ProjectStructureSerializableMutationScope.ForProject(projectId), default, [original.ExpectedProjectAdmission!]);
            context.Add(new ProjectObjectRecord { ProjectId = projectId, NodeKey = nodeId, Title = "Rolled back", ObjectType = ProjectObjectType.Note });
            await context.SaveChangesAsync();
            var beforeCommit = await host.Client.GetFromJsonAsync<ProjectStructureChanges>($"{path}?after={Cursor(baseline.Cursor)}");
            Assert.NotNull(beforeCommit);
            Assert.Empty(beforeCommit.Changes);
        }
        await using var verification = await contexts.CreateDbContextAsync();
        Assert.False(await verification.Set<ProjectObjectRecord>().AnyAsync(node => node.NodeKey == nodeId));
        var afterRollback = await host.Client.GetFromJsonAsync<ProjectStructureChanges>($"{path}?after={Cursor(baseline.Cursor)}");
        Assert.NotNull(afterRollback);
        Assert.Empty(afterRollback.Changes);
    }

    [Fact]
    public async Task Polling_returns_real_committed_nodes_and_resumes_without_duplicates() {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        await using var scope = host.App.Services.CreateAsyncScope();
        var projectId = await CreateProjectAsync(scope.ServiceProvider);
        var path = $"/api/project-structure/projects/{projectId:D}/changes";
        var baseline = await host.Client.GetFromJsonAsync<ProjectStructureChanges>(path);
        Assert.NotNull(baseline);
        Assert.Empty(baseline.Changes);
        var session = scope.ServiceProvider.GetRequiredService<ProjectStructureChangeSession>();
        session.Actor = "Editor B";
        var created = await CreateNodeAsync(scope.ServiceProvider, projectId);
        var page = await host.Client.GetFromJsonAsync<ProjectStructureChanges>($"{path}?after={Cursor(baseline.Cursor)}");
        Assert.NotNull(page);
        var change = Assert.Single(page.Changes);
        Assert.Equal(session.Id, change.SessionId);
        Assert.Equal("Editor B", change.Actor);
        Assert.Contains(change.Nodes, node => node.NodeId == created.Id && node.Kind == ProjectStructureChangeKind.Created);
        var next = await host.Client.GetFromJsonAsync<ProjectStructureChanges>($"{path}?after={Cursor(page.Cursor)}");
        Assert.NotNull(next);
        Assert.Empty(next.Changes);
        var stale = await host.Client.GetFromJsonAsync<ProjectStructureChanges>($"{path}?after={Guid.NewGuid():N}:0");
        Assert.NotNull(stale);
        Assert.True(stale.RequiresRefresh);
    }

    [Fact]
    public async Task Sse_follows_real_writes_and_replays_from_last_event_id() {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        await using var scope = host.App.Services.CreateAsyncScope();
        var projectId = await CreateProjectAsync(scope.ServiceProvider);
        var path = $"/api/project-structure/projects/{projectId:D}/changes/stream";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var response = await host.Client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token));
        var ready = await ReadEventAsync(reader, timeout.Token);
        Assert.Equal("project.structure.ready", ready.Name);
        var created = await CreateNodeAsync(scope.ServiceProvider, projectId);
        var changed = await ReadEventAsync(reader, timeout.Token);
        Assert.Equal("project.structure.changed", changed.Name);
        Assert.Contains(changed.Batch.Changes.SelectMany(item => item.Nodes), node => node.NodeId == created.Id);
        using var replayRequest = new HttpRequestMessage(HttpMethod.Get, path);
        replayRequest.Headers.Add("Last-Event-ID", ready.Id);
        using var replayResponse = await host.Client.SendAsync(replayRequest, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        replayResponse.EnsureSuccessStatusCode();
        using var replayReader = new StreamReader(await replayResponse.Content.ReadAsStreamAsync(timeout.Token));
        var replay = await ReadEventAsync(replayReader, timeout.Token);
        Assert.Contains(replay.Batch.Changes.SelectMany(item => item.Nodes), node => node.NodeId == created.Id);
    }

    [Fact]
    public async Task Polling_and_sse_reject_invalid_conflicting_cursors_and_missing_projects() {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        await using var scope = host.App.Services.CreateAsyncScope();
        var projectId = await CreateProjectAsync(scope.ServiceProvider);
        var path = $"/api/project-structure/projects/{projectId:D}/changes";
        using var invalid = await host.Client.GetAsync($"{path}?after=invalid");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var invalidTake = await host.Client.GetAsync($"{path}?take=101");
        Assert.Equal(HttpStatusCode.BadRequest, invalidTake.StatusCode);
        using var repeated = await host.Client.GetAsync($"{path}?after={Guid.NewGuid():N}:0&after={Guid.NewGuid():N}:0");
        Assert.Equal(HttpStatusCode.BadRequest, repeated.StatusCode);
        using var conflictingRequest = new HttpRequestMessage(HttpMethod.Get, $"{path}/stream?after={Guid.NewGuid():N}:0");
        conflictingRequest.Headers.Add("Last-Event-ID", $"{Guid.NewGuid():N}:0");
        using var conflicting = await host.Client.SendAsync(conflictingRequest);
        Assert.Equal(HttpStatusCode.BadRequest, conflicting.StatusCode);
        using var missing = await host.Client.GetAsync($"/api/project-structure/projects/{Guid.NewGuid():D}/changes");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private static async Task<Guid> CreateProjectAsync(IServiceProvider services) {
        var result = await services.GetRequiredService<ProjectsService>().SaveAsync(new ProjectEditorModel {
            Name = "Structure change test", Objective = "Observe committed nodes", CurrentPhase = "Validation"
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<ProjectStructureNode> CreateNodeAsync(IServiceProvider services, Guid projectId) {
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var original = await workbench.GetStructureAsync(projectId);
        return await workbench.CreateObjectAsync(projectId,
            new(ProjectObjectType.Note, "Changed node", "", "", $"project:{projectId:D}") {
                ExpectedProjectAdmission = original.ExpectedProjectAdmission
            });
    }

    private static string Cursor(ProjectStructureChangeCursor cursor) => $"{cursor.StreamId:N}:{cursor.Sequence}";

    private static async Task<(string Name, string Id, ProjectStructureChanges Batch)> ReadEventAsync(StreamReader reader, CancellationToken cancellationToken) {
        string? name = null;
        string? id = null;
        string? data = null;
        while (await reader.ReadLineAsync(cancellationToken) is { } line) {
            if (line.StartsWith("event: ", StringComparison.Ordinal)) {
                name = line[7..];
            } else if (line.StartsWith("id: ", StringComparison.Ordinal)) {
                id = line[4..];
            } else if (line.StartsWith("data: ", StringComparison.Ordinal)) {
                data = line[6..];
            } else if (line.Length == 0 && data is not null) {
                return (name ?? "message", id ?? "", JsonSerializer.Deserialize<ProjectStructureChanges>(data, JsonSerializerOptions.Web)!);
            }
        }
        throw new InvalidDataException("The project structure event stream ended before a complete event.");
    }
}
