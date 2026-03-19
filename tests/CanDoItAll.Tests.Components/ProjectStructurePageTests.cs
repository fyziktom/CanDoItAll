using Bunit;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructurePageTests
{
    [Fact]
    public async Task Renders_shared_structure_workbench_and_updates_inspector_from_outline_selection()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Structure Test Project";
        project.Description = "Project structure coverage";
        project.Objective = "Verify the shared structure canvas page";
        project.CurrentPhase = "Discovery";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        await workbenchService.SeedProjectObjectsAsync(
            projectId,
            [
                new ProjectObjectSeedRequest(
                    ProjectObjectType.Note,
                    "Architecture note",
                    "Tracks the first implementation idea",
                    "Shared canvas test note",
                    null,
                    null)
            ]);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Structure canvas", cut.Markup);
            Assert.Contains("Project object index", cut.Markup);
            Assert.Contains("Graph health", cut.Markup);
            Assert.Contains("Architecture note", cut.Markup);
        });

        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Architecture note", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Create next to source", cut.Markup);
            Assert.Contains("Architecture note", cut.Markup);
            Assert.Contains("Tracks the first implementation idea", cut.Markup);
        });
    }
}
