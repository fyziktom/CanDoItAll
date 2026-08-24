using System.Text.Json;
using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectStructurePageWorkflowNodeTests
{
    private const string ManualInputJson = "{\"reviewMode\":\"strict\"}";
    private const string SourcePath = "C:\\specs\\canvas-interactions.md";

    [Fact]
    public async Task Workflow_dialog_attaches_to_canonical_task_through_typed_path_and_preserves_input_settings()
    {
        await using var harness = await CreateHarnessAsync();
        var services = harness.Context.Services;
        var projectId = await CreateProjectAsync(
            services.GetRequiredService<ProjectsService>(),
            "Canonical task workflow attachment");
        var workflow = await CreateWorkflowAsync(
            services.GetRequiredService<IWorkflowCatalogService>(),
            "Canvas delivery workflow");
        var task = await services.GetRequiredService<ProjectStructureTaskCreationService>().CreateAsync(
            projectId,
            new ProjectStructureTaskCreateRequest(
                "Implement Canvas interactions",
                DateTimeOffset.Parse("2026-07-25T12:00:00Z"),
                DateTimeOffset.Parse("2026-07-25T20:00:00Z")),
            CreateAgent(projectId));

        var cut = harness.Context.Render<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        await AddWorkflowAsync(
            cut,
            canvasWorkbench,
            task.TaskNodeId,
            workflow);

        var surface = await services.GetRequiredService<ProjectWorkbenchService>()
            .GetStructureAsync(projectId);
        var workflowNode = Assert.Single(surface.Nodes, node =>
            string.Equals(node.ParentId, task.TaskNodeId, StringComparison.Ordinal) &&
            node.ObjectType == ProjectObjectType.WorkflowDefinition);
        var metadata = ProjectObjectMetadataSerializer.Parse(workflowNode.MetadataJson).Workflow;

        Assert.NotNull(metadata);
        Assert.Equal(workflow.Id, metadata!.WorkflowId);
        Assert.Equal(workflow.VersionId, metadata.WorkflowVersionId);
        AssertWorkflowInputSettings(metadata.InputSettings);
        Assert.Empty(cut.FindAll("[data-testid='project-structure-workflow-add-dialog']"));
        Assert.Equal(
            [workflowNode.Id],
            WaitForCanvasWorkbench(cut).Instance.Surface.UiState.SelectedNodeIds);
        Assert.Contains("Workflow is ready to start from project structure.", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(
            cut.Markup,
            "Canonical task creation and task estimate or metadata changes must use the typed task create/update path",
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Workflow_dialog_keeps_generic_creation_path_for_non_task_parent()
    {
        await using var harness = await CreateHarnessAsync();
        var services = harness.Context.Services;
        var projectId = await CreateProjectAsync(
            services.GetRequiredService<ProjectsService>(),
            "Generic workflow parent");
        var workflow = await CreateWorkflowAsync(
            services.GetRequiredService<IWorkflowCatalogService>(),
            "Generic delivery workflow");
        var workbenchService = services.GetRequiredService<ProjectWorkbenchService>();
        var parent = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Implementation",
                "Delivery block",
                "Non-task workflow parent.",
                $"project:{projectId}",
                420,
                260,
                ObjectSubtype: "implementation"));

        var cut = harness.Context.Render<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        await AddWorkflowAsync(
            cut,
            canvasWorkbench,
            parent.Id,
            workflow);

        var surface = await workbenchService.GetStructureAsync(projectId);
        var workflowNode = Assert.Single(surface.Nodes, node =>
            string.Equals(node.ParentId, parent.Id, StringComparison.Ordinal) &&
            node.ObjectType == ProjectObjectType.WorkflowDefinition);
        var metadata = ProjectObjectMetadataSerializer.Parse(workflowNode.MetadataJson).Workflow;

        Assert.NotNull(metadata);
        Assert.Equal(workflow.Id, metadata!.WorkflowId);
        Assert.Equal(workflow.VersionId, metadata.WorkflowVersionId);
        AssertWorkflowInputSettings(metadata.InputSettings);
        Assert.Empty(cut.FindAll("[data-testid='project-structure-workflow-add-dialog']"));
        Assert.Equal(
            [workflowNode.Id],
            WaitForCanvasWorkbench(cut).Instance.Surface.UiState.SelectedNodeIds);
        Assert.Contains("Workflow is ready to start from project structure.", cut.Markup, StringComparison.Ordinal);
    }

    private static async Task AddWorkflowAsync(
        IRenderedComponent<ProjectStructurePage> cut,
        IRenderedComponent<CanvasWorkbench> canvasWorkbench,
        string parentNodeId,
        WorkflowDefinition workflow)
    {
        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextAction(
            parentNodeId,
            "add-workflow",
            0,
            0));
        await cut.WaitForElement(
                $"[data-testid='project-structure-workflow-add-option-{workflow.Id.Value:N}']")
            .ClickAsync(new MouseEventArgs());
        await cut.Find("[data-testid='project-structure-workflow-add-include-subtree']")
            .ChangeAsync(true);
        await cut.Find("[data-testid='project-structure-workflow-add-include-assets']")
            .ChangeAsync(false);
        await cut.Find("[data-testid='project-structure-workflow-add-source-value']")
            .InputAsync(new ChangeEventArgs { Value = SourcePath });
        await cut.Find("[data-testid='project-structure-workflow-add-source-key']")
            .InputAsync(new ChangeEventArgs { Value = "canvas-spec" });
        await cut.Find("[data-testid='project-structure-workflow-add-source-label']")
            .InputAsync(new ChangeEventArgs { Value = "Canvas interaction specification" });
        await cut.Find("[data-testid='project-structure-workflow-add-manual-json']")
            .InputAsync(new ChangeEventArgs { Value = ManualInputJson });
        await cut.Find("[data-testid='project-structure-workflow-add-submit']")
            .ClickAsync(new MouseEventArgs());
    }

    private static void AssertWorkflowInputSettings(ProjectStructureWorkflowInputSettings inputSettings)
    {
        Assert.True(inputSettings.IncludeParentSubtree);
        Assert.False(inputSettings.IncludeAssets);
        Assert.Equal(ManualInputJson, inputSettings.ManualInputJson);
        var source = Assert.Single(inputSettings.AdditionalSources);
        Assert.Equal(ProjectStructureWorkflowInputSourceKind.FilePath, source.Kind);
        Assert.Equal("canvas-spec", source.Key);
        Assert.Equal("Canvas interaction specification", source.Label);
        Assert.Equal(SourcePath, source.Value);
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
            Description = "Project structure workflow-attachment component regression proof.",
            Objective = "Attach a workflow without bypassing canonical task invariants.",
            CurrentPhase = "Delivery"
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static Task<ComponentTestHarness> CreateHarnessAsync()
        => ComponentTestHarness.CreateAsync(services =>
            services.Replace(ServiceDescriptor.Singleton<ISecretVault>(new InMemorySecretVault())));

    private static Task<WorkflowDefinition> CreateWorkflowAsync(
        IWorkflowCatalogService workflowCatalogService,
        string name)
    {
        var start = new WorkflowNodeId("start");
        var end = new WorkflowNodeId("end");
        return workflowCatalogService.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            name,
            $"{name} description",
            WorkflowLifecycleStatus.Active,
            new WorkflowGraph(
                start,
                [
                    CreateWorkflowNode(start, WorkflowNodeKind.Start),
                    CreateWorkflowNode(end, WorkflowNodeKind.End)
                ],
                [
                    new WorkflowEdge(
                        new WorkflowEdgeId("start-to-end"),
                        start,
                        SourcePortId: null,
                        end,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty)
                ]),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false)));
    }

    private static WorkflowNode CreateWorkflowNode(WorkflowNodeId id, WorkflowNodeKind kind)
    {
        return new WorkflowNode(
            id,
            kind,
            id.Value,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text));
    }

    private static ProjectStructureAgentContext CreateAgent(Guid projectId)
        => new(
            "component-tests-workflow-attachment",
            "Component tests",
            Environment.MachineName,
            AppContext.BaseDirectory,
            JsonSerializer.Serialize(new { ProjectId = projectId }),
            $"{projectId:D}-workflow-attachment");
}
