using System.Net;
using System.Net.Http.Json;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class AiStewardProfileApiConcurrencyTests {
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Confidential_business_observations_require_both_current_CRM_scopes(bool read, bool write) {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: true);
        var scopes = new List<string>();
        if (read) {
            scopes.Add(ApiAccessScopeNames.ReadCrmHr);
        }
        if (write) {
            scopes.Add(ApiAccessScopeNames.WriteCrmHr);
        }
        var token = host.App.Services.GetRequiredService<IApiTokenService>().IssueToken(new() {
            Subject = "ai-steward-reader", Scopes = scopes
        });
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);

        using var response = await host.Client.GetAsync($"/api/crm-hr/ai-stewards/{Guid.NewGuid():D}");

        Assert.Equal(read && write ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task HTTP_business_replacement_returns_its_accepted_observation_and_rejects_stale_or_unobserved_replacements() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        using var party = await host.Client.PostAsJsonAsync("/api/crm-hr/parties", new {
            displayName = "HTTP AI stewardship", partyType = PartyType.AiAgent
        });
        Assert.True(party.IsSuccessStatusCode, await party.Content.ReadAsStringAsync());
        var partyId = await party.Content.ReadFromJsonAsync<Guid>();
        var route = $"/api/crm-hr/ai-stewards/{partyId:D}";
        var missing = (await host.Client.GetFromJsonAsync<AiStewardProfileEditorModel>(route))!;
        Assert.Null(missing.Id);
        Assert.NotNull(missing.Original);
        using var createdResponse = await host.Client.PutAsJsonAsync(route, new {
            validationStatus = AiValidationStatus.Draft, notes = "  Native HTTP stewardship  ", original = missing.Original
        });
        Assert.Equal(HttpStatusCode.OK, createdResponse.StatusCode);
        var accepted = (await createdResponse.Content.ReadFromJsonAsync<AiStewardProfileSaveReceipt>())!;
        Assert.Equal("Native HTTP stewardship", accepted.Accepted.Notes);
        var first = (await host.Client.GetFromJsonAsync<AiStewardProfileEditorModel>(route))!;
        Assert.Equal(accepted.Accepted.Original, first.Original);
        using var peer = await host.Client.PutAsJsonAsync(route, new {
            id = first.Id, validationStatus = AiValidationStatus.Approved, notes = "Accepted peer notes", original = first.Original
        });
        Assert.Equal(HttpStatusCode.OK, peer.StatusCode);
        using var stale = await host.Client.PutAsJsonAsync(route, new {
            id = first.Id, validationStatus = AiValidationStatus.Draft, notes = "Stale HTTP notes", original = first.Original
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var absent = await host.Client.PutAsJsonAsync(route, new { id = first.Id, notes = "Unobserved HTTP notes" });
        Assert.Equal(HttpStatusCode.BadRequest, absent.StatusCode);
        var current = (await host.Client.GetFromJsonAsync<AiStewardProfileEditorModel>(route))!;
        Assert.Equal(AiValidationStatus.Approved, current.ValidationStatus);
        Assert.Equal("Accepted peer notes", current.Notes);
    }
}
