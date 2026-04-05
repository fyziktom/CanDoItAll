using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class StaffingAllocationIntegrationTests
{
    [Fact]
    public async Task Staffing_requests_and_project_allocations_drive_capacity_and_candidate_search()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var hrService = scope.ServiceProvider.GetRequiredService<HrService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();

        var projectId = await CreateProjectAsync(projectsService, "B07 Integration Project");
        var workerId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Lina Capacity");
        var unitId = await CreatePartyAsync(partyDirectoryService, PartyType.OrganizationUnit, "Delivery Capacity Unit");

        Assert.True((await hrService.SaveWorkforceProfileAsync(new WorkforceProfileEditorModel
        {
            PartyId = workerId,
            WorkforceKind = WorkforceKind.Employee,
            Status = "Active",
            CapacityHoursPerWeek = 40m,
            JobTitle = "Delivery Engineer",
            Discipline = "Platform",
            Seniority = "Senior",
            Location = "Remote",
            LastChangedBy = "integration-tests"
        })).IsSuccess);

        Assert.True((await hrService.SaveWorkforceProfileAsync(new WorkforceProfileEditorModel
        {
            PartyId = unitId,
            WorkforceKind = WorkforceKind.DeliveryUnit,
            Status = "Active",
            CapacityHoursPerWeek = 40m,
            LastChangedBy = "integration-tests"
        })).IsSuccess);

        var saveSkillResult = await hrService.SaveSkillDefinitionAsync(new SkillDefinitionEditorModel
        {
            Name = "Cloud Architecture",
            Category = "Engineering",
            Description = "Critical staffing skill",
            IsActive = true
        });
        Assert.True(saveSkillResult.IsSuccess);
        var skillId = saveSkillResult.Value;

        Assert.True((await hrService.SavePartySkillAsync(new PartySkillEditorModel
        {
            PartyId = workerId,
            SkillId = skillId,
            Proficiency = SkillProficiencyLevel.Expert,
            YearsExperience = 10,
            CertificationStatus = "Azure Solutions Architect"
        })).IsSuccess);

        Assert.True((await hrService.SaveStaffingRequestAsync(new StaffingRequestEditorModel
        {
            ProjectId = projectId,
            RequestedByPartyId = workerId,
            DeliveryUnitPartyId = unitId,
            Title = "Need cloud architecture coverage",
            NeededRole = "Lead architect",
            SkillIds = [skillId],
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            AllocationPercent = 60m,
            Status = StaffingRequestStatus.Open,
            Notes = "Project demand"
        })).IsSuccess);

        Assert.True((await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = workerId,
            Role = ProjectPartyAssignmentRole.TeamMember,
            AllocationPercent = 60m,
            StartsOn = DateOnly.FromDateTime(DateTime.UtcNow),
            IsPrimary = true,
            Source = "integration-tests"
        })).IsSuccess);

        Assert.True((await hrService.SaveCapacityBlockAsync(new CapacityBlockEditorModel
        {
            PartyId = workerId,
            BlockKind = CapacityBlockKind.Leave,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            Percentage = 50m,
            Notes = "Planned leave"
        })).IsSuccess);

        var candidates = await hrService.SearchStaffingCandidatesAsync(skillId, "Expert");
        Assert.Contains(candidates, item => item.PartyId == workerId);

        var requests = await hrService.ListStaffingRequestsAsync(projectId);
        var request = Assert.Single(requests);
        Assert.Contains(request.NeededSkills, item => item.Id == skillId);

        var workspace = await hrService.GetWorkforceWorkspaceAsync(workerId);
        Assert.NotNull(workspace);
        Assert.Single(workspace.Skills);
        Assert.Single(workspace.CapacityBlocks);
        Assert.Single(workspace.ProjectAllocations);
        Assert.Equal(60m, workspace.CapacitySummary.ActiveAllocationPercent);
        Assert.Equal(50m, workspace.CapacitySummary.ActiveBlockedPercent);
        Assert.True(workspace.CapacitySummary.IsOverallocated);

        var dashboard = await hrService.GetStaffingDashboardAsync();
        Assert.Equal(1, dashboard.OpenRequestCount);
        Assert.Equal(60m, dashboard.OpenDemandPercent);
        Assert.True(dashboard.OverallocatedCount >= 1);
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
