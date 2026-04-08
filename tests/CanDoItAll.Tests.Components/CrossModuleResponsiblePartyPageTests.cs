using Bunit;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Resources.Pages;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.TestLab.Pages;
using CanDoItAll.Modules.Validation;
using CanDoItAll.Modules.Validation.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class CrossModuleResponsiblePartyPageTests
{
    [Fact]
    public async Task Resources_page_saves_owner_and_maintainer_from_project_party_options()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var projectPartyBridge = harness.Context.Services.GetRequiredService<IProjectPartyIntegrationBridge>();
        var resourcesService = harness.Context.Services.GetRequiredService<ResourcesService>();

        var projectId = await CreateProjectAsync(projectsService, "Resource ownership");
        var ownerId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Olivia Owner");
        var maintainerId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Marco Maintainer");

        await SaveAssignmentAsync(projectPartyBridge, projectId, ownerId, ProjectPartyAssignmentRole.TeamMember, "resource-owner");
        await SaveAssignmentAsync(projectPartyBridge, projectId, maintainerId, ProjectPartyAssignmentRole.Manager, "resource-maintainer");

        var cut = harness.Context.RenderComponent<ResourcesPage>();
        cut.Find("[data-testid='resource-project-select']").Change(projectId.ToString());
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Olivia Owner", cut.Markup);
            Assert.Contains("Marco Maintainer", cut.Markup);
        });

        cut.Find("[data-testid='resource-name-input']").Change("Operations repository");
        cut.Find("[data-testid='resource-primary-input']").Change("https://example.test/repositories/operations.git");
        cut.Find("[data-testid='resource-owner-select']").Change(ownerId.ToString());
        cut.Find("[data-testid='resource-maintainer-select']").Change(maintainerId.ToString());
        cut.Find("[data-testid='resource-save-button']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Resource saved.", cut.Markup));

        var resources = await resourcesService.ListAsync();
        var savedSummary = Assert.Single(resources);
        var savedResource = await resourcesService.GetAsync(savedSummary.Id);

        Assert.Equal(ownerId, savedResource.OwnerPartyId);
        Assert.Equal(maintainerId, savedResource.MaintainerPartyId);
    }

    [Fact]
    public async Task Validation_page_saves_responsible_party_from_project_party_options()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var projectPartyBridge = harness.Context.Services.GetRequiredService<IProjectPartyIntegrationBridge>();
        var validationService = harness.Context.Services.GetRequiredService<ValidationService>();

        var projectId = await CreateProjectAsync(projectsService, "Validation ownership");
        var responsiblePartyId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Vera Validator");

        await SaveAssignmentAsync(projectPartyBridge, projectId, responsiblePartyId, ProjectPartyAssignmentRole.Reviewer, "validation-owner");

        var cut = harness.Context.RenderComponent<ValidationCenterPage>();
        cut.Find("[data-testid='validation-project-select']").Change(projectId.ToString());
        cut.WaitForAssertion(() => Assert.Contains("Vera Validator", cut.Markup));

        cut.Find("[data-testid='validation-responsible-party-select']").Change(responsiblePartyId.ToString());
        cut.Find("[data-testid='validation-artifact-title-input']").Change("CRM-HR cross-module review");
        cut.Find("[data-testid='validation-source-content-input']").Change("Cross-module validation source content with architecture and workflow detail.");
        cut.Find("[data-testid='validation-run-button']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Validation completed.", cut.Markup));

        var runs = await validationService.ListRunsAsync();
        var savedRunSummary = Assert.Single(runs);
        var savedRun = await validationService.GetRunAsync(savedRunSummary.Id);

        Assert.Equal(responsiblePartyId, savedRun.ResponsiblePartyId);
    }

    [Fact]
    public async Task Test_lab_page_saves_responsible_party_from_project_party_options()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var projectPartyBridge = harness.Context.Services.GetRequiredService<IProjectPartyIntegrationBridge>();
        var testLabService = harness.Context.Services.GetRequiredService<TestLabService>();

        var projectId = await CreateProjectAsync(projectsService, "Test lab ownership");
        var responsiblePartyId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Tara Tester");

        await SaveAssignmentAsync(projectPartyBridge, projectId, responsiblePartyId, ProjectPartyAssignmentRole.Reviewer, "testlab-owner");

        var cut = harness.Context.RenderComponent<TestLabPage>();
        cut.Find("[data-testid='testlab-project-select']").Change(projectId.ToString());
        cut.WaitForAssertion(() => Assert.Contains("Tara Tester", cut.Markup));

        cut.Find("[data-testid='testlab-responsible-party-select']").Change(responsiblePartyId.ToString());
        cut.Find("[data-testid='testlab-title-input']").Change("CRM-HR route coverage");
        cut.Find("[data-testid='testlab-save-button']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Test plan saved.", cut.Markup));

        var plans = await testLabService.ListAsync();
        var savedPlanSummary = Assert.Single(plans);
        var savedPlan = await testLabService.GetAsync(savedPlanSummary.Id);

        Assert.Equal(responsiblePartyId, savedPlan.ResponsiblePartyId);
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var editor = await projectsService.GetAsync(null);
        editor.Name = name;
        editor.Description = $"{name} description";
        editor.Objective = $"{name} objective";
        editor.CurrentPhase = "Discovery";

        var result = await projectsService.SaveAsync(editor);

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

    private static async Task SaveAssignmentAsync(
        IProjectPartyIntegrationBridge projectPartyBridge,
        Guid projectId,
        Guid partyId,
        ProjectPartyAssignmentRole role,
        string nodeKey)
    {
        var result = await projectPartyBridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = partyId,
            Role = role,
            NodeKey = nodeKey,
            IsPrimary = true,
            Source = "component-tests"
        });

        Assert.True(result.IsSuccess);
    }
}
