using System.Text.Json;
using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructurePageProcessLinkTests
{
    private static readonly Guid SoftwareDeliveryDefinitionId =
        ProcessDefinitionCatalogProjectionService.CreateDefinitionId(
            new ProcessDefinitionCatalogItemKey("software-delivery")).Value;

    [Fact]
    public async Task Process_dialog_attaches_process_to_canonical_task_through_typed_resource_path()
    {
        await using var harness = await CreateHarnessAsync();
        var services = harness.Context.Services;
        var projectId = await CreateProjectAsync(
            services.GetRequiredService<ProjectsService>(),
            "Canonical task process link");
        var task = await services.GetRequiredService<ProjectStructureTaskCreationService>().CreateAsync(
            projectId,
            new ProjectStructureTaskCreateRequest(
                "Main App",
                DateTimeOffset.Parse("2026-07-25T12:00:00Z"),
                DateTimeOffset.Parse("2026-07-25T20:00:00Z")),
            CreateAgent(projectId));

        var cut = harness.Context.Render<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextAction(
            task.TaskNodeId,
            "add-process",
            0,
            0));
        await cut.WaitForElement(
                $"[data-testid='project-structure-process-link-option-{SoftwareDeliveryDefinitionId:N}']")
            .ClickAsync(new MouseEventArgs());
        await cut.Find("[data-testid='project-structure-process-link-submit']")
            .ClickAsync(new MouseEventArgs());

        var surface = await services.GetRequiredService<ProjectWorkbenchService>()
            .GetStructureAsync(projectId);
        var link = Assert.Single(surface.Links, candidate =>
            string.Equals(candidate.SourceId, task.TaskNodeId, StringComparison.Ordinal) &&
            candidate.Kind == ProjectObjectLinkKind.Uses);
        Assert.Equal(
            ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(SoftwareDeliveryDefinitionId),
            link.TargetId);
        Assert.DoesNotContain(
            cut.Markup,
            "Canonical task creation and task estimate or metadata changes must use the typed task create/update path",
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Process_dialog_keeps_generic_link_path_for_non_task_nodes()
    {
        await using var harness = await CreateHarnessAsync();
        var services = harness.Context.Services;
        var projectId = await CreateProjectAsync(
            services.GetRequiredService<ProjectsService>(),
            "Project block process link");
        var workbenchService = services.GetRequiredService<ProjectWorkbenchService>();
        var block = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Implementation",
                "Delivery block",
                "Non-task process target.",
                $"project:{projectId}",
                420,
                260,
                ObjectSubtype: "implementation"));

        var cut = harness.Context.Render<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextAction(
            block.Id,
            "add-process",
            0,
            0));
        await cut.WaitForElement(
                $"[data-testid='project-structure-process-link-option-{SoftwareDeliveryDefinitionId:N}']")
            .ClickAsync(new MouseEventArgs());
        await cut.Find("[data-testid='project-structure-process-link-submit']")
            .ClickAsync(new MouseEventArgs());

        var surface = await workbenchService.GetStructureAsync(projectId);
        var link = Assert.Single(surface.Links, candidate =>
            string.Equals(candidate.SourceId, block.Id, StringComparison.Ordinal) &&
            candidate.Kind == ProjectObjectLinkKind.Uses);
        Assert.Equal(
            ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(SoftwareDeliveryDefinitionId),
            link.TargetId);
    }

    private static IRenderedComponent<CanvasWorkbench> WaitForCanvasWorkbench(
        IRenderedComponent<ProjectStructurePage> cut)
    {
        IRenderedComponent<CanvasWorkbench>? canvasWorkbench = null;
        cut.WaitForAssertion(
            () => canvasWorkbench = cut.FindComponent<CanvasWorkbench>(),
            TimeSpan.FromSeconds(20));
        return Assert.IsAssignableFrom<IRenderedComponent<CanvasWorkbench>>(canvasWorkbench);
    }

    private static async Task<Guid> CreateProjectAsync(
        ProjectsService projectsService,
        string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = $"{name} {Guid.NewGuid():N}",
            Description = "Project structure process-link component regression proof.",
            Objective = "Attach a process without bypassing canonical task invariants.",
            CurrentPhase = "Delivery"
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static Task<ComponentTestHarness> CreateHarnessAsync()
        => ComponentTestHarness.CreateAsync(services =>
            services.Replace(ServiceDescriptor.Singleton<ISecretVault>(new InMemorySecretVault())));

    private static ProjectStructureAgentContext CreateAgent(Guid projectId)
        => new(
            "component-tests-process-link",
            "Component tests",
            Environment.MachineName,
            AppContext.BaseDirectory,
            JsonSerializer.Serialize(new { ProjectId = projectId }),
            $"{projectId:D}-process-link");
}
