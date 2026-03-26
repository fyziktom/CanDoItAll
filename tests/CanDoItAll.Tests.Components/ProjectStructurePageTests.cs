using Bunit;
using CanDoItAll.ComponentKit.Canvas;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructurePageTests
{
    [Fact]
    public async Task Renders_selection_and_health_as_floating_windows_without_stage_inspector_column()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Windowed Structure Project";
        project.Description = "Verify floating workbench windows.";
        project.Objective = "Keep inspector and health in the canvas.";
        project.CurrentPhase = "Validation";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        await workbenchService.SeedProjectObjectsAsync(
            projectId,
            [
                new ProjectObjectSeedRequest(
                    ProjectObjectType.Note,
                    "Floating window note",
                    "Window seed",
                    "Exercise the selection window.")
            ]);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Inspector", cut.Markup);
            Assert.Contains("Health", cut.Markup);
            Assert.Contains("project-structure-selection-window", cut.Markup);
            Assert.Contains("project-structure-validation-window", cut.Markup);
            Assert.DoesNotContain("cw-inspector-column", cut.Markup, StringComparison.Ordinal);
        });
    }

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
            Assert.Single(cut.FindAll("[data-testid='project-structure-action-catalog-adapter']"));
            Assert.Single(cut.FindAll("[data-testid='project-structure-placement-policy']"));
        });

        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Architecture note", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Create next to source", cut.Markup);
            Assert.Contains("Architecture note", cut.Markup);
            Assert.Contains("Tracks the first implementation idea", cut.Markup);
            Assert.Contains("Assets", cut.Markup);
            Assert.Contains(">Link<", cut.Markup);
            Assert.Contains(">Image<", cut.Markup);
            Assert.Contains(">Video<", cut.Markup);
            Assert.Contains(">Secret<", cut.Markup);
            Assert.Contains(">Feature block<", cut.Markup);
            Assert.Contains(">Support block<", cut.Markup);
            Assert.Contains(">Test plan<", cut.Markup);
        });
    }

    [Fact]
    public async Task Prompt_flow_nodes_expose_wizard_navigation_from_the_inspector()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Prompt Flow Structure";
        project.Description = "Prompt flow navigation coverage";
        project.Objective = "Open the prompt wizard from the structure page";
        project.CurrentPhase = "Discovery";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var created = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.PromptFlow,
                "Feature wizard flow",
                "Feature discovery",
                "Start from the structure canvas.",
                $"project:{projectId}",
                420,
                260));

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() => Assert.Contains("Feature wizard flow", cut.Markup));

        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Feature wizard flow", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(">Wizard<", cut.Markup);
        });

        cut.FindAll("button")
            .First(button => string.Equals(button.TextContent.Trim(), "Wizard", StringComparison.Ordinal))
            .Click();

        Assert.Contains("/prompt-factory?sessionId=", navigation.Uri, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(created.ArtifactId!.Value.ToString(), navigation.Uri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Persisted_multi_select_state_renders_common_actions_in_selection_window()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Persisted Multi Select";
        project.Description = "Verify the shared multi-select action surface.";
        project.Objective = "Restore batch actions from saved workbench state.";
        project.CurrentPhase = "Validation";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var feature = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Feature block",
                "Feature cluster",
                "Use for multi-select shared actions.",
                $"project:{projectId}",
                620,
                220,
                null,
                null,
                "feature"));

        var support = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Support block",
                "Support cluster",
                "Use for multi-select shared actions.",
                $"project:{projectId}",
                860,
                360,
                null,
                null,
                "support"));

        await workbenchService.SaveViewStateAsync(
            projectId,
            "structure",
            new CanvasWorkbenchUiState
            {
                SelectedNodeIds = [feature.Id, support.Id],
                WindowStates = new Dictionary<string, CanvasWorkbenchWindowState>(StringComparer.Ordinal)
                {
                    ["project-structure.selection"] = new CanvasWorkbenchWindowState { IsVisible = true },
                    ["project-structure.health"] = new CanvasWorkbenchWindowState { IsVisible = true }
                }
            }.ToJson());

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("2 nodes selected", cut.Markup);
            Assert.Contains("Grouping", cut.Markup);
            Assert.Contains(">P1<", cut.Markup);
            Assert.Contains(">50%<", cut.Markup);
            Assert.Contains(">Question<", cut.Markup);
            Assert.Contains(">Border<", cut.Markup);
        });
    }

    [Fact]
    public async Task Pdf_attachment_nodes_render_inline_preview_and_open_modal_without_navigation()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        var project = await projectsService.GetAsync(null);
        project.Name = "PDF Preview Project";
        project.Description = "Verify attachment previews in the inspector.";
        project.Objective = "Keep PDF viewing inside project structure.";
        project.CurrentPhase = "Review";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                "Architecture spec",
                "Uploaded PDF",
                "Attachment preview coverage",
                $"project:{projectId}",
                540,
                260,
                null,
                null,
                string.Empty,
                new ProjectObjectMediaPayload(
                    "architecture-spec.pdf",
                    "application/pdf",
                    Convert.ToBase64String("%PDF-1.4 test payload"u8.ToArray()))));

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() => Assert.Contains("Architecture spec", cut.Markup));

        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Architecture spec", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Attachment preview", cut.Markup);
            Assert.Contains("application/pdf", cut.Markup);
            Assert.Contains("project-structure-document-preview", cut.Markup);
        });

        var uriBeforeOpen = navigation.Uri;

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Expand preview", cut.Markup);
            Assert.Contains("Open in new tab", cut.Markup);
        });

        cut.FindAll("button")
            .First(button => string.Equals(button.TextContent.Trim(), "Open", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-preview-dialog", cut.Markup);
            Assert.Contains("dialog preview", cut.Markup);
        });

        Assert.Equal(uriBeforeOpen, navigation.Uri);
    }

    [Fact]
    public async Task Audio_attachment_nodes_render_audio_preview_and_local_open_action_when_host_supports_it()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();

        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Audio Preview Project";
        project.Description = "Verify audio and local-open coverage.";
        project.Objective = "Keep audio attachment handling inside project structure.";
        project.CurrentPhase = "Review";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var audioNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                "Interview clip",
                "Uploaded audio",
                "Audio preview coverage",
                $"project:{projectId}",
                540,
                260,
                null,
                null,
                string.Empty,
                new ProjectObjectMediaPayload(
                    "interview-clip.mp3",
                    "audio/mpeg",
                    Convert.ToBase64String(new byte[] { 0x49, 0x44, 0x33, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x21 }))));

        await workbenchService.SaveViewStateAsync(
            projectId,
            "structure",
            new CanvasWorkbenchUiState
            {
                SelectedNodeIds = [audioNode.Id]
            }.ToJson());

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Interview clip", cut.Markup);
            Assert.Contains("project-structure-audio-preview", cut.Markup);
            Assert.Contains("audio/mpeg", cut.Markup);
            Assert.Contains("Open locally", cut.Markup);
            Assert.Contains("Expand preview", cut.Markup);
        });
    }
}
