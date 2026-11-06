using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Web.Api;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class AgentConfigurationApiConcurrencyTests {
    [Fact]
    public async Task Agent_HTTP_replacement_and_deletion_require_the_reviewed_original_and_return_typed_conflicts() {
        await using var host = await ApiTestHost.CreateAsync(false, useInMemoryDatabase: true);
        var id = await CreateAgentAsync(host.Client, "HTTP original agent");
        var original = await host.Client.GetFromJsonAsync<AgentEditorModel>($"/api/agents/{id}") ?? throw new InvalidOperationException("Missing original agent.");
        var stale = AgentEditorModelSnapshot.Copy(original);
        original.Name = "HTTP accepted peer name";
        using var accepted = await host.Client.PostAsJsonAsync("/api/agents/", original);
        accepted.EnsureSuccessStatusCode();
        var expected = stale.ExpectedUpdatedAtUtc;
        stale.ExpectedUpdatedAtUtc = null;
        using var missing = await host.Client.PostAsJsonAsync("/api/agents/", stale);
        await AssertConflictAsync(missing, "agents.configuration-conflict");
        stale.ExpectedUpdatedAtUtc = expected;
        using var changed = await host.Client.PostAsJsonAsync("/api/agents/", stale);
        await AssertConflictAsync(changed, "agents.configuration-conflict");
        using var tokenlessDelete = await host.Client.DeleteAsync($"/api/agents/{id}");
        await AssertConflictAsync(tokenlessDelete, "agents.configuration-conflict");
        using var staleDelete = await host.Client.DeleteAsync($"/api/agents/{id}?expectedUpdatedAtUtc={Stamp(expected!.Value)}");
        await AssertConflictAsync(staleDelete, "agents.configuration-conflict");
        var current = await host.Client.GetFromJsonAsync<AgentEditorModel>($"/api/agents/{id}") ?? throw new InvalidOperationException("Missing current agent.");
        Assert.Equal("HTTP accepted peer name", current.Name);
        using var delete = await host.Client.DeleteAsync($"/api/agents/{id}?expectedUpdatedAtUtc={Stamp(current.ExpectedUpdatedAtUtc!.Value)}");
        delete.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Team_HTTP_replacement_member_set_and_deletion_refuse_missing_or_stale_originals() {
        await using var host = await ApiTestHost.CreateAsync(false, useInMemoryDatabase: true);
        using var invalidIdentity = await host.Client.PostAsJsonAsync("/api/agents/teams", new AgentTeamEditorModel { Id = Guid.Empty, Name = "Invalid empty team identity" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidIdentity.StatusCode);
        var first = await CreateAgentAsync(host.Client, "HTTP team member one");
        var second = await CreateAgentAsync(host.Client, "HTTP team member two");
        using var created = await host.Client.PostAsJsonAsync("/api/agents/teams", new AgentTeamEditorModel { Name = "HTTP original team", AgentIds = [first] });
        created.EnsureSuccessStatusCode();
        var id = await created.Content.ReadFromJsonAsync<Guid>();
        var original = await host.Client.GetFromJsonAsync<AgentTeamEditorModel>($"/api/agents/teams/{id}/editor") ?? throw new InvalidOperationException("Missing original team.");
        var expected = original.ExpectedUpdatedAtUtc!.Value;
        original.Name = "HTTP peer team";
        original.AgentIds = [first, second];
        using var peer = await host.Client.PutAsJsonAsync($"/api/agents/teams/{id}", original);
        peer.EnsureSuccessStatusCode();
        var stale = new AgentTeamEditorModel { Id = id, Name = "Stale team", AgentIds = [first] };
        using var tokenless = await host.Client.PostAsJsonAsync("/api/agents/teams", stale);
        await AssertConflictAsync(tokenless, "agents.team-configuration-conflict");
        stale.ExpectedUpdatedAtUtc = expected;
        using var staleReplacement = await host.Client.PutAsJsonAsync($"/api/agents/teams/{id}", stale);
        await AssertConflictAsync(staleReplacement, "agents.team-configuration-conflict");
        using var staleMembers = await host.Client.PostAsJsonAsync($"/api/agents/teams/{id}/members", new AgentTeamMembersApiRequest([first], expected));
        await AssertConflictAsync(staleMembers, "agents.team-configuration-conflict");
        using var tokenlessMembers = await host.Client.PutAsJsonAsync($"/api/agents/teams/{id}/members", new AgentTeamMembersApiRequest([first]));
        await AssertConflictAsync(tokenlessMembers, "agents.team-configuration-conflict");
        var current = await host.Client.GetFromJsonAsync<AgentTeamDefinition>($"/api/agents/teams/{id}") ?? throw new InvalidOperationException("Missing current team.");
        Assert.Equal("HTTP peer team", current.Name);
        Assert.Equal(2, current.AgentIds.Count);
        using var acceptedMembers = await host.Client.PutAsJsonAsync($"/api/agents/teams/{id}/members", new AgentTeamMembersApiRequest([second], current.UpdatedAtUtc));
        acceptedMembers.EnsureSuccessStatusCode();
        var accepted = await acceptedMembers.Content.ReadFromJsonAsync<AgentTeamDefinition>() ?? throw new InvalidOperationException("Missing accepted team.");
        Assert.Equal("HTTP peer team", accepted.Name);
        Assert.Equal(new[] { second }, accepted.AgentIds.ToArray());
        using var tokenlessDelete = await host.Client.DeleteAsync($"/api/agents/teams/{id}");
        await AssertConflictAsync(tokenlessDelete, "agents.team-configuration-conflict");
        using var staleDelete = await host.Client.DeleteAsync($"/api/agents/teams/{id}?expectedUpdatedAtUtc={Stamp(expected)}");
        await AssertConflictAsync(staleDelete, "agents.team-configuration-conflict");
        using var delete = await host.Client.DeleteAsync($"/api/agents/teams/{id}?expectedUpdatedAtUtc={Stamp(accepted.UpdatedAtUtc)}");
        delete.EnsureSuccessStatusCode();
    }

    private static async Task<Guid> CreateAgentAsync(HttpClient client, string name) {
        using var response = await client.PostAsJsonAsync("/api/agents/", new AgentEditorModel { Name = name });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task AssertConflictAsync(HttpResponseMessage response, string code) {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal(code, Assert.Single(error!.Errors).Code);
    }

    private static string Stamp(DateTimeOffset value) => Uri.EscapeDataString(value.ToString("O", CultureInfo.InvariantCulture));
}
