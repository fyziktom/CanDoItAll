using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class RecruitmentLifecycleIntegrationTests
{
    [Fact]
    public async Task Recruitment_application_query_pages_filters_and_never_projects_private_contacts()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var recruitingService = scope.ServiceProvider.GetRequiredService<RecruitingService>();

        var appliedPartyId = await CreatePartyAsync(
            partyDirectoryService,
            PartyType.Person,
            "Paged Applied Candidate",
            "public-applied@example.test",
            isPublic: true);
        var interviewingPartyId = await CreatePartyAsync(
            partyDirectoryService,
            PartyType.Person,
            "Paged Interview Candidate",
            "private-interview-marker@example.test",
            isPublic: false);
        var offerPartyId = await CreatePartyAsync(
            partyDirectoryService,
            PartyType.Person,
            "Paged Offer Candidate",
            "public-offer@example.test",
            isPublic: true);

        foreach (var candidate in new[]
                 {
                     (PartyId: appliedPartyId, Role: "Applied role", Stage: RecruitmentStage.Applied),
                     (PartyId: interviewingPartyId, Role: "Interview role", Stage: RecruitmentStage.Interviewing),
                     (PartyId: offerPartyId, Role: "Offer role", Stage: RecruitmentStage.Offer)
                 })
        {
            var result = await recruitingService.SaveRecruitmentApplicationAsync(
                new RecruitmentApplicationEditorModel
                {
                    PartyId = candidate.PartyId,
                    DesiredRole = candidate.Role,
                    Stage = candidate.Stage,
                    LastChangedBy = "integration-tests"
                });
            Assert.True(result.IsSuccess);
        }

        var firstPage = await recruitingService.SearchRecruitmentApplicationsAsync(
            new RecruitmentApplicationQuery(PageSize: 2));
        var secondPage = await recruitingService.SearchRecruitmentApplicationsAsync(
            new RecruitmentApplicationQuery(PageIndex: 1, PageSize: 2));

        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Single(secondPage.Items);
        Assert.Empty(firstPage.Items.Select(item => item.Id).Intersect(secondPage.Items.Select(item => item.Id)));

        var interviewing = await recruitingService.SearchRecruitmentApplicationsAsync(
            new RecruitmentApplicationQuery(
                Scope: RecruitmentApplicationScope.Interviewing,
                PageSize: 10));
        var interviewingItem = Assert.Single(interviewing.Items);
        Assert.Equal(interviewingPartyId, interviewingItem.PartyId);
        Assert.Empty(interviewingItem.PrimaryEmail);

        var privateContactSearch = await recruitingService.SearchRecruitmentApplicationsAsync(
            new RecruitmentApplicationQuery(
                "private-interview-marker",
                PageSize: 10));
        Assert.Equal(0, privateContactSearch.TotalCount);

        var publicContactSearch = await recruitingService.SearchRecruitmentApplicationsAsync(
            new RecruitmentApplicationQuery(
                "public-offer",
                PageSize: 10));
        Assert.Equal(offerPartyId, Assert.Single(publicContactSearch.Items).PartyId);

        var summary = await recruitingService.GetRecruitmentApplicationSummaryAsync();
        Assert.Equal(3, summary.TotalCount);
        Assert.Equal(1, summary.InterviewingCount);
        Assert.Equal(1, summary.OfferOrHiredCount);
    }

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
            Decision = RecruitmentDecision.Approved,
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
            TaskKind = LifecycleTaskKind.Training,
            Title = "Complete platform security training",
            OwnerPartyId = hiringManagerId,
            DueDate = new DateOnly(2026, 4, 20),
            Status = LifecycleTaskStatus.NotStarted,
            RelatedProjectId = projectId,
            Notes = "Recheck platform access practices after completion."
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
        Assert.Equal(PartyType.Person, workspace.CandidatePartyType);
        Assert.Null(workspace.CandidateTechnicalAgentId);
        Assert.Equal(AiResourceBindingStatus.Unbound, workspace.CandidateBindingStatus);
        Assert.Equal(RecruitmentStage.Hired, workspace.Application.Stage);
        Assert.Equal(RecruitmentDecision.Approved, workspace.Application.Decision);
        Assert.True(workspace.StageHistory.Count >= 2);
        Assert.Single(workspace.Interviews);
        var trainingTask = Assert.Single(workspace.LifecycleTasks);
        Assert.Equal(LifecycleTaskKind.Training, trainingTask.TaskKind);
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

    [Fact]
    public async Task Recruitment_accepts_bound_ai_candidate_but_keeps_human_oversight_roles_people_only()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var recruitingService = scope.ServiceProvider.GetRequiredService<RecruitingService>();
        var hrService = scope.ServiceProvider.GetRequiredService<HrService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var candidateId = await CreatePartyAsync(
            partyDirectoryService,
            PartyType.AiAgent,
            "Architecture Agent Candidate");
        var recruiterId = await CreatePartyAsync(
            partyDirectoryService,
            PartyType.Person,
            "Human Recruiter");
        var aiSupervisorId = await CreatePartyAsync(
            partyDirectoryService,
            PartyType.AiAgent,
            "AI Supervisor");
        var organizationId = await CreatePartyAsync(
            partyDirectoryService,
            PartyType.Organization,
            "Not A Candidate Organization");
        var technicalAgentId = Guid.NewGuid();

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Set<AiResourceBinding>().Add(new AiResourceBinding
            {
                PartyId = candidateId,
                TechnicalAgentId = technicalAgentId,
                BindingStatus = AiResourceBindingStatus.Bound,
                BindingReason = "Recruiting integration test",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        var saveResult = await recruitingService.SaveRecruitmentApplicationAsync(
            new RecruitmentApplicationEditorModel
            {
                PartyId = candidateId,
                RecruiterPartyId = recruiterId,
                DesiredRole = "Architecture assessment agent",
                Stage = RecruitmentStage.Screening,
                LastChangedBy = "integration-tests"
            });

        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));
        var workspace = await recruitingService.GetRecruitmentWorkspaceAsync(saveResult.Value);
        Assert.True(workspace.HasSelectedApplication);
        Assert.Equal(PartyType.AiAgent, workspace.CandidatePartyType);
        Assert.Equal(technicalAgentId, workspace.CandidateTechnicalAgentId);
        Assert.Equal(AiResourceBindingStatus.Bound, workspace.CandidateBindingStatus);

        var invalidOrganization = await recruitingService.SaveRecruitmentApplicationAsync(
            new RecruitmentApplicationEditorModel
            {
                PartyId = organizationId,
                DesiredRole = "Invalid organization candidate",
                LastChangedBy = "integration-tests"
            });
        Assert.Contains(invalidOrganization.Errors, error => error.Code == "crmhr.recruiting.candidate-invalid");

        var invalidRecruiter = await recruitingService.SaveRecruitmentApplicationAsync(
            new RecruitmentApplicationEditorModel
            {
                Id = saveResult.Value,
                PartyId = candidateId,
                RecruiterPartyId = aiSupervisorId,
                DesiredRole = "Architecture assessment agent",
                Stage = RecruitmentStage.Screening,
                LastChangedBy = "integration-tests"
            });
        Assert.Contains(invalidRecruiter.Errors, error => error.Code == "crmhr.recruiting.recruiter-invalid");

        var invalidManager = await recruitingService.SaveRecruitmentApplicationAsync(
            new RecruitmentApplicationEditorModel
            {
                Id = saveResult.Value,
                PartyId = candidateId,
                RecruiterPartyId = recruiterId,
                HiringManagerPartyId = aiSupervisorId,
                DesiredRole = "Architecture assessment agent",
                Stage = RecruitmentStage.Screening,
                LastChangedBy = "integration-tests"
            });
        Assert.Contains(invalidManager.Errors, error => error.Code == "crmhr.recruiting.hiring-manager-invalid");

        var invalidInterviewer = await recruitingService.SaveRecruitmentInterviewAsync(
            new RecruitmentInterviewEditorModel
            {
                ApplicationId = saveResult.Value,
                ScheduledAtLocal = new DateTime(2026, 7, 27, 10, 0, 0),
                InterviewerPartyId = aiSupervisorId
            });
        Assert.Contains(invalidInterviewer.Errors, error => error.Code == "crmhr.recruiting.interview.interviewer-invalid");

        var approvedUpdate = await recruitingService.SaveRecruitmentApplicationAsync(
            new RecruitmentApplicationEditorModel
            {
                Id = saveResult.Value,
                PartyId = candidateId,
                RecruiterPartyId = recruiterId,
                DesiredRole = "Architecture assessment agent",
                Stage = RecruitmentStage.Offer,
                Decision = RecruitmentDecision.Approved,
                LastChangedBy = "integration-tests"
            });
        Assert.True(approvedUpdate.IsSuccess);

        var blockedConversion = await recruitingService.ConvertCandidateAsync(
            new RecruitmentConversionEditorModel
            {
                ApplicationId = saveResult.Value,
                JobTitle = "Architecture assessment agent",
                LastChangedBy = "integration-tests"
            });
        Assert.Contains(
            blockedConversion.Errors,
            error => error.Code == RecruitmentConversionPolicy.AssessmentNotReadyErrorCode);
        Assert.DoesNotContain(
            await hrService.ListWorkforceProfilesAsync(),
            profile => profile.PartyId == candidateId);

        var emptyWorkspace = await recruitingService.GetRecruitmentWorkspaceAsync();
        Assert.Null(emptyWorkspace.CandidatePartyType);
        Assert.Null(emptyWorkspace.CandidateTechnicalAgentId);
        Assert.Equal(AiResourceBindingStatus.Unbound, emptyWorkspace.CandidateBindingStatus);
    }

    [Fact]
    public async Task Inline_created_human_candidate_starts_in_candidate_lifecycle()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var recruitingService = scope.ServiceProvider.GetRequiredService<RecruitingService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var saveResult = await recruitingService.SaveRecruitmentApplicationAsync(
            new RecruitmentApplicationEditorModel
            {
                CandidateName = "New Human Candidate",
                CandidateEmail = "new-candidate@example.test",
                DesiredRole = "Software engineer",
                LastChangedBy = "integration-tests"
            });

        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));
        var workspace = await recruitingService.GetRecruitmentWorkspaceAsync(saveResult.Value);
        var candidateId = Assert.IsType<Guid>(workspace.Application.PartyId);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var candidate = await dbContext.Set<Party>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == candidateId);
        Assert.Equal(PartyType.Person, candidate.PartyType);
        Assert.Equal(PartyLifecycleStatus.Candidate, candidate.LifecycleStatus);
    }

    [Fact]
    public async Task Conversion_policy_fails_before_creating_workforce_profile()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var recruitingService = scope.ServiceProvider.GetRequiredService<RecruitingService>();
        var hrService = scope.ServiceProvider.GetRequiredService<HrService>();

        var candidateId = await CreatePartyAsync(
            partyDirectoryService,
            PartyType.Person,
            "Conversion Policy Candidate",
            lifecycleStatus: PartyLifecycleStatus.Candidate);
        var applicationResult = await recruitingService.SaveRecruitmentApplicationAsync(
            new RecruitmentApplicationEditorModel
            {
                PartyId = candidateId,
                DesiredRole = "Platform engineer",
                Stage = RecruitmentStage.Offer,
                Decision = RecruitmentDecision.Pending,
                LastChangedBy = "integration-tests"
            });
        Assert.True(applicationResult.IsSuccess);

        var pendingDecisionResult = await recruitingService.ConvertCandidateAsync(
            new RecruitmentConversionEditorModel
            {
                ApplicationId = applicationResult.Value,
                JobTitle = "Platform engineer",
                LastChangedBy = "integration-tests"
            });
        Assert.Contains(
            pendingDecisionResult.Errors,
            error => error.Code == RecruitmentConversionPolicy.DecisionNotApprovedErrorCode);
        Assert.DoesNotContain(
            await hrService.ListWorkforceProfilesAsync(),
            profile => profile.PartyId == candidateId);

        var rejectResult = await recruitingService.SaveRecruitmentApplicationAsync(
            new RecruitmentApplicationEditorModel
            {
                Id = applicationResult.Value,
                PartyId = candidateId,
                DesiredRole = "Platform engineer",
                Stage = RecruitmentStage.Rejected,
                Decision = RecruitmentDecision.Approved,
                LastChangedBy = "integration-tests"
            });
        Assert.True(rejectResult.IsSuccess);

        var rejectedStageResult = await recruitingService.ConvertCandidateAsync(
            new RecruitmentConversionEditorModel
            {
                ApplicationId = applicationResult.Value,
                JobTitle = "Platform engineer",
                LastChangedBy = "integration-tests"
            });
        Assert.Contains(
            rejectedStageResult.Errors,
            error => error.Code == RecruitmentConversionPolicy.IneligibleStageErrorCode);
        Assert.DoesNotContain(
            await hrService.ListWorkforceProfilesAsync(),
            profile => profile.PartyId == candidateId);
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

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService partyDirectoryService,
        PartyType partyType,
        string displayName,
        string? email = null,
        bool isPublic = true,
        PartyLifecycleStatus lifecycleStatus = PartyLifecycleStatus.Active)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = lifecycleStatus,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            ContactPoints = string.IsNullOrWhiteSpace(email)
                ? []
                :
                [
                    new PartyContactPointEditorModel
                    {
                        ContactType = PartyContactType.Email,
                        Label = "Work",
                        Value = email,
                        IsPrimary = true,
                        IsPublic = isPublic
                    }
                ],
            LastChangedBy = "integration-tests"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
