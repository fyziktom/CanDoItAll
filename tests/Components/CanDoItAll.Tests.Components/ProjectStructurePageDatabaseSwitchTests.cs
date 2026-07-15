using Bunit;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructurePageDatabaseSwitchTests
{
    [Fact]
    public async Task Missing_project_routes_render_a_safe_structure_recovery_state()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var missingProjectId = Guid.NewGuid();

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, missingProjectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Project structure unavailable", cut.Markup);
            Assert.Contains("does not exist in the active database profile", cut.Markup);
        });

        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Open projects", StringComparison.Ordinal))
            .Click();

        Assert.EndsWith("/projects", navigation.Uri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Changing_projects_resets_to_canvas_and_keeps_gantt_uninitialized_until_selected()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var firstProjectId = await CreateProjectAsync(projectsService, "First lazy Gantt project");
        var secondProjectId = await CreateProjectAsync(projectsService, "Second lazy Gantt project");

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, firstProjectId));

        cut.WaitForElement(".cad-tabs__tab");
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='project-structure-gantt-panel']")));
        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".cad-tabs__tab")
                .Single(tab => tab.TextContent.Contains("Gantt", StringComparison.Ordinal))
                .Click();
        });
        cut.WaitForElement("[data-testid='project-structure-gantt-panel']");

        cut.SetParametersAndRender(parameters => parameters.Add(page => page.ProjectId, secondProjectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid='project-structure-gantt-panel']"));
            var selectedTab = Assert.Single(cut.FindAll(".cad-tabs__tab[aria-selected='true']"));
            Assert.Contains("Canvas", selectedTab.TextContent, StringComparison.Ordinal);
        });
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var project = await projectsService.GetAsync(null);
        project.Name = name;
        project.Description = $"{name} description";
        project.Objective = $"{name} objective";
        project.CurrentPhase = "Execution";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        return saveResult.Value;
    }
}
