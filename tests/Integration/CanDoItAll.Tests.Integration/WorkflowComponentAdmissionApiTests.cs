using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkflowComponentAdmissionApiTests {
    private const string Route = "/api/workflows/components";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Replacement_and_delete_require_the_original_and_preserve_the_peer_component(bool stale) {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var request = WorkflowComponentConcurrencyPersistenceTests.CreateRequest();
        using var created = await host.Client.PostAsJsonAsync(Route, request);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var original = (await created.Content.ReadFromJsonAsync<LlmCallComponent>())!;
        using var updated = await host.Client.PostAsJsonAsync(Route, request with { Id = original.Id, ExpectedRevision = original.Revision, Instructions = "Peer HTTP content" });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var winner = (await updated.Content.ReadFromJsonAsync<LlmCallComponent>())!;
        using var refused = await host.Client.PostAsJsonAsync(Route, request with { Id = original.Id, ExpectedRevision = stale ? original.Revision : null });
        await AssertConflictAsync(refused);
        var itemRoute = $"{Route}/{original.Id.Value:D}";
        using var delete = await host.Client.DeleteAsync(stale ? $"{itemRoute}?expectedRevision={original.Revision:D}" : itemRoute);
        await AssertConflictAsync(delete);
        var current = await host.Client.GetFromJsonAsync<LlmCallComponent>(itemRoute);
        Assert.Equal(JsonSerializer.Serialize(winner), JsonSerializer.Serialize(current));
        using var accepted = await host.Client.DeleteAsync($"{itemRoute}?expectedRevision={winner.Revision:D}");
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var missing = await host.Client.GetAsync(itemRoute);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Openapi_declares_required_original_and_conflict_responses() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        using var document = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.GetProperty(Route).GetProperty("post").GetProperty("responses").TryGetProperty("409", out _));
        var deletion = paths.GetProperty(Route + "/{componentId}").GetProperty("delete");
        Assert.True(deletion.GetProperty("responses").TryGetProperty("409", out _));
        var original = deletion.GetProperty("parameters").EnumerateArray().Single(item => item.GetProperty("name").GetString() == "expectedRevision");
        Assert.Contains("original", original.GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
        var property = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(nameof(LlmCallComponentSaveRequest))
            .GetProperty("properties").GetProperty("expectedRevision");
        Assert.Contains("create-only", property.GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AssertConflictAsync(HttpResponseMessage response) {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<ApiErrorResponse>())!;
        Assert.Equal("workflows.component-conflict", Assert.Single(error.Errors).Code);
    }
}
