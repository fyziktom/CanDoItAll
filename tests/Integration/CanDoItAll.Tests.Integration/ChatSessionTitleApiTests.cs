using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class ChatSessionTitleApiTests {
    [Theory]
    [InlineData(null, "Changed", HttpStatusCode.PreconditionRequired, "chat.title_precondition_required")]
    [InlineData("Stale", "Changed", HttpStatusCode.Conflict, "chat.title_conflict")]
    [InlineData("Original", " ", HttpStatusCode.BadRequest, "chat.title_invalid")]
    public async Task Refused_rename_leaves_the_session_unchanged(string? expected, string title, HttpStatusCode status, string code) {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, useInMemoryDatabase: true);
        await using var scope = host.App.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var agent = (await workspace.ListAgentsAsync(includeTemplates: false)).First(item => item.ProviderProfileId.HasValue);
        var original = await workspace.GetOrCreateChatSessionAsync(agent.Id, initialTitle: "Original");

        using var response = await host.Client.PostAsJsonAsync($"/api/agents/{agent.Id:D}/chat-sessions/{original.Id:D}/rename",
            new ChatSessionRenameApiRequest(title, expected));

        Assert.Equal(status, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(code, body, StringComparison.Ordinal);
        Assert.DoesNotContain("Original", body, StringComparison.Ordinal);
        var saved = await workspace.GetOrCreateChatSessionAsync(agent.Id, original.Id);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(saved));
    }

    [Fact]
    public async Task Accepted_rename_is_conditional_and_replay_does_not_replace_a_later_title() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, useInMemoryDatabase: true);
        await using var scope = host.App.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var agent = (await workspace.ListAgentsAsync(includeTemplates: false)).First(item => item.ProviderProfileId.HasValue);
        var original = await workspace.GetOrCreateChatSessionAsync(agent.Id, initialTitle: "Original");
        var route = $"/api/agents/{agent.Id:D}/chat-sessions/{original.Id:D}/rename";
        var request = new ChatSessionRenameApiRequest("  Reviewed\t title ", original.Title);

        using var accepted = await host.Client.PostAsJsonAsync(route, request);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var publicSession = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync());
        Assert.Equal("Reviewed title", publicSession.RootElement.GetProperty("title").GetString());
        Assert.False(publicSession.RootElement.TryGetProperty("compatibility", out _));
        using var refused = await host.Client.PostAsJsonAsync(route, request);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("Reviewed title", (await workspace.GetOrCreateChatSessionAsync(agent.Id, original.Id)).Title);
    }
}
