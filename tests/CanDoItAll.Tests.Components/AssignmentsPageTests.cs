using Bunit;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using CanDoItAll.Modules.Projects;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class AssignmentsPageTests
{
    [Fact]
    public async Task Saves_staffing_requests_and_project_allocations_from_assignments_workspace()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var hrService = harness.Context.Services.GetRequiredService<HrService>();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var bridge = harness.Context.Services.GetRequiredService<IProjectPartyIntegrationBridge>();

        var projectId = await CreateProjectAsync(projectsService, "Assignments Proof Project");
        var requesterId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Paula Requester");
        var workerId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Rico Candidate");
        var deliveryUnitId = await CreatePartyAsync(partyDirectoryService, PartyType.OrganizationUnit, "North Delivery");

        Assert.True((await hrService.SaveWorkforceProfileAsync(new WorkforceProfileEditorModel
        {
            PartyId = workerId,
            WorkforceKind = WorkforceKind.Employee,
            Status = "Active",
            JobTitle = "Backend Engineer",
            Discipline = "Platform",
            Seniority = "Senior",
            Location = "Remote",
            CapacityHoursPerWeek = 40m,
            LastChangedBy = "component-tests"
        })).IsSuccess);

        var saveSkillResult = await hrService.SaveSkillDefinitionAsync(new SkillDefinitionEditorModel
        {
            Name = "Distributed Systems",
            Category = "Engineering",
            Description = "Platform skill",
            IsActive = true
        });
        Assert.True(saveSkillResult.IsSuccess);
        var skillId = saveSkillResult.Value;

        Assert.True((await hrService.SavePartySkillAsync(new PartySkillEditorModel
        {
            PartyId = workerId,
            SkillId = skillId,
            Proficiency = SkillProficiencyLevel.Expert,
            YearsExperience = 9,
            CertificationStatus = "CKA"
        })).IsSuccess);

        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/crm-hr/assignments?projectId={projectId}");

        var cut = harness.Context.RenderComponent<CrmHrAssignmentsPage>();

        cut.WaitForElement("[data-testid='crmhr-staffing-request-title']");
        cut.Find("[data-testid='crmhr-staffing-request-title']").Change("Need senior platform coverage");
        cut.Find("[data-testid='crmhr-staffing-request-role']").Change("Senior Platform Engineer");
        cut.Find("[data-testid='crmhr-staffing-request-allocation']").Change("60");
        cut.Find("[data-testid='crmhr-staffing-request-requested-by']").Change(requesterId.ToString());
        cut.Find("[data-testid='crmhr-staffing-request-delivery-unit']").Change(deliveryUnitId.ToString());
        cut.FindAll("[data-testid='crmhr-staffing-request-skill']").Single().Change(true);
        cut.Find("[data-testid='crmhr-staffing-request-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Staffing request saved.", cut.Markup);
            Assert.Contains("Need senior platform coverage", cut.Markup);
        });

        cut.Find("[data-testid='crmhr-allocation-candidate-skill']").Change(skillId.ToString());
        cut.Find("[data-testid='crmhr-allocation-candidate-search-button']").Click();

        cut.WaitForAssertion(() =>
        {
            var candidateCard = cut.FindAll("[data-testid='crmhr-staffing-candidate-item']")
                .Single(card => card.TextContent.Contains("Rico Candidate", StringComparison.Ordinal));
            Assert.Contains("Expert", candidateCard.TextContent);
        });

        cut.FindAll("[data-testid='crmhr-allocation-use-candidate']").Single().Click();
        cut.Find("[data-testid='crmhr-allocation-percent']").Change("60");
        cut.Find("[data-testid='crmhr-allocation-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Project allocation saved.", cut.Markup);
            Assert.Contains("Rico Candidate", cut.Markup);
        });

        var requests = await hrService.ListStaffingRequestsAsync(projectId);
        Assert.Single(requests);
        Assert.Contains(requests[0].NeededSkills, skill => skill.Id == skillId);

        var assignments = await bridge.ListAssignmentsDetailedAsync(projectId);
        Assert.Contains(assignments, item => item.PartyId == workerId && item.AllocationPercent == 60m);
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
        string displayName)
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
