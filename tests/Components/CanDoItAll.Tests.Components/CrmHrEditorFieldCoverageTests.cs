using Bunit;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

// The secondary fields of the CRM / HR record editors, typed into the shipped forms and read back from their owners.
//
// The opportunity's economics and attribution, the stage note an advance records, and the recruiting interview outcome,
// stage note and lifecycle task note used to be driven only by the retired evidence-capture browser scripts, which
// asserted almost nothing about them and no longer run at all. Here every one of them travels from the rendered input
// to PostgreSQL and back through its owner, so a field that stops being bound fails a test instead of disappearing
// quietly.
//
// The dialog's footer Save is bound to its form by the HTML form attribute, which a rendered
// component test does not follow, so these tests submit the form the button targets. That the
// button itself reaches it in a browser is proven by `CrmHrOpportunityJourneyTests`, which clicks
// `crmhr-opportunity-save-button` in both the create and the edit dialog.
public sealed class CrmHrEditorFieldCoverageTests
{
    [Fact]
    public async Task An_opportunity_carries_its_economics_and_attribution_from_the_form_to_its_owner()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var crm = harness.Context.Services.GetRequiredService<CrmService>();
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var suffix = Suffix();
        var accountId = await CreatePartyAsync(parties, $"Field coverage account {suffix}", PartyType.Organization, PartyRoleKind.Customer);
        var ownerId = await CreatePartyAsync(parties, $"Field coverage owner {suffix}", PartyType.Person, PartyRoleKind.AccountManager);
        var created = await crm.SaveOpportunityAsync(new CrmOpportunityEditorModel
        {
            AccountPartyId = accountId,
            Title = $"Seeded opportunity {suffix}",
            Stage = OpportunityStage.Identified,
            OpportunitySource = OpportunitySource.Direct,
            OwnerPartyId = ownerId,
            CurrencyCode = "USD",
            Amount = 1000m,
            ProbabilityPercent = 20,
            LastChangedBy = "component-tests"
        });
        Assert.True(created.IsSuccess, string.Join(" ", created.Errors.Select(error => error.Message)));
        // The edit dialog shows the whole editor, every section at once.
        var cut = RenderOpportunity(harness, accountId, created.Value);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        OpenEditDialog(cut);
        Field(cut, "crmhr-opportunity-title").Change($"Field coverage opportunity {suffix}");
        Field(cut, "crmhr-opportunity-stage").Change(OpportunityStage.Qualified.ToString());
        Field(cut, "crmhr-opportunity-source").Change(OpportunitySource.Partner.ToString());
        Field(cut, "crmhr-opportunity-currency").Change("EUR");
        Field(cut, "crmhr-opportunity-amount").Change("125000");
        Field(cut, "crmhr-opportunity-probability").Change("65");
        Field(cut, "crmhr-opportunity-close-date").Change("2027-03-31");
        Field(cut, "crmhr-opportunity-summary").Change($"Renewal and expansion {suffix}");
        Field(cut, "crmhr-opportunity-notes").Change($"Budget confirmed by the sponsor {suffix}");
        Field(cut, "crmhr-opportunity-competitor").Change($"Northbound Systems {suffix}");
        Field(cut, "crmhr-opportunity-partner-contribution").Change($"Partner delivers the integration {suffix}");

        cut.Find("#crmhr-opportunity-edit-form").Submit();
        cut.WaitForAssertion(() => Assert.False(view.IsOpportunityEditDialogOpen));

