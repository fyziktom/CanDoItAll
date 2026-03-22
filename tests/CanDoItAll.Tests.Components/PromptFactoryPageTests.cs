using Bunit;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Factory.Pages;
using CanDoItAll.Modules.Projects;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class PromptFactoryPageTests
{
    [Fact]
    public async Task Page_renders_canvas_history_controls_and_inspector_workflow_steps()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var cut = harness.Context.RenderComponent<PromptFactoryPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindAll("[data-testid='prompt-factory-undo']"));
            Assert.Single(cut.FindAll("[data-testid='prompt-factory-redo']"));
            Assert.Equal(4, cut.FindAll(".pf-inspector-step").Count);
            Assert.Contains("Switch stages from the inspector", cut.Markup);
        });
    }

    [Fact]
    public async Task Preview_query_opens_built_prompt_modal()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var factoryService = harness.Context.Services.GetRequiredService<PromptFactoryService>();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Component Test Project";
        project.Description = "Component test description";
        project.Objective = "Exercise the factory page";
        project.CurrentPhase = "Review";
        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);

        var sessionId = await factoryService.CreateBlankProjectSessionAsync(saveResult.Value!, "Component test prompt session", "review");
        var editor = await factoryService.GetEditorAsync(sessionId);
        var buildResult = await factoryService.BuildAsync(editor);
        Assert.True(buildResult.IsSuccess);

        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/prompt-factory?sessionId={sessionId}&preview=true");
        var cut = harness.Context.RenderComponent<PromptFactoryPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Prompt session workbench", cut.Markup);
            Assert.Single(cut.FindAll("[data-testid='prompt-factory-prompt-modal']"));
            Assert.Contains("Final prompt", cut.Markup);
            Assert.Contains("# Prompt Request", cut.Markup);
        });

        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Close", StringComparison.OrdinalIgnoreCase))
            .Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='prompt-factory-prompt-modal']")));
    }
}
