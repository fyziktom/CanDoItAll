using Bunit;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components;

public sealed class CrmHrWorkforcePageTests
{
    [Fact]
    public async Task Creates_delivery_unit_from_workforce_page()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        await CreatePartyAsync(
            partyDirectoryService,
            "Manager North",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Employee,
            "manager.north@example.test");

        var cut = harness.Context.RenderComponent<CrmHrWorkforcePage>();

        cut.Find("[data-testid='crmhr-delivery-unit-name']").Change("Northwind Delivery");
        cut.Find("[data-testid='crmhr-delivery-unit-code']").Change("NW-DEL");
        cut.Find("[data-testid='crmhr-delivery-unit-summary']").Change("Core delivery organization unit");
        cut.Find("[data-testid='crmhr-delivery-unit-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Delivery unit created.", cut.Markup);
            Assert.Contains("Northwind Delivery", cut.Markup);
        });

        var directoryItems = await partyDirectoryService.ListDirectoryAsync();
        Assert.Contains(
            directoryItems,
            item => item.DisplayName == "Northwind Delivery" &&
                    item.PartyType == PartyType.OrganizationUnit &&
                    item.Roles.Contains(PartyRoleKind.DeliveryUnit));
    }

    [Fact]
    public async Task Saves_workforce_profile_for_existing_person()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var hrService = harness.Context.Services.GetRequiredService<HrService>();

        var managerId = await CreatePartyAsync(
            partyDirectoryService,
            "Mira Lead",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Employee,
            "mira.lead@example.test");
        var workerId = await CreatePartyAsync(
            partyDirectoryService,
            "Tomas Worker",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Employee,
            "tomas.worker@example.test");
        var unitId = await CreatePartyAsync(
            partyDirectoryService,
            "Platform Delivery",
            PartyType.OrganizationUnit,
            PartyLifecycleStatus.Active,
            PartyRoleKind.DeliveryUnit,
            "platform.delivery@example.test");

        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/crm-hr/workforce?partyId={workerId}");

        var cut = harness.Context.RenderComponent<CrmHrWorkforcePage>();

        cut.WaitForElement("[data-testid='crmhr-workforce-job-title']");
        cut.Find("[data-testid='crmhr-workforce-kind']").Change(WorkforceKind.Employee.ToString());
        cut.Find("[data-testid='crmhr-workforce-status']").Change("Active");
        cut.Find("[data-testid='crmhr-workforce-employee-code']").Change("EMP-42");
        cut.Find("[data-testid='crmhr-workforce-job-title']").Change("Senior Platform Engineer");
        cut.Find("[data-testid='crmhr-workforce-discipline']").Change("Platform");
        cut.Find("[data-testid='crmhr-workforce-seniority']").Change("Senior");
        cut.Find("[data-testid='crmhr-workforce-home-unit']").Change(unitId.ToString());
        cut.Find("[data-testid='crmhr-workforce-manager']").Change(managerId.ToString());
        cut.Find("[data-testid='crmhr-workforce-start-date']").Change("2026-04-01");
        cut.Find("[data-testid='crmhr-workforce-location']").Change("Remote");
        cut.Find("[data-testid='crmhr-workforce-timezone']").Change("Europe/Prague");
        cut.Find("[data-testid='crmhr-workforce-internal-rate']").Change("120");
        cut.Find("[data-testid='crmhr-workforce-external-rate']").Change("180");
        cut.Find("[data-testid='crmhr-workforce-capacity']").Change("37.5");
        cut.Find("[data-testid='crmhr-workforce-notes']").Change("Owns platform delivery across shared accounts.");
        cut.Find("[data-testid='crmhr-workforce-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Workforce profile saved.", cut.Markup);
            Assert.Contains("Senior Platform Engineer", cut.Markup);
            Assert.Contains("Platform Delivery", cut.Markup);
            Assert.Contains("Mira Lead", cut.Markup);
        });

        var workspace = await hrService.GetWorkforceWorkspaceAsync(workerId);
        Assert.NotNull(workspace);
        Assert.Equal(WorkforceKind.Employee, workspace.Profile.WorkforceKind);
        Assert.Equal("Senior Platform Engineer", workspace.Profile.JobTitle);
        Assert.Equal("Platform", workspace.Profile.Discipline);
        Assert.Equal("Senior", workspace.Profile.Seniority);
        Assert.Equal(unitId, workspace.Profile.HomeUnitPartyId);
        Assert.Equal(managerId, workspace.Profile.ManagerPartyId);
        Assert.Equal("Remote", workspace.Profile.Location);
        Assert.Equal("Europe/Prague", workspace.Profile.TimeZone);
        Assert.Equal(120m, workspace.Profile.InternalCostRate);
        Assert.Equal(180m, workspace.Profile.ExternalBillingRate);
        Assert.Equal(37.5m, workspace.Profile.CapacityHoursPerWeek);
    }

    [Fact]
    public async Task Saves_skills_and_capacity_blocks_and_surfaces_project_conflicts()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var hrService = harness.Context.Services.GetRequiredService<HrService>();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var bridge = harness.Context.Services.GetRequiredService<IProjectPartyIntegrationBridge>();

        var workerId = await CreatePartyAsync(
            partyDirectoryService,
            "Nika Specialist",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Employee,
            "nika.specialist@example.test");
        var projectId = await CreateProjectAsync(projectsService, "Capacity Proof Project");

        Assert.True((await hrService.SaveWorkforceProfileAsync(new WorkforceProfileEditorModel
        {
            PartyId = workerId,
            WorkforceKind = WorkforceKind.Employee,
            Status = "Active",
            CapacityHoursPerWeek = 40m,
            JobTitle = "Integration Engineer",
            LastChangedBy = "component-tests"
        })).IsSuccess);
        Assert.True((await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = workerId,
            Role = ProjectPartyAssignmentRole.TeamMember,
            AllocationPercent = 60m,
            IsPrimary = true,
            Source = "component-tests"
        })).IsSuccess);

        var skillId = (await hrService.SaveSkillDefinitionAsync(new SkillDefinitionEditorModel
        {
            Name = "Platform Engineering",
            Category = "Delivery",
            Description = "Shared delivery skill",
            IsActive = true
        })).Value;

        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/crm-hr/workforce?partyId={workerId}");

        var cut = harness.Context.RenderComponent<CrmHrWorkforcePage>();

        cut.WaitForElement("[data-testid='crmhr-skill-skill-id']");
        cut.Find("[data-testid='crmhr-skill-skill-id']").Change(skillId.ToString());
        cut.Find("[data-testid='crmhr-skill-proficiency']").Change(SkillProficiencyLevel.Expert.ToString());
        cut.Find("[data-testid='crmhr-skill-years']").Change("8");
        cut.Find("[data-testid='crmhr-skill-certification']").Change("AWS SA Pro");
        cut.Find("[data-testid='crmhr-skill-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Skill saved.", cut.Markup);
            Assert.Contains("Platform Engineering", cut.Markup);
            Assert.Contains("Expert", cut.Markup);
        });

        cut.Find("[data-testid='crmhr-capacity-block-percentage']").Change("50");
        cut.Find("[data-testid='crmhr-capacity-block-notes']").Change("Planned leave");
        cut.Find("[data-testid='crmhr-capacity-block-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Capacity block saved.", cut.Markup);
            Assert.Contains("crmhr-capacity-conflict-callout", cut.Markup);
            Assert.Contains("Capacity Proof Project", cut.Markup);
        });

        var workspace = await hrService.GetWorkforceWorkspaceAsync(workerId);
        Assert.NotNull(workspace);
        Assert.Single(workspace.Skills);
        Assert.Single(workspace.CapacityBlocks);
        Assert.Single(workspace.ProjectAllocations);
        Assert.Equal(60m, workspace.CapacitySummary.ActiveAllocationPercent);
        Assert.Equal(50m, workspace.CapacitySummary.ActiveBlockedPercent);
        Assert.True(workspace.CapacitySummary.IsOverallocated);
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
            LastChangedBy = "component-tests",
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