        var saved = Assert.Single(
            await crm.ListOpportunitiesAsync(),
            item => item.AccountPartyId == accountId);
        Assert.Equal(created.Value, saved.Id);
        var detail = await crm.GetOpportunityAsync(saved.Id);
        Assert.NotNull(detail);
        Assert.Equal($"Field coverage opportunity {suffix}", detail!.Title);
        Assert.Equal(OpportunityStage.Qualified, detail.Stage);
        Assert.Equal(OpportunitySource.Partner, detail.OpportunitySource);
        Assert.Equal("EUR", detail.CurrencyCode);
        Assert.Equal(125000m, detail.Amount);
        Assert.Equal(65, detail.ProbabilityPercent);
        Assert.Equal(new DateOnly(2027, 3, 31), detail.ExpectedCloseOn);
        Assert.Equal($"Renewal and expansion {suffix}", detail.Summary);
        Assert.Equal($"Budget confirmed by the sponsor {suffix}", detail.Notes);
        Assert.Equal($"Northbound Systems {suffix}", detail.CompetitorName);
        Assert.Equal($"Partner delivers the integration {suffix}", detail.PartnerContributionSummary);
        Assert.Equal(ownerId, detail.OwnerPartyId);
    }

    [Fact]
    public async Task A_lost_opportunity_records_the_reason_and_the_stage_note_of_the_change()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var crm = harness.Context.Services.GetRequiredService<CrmService>();
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var suffix = Suffix();
        var accountId = await CreatePartyAsync(parties, $"Lost reason account {suffix}", PartyType.Organization, PartyRoleKind.Customer);
        var ownerId = await CreatePartyAsync(parties, $"Lost reason owner {suffix}", PartyType.Person, PartyRoleKind.AccountManager);
        var created = await crm.SaveOpportunityAsync(new CrmOpportunityEditorModel
        {
            AccountPartyId = accountId,
            Title = $"Lost reason opportunity {suffix}",
            Stage = OpportunityStage.Negotiation,
            OpportunitySource = OpportunitySource.Direct,
            OwnerPartyId = ownerId,
            CurrencyCode = "USD",
            Amount = 5000m,
            ProbabilityPercent = 50,
            LastChangedBy = "component-tests"
        });
        Assert.True(created.IsSuccess, string.Join(" ", created.Errors.Select(error => error.Message)));
        var cut = RenderOpportunity(harness, accountId, created.Value);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        OpenEditDialog(cut);

        // A stage that ends the opportunity asks for its reason, and the change carries a note of its own.
        Field(cut, "crmhr-opportunity-stage").Change(OpportunityStage.Lost.ToString());
        Field(cut, "crmhr-opportunity-lost-reason").Change($"Budget moved to next year {suffix}");
        Field(cut, "crmhr-opportunity-stage-notes").Change($"Sponsor confirmed the deferral {suffix}");

        cut.Find("#crmhr-opportunity-edit-form").Submit();
        cut.WaitForAssertion(() => Assert.False(view.IsOpportunityEditDialogOpen));

        var detail = await crm.GetOpportunityAsync(created.Value);
        Assert.NotNull(detail);
        Assert.Equal(OpportunityStage.Lost, detail!.Stage);
        Assert.Equal($"Budget moved to next year {suffix}", detail.LostReason);
        // The stage note belongs to the stage change, not to the record: it is kept with the history entry.
        Assert.Contains(
            detail.StageHistory,
            entry => entry.Stage == OpportunityStage.Lost &&
                     entry.Notes.Contains($"Sponsor confirmed the deferral {suffix}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_recruiting_interview_and_lifecycle_task_carry_their_outcome_and_notes_to_their_owner()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var recruiting = harness.Context.Services.GetRequiredService<RecruitingService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var notifications = harness.Context.Services.GetRequiredService<CanDoItAll.Components.BaseLib.NotificationService>();
        var suffix = Suffix();
        var candidateId = await CreatePartyAsync(parties, $"Field coverage candidate {suffix}", PartyType.Person, PartyRoleKind.Candidate);
        var recruiterId = await CreatePartyAsync(parties, $"Field coverage recruiter {suffix}", PartyType.Person, PartyRoleKind.Recruiter);
        var application = await recruiting.SaveRecruitmentApplicationAsync(new RecruitmentApplicationEditorModel
        {
            PartyId = candidateId,
            DesiredRole = $"Field coverage role {suffix}",
            Stage = RecruitmentStage.Interviewing,
            Decision = RecruitmentDecision.Pending,
            LastChangedBy = "component-tests"
        });
        Assert.True(application.IsSuccess, string.Join(" ", application.Errors.Select(error => error.Message)));
        navigation.NavigateTo($"/crm-hr/recruiting?applicationId={application.Value:D}");
        var cut = harness.Context.Render<CrmHrRecruitingPage>();
        var view = (ICrmHrRecruitingWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal($"Field coverage role {suffix}", view.ApplicationEditor.DesiredRole));

        // A stage change and the note that explains it, each typed into the tab of the form that owns it.
        cut.WaitForElement("[data-testid='crmhr-recruiting-application-tab-stage']").Click();
        cut.WaitForElement("[data-testid='crmhr-recruiting-stage']").Change(RecruitmentStage.Offer.ToString());
        cut.WaitForElement("[data-testid='crmhr-recruiting-application-tab-notes']").Click();
        cut.WaitForElement("[data-testid='crmhr-recruiting-stage-notes']").Change($"Panel agreed to continue {suffix}");
        await cut.InvokeAsync(view.SaveApplicationAsync);

        // An interview with its outcome, recommendation and feedback.
        view.InterviewEditor.ScheduledAtLocal = new DateTime(2027, 2, 10, 9, 0, 0, DateTimeKind.Unspecified);
        view.InterviewEditor.Outcome = RecruitmentInterviewOutcome.StrongYes;
        view.InterviewEditor.Recommendation = $"Hire at the senior level {suffix}";
        view.InterviewEditor.Feedback = $"Strong systems design {suffix}";
        await cut.InvokeAsync(view.SaveInterviewAsync);

        // A lifecycle task with the note the operator left on it.
        view.TaskEditor.Title = $"Prepare the offer {suffix}";
        view.TaskEditor.OwnerPartyId = recruiterId;
        view.TaskEditor.Notes = $"Confirm the level with finance {suffix}";
        await cut.InvokeAsync(view.SaveTaskAsync);

        Assert.True(
            notifications.Messages.All(message => message.Severity != CanDoItAll.Components.BaseLib.NotificationSeverity.Error),
            string.Join(" | ", notifications.Messages.Select(message => $"{message.Severity}: {message.Summary} {message.Detail}")));
        var workspace = await recruiting.GetRecruitmentWorkspaceAsync(application.Value);
        Assert.Equal(RecruitmentStage.Offer, workspace.Application.Stage);
        // The stage note belongs to the stage change: it is kept with the history entry, not on the record.
        Assert.Contains(
            workspace.StageHistory,
            entry => entry.Notes.Contains($"Panel agreed to continue {suffix}", StringComparison.Ordinal));
        var interview = Assert.Single(workspace.Interviews);
        Assert.Equal(RecruitmentInterviewOutcome.StrongYes, interview.Outcome);
        Assert.Equal($"Hire at the senior level {suffix}", interview.Recommendation);
        Assert.Equal($"Strong systems design {suffix}", interview.Feedback);
        var task = Assert.Single(workspace.LifecycleTasks);
        Assert.Equal($"Prepare the offer {suffix}", task.Title);
        Assert.Equal($"Confirm the level with finance {suffix}", task.Notes);
    }

    // The real Edit control of the detail dialog, so the record dialog renders the editor the operator sees.
    private static void OpenEditDialog(IRenderedComponent<CrmHrCrmPage> cut)
    {
        cut.WaitForElement("[data-testid='crmhr-opportunity-edit-button']").Click();
        cut.WaitForElement("[data-testid='crmhr-opportunity-edit-dialog']");
    }

    private static AngleSharp.Dom.IElement Field(IRenderedComponent<CrmHrCrmPage> cut, string testId)
        => cut.WaitForElement($"[data-testid='crmhr-opportunity-edit-dialog'] [data-testid='{testId}']");

    private static IRenderedComponent<CrmHrCrmPage> RenderOpportunity(
        ComponentTestHarness harness,
        Guid accountId,
        Guid opportunityId)
    {
        harness.Context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo($"/crm-hr/crm?accountId={accountId:D}&opportunityId={opportunityId:D}");
        var cut = harness.Context.Render<CrmHrCrmPage>();
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(accountId, view.SelectedAccount?.AccountPartyId);
            Assert.Equal(opportunityId, view.SelectedOpportunityId);
            Assert.True(view.IsOpportunityDetailDialogOpen);
        });
        return cut;
    }

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService parties,
        string displayName,
        PartyType partyType,
        PartyRoleKind roleKind)
    {
        var result = await parties.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = roleKind == PartyRoleKind.Candidate
                ? PartyLifecycleStatus.Candidate
                : PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "component-tests",
            Roles = [new PartyRoleAssignmentEditorModel { RoleKind = roleKind, Title = roleKind.ToString(), IsPrimary = true }]
        });
        Assert.True(result.IsSuccess, string.Join(" ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    private static string Suffix() => Guid.NewGuid().ToString("N")[..8];
}
