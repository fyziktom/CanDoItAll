using Bunit;
using CanDoItAll.Modules.Factory.Pages;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class PromptFactoryPageTests
{
    [Fact]
    public async Task Renders_seeded_blocks_and_builds_prompt_for_selected_project()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Component Test Project";
        project.Description = "Component test description";
        project.Objective = "Exercise the factory page";
        project.CurrentPhase = "Review";
        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);

        var cut = harness.Context.RenderComponent<PromptFactoryPage>();
        cut.WaitForAssertion(() => Assert.Contains("Delivery Constraints", cut.Markup));

        cut.Find("[data-testid='prompt-factory-project']").Change(saveResult.Value!.ToString());
        cut.Find("[data-testid='prompt-factory-build']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Prompt built.", cut.Markup);
            Assert.Contains("Prompt run nodes", cut.Markup);
        });
    }
}
