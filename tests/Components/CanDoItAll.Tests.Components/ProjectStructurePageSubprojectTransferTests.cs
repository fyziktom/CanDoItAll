using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectStructurePageSubprojectTransferTests
{
    [Fact]
    public async Task Dialog_transfer_creates_a_linked_child_and_moves_descendants_through_shared_coordinator()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var sourceProjectId = await CreateProjectAsync(projects);
        var sourceAnchor = await CreateNodeAsync(
            workbench,
            sourceProjectId,
            "Planning anchor",
            $"project:{sourceProjectId:D}");
        _ = await CreateNodeAsync(
            workbench,
            sourceProjectId,
            "Moved planning note",
            sourceAnchor.Id);
        var page = harness.Context.Render<ProjectStructurePage>(parameters =>
            parameters.Add(component => component.ProjectId, sourceProjectId));
        var canvas = WaitForCanvasWorkbench(page);

        await page.InvokeAsync(() => canvas.Instance.OnContextAction(
            sourceAnchor.Id,
            "move-descendants-to-subproject",
            sourceAnchor.X,
            sourceAnchor.Y));

        page.WaitForElement("[data-testid='project-structure-subproject-transfer-dialog']");
        page.Find("[data-testid='project-structure-subproject-transfer-name']")
            .Input("UI extracted plan");
        await page.Find("[data-testid='project-structure-subproject-transfer-submit']")
            .ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() =>
        {
            Assert.Empty(page.FindAll("[data-testid='project-structure-subproject-transfer-dialog']"));
            Assert.Contains("Created UI extracted plan and moved 1 descendant into it.", page.Markup, StringComparison.Ordinal);
        });

    }

    private static IRenderedComponent<CanvasWorkbench> WaitForCanvasWorkbench(
        IRenderedComponent<IComponent> page)
    {
        IRenderedComponent<CanvasWorkbench>? canvas = null;
        page.WaitForAssertion(() => canvas = page.FindComponent<CanvasWorkbench>());
        return canvas ?? throw new InvalidOperationException("Canvas workbench did not render.");
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projects)
    {
        var result = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "UI subproject transfer source",
            Description = "Component coverage for linked subproject transfer.",
            Objective = "Prove UI and agent orchestration share one coordinator.",
            CurrentPhase = "Planning",
            Status = ProjectStatus.Active
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static Task<ProjectStructureNode> CreateNodeAsync(
        ProjectWorkbenchService workbench,
        Guid projectId,
        string title,
        string parentNodeId)
    {
        return workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                title,
                string.Empty,
                $"{title} notes.",
                parentNodeId));
    }
}
