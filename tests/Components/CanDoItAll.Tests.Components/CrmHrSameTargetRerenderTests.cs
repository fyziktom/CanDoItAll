using Bunit;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

// A record workspace holds several editors over one target. A reload of the target already on screen (another editor's
// commit, a sub-selection in the route) keeps what the operator typed into the editors it did not commit, takes the
// owner's newer value of every field they did not touch, and never writes the unsaved draft. A real target change and
// closing the record still discard the drafts. The hosts run against the real services and PostgreSQL through the
// component harness; drafts are read and edited through the workspace view contracts the renderer binds to.
public sealed class CrmHrSameTargetRerenderTests
{
    [Fact]
    public async Task Workforce_saving_a_skill_keeps_the_unsaved_profile_draft_and_a_new_person_replaces_it()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var hr = harness.Context.Services.GetRequiredService<HrService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var firstId = await CreateWorkforcePersonAsync(parties, hr, "Draft workforce first", "First title");
        var secondId = await CreateWorkforcePersonAsync(parties, hr, "Draft workforce second", "Second title");
        var skill = await hr.SaveSkillDefinitionAsync(new SkillDefinitionEditorModel { Name = "Draft preservation skill", Category = "Testing" });
        Assert.True(skill.IsSuccess);
        navigation.NavigateTo($"/crm-hr/workforce?partyId={firstId:D}");
        var cut = harness.Context.Render<CrmHrWorkforcePage>();
        var view = (ICrmHrWorkforceWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal("First title", view.ProfileEditor.JobTitle));
        var profileDraft = view.ProfileEditor;
        profileDraft.JobTitle = "Typed and not saved";
        view.SkillEditor.SkillId = skill.Value;
        view.SkillEditor.YearsExperience = 3;

        await cut.InvokeAsync(view.SavePartySkillAsync);

        // The skill committed and its editor starts over; the profile editor it did not commit keeps the typed title.
        var afterSkill = await hr.GetWorkforceProfileWorkspaceAsync(firstId);
        Assert.Equal(skill.Value, Assert.Single(afterSkill!.Skills).SkillId);
        Assert.Equal("First title", afterSkill.Profile.JobTitle);
        Assert.Equal(Guid.Empty, view.SkillEditor.SkillId);
        Assert.Same(profileDraft, view.ProfileEditor);
        Assert.Equal("Typed and not saved", view.ProfileEditor.JobTitle);
        Assert.NotNull(cut.Find("[data-testid='crmhr-workforce-record-dialog']"));

        // Its own commit takes the owner's accepted values into a new draft.
        await cut.InvokeAsync(view.SaveWorkforceProfileAsync);

        var afterProfile = await hr.GetWorkforceProfileWorkspaceAsync(firstId);
        Assert.Equal("Typed and not saved", afterProfile!.Profile.JobTitle);
        Assert.NotSame(profileDraft, view.ProfileEditor);
        Assert.Equal("Typed and not saved", view.ProfileEditor.JobTitle);

        // A different person is a different target: an unsaved draft never follows the operator there.
        view.ProfileEditor.JobTitle = "Typed for the first person only";
        navigation.NavigateTo($"/crm-hr/workforce?partyId={secondId:D}");

