using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Activity;
using CanDoItAll.Modules.Automation;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class CrmHrCrossModuleIntegrationTests
{
    [Fact]
    public async Task Safe_crm_hr_records_surface_in_search_activity_and_automation()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var crmService = scope.ServiceProvider.GetRequiredService<CrmService>();
        var recruitingService = scope.ServiceProvider.GetRequiredService<RecruitingService>();
        var aiAgentService = scope.ServiceProvider.GetRequiredService<AiAgentService>();
        var searchIndexService = scope.ServiceProvider.GetRequiredService<ISearchIndexService>();
        var activityService = scope.ServiceProvider.GetRequiredService<ActivityService>();
        var automationWorkspaceService = scope.ServiceProvider.GetRequiredService<AutomationWorkspaceService>();

        var recruiterId = await CreatePartyAsync(
            partyDirectoryService,
            "Ava Recruiter",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Recruiter,
            "ava.recruiter@example.test");
        var managerId = await CreatePartyAsync(
            partyDirectoryService,
            "Noah Manager",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Stakeholder,
            "noah.manager@example.test");
        var candidateId = await CreatePartyAsync(
            partyDirectoryService,
            "Mila Candidate",
            PartyType.Person,
            PartyLifecycleStatus.Candidate,
            PartyRoleKind.Candidate,
            "mila.candidate@example.test");
        var unitId = await CreatePartyAsync(
            partyDirectoryService,
            "Platform Delivery",
            PartyType.OrganizationUnit,
            PartyLifecycleStatus.Active,
            PartyRoleKind.DeliveryUnit,
            "platform.delivery@example.test");
        var accountId = await CreatePartyAsync(
            partyDirectoryService,
            "Borealis Systems",
            PartyType.Organization,
            PartyLifecycleStatus.Prospect,
            PartyRoleKind.Customer,
            "crm@borealis.example");
        var agentId = await CreatePartyAsync(
            partyDirectoryService,
            "Pipeline Reviewer",
            PartyType.AiAgent,
            PartyLifecycleStatus.Active,
            PartyRoleKind.AiSteward,
            "pipeline.reviewer@example.test");

        var agentProfileResult = await aiAgentService.SaveAgentProfileAsync(new AiAgentProfileEditorModel
        {
            PartyId = agentId,
            OwnerPartyId = recruiterId,
            DefaultModel = "gpt-5.4-mini",
            ExecutionMode = AiExecutionMode.Remote,
            ValidationStatus = AiValidationStatus.ReviewRequired,
            Notes = "Screens incoming CRM-HR activity for follow-up signals.",
            LastChangedBy = "integration-tests"
        });
        Assert.True(agentProfileResult.IsSuccess);

        var applicationResult = await recruitingService.SaveRecruitmentApplicationAsync(new RecruitmentApplicationEditorModel
        {
            PartyId = candidateId,
            RecruiterPartyId = recruiterId,
            HiringManagerPartyId = managerId,
            TargetUnitPartyId = unitId,
            DesiredRole = "Senior Platform Engineer",
            Source = "Referral",
            Stage = RecruitmentStage.Interviewing,
            Notes = "Strong architecture profile.",
            LastChangedBy = "integration-tests"
        });
        Assert.True(applicationResult.IsSuccess);
        var applicationId = applicationResult.Value;

        var taskResult = await recruitingService.SaveLifecycleTaskAsync(new LifecycleTaskEditorModel
        {
            PartyId = candidateId,
            TaskKind = LifecycleTaskKind.Onboarding,
            Title = "Prepare laptop and VPN",
            OwnerPartyId = managerId,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            Status = LifecycleTaskStatus.NotStarted,
            Notes = "Provision core delivery access."
        });
        Assert.True(taskResult.IsSuccess);

        var convertResult = await recruitingService.ConvertCandidateAsync(new RecruitmentConversionEditorModel
        {
            ApplicationId = applicationId,
            WorkforceKind = WorkforceKind.Employee,
            JobTitle = "Senior Platform Engineer",
            Discipline = "Platform",
            Seniority = "Senior",
            HomeUnitPartyId = unitId,
            ManagerPartyId = managerId,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            Location = "Remote",
            TimeZone = "UTC",
            CapacityHoursPerWeek = 40m,
            Status = "Active",
            Notes = "Converted during cross-module integration proof.",
            LastChangedBy = "integration-tests"
        });
        Assert.True(convertResult.IsSuccess);

        var interactionResult = await crmService.AddInteractionAsync(
            accountId,
            new CrmInteractionEditorModel
            {
                InteractionType = InteractionType.Meeting,
                Subject = "Quarterly expansion review",
                Summary = "Confirmed next-step ownership.",
                Notes = "Follow-up needs pricing and staffing input.",
                NextActionText = "Send pricing update",
                NextActionOwnerPartyId = recruiterId,
                NextActionDueOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
                ParticipantPartyIds = [recruiterId]
            },
            "integration-tests");
        Assert.True(interactionResult.IsSuccess);

        var candidateSearchResults = await searchIndexService.SearchAsync("Mila Candidate");
        var interactionSearchResults = await searchIndexService.SearchAsync("Quarterly expansion review");
        var agentSearchResults = await searchIndexService.SearchAsync("Pipeline Reviewer");
        var activityItems = await activityService.ListRecentAsync();
        var automationSignals = await automationWorkspaceService.ListSignalsAsync();

        Assert.Contains(
            candidateSearchResults,
            item => item.Route.Contains($"/crm-hr/directory?partyId={candidateId}", StringComparison.Ordinal));
        Assert.Contains(
            candidateSearchResults,
            item => item.Route.Contains($"/crm-hr/recruiting?applicationId={applicationId}", StringComparison.Ordinal));
        Assert.Contains(
            candidateSearchResults,
            item => item.Route.Contains($"/crm-hr/workforce?partyId={candidateId}", StringComparison.Ordinal));
        Assert.Contains(
            interactionSearchResults,
            item => string.Equals(item.Title, "Quarterly expansion review", StringComparison.Ordinal));
        Assert.Contains(
            agentSearchResults,
            item => item.Route.Contains($"/crm-hr/agents?partyId={agentId}", StringComparison.Ordinal));

        Assert.Contains(
            activityItems,
            item => item.Title.Contains("Converted Mila Candidate to workforce", StringComparison.Ordinal));
        Assert.Contains(
            activityItems,
            item => item.Title.Contains("Saved AI agent profile for Pipeline Reviewer", StringComparison.Ordinal));
        Assert.Contains(
            activityItems,
            item => item.Title.Contains("Logged Meeting for Borealis Systems", StringComparison.Ordinal));

        Assert.Contains(
            automationSignals,
            item => string.Equals(item.Title, "CRM follow-ups overdue", StringComparison.Ordinal));
        Assert.Contains(
            automationSignals,
            item => string.Equals(item.Title, "Lifecycle tasks due or overdue", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Responsible_party_links_round_trip_for_resources_validation_and_test_lab()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var resourcesService = scope.ServiceProvider.GetRequiredService<ResourcesService>();
        var validationService = scope.ServiceProvider.GetRequiredService<ValidationService>();
        var testLabService = scope.ServiceProvider.GetRequiredService<TestLabService>();

        var projectId = await CreateProjectAsync(projectsService, "Cross-module ownership");
        var ownerId = await CreatePartyAsync(
            partyDirectoryService,
            "Riley Responsible",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Stakeholder,
            "riley.responsible@example.test");
        var maintainerId = await CreatePartyAsync(
            partyDirectoryService,
            "Morgan Maintainer",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Employee,
            "morgan.maintainer@example.test");

        var resourceResult = await resourcesService.SaveAsync(new ResourceEditorModel
        {
            ProjectId = projectId,
            OwnerPartyId = ownerId,
            MaintainerPartyId = maintainerId,
            ResourceKind = ResourceKind.Folder,
            Name = "Implementation notes",
            FolderPath = @"C:\repositories\CanDoItAll\docs\implementation",
            ValidationStatus = ResourceValidationStatus.Valid,
            Sensitivity = ResourceSensitivity.Normal,
            SupportsPreview = true,
            SupportsIndexing = true
        });
        Assert.True(resourceResult.IsSuccess);

        var validationResult = await validationService.RunAsync(new ValidationRunEditorModel
        {
            ProjectId = projectId,
            ResponsiblePartyId = ownerId,
            ValidationType = ValidationType.Architecture,
            ArtifactTitle = "CRM-HR bundle review",
            ArtifactRoute = "/crm-hr",
            SourceContent = "Cross-module validation source for CRM-HR resources, automation, and routing."
        });
        Assert.True(validationResult.IsSuccess);

        var testPlanResult = await testLabService.SaveAsync(new TestPlanEditorModel
        {
            ProjectId = projectId,
            ResponsiblePartyId = ownerId,
            Title = "CRM-HR cross-module proof",
            Phase = "B11",
            CoverageGoal = "Cover search, activity, responsible ownership, and automation signals."
        });
        Assert.True(testPlanResult.IsSuccess);

        var savedResource = await resourcesService.GetAsync(resourceResult.Value);
        var savedValidationRun = await validationService.GetRunAsync(validationResult.Value);
        var savedTestPlan = await testLabService.GetAsync(testPlanResult.Value);

        Assert.Equal(ownerId, savedResource.OwnerPartyId);
        Assert.Equal(maintainerId, savedResource.MaintainerPartyId);
        Assert.Equal(ownerId, savedValidationRun.ResponsiblePartyId);
        Assert.Equal(ownerId, savedTestPlan.ResponsiblePartyId);
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
        string displayName,
        PartyType partyType,
        PartyLifecycleStatus lifecycleStatus,
        PartyRoleKind roleKind,
        string email)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = lifecycleStatus,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "integration-tests",
            Roles =
            [
                new PartyRoleAssignmentEditorModel
                {
                    RoleKind = roleKind,
                    Title = roleKind.ToString(),
                    IsPrimary = true
                }
            ],
            ContactPoints =
            [
                new PartyContactPointEditorModel
                {
                    ContactType = PartyContactType.Email,
                    Label = "Primary email",
                    Value = email,
                    NormalizedValue = email.ToLowerInvariant(),
                    IsPrimary = true,
                    IsPublic = true
                }
            ]
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
