using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class ProjectStructureStaleEditHttpTests {
    public enum CommandOriginal { Current, Missing, PeerTitle, PeerBinding }

    [Theory]
    [InlineData(CommandOriginal.Current)]
    [InlineData(CommandOriginal.Missing)]
    [InlineData(CommandOriginal.PeerTitle)]
    [InlineData(CommandOriginal.PeerBinding)]
    public async Task Materializing_HTTP_command_requires_unchanged_original_node_and_preserves_peer_binding(CommandOriginal original) {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        var (project, created) = await CreateNodeAsync(host);
        await using var scope = host.App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using (var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync()) {
            var unbound = await database.Set<ProjectObjectRecord>().SingleAsync(node => node.ProjectId == project.Id && node.NodeKey == created.Id);
            unbound.ObjectType = ProjectObjectType.PromptFlow;
            await database.SaveChangesAsync();
        }
        using var read = await host.Client.PostAsJsonAsync($"/api/project-structure/projects/{project.Id:D}/structure/read",
            new ProjectStructureReadRequest(NodeIds: [created.Id], IncludeNotes: true, IncludeMetadata: true), ProjectStructureHttpContractTestJson.SerializerOptions);
        read.EnsureSuccessStatusCode();
        var displayed = Assert.IsType<ProjectStructureReadResponse>(await read.Content.ReadFromJsonAsync<ProjectStructureReadResponse>(ProjectStructureHttpContractTestJson.SerializerOptions));
        var state = Assert.IsType<ProjectStructureNodeState>(Assert.Single(displayed.Nodes).ExpectedState);
        var request = new ProjectStructureNodeCommandInput(ProjectStructureCommandKind.Open) {
            ExpectedState = original == CommandOriginal.Missing ? null : state
        };
        var path = NodeRoute(project.Id, created.Id) + "/command";
        if (original == CommandOriginal.PeerTitle) {
            await using var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
            var peer = await database.Set<ProjectObjectRecord>().SingleAsync(node => node.ProjectId == project.Id && node.NodeKey == created.Id);
            peer.Title = "HTTP peer title";
            await database.SaveChangesAsync();
        } else if (original == CommandOriginal.PeerBinding) {
            using var peer = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(request) };
            peer.Headers.Add(ProjectStructureAgentHttpHeaders.SessionId, Guid.NewGuid().ToString("N"));
            using var accepted = await host.Client.SendAsync(peer);
            accepted.EnsureSuccessStatusCode();
        }
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var before = Assert.Single((await workbench.GetStructureAsync(project.Id)).Nodes, node => node.Id == created.Id);
        using var response = await host.Client.PostAsJsonAsync(path, request);
        Assert.Equal(original == CommandOriginal.Current ? HttpStatusCode.OK : HttpStatusCode.Conflict, response.StatusCode);
        var after = Assert.Single((await workbench.GetStructureAsync(project.Id)).Nodes, node => node.Id == created.Id);
        if (original == CommandOriginal.Current) {
            var artifact = Assert.IsType<ArtifactReference>(await response.Content.ReadFromJsonAsync<ArtifactReference>());
            Assert.Equal(after.ArtifactId, artifact.EntityId);
            Assert.Equal(created.Title, artifact.Title);
        } else {
            Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
        }
        await using var prompts = await services.GetRequiredService<IDbContextFactory<PromptsDbContext>>().CreateDbContextAsync();
        Assert.Equal(original is CommandOriginal.Current or CommandOriginal.PeerBinding ? 1 : 0,
            await prompts.Set<PromptArtifact>().CountAsync(prompt => prompt.ProjectId == project.Id));
    }

    [Fact]
    public async Task Readonly_HTTP_test_command_keeps_its_native_authority_without_a_content_write_witness() {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        var (project, created) = await CreateNodeAsync(host);
        using var response = await host.Client.PostAsJsonAsync(NodeRoute(project.Id, created.Id) + "/command",
            new ProjectStructureNodeCommandInput(ProjectStructureCommandKind.Test));
        response.EnsureSuccessStatusCode();
        var artifact = Assert.IsType<ArtifactReference>(await response.Content.ReadFromJsonAsync<ArtifactReference>());
        Assert.Contains("/test-lab?projectId=", artifact.Route, StringComparison.Ordinal);
    }
}
