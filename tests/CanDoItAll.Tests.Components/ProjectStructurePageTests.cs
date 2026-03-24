using Bunit;
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
}
