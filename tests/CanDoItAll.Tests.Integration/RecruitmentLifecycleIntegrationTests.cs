using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class RecruitmentLifecycleIntegrationTests
{
    [Fact]
    public async Task Recruitment_stage_history_interviews_tasks_support_roles_and_conversion_persist_without_duplicate_party()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var recruitingService = scope.ServiceProvider.GetRequiredService<RecruitingService>();
        var hrService = scope.ServiceProvider.GetRequiredService<HrService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();

        var candidateId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Lina Candidate");
        var recruiterId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Rafi Recruiter");
        var hiringManagerId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Nora Hiring");
        var buddyId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Bela Buddy");
        var mentorId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Miro Mentor");
        var unitId = await CreatePartyAsync(partyDirectoryService, PartyType.OrganizationUnit, "Delivery North");
        var projectId = await CreateProjectAsync(projectsService, "Recruitment Integration Project");

        var partyCountBeforeConvert = (await partyDirectoryService.ListDirectoryAsync()).Count;

        var saveApplicationResult = await recruitingService.SaveRecruitmentApplicationAsync(new RecruitmentApplicationEditorModel
        {
            PartyId = candidateId,
            RecruiterPartyId = recruiterId,
            HiringManagerPartyId = hiringManagerId,
            TargetUnitPartyId = unitId,
            DesiredRole = "Senior Platform Engineer",
            Source = "Referral",
            Stage = RecruitmentStage.Applied,
            LastChangedBy = "integration-tests"
        });
        Assert.True(saveApplicationResult.IsSuccess);
        var applicationId = saveApplicationResult.Value;

        var moveStageResult = await recruitingService.SaveRecruitmentApplicationAsync(new RecruitmentApplicationEditorModel
        {
            Id = applicationId,
            PartyId = candidateId,
            RecruiterPartyId = recruiterId,
            HiringManagerPartyId = hiringManagerId,
            TargetUnitPartyId = unitId,
            DesiredRole = "Senior Platform Engineer",
            Source = "Referral",
            Stage = RecruitmentStage.Interviewing,
            StageNotes = "Technical loop started.",
            LastChangedBy = "integration-tests"
        });
        Assert.True(moveStageResult.IsSuccess);

        var saveInterviewResult = await recruitingService.SaveRecruitmentInterviewAsync(new RecruitmentInterviewEditorModel
        {
            ApplicationId = applicationId,
            ScheduledAtLocal = new DateTime(2026, 4, 15, 10, 30, 0),
            InterviewType = RecruitmentInterviewType.Technical,
            InterviewerPartyId = hiringManagerId,
            Outcome = RecruitmentInterviewOutcome.Yes,
            Recommendation = "Proceed",
            Feedback = "Strong architecture and delivery fit."
        });
        Assert.True(saveInterviewResult.IsSuccess);

        Assert.True((await recruitingService.SaveSupportAssignmentsAsync(new RecruitmentSupportAssignmentsEditorModel
        {
            PartyId = candidateId,
            ManagerPartyId = hiringManagerId,
            BuddyPartyId = buddyId,
            MentorPartyId = mentorId,
            LastChangedBy = "integration-tests"
        })).IsSuccess);

        var saveTaskResult = await recruitingService.SaveLifecycleTaskAsync(new LifecycleTaskEditorModel
        {
            PartyId = candidateId,
            TaskKind = LifecycleTaskKind.Onboarding,
            Title = "Prepare equipment and access",
            OwnerPartyId = hiringManagerId,
            DueDate = new DateOnly(2026, 4, 20),
            Status = LifecycleTaskStatus.NotStarted,
            RelatedProjectId = projectId,
            Notes = "Provision accounts, VPN, and laptop."
        });
        Assert.True(saveTaskResult.IsSuccess);

        var convertResult = await recruitingService.ConvertCandidateAsync(new RecruitmentConversionEditorModel
        {
            ApplicationId = applicationId,
            WorkforceKind = WorkforceKind.Employee,
            JobTitle = "Senior Platform Engineer",
            Discipline = "Platform",
            Seniority = "Senior",
            HomeUnitPartyId = unitId,
            ManagerPartyId = hiringManagerId,
            StartDate = new DateOnly(2026, 5, 1),
            Location = "Remote",
            TimeZone = "Europe/Prague",
            CapacityHoursPerWeek = 40m,
            Status = "Active",
            Notes = "Converted from recruiting handoff.",
            LastChangedBy = "integration-tests"
        });
        Assert.True(convertResult.IsSuccess);
        Assert.Equal(candidateId, convertResult.Value);

        var partyCountAfterConvert = (await partyDirectoryService.ListDirectoryAsync()).Count;
        Assert.Equal(partyCountBeforeConvert, partyCountAfterConvert);

        var workspace = await recruitingService.GetRecruitmentWorkspaceAsync(applicationId);
        Assert.True(workspace.HasSelectedApplication);
        Assert.True(workspace.HasWorkforceProfile);
        Assert.Equal(RecruitmentStage.Hired, workspace.Application.Stage);
        Assert.Equal(RecruitmentDecision.Approved, workspace.Application.Decision);
        Assert.True(workspace.StageHistory.Count >= 2);
        Assert.Single(workspace.Interviews);
        Assert.Single(workspace.LifecycleTasks);
        Assert.Equal(hiringManagerId, workspace.SupportAssignments.ManagerPartyId);
        Assert.Equal(buddyId, workspace.SupportAssignments.BuddyPartyId);
        Assert.Equal(mentorId, workspace.SupportAssignments.MentorPartyId);

        var workforce = await hrService.GetWorkforceWorkspaceAsync(candidateId);
        Assert.NotNull(workforce);
        Assert.Equal("Senior Platform Engineer", workforce.Profile.JobTitle);
        Assert.Equal("Platform", workforce.Profile.Discipline);
        Assert.Equal("Senior", workforce.Profile.Seniority);
        Assert.Equal(hiringManagerId, workforce.Profile.ManagerPartyId);
        Assert.Equal(unitId, workforce.Profile.HomeUnitPartyId);

        var profiles = await hrService.ListWorkforceProfilesAsync();
        Assert.Single(profiles, item => item.PartyId == candidateId);
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
            LastChangedBy = "integration-tests"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
