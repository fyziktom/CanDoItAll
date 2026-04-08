using Bunit;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using CanDoItAll.Modules.Projects;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class RecruitingPageTests
{
    [Fact]
    public async Task Creates_candidate_application_from_recruiting_workspace()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var recruitingService = harness.Context.Services.GetRequiredService<RecruitingService>();

        var recruiterId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Rina Recruiter");
        var hiringManagerId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Marek Hiring");
        var unitId = await CreatePartyAsync(partyDirectoryService, PartyType.OrganizationUnit, "Platform Unit");

        var cut = harness.Context.RenderComponent<CrmHrRecruitingPage>();

        cut.Find("[data-testid='crmhr-recruiting-candidate-name']").Change("Nina Candidate");
        cut.Find("[data-testid='crmhr-recruiting-candidate-email']").Change("nina.candidate@example.test");
        cut.Find("[data-testid='crmhr-recruiting-candidate-phone']").Change("+1 555 0101");
        cut.Find("[data-testid='crmhr-recruiting-candidate-summary']").Change("Platform engineer candidate");
        cut.Find("[data-testid='crmhr-recruiting-role']").Change("Platform Engineer");
        cut.Find("[data-testid='crmhr-recruiting-source']").Change("Referral");
        cut.Find("[data-testid='crmhr-recruiting-recruiter']").Change(recruiterId.ToString());
        cut.Find("[data-testid='crmhr-recruiting-hiring-manager']").Change(hiringManagerId.ToString());
        cut.Find("[data-testid='crmhr-recruiting-target-unit']").Change(unitId.ToString());
        cut.Find("[data-testid='crmhr-recruiting-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Recruitment application saved.", cut.Markup);
        });

        var applications = await recruitingService.ListRecruitmentApplicationsAsync();
        var application = Assert.Single(applications);
        Assert.Equal("Nina Candidate", application.CandidateName);
        Assert.Equal("Platform Engineer", application.DesiredRole);
        Assert.Equal(RecruitmentStage.Applied, application.Stage);
    }

    [Fact]
    public async Task Saves_stage_interview_tasks_support_roles_and_conversion_for_selected_candidate()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var recruitingService = harness.Context.Services.GetRequiredService<RecruitingService>();
        var hrService = harness.Context.Services.GetRequiredService<HrService>();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();

        var candidateId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Rico Recruit");
        var recruiterId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Zara Recruiter");
        var hiringManagerId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Elena Hiring");
        var buddyId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Boris Buddy");
        var mentorId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Mila Mentor");
        var unitId = await CreatePartyAsync(partyDirectoryService, PartyType.OrganizationUnit, "North Platform");
        var projectId = await CreateProjectAsync(projectsService, "Recruiting Proof Project");

        var saveApplicationResult = await recruitingService.SaveRecruitmentApplicationAsync(new RecruitmentApplicationEditorModel
        {
            PartyId = candidateId,
            RecruiterPartyId = recruiterId,
            HiringManagerPartyId = hiringManagerId,
            TargetUnitPartyId = unitId,
            DesiredRole = "Senior Platform Engineer",
            Source = "Sourcing",
            Stage = RecruitmentStage.Applied,
            LastChangedBy = "component-tests"
        });
        Assert.True(saveApplicationResult.IsSuccess);
        var applicationId = saveApplicationResult.Value;

        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/crm-hr/recruiting?applicationId={applicationId}");

        var cut = harness.Context.RenderComponent<CrmHrRecruitingPage>();

        cut.WaitForElement("[data-testid='crmhr-recruiting-stage']");
        cut.Find("[data-testid='crmhr-recruiting-stage']").Change(RecruitmentStage.Interviewing.ToString());
        cut.Find("[data-testid='crmhr-recruiting-stage-notes']").Change("Move candidate to active interview loop.");
        cut.Find("[data-testid='crmhr-recruiting-save-button']").Click();
        cut.WaitForAssertion(() => Assert.Contains("Recruitment application saved.", cut.Markup));

        cut.Find("[data-testid='crmhr-recruiting-interview-scheduled']").Change("2026-04-15T10:30");
        cut.Find("[data-testid='crmhr-recruiting-interviewer']").Change(hiringManagerId.ToString());
        cut.Find("[data-testid='crmhr-recruiting-interview-outcome']").Change(RecruitmentInterviewOutcome.Yes.ToString());
        cut.Find("[data-testid='crmhr-recruiting-interview-recommendation']").Change("Proceed to offer");
        cut.Find("[data-testid='crmhr-recruiting-interview-feedback']").Change("Strong systems and communication fit.");
        cut.Find("[data-testid='crmhr-recruiting-interview-save-button']").Click();
        cut.WaitForAssertion(() => Assert.Contains("Interview saved.", cut.Markup));

        cut.Find("[data-testid='crmhr-recruiting-support-manager']").Change(hiringManagerId.ToString());
        cut.Find("[data-testid='crmhr-recruiting-support-buddy']").Change(buddyId.ToString());
        cut.Find("[data-testid='crmhr-recruiting-support-mentor']").Change(mentorId.ToString());
        cut.Find("[data-testid='crmhr-recruiting-support-save-button']").Click();
        cut.WaitForAssertion(() => Assert.Contains("Support assignments saved.", cut.Markup));

        cut.Find("[data-testid='crmhr-recruiting-task-kind']").Change(LifecycleTaskKind.Onboarding.ToString());
        cut.Find("[data-testid='crmhr-recruiting-task-title']").Change("Prepare laptop and access");
        cut.Find("[data-testid='crmhr-recruiting-task-owner']").Change(hiringManagerId.ToString());
        cut.Find("[data-testid='crmhr-recruiting-task-due-date']").Change("2026-04-20");
        cut.Find("[data-testid='crmhr-recruiting-task-project']").Change(projectId.ToString());
        cut.Find("[data-testid='crmhr-recruiting-task-notes']").Change("Provision equipment and starter access.");
        cut.Find("[data-testid='crmhr-recruiting-task-save-button']").Click();
        cut.WaitForAssertion(() => Assert.Contains("Lifecycle task saved.", cut.Markup));

        cut.Find("[data-testid='crmhr-recruiting-convert-job-title']").Change("Senior Platform Engineer");
        cut.Find("[data-testid='crmhr-recruiting-convert-discipline']").Change("Platform");
        cut.Find("[data-testid='crmhr-recruiting-convert-seniority']").Change("Senior");
        cut.Find("[data-testid='crmhr-recruiting-convert-home-unit']").Change(unitId.ToString());
        cut.Find("[data-testid='crmhr-recruiting-convert-manager']").Change(hiringManagerId.ToString());
        cut.Find("[data-testid='crmhr-recruiting-convert-location']").Change("Remote");
        cut.Find("[data-testid='crmhr-recruiting-convert-timezone']").Change("Europe/Prague");
        cut.Find("[data-testid='crmhr-recruiting-convert-capacity']").Change("40");
        cut.Find("[data-testid='crmhr-recruiting-convert-save-button']").Click();
        cut.WaitForAssertion(() => Assert.Contains("Candidate converted to workforce.", cut.Markup));

        var workspace = await recruitingService.GetRecruitmentWorkspaceAsync(applicationId);
        Assert.True(workspace.HasWorkforceProfile);
        Assert.Equal(RecruitmentStage.Hired, workspace.Application.Stage);
        Assert.True(workspace.StageHistory.Count >= 2);
        Assert.Single(workspace.Interviews);
        Assert.Single(workspace.LifecycleTasks);
        Assert.Equal(hiringManagerId, workspace.SupportAssignments.ManagerPartyId);
        Assert.Equal(buddyId, workspace.SupportAssignments.BuddyPartyId);
        Assert.Equal(mentorId, workspace.SupportAssignments.MentorPartyId);

        var workforce = await hrService.GetWorkforceWorkspaceAsync(candidateId);
        Assert.NotNull(workforce);
        Assert.Equal("Senior Platform Engineer", workforce.Profile.JobTitle);
        Assert.Equal(hiringManagerId, workforce.Profile.ManagerPartyId);
        Assert.Equal(unitId, workforce.Profile.HomeUnitPartyId);
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Discovery"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<Guid> CreatePartyAsync(PartyDirectoryService partyDirectoryService, PartyType partyType, string displayName)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "component-tests"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
