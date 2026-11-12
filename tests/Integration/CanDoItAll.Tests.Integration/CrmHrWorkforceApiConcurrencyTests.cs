using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class CrmHrWorkforceApiConcurrencyTests {
    [Fact]
    public async Task Profile_HTTP_updates_require_the_original_and_a_stale_request_returns_conflict() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var party = await CreatePartyAsync(host.Client);
        await PostAsync(host.Client, "/api/crm-hr/workforce/profiles", new { partyId = party, jobTitle = "Original title" });
        var workspace = (await host.Client.GetFromJsonAsync<WorkforceWorkspaceModel>($"/api/crm-hr/workforce/{party:D}"))!;
        Assert.NotNull(workspace.Profile.Original);
        await PostAsync(host.Client, "/api/crm-hr/workforce/profiles", new {
            partyId = party, jobTitle = "Peer title", original = workspace.Profile.Original
        });

        using var stale = await host.Client.PostAsJsonAsync("/api/crm-hr/workforce/profiles", new {
            partyId = party, jobTitle = "Stale title", original = workspace.Profile.Original
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(CrmHrEditingErrorCodes.WorkforceConflict, await ErrorCodeAsync(stale));
        using var missing = await host.Client.PostAsJsonAsync("/api/crm-hr/workforce/profiles", new { partyId = party, jobTitle = "Unobserved" });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(CrmHrEditingErrorCodes.WorkforceOriginalRequired, await ErrorCodeAsync(missing));
        Assert.Equal("Peer title", (await host.Client.GetFromJsonAsync<WorkforceWorkspaceModel>($"/api/crm-hr/workforce/{party:D}"))!.Profile.JobTitle);
    }

    [Fact]
    public async Task Skill_and_capacity_HTTP_requests_carry_their_native_row_witnesses_and_cannot_overwrite_a_peer() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var party = await CreatePartyAsync(host.Client);
        var definitionId = await PostAsync(host.Client, "/api/crm-hr/workforce/skills", new { name = "Native HTTP skill" });
        var definition = Assert.Single((await host.Client.GetFromJsonAsync<SkillCatalogItemModel[]>("/api/crm-hr/workforce/skills"))!);
        await PostAsync(host.Client, "/api/crm-hr/workforce/skills", new {
            id = definitionId, name = definition.Name, description = "Peer definition", original = definition
        });
        using var definitionConflict = await host.Client.PostAsJsonAsync("/api/crm-hr/workforce/skills", new {
            id = definitionId, name = definition.Name, description = "Stale definition", original = definition
        });
        Assert.Equal(HttpStatusCode.Conflict, definitionConflict.StatusCode);
        Assert.Equal(CrmHrEditingErrorCodes.SkillConflict, await ErrorCodeAsync(definitionConflict));
        using var nameConflict = await host.Client.PostAsJsonAsync("/api/crm-hr/workforce/skills", new { name = definition.Name, description = "Unobserved" });
        Assert.Equal(HttpStatusCode.Conflict, nameConflict.StatusCode);

        var skillId = await PostAsync(host.Client, "/api/crm-hr/workforce/party-skills", new { partyId = party, skillId = definitionId, notes = "Original skill" });
        var workspace = (await host.Client.GetFromJsonAsync<WorkforceWorkspaceModel>($"/api/crm-hr/workforce/{party:D}"))!;
        var skill = Assert.Single(workspace.Skills);
        Assert.NotNull(skill.Original);
        await PostAsync(host.Client, "/api/crm-hr/workforce/party-skills", new {
            id = skillId, partyId = party, skillId = definitionId, notes = "Peer skill", original = skill.Original
        });
        using var skillConflict = await host.Client.PostAsJsonAsync("/api/crm-hr/workforce/party-skills", new {
            id = skillId, partyId = party, skillId = definitionId, notes = "Stale skill", original = skill.Original
        });
        Assert.Equal(HttpStatusCode.Conflict, skillConflict.StatusCode);
        Assert.Equal(CrmHrEditingErrorCodes.SkillConflict, await ErrorCodeAsync(skillConflict));

        var start = new DateOnly(2026, 11, 2);
        var end = start.AddDays(2);
        var capacityId = await PostAsync(host.Client, "/api/crm-hr/workforce/capacity-blocks", new {
            partyId = party, startDate = start, endDate = end, percentage = 50m, notes = "Original capacity"
        });
        workspace = (await host.Client.GetFromJsonAsync<WorkforceWorkspaceModel>($"/api/crm-hr/workforce/{party:D}"))!;
        var block = Assert.Single(workspace.CapacityBlocks);
        Assert.NotNull(block.Original);
        await PostAsync(host.Client, "/api/crm-hr/workforce/capacity-blocks", new {
            id = capacityId, partyId = party, startDate = start, endDate = end, percentage = 75m, notes = "Peer capacity", original = block.Original
        });
        using var capacityConflict = await host.Client.PostAsJsonAsync("/api/crm-hr/workforce/capacity-blocks", new {
            id = capacityId, partyId = party, startDate = start, endDate = end, percentage = 25m, notes = "Stale capacity", original = block.Original
        });
        Assert.Equal(HttpStatusCode.Conflict, capacityConflict.StatusCode);
        Assert.Equal(CrmHrEditingErrorCodes.CapacityConflict, await ErrorCodeAsync(capacityConflict));
        workspace = (await host.Client.GetFromJsonAsync<WorkforceWorkspaceModel>($"/api/crm-hr/workforce/{party:D}"))!;
        Assert.Equal("Peer skill", Assert.Single(workspace.Skills).Notes);
        Assert.Equal(75m, Assert.Single(workspace.CapacityBlocks).Percentage);
    }

    private static Task<Guid> CreatePartyAsync(HttpClient client) => PostAsync(client, "/api/crm-hr/parties", new {
        partyType = PartyType.Person, displayName = "Native workforce HTTP worker"
    });

    private static async Task<Guid> PostAsync(HttpClient client, string route, object request) {
        using var response = await client.PostAsJsonAsync(route, request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return Assert.Single(body.RootElement.GetProperty("errors").EnumerateArray()).GetProperty("code").GetString();
    }
}