        cut.WaitForAssertion(() => Assert.Equal("Second title", view.ProfileEditor.JobTitle));
        var firstPersisted = await hr.GetWorkforceProfileWorkspaceAsync(firstId);
        Assert.Equal("Typed and not saved", firstPersisted!.Profile.JobTitle);
    }

    [Fact]
    public async Task Recruiting_saving_an_interview_keeps_the_unsaved_application_draft_and_closing_the_record_discards_it()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var recruiting = harness.Context.Services.GetRequiredService<RecruitingService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var candidateId = await CreatePartyAsync(parties, "Draft recruiting candidate", PartyRoleKind.Candidate, PartyType.Person, PartyLifecycleStatus.Candidate);
        var application = await recruiting.SaveRecruitmentApplicationAsync(new RecruitmentApplicationEditorModel
        {
            PartyId = candidateId,
            DesiredRole = "Saved role",
            Stage = RecruitmentStage.Interviewing,
            Decision = RecruitmentDecision.Pending,
            LastChangedBy = "component-tests"
        });
        Assert.True(application.IsSuccess);
        navigation.NavigateTo($"/crm-hr/recruiting?applicationId={application.Value:D}");
        var cut = harness.Context.Render<CrmHrRecruitingPage>();
        var view = (ICrmHrRecruitingWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal("Saved role", view.ApplicationEditor.DesiredRole));
        var applicationDraft = view.ApplicationEditor;
        applicationDraft.DesiredRole = "Typed and not saved";
        view.InterviewEditor.ScheduledAtLocal = new DateTime(2031, 1, 15, 10, 0, 0, DateTimeKind.Unspecified);
        view.InterviewEditor.Feedback = "Interview feedback to commit";

        await cut.InvokeAsync(view.SaveInterviewAsync);

        var afterInterview = await recruiting.GetRecruitmentWorkspaceAsync(application.Value, null);
        Assert.Equal("Interview feedback to commit", Assert.Single(afterInterview.Interviews).Feedback);
        Assert.Equal("Saved role", afterInterview.Application.DesiredRole);
        Assert.Equal(string.Empty, view.InterviewEditor.Feedback);
        Assert.Same(applicationDraft, view.ApplicationEditor);
        Assert.Equal("Typed and not saved", view.ApplicationEditor.DesiredRole);
        Assert.NotNull(cut.Find("[data-testid='crmhr-recruiting-record-dialog']"));

        // Closing the record is the operator's own discard.
        await cut.InvokeAsync(view.CloseRecruitmentDialogAsync);

        Assert.NotSame(applicationDraft, view.ApplicationEditor);
        Assert.Equal("Saved role", view.ApplicationEditor.DesiredRole);
        var persisted = await recruiting.GetRecruitmentWorkspaceAsync(application.Value, null);
        Assert.Equal("Saved role", persisted.Application.DesiredRole);
    }

    [Fact]
    public async Task Crm_selecting_an_opportunity_and_saving_connections_keep_the_unsaved_drafts_without_reverting_the_owner()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var crm = harness.Context.Services.GetRequiredService<CrmService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var accountId = await CreateAccountAsync(parties, crm, "Draft account first", "First commercial note");
        var otherAccountId = await CreateAccountAsync(parties, crm, "Draft account second", "Second commercial note");
        var ownerId = await CreatePartyAsync(parties, "Draft opportunity owner", PartyRoleKind.AccountManager);
        var opportunity = await crm.SaveOpportunityAsync(new CrmOpportunityEditorModel
        {
            AccountPartyId = accountId,
            Title = "Draft preservation opportunity",
            Stage = OpportunityStage.Qualified,
            OpportunitySource = OpportunitySource.Direct,
            OwnerPartyId = ownerId,
            ProbabilityPercent = 40,
            LastChangedBy = "component-tests"
        });
        Assert.True(opportunity.IsSuccess);
        navigation.NavigateTo($"/crm-hr/crm?accountId={accountId:D}");
        var cut = harness.Context.Render<CrmHrCrmPage>();
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal("First commercial note", view.ProfileEditor.CommercialNotes));
        var profileDraft = view.ProfileEditor;
        profileDraft.CommercialNotes = "Typed and not saved";
        var interactionDraft = view.InteractionEditor;
        interactionDraft.Subject = "Typed subject";
        Assert.Null(interactionDraft.RelatedOpportunityId);

        // A sub-selection in the route reloads the same account.
        await cut.InvokeAsync(() => view.SelectOpportunityAsync(opportunity.Value));

        cut.WaitForAssertion(() => Assert.Equal(opportunity.Value, view.SelectedOpportunityId));
        Assert.Same(profileDraft, view.ProfileEditor);
        Assert.Equal("Typed and not saved", view.ProfileEditor.CommercialNotes);
        Assert.Same(interactionDraft, view.InteractionEditor);
        Assert.Equal("Typed subject", view.InteractionEditor.Subject);
        // The related opportunity was not chosen by the operator, so it follows the selection.
        Assert.Equal(opportunity.Value, view.InteractionEditor.RelatedOpportunityId);

        // Meanwhile the owner moved the account forward; another editor's commit reloads the same account.
        var ownerChange = await crm.SaveAccountProfileAsync(new CrmAccountProfileEditorModel
        {
            Id = profileDraft.Id,
            AccountPartyId = accountId,
            RelationshipStage = CrmAccountRelationshipStage.ActiveCustomer,
            CommercialNotes = "First commercial note",
            LastChangedBy = "component-tests"
        });
        Assert.True(ownerChange.IsSuccess);
        await cut.InvokeAsync(view.SaveConnectedRecordsAsync);

        Assert.Same(profileDraft, view.ProfileEditor);
        Assert.Equal("Typed and not saved", view.ProfileEditor.CommercialNotes);
        Assert.Equal(CrmAccountRelationshipStage.ActiveCustomer, view.ProfileEditor.RelationshipStage);
        var beforeProfileSave = await crm.GetAccountWorkspaceAsync(accountId);
        Assert.Equal("First commercial note", beforeProfileSave!.Profile.CommercialNotes);

        // Saving the kept draft writes the typed note and does not revert the stage the owner accepted meanwhile.
        await cut.InvokeAsync(view.SaveAccountProfileAsync);

        var afterProfileSave = await crm.GetAccountWorkspaceAsync(accountId);
        Assert.Equal("Typed and not saved", afterProfileSave!.Profile.CommercialNotes);
        Assert.Equal(CrmAccountRelationshipStage.ActiveCustomer, afterProfileSave.Profile.RelationshipStage);
        Assert.NotSame(profileDraft, view.ProfileEditor);

        // A different account is a different target: no draft follows the operator there.
        view.ProfileEditor.CommercialNotes = "Typed for the first account only";
        navigation.NavigateTo($"/crm-hr/crm?accountId={otherAccountId:D}");

        cut.WaitForAssertion(() => Assert.Equal("Second commercial note", view.ProfileEditor.CommercialNotes));
        Assert.NotSame(interactionDraft, view.InteractionEditor);
        Assert.Equal(string.Empty, view.InteractionEditor.Subject);
        var firstPersisted = await crm.GetAccountWorkspaceAsync(accountId);
        Assert.Equal("Typed and not saved", firstPersisted!.Profile.CommercialNotes);
    }

    private static async Task<Guid> CreateWorkforcePersonAsync(PartyDirectoryService parties, HrService hr, string displayName, string jobTitle)
    {
        var partyId = await CreatePartyAsync(parties, displayName, PartyRoleKind.Employee);
        var profile = await hr.SaveWorkforceProfileAsync(new WorkforceProfileEditorModel
        {
            PartyId = partyId,
            WorkforceKind = WorkforceKind.Employee,
            Status = "Active",
            JobTitle = jobTitle,
            CapacityHoursPerWeek = 40m,
            LastChangedBy = "component-tests"
        });
        Assert.True(profile.IsSuccess);
        return partyId;
    }

    private static async Task<Guid> CreateAccountAsync(PartyDirectoryService parties, CrmService crm, string displayName, string commercialNotes)
    {
        var accountId = await CreatePartyAsync(parties, displayName, PartyRoleKind.Customer, PartyType.Organization, PartyLifecycleStatus.Prospect);
        var profile = await crm.SaveAccountProfileAsync(new CrmAccountProfileEditorModel
        {
            AccountPartyId = accountId,
            RelationshipStage = CrmAccountRelationshipStage.Prospect,
            CommercialNotes = commercialNotes,
            LastChangedBy = "component-tests"
        });
        Assert.True(profile.IsSuccess);
        return accountId;
    }

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService parties,
        string displayName,
        PartyRoleKind roleKind,
        PartyType partyType = PartyType.Person,
        PartyLifecycleStatus lifecycleStatus = PartyLifecycleStatus.Active)
    {
        var result = await parties.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = lifecycleStatus,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "component-tests",
            Roles = [new PartyRoleAssignmentEditorModel { RoleKind = roleKind, Title = roleKind.ToString(), IsPrimary = true }]
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
