using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.SharedKernel;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureStaleEditHttpTests {
    [Fact]
    public async Task GraphPhantomLink_BlocksStaleCascadeDelete() {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        var (project, created) = await CreateNodeAsync(host);
        await using var scope = host.App.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var peer = await owner.CreateObjectAsync(project.Id, new(ProjectObjectType.Note, "Peer", "", "", $"project:{project.Id:D}"));
        var original = await owner.GetStructureAsync(project.Id);
        var intent = new ProjectStructureAgentContext("human", "Human", "", "", "", "") {
            ExpectedProjectAdmission = original.ExpectedProjectAdmission, ExpectedGraph = ProjectStructureGraphStateGuard.Capture(original)
        };
        await owner.LinkObjectsDetailedAsync(original.ExpectedProjectAdmission!, created.Id, peer.Id, ProjectObjectLinkKind.DependsOn,
            original.Nodes.Where(node => node.Id == created.Id || node.Id == peer.Id).ToArray());
        await Assert.ThrowsAsync<ProjectStructureEditConflictException>(() => owner.DeleteObjectDetailedAsync(project.Id, created.Id,
            ProjectStructureManagedStorageDisposition.RetainManagedFiles, mutationOwner: intent));
        var current = await owner.GetStructureAsync(project.Id);
        Assert.Contains(current.Nodes, node => node.Id == created.Id);
        Assert.Contains(current.Links, link => link.SourceId == created.Id && link.TargetId == peer.Id);
    }

    [Theory]
    [InlineData(PresentationOperation.Status)]
    [InlineData(PresentationOperation.Progress)]
    [InlineData(PresentationOperation.Marker)]
    [InlineData(PresentationOperation.Priority)]
    public async Task PresentationMutation_RejectsStaleTargetAndPreservesAcceptedValue(PresentationOperation operation) {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        var (project, created) = await CreateNodeAsync(host);
        var (route, payload) = operation switch {
            PresentationOperation.Status => ("status", (object)new ProjectStructureStatusInput("Done") { ExpectedState = created.ExpectedState }),
            PresentationOperation.Progress => ("progress", new ProjectStructureProgressInput("progress", 73) { ExpectedState = created.ExpectedState }),
            PresentationOperation.Marker => ("markers", new ProjectStructureMarkerInput(MarkerIcon: "flag", MarkerTone: "danger", MarkerLabel: "Review") { ExpectedState = created.ExpectedState }),
            PresentationOperation.Priority => ("priority", new ProjectStructurePriorityInput(3) { ExpectedState = created.ExpectedState }),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        using var first = await host.Client.PostAsJsonAsync(NodeRoute(project.Id, created.Id) + "/" + route, payload);
        first.EnsureSuccessStatusCode();
        await using var scope = host.App.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var accepted = (await owner.GetStructureAsync(project.Id)).Nodes.Single(node => node.Id == created.Id);
        using var stale = await host.Client.PostAsJsonAsync(NodeRoute(project.Id, created.Id) + "/" + route, payload);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var current = (await owner.GetStructureAsync(project.Id)).Nodes.Single(node => node.Id == created.Id);
        Assert.Equal(ProjectStructureNodeStateGuard.Fingerprint(accepted), ProjectStructureNodeStateGuard.Fingerprint(current));
    }

    public enum PresentationOperation { Status, Progress, Marker, Priority }

    [Theory]
    [InlineData(GraphOperation.Delete)]
    [InlineData(GraphOperation.Copy)]
    [InlineData(GraphOperation.Reparent)]
    public async Task GraphPhantomChild_BlocksStaleBranchMutation(GraphOperation operation) {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        var (project, created) = await CreateNodeAsync(host);
        await using var scope = host.App.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var destination = await owner.CreateObjectAsync(project.Id, new(ProjectObjectType.ProjectBlock,
            "Destination", "", "", $"project:{project.Id:D}"));
        using var read = await host.Client.PostAsJsonAsync($"/api/project-structure/projects/{project.Id:D}/structure/read",
            new ProjectStructureReadRequest { IncludeMutationGraph = true });
        read.EnsureSuccessStatusCode();
        var original = (await read.Content.ReadFromJsonAsync<ProjectStructureReadResponse>(ProjectStructureHttpContractTestJson.SerializerOptions))!;
        var intent = new ProjectStructureAgentContext("human", "Human", "", "", "", "") {
            ExpectedProjectAdmission = original.ExpectedProjectAdmission, ExpectedGraph = original.ExpectedGraph
        };
        var child = await owner.CreateObjectAsync(project.Id, new(ProjectObjectType.Note, "New child", "", "", created.Id));

        await Assert.ThrowsAsync<ProjectStructureEditConflictException>(async () => {
            switch (operation) {
                case GraphOperation.Delete:
                    await owner.DeleteObjectDetailedAsync(project.Id, created.Id, ProjectStructureManagedStorageDisposition.RetainManagedFiles, mutationOwner: intent);
                    break;
                case GraphOperation.Copy:
                    await owner.CopySubtreesAsync(project.Id, [created.Id], destination.Id, mutationOwner: intent);
                    break;
                case GraphOperation.Reparent:
                    await owner.ReparentObjectAsync(project.Id, created.Id, destination.Id, mutationOwner: intent);
                    break;
            }
        });
        var current = await owner.GetStructureAsync(project.Id);
        Assert.Equal(3, current.Nodes.Count(node => !node.IsSystemManaged));
        Assert.Equal(created.ParentId, current.Nodes.Single(node => node.Id == created.Id).ParentId);
        Assert.Equal(created.Id, current.Nodes.Single(node => node.Id == child.Id).ParentId);
    }

    [Fact]
    public async Task DisjointMutation_DoesNotBlockUnrelatedBranchDeletion() {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        var (project, created) = await CreateNodeAsync(host);
        await using var scope = host.App.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var other = await owner.CreateObjectAsync(project.Id, new(ProjectObjectType.Note, "Other", "", "", $"project:{project.Id:D}"));
        var surface = await owner.GetStructureAsync(project.Id);
        var intent = new ProjectStructureAgentContext("human", "Human", "", "", "", "") {
            ExpectedProjectAdmission = surface.ExpectedProjectAdmission, ExpectedGraph = ProjectStructureGraphStateGuard.Capture(surface)
        };
        await owner.UpdateObjectAsync(project.Id, other.Id, new("New unrelated title", other.Subtitle, other.Notes,
            other.StartUtc, other.EndUtc, other.MetadataJson) { ExpectedNode = other, ExpectedProjectAdmission = surface.ExpectedProjectAdmission });
        var deletion = await owner.DeleteObjectDetailedAsync(project.Id, created.Id, ProjectStructureManagedStorageDisposition.RetainManagedFiles, mutationOwner: intent);
        Assert.Equal(1, deletion.DeletedNodeCount);
        var current = Assert.Single((await owner.GetStructureAsync(project.Id)).Nodes, node => !node.IsSystemManaged);
        Assert.Equal(other.Id, current.Id);
        Assert.Equal("New unrelated title", current.Title);
    }

    [Fact]
    public async Task MetadataReplacement_PreservesConcurrentChangeByRejectingStaleInput() {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        var (project, created) = await CreateNodeAsync(host);
        using var accepted = await host.Client.PostAsJsonAsync(NodeRoute(project.Id, created.Id) + "/metadata",
            new ProjectStructureNodeMetadataInput("{}", Notes: "Accepted notes") { ExpectedState = created.ExpectedState });
        accepted.EnsureSuccessStatusCode();
        using var stale = await host.Client.PostAsJsonAsync(NodeRoute(project.Id, created.Id) + "/metadata",
            new ProjectStructureNodeMetadataInput("{}", Notes: "Stale replacement") { ExpectedState = created.ExpectedState });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await using var scope = host.App.Services.CreateAsyncScope();
        var current = (await scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(project.Id)).Nodes.Single(node => node.Id == created.Id);
        Assert.Equal("Accepted notes", current.Notes);
    }

    public enum GraphOperation { Delete, Copy, Reparent }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task StaleWriterPair_RejectsSecondConflictingCommit(bool firstUsesHttp, bool secondUsesHttp) {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        var (project, created) = await CreateNodeAsync(host);
        await using var scope = host.App.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var surface = await owner.GetStructureAsync(project.Id);
        var original = surface.Nodes.Single(node => node.Id == created.Id);
        Assert.NotNull(created.ExpectedState);

        async Task WriteAsync(bool useHttp, string title, bool expectConflict) {
            if (useHttp) {
                using var response = await host.Client.PutAsJsonAsync(NodeRoute(project.Id, created.Id),
                    new ProjectStructureNodeEditInput(title, original.Subtitle, original.Notes) { ExpectedState = created.ExpectedState });
                Assert.Equal(expectConflict ? HttpStatusCode.Conflict : HttpStatusCode.OK, response.StatusCode);
                if (!expectConflict) {
                    var accepted = await response.Content.ReadFromJsonAsync<ProjectStructureNodeSummary>(ProjectStructureHttpContractTestJson.SerializerOptions);
                    Assert.NotNull(accepted!.ExpectedState);
                    Assert.NotEqual(created.ExpectedState, accepted.ExpectedState);
                }
                return;
            }
            var request = new ProjectObjectEditRequest(title, original.Subtitle, original.Notes,
                original.StartUtc, original.EndUtc, original.MetadataJson, original.DurationSeconds) {
                ExpectedNode = original, ExpectedProjectAdmission = surface.ExpectedProjectAdmission
            };
            if (expectConflict) {
                await Assert.ThrowsAsync<ProjectStructureEditConflictException>(() => owner.UpdateObjectAsync(project.Id, created.Id, request));
            } else {
                Assert.NotNull(await owner.UpdateObjectAsync(project.Id, created.Id, request));
            }
        }

        await WriteAsync(firstUsesHttp, "Accepted change", false);
        await WriteAsync(secondUsesHttp, "Stale replacement", true);
        var current = (await owner.GetStructureAsync(project.Id)).Nodes.Single(node => node.Id == created.Id);
        Assert.Equal("Accepted change", current.Title);
        Assert.Equal(original.Notes, current.Notes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingExpectation_RejectsBeforeMutation(bool metadataOnly) {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        var (project, created) = await CreateNodeAsync(host);
        using var response = metadataOnly
            ? await host.Client.PostAsJsonAsync(NodeRoute(project.Id, created.Id) + "/metadata", new { metadataJson = "{}", notes = "Stale notes" })
            : await host.Client.PutAsJsonAsync(NodeRoute(project.Id, created.Id), new { title = "Stale title", notes = "Stale notes" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var scope = host.App.Services.CreateAsyncScope();
        var current = (await scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(project.Id)).Nodes.Single(node => node.Id == created.Id);
        Assert.Equal(created.Title, current.Title);
        Assert.Equal(created.Notes, current.Notes);
    }

    private static string NodeRoute(Guid projectId, string nodeId) =>
        $"/api/project-structure/projects/{projectId:D}/nodes/{Uri.EscapeDataString(nodeId)}";

    private static async Task<(ProjectSummary Project, ProjectStructureNodeSummary Node)> CreateNodeAsync(ProjectStructureAgentApiTestHost host) {
        using var createProject = await host.Client.PostAsJsonAsync("/api/project-structure/projects",
            new ProjectStructureProjectSaveRequest("Concurrent edits", "", "", "Validation", ProjectStatus.Active));
        createProject.EnsureSuccessStatusCode();
        var project = (await createProject.Content.ReadFromJsonAsync<ProjectSummary>(ProjectStructureHttpContractTestJson.SerializerOptions))!;
        using var createNode = await host.Client.PostAsJsonAsync($"/api/project-structure/projects/{project.Id:D}/nodes",
            new ProjectStructureNodeCreateInput(ProjectObjectType.Note, "Original title", "", "Original notes", $"project:{project.Id:D}"));
        createNode.EnsureSuccessStatusCode();
        return (project, (await createNode.Content.ReadFromJsonAsync<ProjectStructureNodeSummary>(ProjectStructureHttpContractTestJson.SerializerOptions))!);
    }

    [Fact]
    public async Task GenericHttpEdit_WithStaleOriginalState_CannotSilentlyOverwrite() {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        using var createProject = await host.Client.PostAsJsonAsync("/api/project-structure/projects",
            new ProjectStructureProjectSaveRequest("Concurrent edits", "", "", "Validation", ProjectStatus.Active));
        createProject.EnsureSuccessStatusCode();
        var project = (await createProject.Content.ReadFromJsonAsync<ProjectSummary>(ProjectStructureHttpContractTestJson.SerializerOptions))!;
        using var createNode = await host.Client.PostAsJsonAsync($"/api/project-structure/projects/{project.Id:D}/nodes",
            new ProjectStructureNodeCreateInput(ProjectObjectType.Note, "Original title", "", "Original notes", $"project:{project.Id:D}"));
        createNode.EnsureSuccessStatusCode();
        var node = (await createNode.Content.ReadFromJsonAsync<ProjectStructureNodeSummary>(ProjectStructureHttpContractTestJson.SerializerOptions))!;
        using var read = await host.Client.PostAsJsonAsync($"/api/project-structure/projects/{project.Id:D}/structure/read",
            new ProjectStructureReadRequest(IncludeMetadata: true, IncludeNotes: true));
        read.EnsureSuccessStatusCode();
        var readJson = await read.Content.ReadFromJsonAsync<JsonElement>();
        var original = readJson.GetProperty("nodes").EnumerateArray().Single(item => item.GetProperty("id").GetString() == node.Id);
        var expectedState = original.TryGetProperty("expectedState", out var state) ? state.Clone() : (JsonElement?)null;

        await using var scope = host.App.Services.CreateAsyncScope();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var surface = await workbench.GetStructureAsync(project.Id);
        var humanOriginal = surface.Nodes.Single(item => item.Id == node.Id);
        await workbench.UpdateObjectAsync(project.Id, node.Id, new ProjectObjectEditRequest(
            "Human's newer title", humanOriginal.Subtitle, humanOriginal.Notes, humanOriginal.StartUtc, humanOriginal.EndUtc,
            humanOriginal.MetadataJson, humanOriginal.DurationSeconds) {
            ExpectedNode = humanOriginal, ExpectedProjectAdmission = surface.ExpectedProjectAdmission
        });

        using var stale = await host.Client.PutAsJsonAsync($"/api/project-structure/projects/{project.Id:D}/nodes/{Uri.EscapeDataString(node.Id)}",
            new { title = "Agent's stale title", subtitle = node.Subtitle, notes = "Agent's stale notes", expectedState });

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.True(expectedState.HasValue, "A readable node must supply its original-state precondition.");
        var current = (await workbench.GetStructureAsync(project.Id)).Nodes.Single(item => item.Id == node.Id);
        Assert.Equal("Human's newer title", current.Title);
        Assert.Equal("Original notes", current.Notes);
    }
}
