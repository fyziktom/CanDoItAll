using System.Text.Json;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class StaffingAllocationIntegrationTests
{
    [Fact]
    public async Task Home_agent_count_uses_only_bound_agent_framework_projections()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var homeQueryService = scope.ServiceProvider.GetRequiredService<ICrmHrHomeQueryService>();
        var now = DateTimeOffset.UtcNow;
        var boundParty = CreateAiAgentParty("Bound projected agent", now);
        var pendingParty = CreateAiAgentParty("Pending projected agent", now);
        var legacyOnlyParty = CreateAiAgentParty("Legacy-only agent", now);

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.AddRange(boundParty, pendingParty, legacyOnlyParty);
            dbContext.AddRange(
                new AiResourceBinding
                {
                    PartyId = boundParty.Id,
                    TechnicalAgentId = Guid.NewGuid(),
                    BindingStatus = AiResourceBindingStatus.Bound,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                },
                new AiResourceBinding
                {
                    PartyId = pendingParty.Id,
                    TechnicalAgentId = Guid.NewGuid(),
                    BindingStatus = AiResourceBindingStatus.PendingBackfill,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
            dbContext.Add(new AiAgentProfile
            {
                PartyId = legacyOnlyParty.Id,
                DefaultModel = "legacy-model"
            });
            await dbContext.SaveChangesAsync();
        }

        var homeSnapshot = await homeQueryService.GetAsync();

        Assert.Equal(1, homeSnapshot.AgentProjectionCount);
    }

    [Fact]
    public async Task Staffing_and_home_queries_enforce_server_page_bounds()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var hrService = scope.ServiceProvider.GetRequiredService<HrService>();
        var homeQueryService = scope.ServiceProvider.GetRequiredService<ICrmHrHomeQueryService>();
        var projectId = await CreateProjectAsync(projectsService, "Bounded staffing project");
        var now = DateTimeOffset.UtcNow;
        var parties = Enumerable.Range(0, 9)
            .Select(index => new Party
            {
                PartyType = PartyType.Person,
                LifecycleStatus = PartyLifecycleStatus.Active,
                DisplayName = $"Bounded worker {index:D2}",
                Summary = $"Bounded worker {index:D2} summary",
                IsSensitive = index < 5,
                LastChangedBy = "integration-tests",
                CreatedAtUtc = now.AddMinutes(-index),
                UpdatedAtUtc = now.AddMinutes(-index)
            })
            .ToList();
        var profiles = parties
            .Select((party, index) => new WorkforceProfile
            {
                PartyId = party.Id,
                WorkforceKind = WorkforceKind.Employee,
                JobTitle = $"Engineer {index:D2}",
                Discipline = "Platform",
                Seniority = "Senior",
                Location = "Remote",
                Status = "Active",
                CapacityHoursPerWeek = 40m,
                RateCurrencyCode = "USD"
            })
            .ToList();
        var requests = Enumerable.Range(0, 8)
            .Select(index => new StaffingRequest
            {
                ProjectId = projectId,
                RequestedByPartyId = parties[index].Id,
                Title = $"Bounded request {index:D2}",
                NeededRole = "Engineer",
                AllocationPercent = 25m,
                Status = StaffingRequestStatus.Open,
                Notes = $"Request note {index:D2}"
            })
            .ToList();
        var rareSkill = new SkillDefinition
        {
            Name = "Rare bounded skill",
            Category = "Integration",
            Description = "Verifies server-side staffing request skill filtering.",
            IsActive = true
        };
        requests[^1].NeededSkillsJson = JsonSerializer.Serialize(new[] { rareSkill.Id });
        var opportunities = Enumerable.Range(0, 8)
            .Select(index => new Opportunity
            {
                Title = $"Bounded opportunity {index:D2}",
                Stage = OpportunityStage.Identified,
                AccountPartyId = parties[0].Id,
                OwnerPartyId = parties[1].Id,
                OpportunitySource = OpportunitySource.Direct,
                CurrencyCode = "USD",
                ProbabilityPercent = 25,
                CreatedAtUtc = now.AddDays(-index),
                UpdatedAtUtc = now.AddDays(-index)
            })
            .ToList();

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.AddRange(parties);
            dbContext.AddRange(profiles);
            dbContext.AddRange(requests);
            dbContext.AddRange(opportunities);
            dbContext.Add(rareSkill);
            await dbContext.SaveChangesAsync();
        }

        var firstCandidatePage = await hrService.SearchStaffingCandidatesAsync(
            new StaffingCandidateQuery(PageSize: 3));
        var secondCandidatePage = await hrService.SearchStaffingCandidatesAsync(
            new StaffingCandidateQuery(PageIndex: 1, PageSize: 3));
        Assert.Equal(9, firstCandidatePage.TotalCount);
        Assert.Equal(3, firstCandidatePage.Items.Count);
        Assert.Equal(3, secondCandidatePage.Items.Count);
        Assert.Empty(firstCandidatePage.Items
            .Select(item => item.PartyId)
            .Intersect(secondCandidatePage.Items.Select(item => item.PartyId)));

        var requestPage = await hrService.SearchStaffingRequestsAsync(
            new StaffingRequestQuery(projectId, PageSize: 2));
        Assert.Equal(8, requestPage.TotalCount);
        Assert.Equal(2, requestPage.Items.Count);
        Assert.Equal("Bounded request 00", requestPage.Items[0].Title);
        var filteredRequestPage = await hrService.SearchStaffingRequestsAsync(
            new StaffingRequestQuery(projectId, "request 07", PageSize: 2));
        Assert.Equal(1, filteredRequestPage.TotalCount);
        Assert.Equal("Bounded request 07", Assert.Single(filteredRequestPage.Items).Title);
        var skillFilteredRequestPage = await hrService.SearchStaffingRequestsAsync(
            new StaffingRequestQuery(projectId, "rare bounded skill", PageSize: 2));
        Assert.Equal(1, skillFilteredRequestPage.TotalCount);
        Assert.Equal("Bounded request 07", Assert.Single(skillFilteredRequestPage.Items).Title);

        var homeSnapshot = await homeQueryService.GetAsync();
        Assert.Equal(9, homeSnapshot.PartyCount);
        Assert.Equal(8, homeSnapshot.OpportunityCount);
        Assert.Equal(9, homeSnapshot.WorkforceProfileCount);
        Assert.Equal(CrmHrHomeQueryLimits.DirectoryPreviewSize, homeSnapshot.DirectoryPreview.Count);
        Assert.Equal(CrmHrHomeQueryLimits.SensitivePreviewSize, homeSnapshot.SensitivePreview.Count);
        Assert.Equal(CrmHrHomeQueryLimits.OpenPipelinePreviewSize, homeSnapshot.OpenPipelinePreview.Count);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            hrService.SearchStaffingCandidatesAsync(
                new StaffingCandidateQuery(
                    PageSize: StaffingQueryLimits.MaximumPageSize + 1)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            hrService.SearchStaffingRequestsAsync(
                new StaffingRequestQuery(
                    projectId,
                    PageSize: StaffingQueryLimits.MaximumPageSize + 1)));
    }

    private static Party CreateAiAgentParty(string displayName, DateTimeOffset now)
    {
        return new Party
        {
            PartyType = PartyType.AiAgent,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            LastChangedBy = "integration-tests",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

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

        var candidates = await hrService.SearchStaffingCandidatesAsync(
            new StaffingCandidateQuery(skillId, "Expert"));
        Assert.Contains(candidates.Items, item => item.PartyId == workerId);

        var requests = await hrService.SearchStaffingRequestsAsync(
            new StaffingRequestQuery(projectId));
        var request = Assert.Single(requests.Items);
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
