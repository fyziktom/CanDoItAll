using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkflowSettingsAdmissionApiTests {
    private const string Route = "/api/workflows/settings";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_stale_original_returns_conflict_and_preserves_current_settings(bool staleOriginal) {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        Assert.Equal(TestDatabaseProviderKind.PostgreSql, host.ActiveProfile.Provider);
        var initial = await host.Client.GetFromJsonAsync<WorkflowSettings>(Route);
        Assert.NotNull(initial);
        using var created = await host.Client.PostAsJsonAsync(Route, initial);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var original = await created.Content.ReadFromJsonAsync<WorkflowSettings>();
        Assert.NotNull(original);
        Assert.NotNull(original.UpdatedAtUtc);
        using var peer = await host.Client.PostAsJsonAsync(Route, original with {
            ArtifactPolicy = original.ArtifactPolicy with { MaxInlinePayloadCharacters = 800 }
        });
        Assert.Equal(HttpStatusCode.OK, peer.StatusCode);
        var winner = await peer.Content.ReadFromJsonAsync<WorkflowSettings>();
        Assert.NotNull(winner);
        using var refused = await host.Client.PostAsJsonAsync(Route, original with {
            UpdatedAtUtc = staleOriginal ? original.UpdatedAtUtc : null,
            ArtifactPolicy = original.ArtifactPolicy with { MaxInlinePayloadCharacters = 512 }
        });
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        var error = await refused.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("workflows.settings-conflict", Assert.Single(error.Errors).Code);
        Assert.Equal(JsonSerializer.Serialize(winner), JsonSerializer.Serialize(await host.Client.GetFromJsonAsync<WorkflowSettings>(Route)));
    }

    [Fact]
    public async Task Openapi_declares_the_original_settings_revision_and_conflict_response() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        using var document = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));
        Assert.True(document.RootElement.GetProperty("paths").GetProperty(Route).GetProperty("post")
            .GetProperty("responses").TryGetProperty("409", out _));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var original = schemas.GetProperty(nameof(WorkflowSettings)).GetProperty("properties").GetProperty("updatedAtUtc");
        Assert.Contains("create-only", original.GetProperty("description").GetString(), StringComparison.Ordinal);
        var voice = schemas.GetProperty(nameof(AgentVoiceSettings)).GetProperty("properties").GetProperty("updatedAtUtc");
        Assert.Contains("Original", voice.GetProperty("description").GetString(), StringComparison.Ordinal);
    }
}
