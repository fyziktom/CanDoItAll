using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class CrmHrRecruitingApiConcurrencyTests {
    [Fact]
    public async Task Application_and_conversion_HTTP_commands_require_the_shown_original_and_reject_stale_preflights() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var id = await CreateApplicationAsync(host.Client);
        var shown = await ReadAsync(host.Client, id);
        Assert.NotNull(shown.Application.Original);
        Assert.NotNull(shown.Conversion.Original);
        await PostAsync(host.Client, "/api/crm-hr/recruiting/applications", new {
            id, partyId = shown.Application.PartyId, desiredRole = "Peer accepted role",
            stage = RecruitmentStage.Offer, decision = RecruitmentDecision.Approved, original = shown.Application.Original
        });
        using var stale = await host.Client.PostAsJsonAsync("/api/crm-hr/recruiting/applications", new {
            id, partyId = shown.Application.PartyId, desiredRole = "Stale role", original = shown.Application.Original
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(RecruitmentEditingErrors.Conflict, await ErrorCodeAsync(stale));
        using var absent = await host.Client.PostAsJsonAsync("/api/crm-hr/recruiting/applications", new {
            id, partyId = shown.Application.PartyId, desiredRole = "Unobserved role"
        });
        Assert.Equal(HttpStatusCode.BadRequest, absent.StatusCode);
        Assert.Equal(RecruitmentEditingErrors.OriginalRequired, await ErrorCodeAsync(absent));
        using var staleConversion = await host.Client.PostAsJsonAsync("/api/crm-hr/recruiting/conversions", new {
            applicationId = id, jobTitle = "Stale conversion", original = shown.Conversion.Original
        });
        Assert.Equal(HttpStatusCode.Conflict, staleConversion.StatusCode);
        Assert.Equal(RecruitmentEditingErrors.ConversionConflict, await ErrorCodeAsync(staleConversion));
        var current = await ReadAsync(host.Client, id);
        Assert.False(current.HasWorkforceProfile);
        var converted = await PostAsync(host.Client, "/api/crm-hr/recruiting/conversions", new {
            applicationId = id, jobTitle = "Current conversion", original = current.Conversion.Original
        });
        Assert.Equal(current.Application.PartyId, converted);
        current = await ReadAsync(host.Client, id);
        Assert.Equal(RecruitmentStage.Hired, current.Application.Stage);
        Assert.True(current.HasWorkforceProfile);
    }

    [Fact]
    public async Task Interview_and_checklist_HTTP_updates_bind_complete_native_row_observations() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var id = await CreateApplicationAsync(host.Client);
        var shown = await ReadAsync(host.Client, id);
        var partyId = shown.Application.PartyId!.Value;
        var scheduled = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);
        var interviewId = await PostAsync(host.Client, "/api/crm-hr/recruiting/interviews", new {
            applicationId = id, scheduledAtUtc = scheduled, feedback = "Original feedback"
        });
        var due = new DateOnly(2026, 11, 6);
        var taskId = await PostAsync(host.Client, "/api/crm-hr/recruiting/lifecycle-tasks", new {
            partyId, ownerPartyId = partyId, dueDate = due, title = "Original checklist task"
        });
        shown = await ReadAsync(host.Client, id);
        var interview = Assert.Single(shown.Interviews);
        var task = Assert.Single(shown.LifecycleTasks);
        Assert.NotNull(interview.Original);
        Assert.NotNull(task.Original);
        await PostAsync(host.Client, "/api/crm-hr/recruiting/interviews", new {
            id = interviewId, applicationId = id, scheduledAtUtc = scheduled, feedback = "Accepted feedback", original = interview.Original
        });
        await PostAsync(host.Client, "/api/crm-hr/recruiting/lifecycle-tasks", new {
            id = taskId, partyId, ownerPartyId = partyId, dueDate = due, title = "Accepted checklist task", original = task.Original
        });
        using var staleInterview = await host.Client.PostAsJsonAsync("/api/crm-hr/recruiting/interviews", new {
            id = interviewId, applicationId = id, scheduledAtUtc = scheduled, feedback = "Stale feedback", original = interview.Original
        });
        using var staleTask = await host.Client.PostAsJsonAsync("/api/crm-hr/recruiting/lifecycle-tasks", new {
            id = taskId, partyId, ownerPartyId = partyId, dueDate = due, title = "Stale checklist task", original = task.Original
        });
        Assert.Equal(HttpStatusCode.Conflict, staleInterview.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, staleTask.StatusCode);
        Assert.Equal(RecruitmentEditingErrors.Conflict, await ErrorCodeAsync(staleInterview));
        Assert.Equal(RecruitmentEditingErrors.Conflict, await ErrorCodeAsync(staleTask));
        var current = await ReadAsync(host.Client, id);
        Assert.Equal("Accepted feedback", Assert.Single(current.Interviews).Feedback);
        Assert.Equal("Accepted checklist task", Assert.Single(current.LifecycleTasks).Title);
    }

    private static Task<Guid> CreateApplicationAsync(HttpClient client) => PostAsync(client, "/api/crm-hr/recruiting/applications", new {
        candidateName = "Native HTTP candidate", desiredRole = "Observed HTTP role", stage = RecruitmentStage.Offer, decision = RecruitmentDecision.Approved
    });
    private static async Task<RecruitmentWorkspaceModel> ReadAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<RecruitmentWorkspaceModel>($"/api/crm-hr/recruiting/applications/{id:D}"))!;
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
