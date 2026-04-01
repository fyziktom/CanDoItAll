using Bunit;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Factory.Pages;
using CanDoItAll.Modules.Projects;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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
            Assert.Single(cut.FindAll("[data-testid='prompt-factory-components-toolbox-toggle']"));
            Assert.Single(cut.FindAll("[data-testid='prompt-factory-components-toolbox-window']"));
            Assert.Single(cut.FindAll("[data-testid='prompt-factory-components-toolbox']"));
            Assert.Empty(cut.FindAll("[data-testid='prompt-factory-component-preview-popover']"));
            Assert.Single(cut.FindAll("[data-testid='prompt-factory-undo-redo-adapter']"));
            Assert.Single(cut.FindAll("[data-testid='chip-badge-primitive']"));
            Assert.Single(cut.FindAll("[data-testid='connector-path-primitive']"));
            Assert.Single(cut.FindAll("[data-testid='container-primitive']"));
            Assert.Single(cut.FindAll("[data-testid='context-menu-host']"));
            Assert.Single(cut.FindAll("[data-testid='create-action-palette']"));
            Assert.Single(cut.FindAll("[data-testid='floating-inspector-host']"));
            Assert.Single(cut.FindAll("[data-testid='group-frame-overlay']"));
            Assert.Single(cut.FindAll("[data-testid='icon-glyph-primitive']"));
            Assert.Single(cut.FindAll("[data-testid='image-primitive']"));
            Assert.Single(cut.FindAll("[data-testid='inline-editor-composer']"));
            Assert.Single(cut.FindAll("[data-testid='node-card-composer']"));
            Assert.Single(cut.FindAll("[data-testid='text-block-primitive']"));
            Assert.Contains("Prompt Factory tabs", cut.Markup);
            Assert.Contains("Canvas inspector", cut.Markup);
        });
    }

    [Fact]
    public async Task Eye_preview_opens_floating_component_popover()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var cut = harness.Context.RenderComponent<PromptFactoryPage>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".pf-components-toolbox__preview-toggle")));

        var previewButton = cut.FindAll("button")
            .First(button => (button.GetAttribute("aria-label") ?? string.Empty).StartsWith("Preview ", StringComparison.Ordinal));
        var previewedComponentName = previewButton.GetAttribute("aria-label")!["Preview ".Length..];

        previewButton.TriggerEvent("onmouseenter", new MouseEventArgs
        {
            ClientX = 220,
            ClientY = 168
        });

        cut.WaitForAssertion(() =>
        {
            var popover = cut.Find("[data-testid='prompt-factory-component-preview-popover']");
            Assert.Equal("right", popover.GetAttribute("data-placement"));
            Assert.Contains(previewedComponentName, popover.TextContent);
        });

        previewButton.TriggerEvent("onmouseleave", new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='prompt-factory-component-preview-popover']")));
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

        var cut = harness.Context.RenderComponent<PromptFactoryPage>();
        await cut.InvokeAsync(async () =>
        {
            cut.Instance.SessionIdQuery = sessionId;
            cut.Instance.ShowPromptPreviewQuery = true;
            await cut.Instance.SetParametersAsync(ParameterView.Empty);
        });

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Prompt session workbench", cut.Markup);
            Assert.Single(cut.FindAll("[data-testid='prompt-factory-prompt-modal']"));
            Assert.Contains("Copy prompt", cut.Markup);
            var promptPreviewText = (string?)typeof(PromptFactoryPage)
                .GetField("promptPreviewText", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(cut.Instance);
            Assert.Equal(buildResult.Value!.GeneratedPrompt, promptPreviewText);
        });

        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Close", StringComparison.OrdinalIgnoreCase))
            .Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='prompt-factory-prompt-modal']")));
    }
}


