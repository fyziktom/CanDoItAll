using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed class ProjectStructureImportIdentityRawJsonTests(ProjectStructureImportIdentityRawJsonTests.HostFixture fixture)
    : IClassFixture<ProjectStructureImportIdentityRawJsonTests.HostFixture> {
    [Fact]
    public async Task Imported_source_keys_survive_duplicate_display_names_without_polluting_notes() {
        var projectId = await CreateProject();
        const string outline = """
            [{"sourceKey":"phase","title":"Pre-arrival","notes":"Review the stay.","children":[
              {"sourceKey":"check-a","title":"Check","notes":"Confirm arrival."},
              {"sourceKey":"check-b","title":"Check","notes":"Confirm room."}]}]
            """;
        using var response = await Import(projectId, outline);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonObject>();
        var container = result!["containerNodeId"]!.GetValue<string>();
        var nodes = await ReadNodes(projectId);
        var imported = nodes.Select(node => (Node: node, Metadata: JsonNode.Parse(node["metadataJson"]?.GetValue<string>() ?? "{}")!))
            .Where(item => item.Metadata["importSource"] is not null).ToArray();
        Assert.Equal(3, imported.Length);
        var expectedNotes = new Dictionary<string, string> { ["phase"] = "Review the stay.", ["check-a"] = "Confirm arrival.", ["check-b"] = "Confirm room." };
        foreach (var (node, metadata) in imported) {
            var source = metadata["importSource"]!;
            var key = source["sourceKey"]!.GetValue<string>();
            Assert.Equal(container, source["containerNodeId"]!.GetValue<string>());
            Assert.Equal(3, source["sourceKind"]!.GetValue<int>());
            Assert.Equal(expectedNotes[key], node["notes"]!.GetValue<string>());
            if (key != "phase") {
                Assert.Equal("task", metadata["workItem"]!["workItemKind"]!.GetValue<string>());
                Assert.Equal(expectedNotes[key], metadata["workItem"]!["description"]!.GetValue<string>());
            }
        }
        Assert.Equal(2, imported.Count(item => item.Node["title"]!.GetValue<string>() == "Check"));
    }

    [Theory]
    [InlineData("[{\"title\":\"A\",\"sourceKey\":null}]", "InvalidImportSourceKey")]
    [InlineData("[{\"title\":\"A\",\"sourceKey\":42}]", "InvalidImportSourceKey")]
    [InlineData("[{\"title\":\"A\"}]", "ImportSourceKeyRequired")]
    [InlineData("[{\"title\":\"A\",\"sourceKey\":\"a b\"}]", "InvalidImportSourceKey")]
    [InlineData("[{\"title\":\"A\",\"sourceKey\":\"same\",\"children\":[{\"title\":\"B\",\"sourceKey\":\"same\"}]}]", "DuplicateImportSourceKey")]
    [InlineData("[{\"title\":\"A\",\"sourceKey\":\"same\"},{\"title\":\"B\",\"sourceKey\":\"same\"}]", "DuplicateImportSourceKey")]
    public async Task Invalid_or_duplicate_keys_fail_before_creating_any_import_nodes(string outline, string errorCode) {
        var projectId = await CreateProject();
        var before = (await ReadNodes(projectId)).Select(node => node["id"]!.GetValue<string>()).Order().ToArray();
        using var response = await Import(projectId, outline);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal(errorCode, error!["error"]!["errorCode"]!.GetValue<string>());
        var after = (await ReadNodes(projectId)).Select(node => node["id"]!.GetValue<string>()).Order().ToArray();
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Required_source_keys_reject_other_import_formats_before_writes() {
        var projectId = await CreateProject();
        var before = (await ReadNodes(projectId)).Select(node => node["id"]!.GetValue<string>()).Order().ToArray();
        using var response = await fixture.Host.Client.PostAsJsonAsync("/api/project-structure/imports", new {
            projectId, sourceKind = 0, title = "Synthetic unsupported identity mode", sourceText = "mindmap\n  root\n    child", requireSourceKeys = true
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("ImportSourceKeysUnsupported", error!["error"]!["errorCode"]!.GetValue<string>());
        Assert.Equal(before, (await ReadNodes(projectId)).Select(node => node["id"]!.GetValue<string>()).Order().ToArray());
    }

    private async Task<Guid> CreateProject() {
        using var response = await fixture.Host.Client.PostAsJsonAsync("/api/projects", new {
            name = "Synthetic import identity contract", description = "Only invented outline data.", objective = "Verify source identity.", currentPhase = "Test", status = 1
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private Task<HttpResponseMessage> Import(Guid projectId, string sourceText) => fixture.Host.Client.PostAsJsonAsync("/api/project-structure/imports", new {
        projectId, parentNodeKey = $"project:{projectId:D}", sourceKind = 3, title = "Synthetic imported outline", sourceText, requireSourceKeys = true
    });

    private async Task<JsonObject[]> ReadNodes(Guid projectId) {
        using var response = await fixture.Host.Client.PostAsJsonAsync($"/api/project-structure/projects/{projectId:D}/structure/read", new {
            includeMetadata = true, includeNotes = true, source = 2
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var structure = await response.Content.ReadFromJsonAsync<JsonObject>();
        return structure!["nodes"]!.AsArray().OfType<JsonObject>().ToArray();
    }

    public sealed class HostFixture : IAsyncLifetime {
        internal ProjectStructureAgentApiTestHost Host { get; private set; } = null!;

        public async Task InitializeAsync() => Host = await ProjectStructureAgentApiTestHost.CreateAsync(
            "project-structure-import-identity", environment => environment.CreatePostgreSqlProfile("import-identity"));

        public async Task DisposeAsync() => await Host.DisposeAsync();
    }
}
