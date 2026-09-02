using System.Text.Json;
using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectStructurePageTaskAssigneeCreationTests
{
    [Fact]
    public async Task Locked_multi_assignment_edit_attaches_workflow_without_replacing_direct_assignments()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var projectsService = services.GetRequiredService<ProjectsService>();
        var partyDirectoryService = services.GetRequiredService<PartyDirectoryService>();
        var workbenchService = services.GetRequiredService<ProjectWorkbenchService>();
        var partyBridge = services.GetRequiredService<IProjectPartyIntegrationBridge>();
        var projectId = await CreateProjectAsync(
            projectsService,
            TaskCreateEntryPoint.CreateActionInvoked);
        var primaryPersonId = await CreatePartyAsync(
            partyDirectoryService,
            "Morgan Lee",
            PartyType.Person);
        var supportingAgentId = await CreatePartyAsync(
            partyDirectoryService,
            "Atlas Build Agent",
            PartyType.AiAgent);
        var workflow = await CreateWorkflowAsync(
            services.GetRequiredService<IWorkflowCatalogService>(),
            "Canvas delivery workflow");
        var task = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Implement Canvas interactions",
                "Delivery",
                string.Empty,
                $"project:{projectId}",
                420,
                260,
                ObjectSubtype: "task",
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(
                    new ProjectObjectMetadataEnvelope
                    {
                        WorkItem = new ProjectWorkItemMetadata
                        {
                            WorkItemKind = ProjectWorkItemKind.Task,
                            ExecutionState = ProjectTaskExecutionState.NotStarted,
                            ExpectedEffortHours = 8m,
                            ExpectedEffortUnit = ProjectWorkItemEffortUnit.Hours
                        }
                    })));
        var assignmentResult = await partyBridge.ReplaceNodeAssignmentsAsync(
            projectId,
            new ProjectNodeReference(task.Id),
            [
                CreateAssignment(
                    projectId,
                    task.Id,
                    primaryPersonId,
                    isPrimary: true),
                CreateAssignment(
                    projectId,
                    task.Id,
                    supportingAgentId,
                    isPrimary: false)
            ],
            [ProjectPartyAssignmentRole.WorkItemAssignee]);
        Assert.True(assignmentResult.IsSuccess);
        var assignmentsBefore = await ReadDirectAssignmentsAsync(
            partyBridge,
            projectId,
            task.Id);
        var revisionBefore = ReadDirectAssignmentRevision(
            await workbenchService.GetStructureAsync(projectId),
            task.Id);

        var dialogHost = harness.Context.Render<DialogHost>();
        var page = harness.Context.Render<ProjectStructurePage>(
            parameters => parameters.Add(component => component.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(page);
        await page.InvokeAsync(() =>
            canvasWorkbench.Instance.NodeOpened.InvokeAsync(task.Id));
        page.WaitForElement("[data-testid='project-structure-node-quick-actions']");
        var dialogs = page.FindComponents<ProjectStructureCanvasDialogs>()
            .Single(component => component.Instance.QuickActionDialog is not null);
        var quickActions = Assert.IsType<ProjectStructureQuickActionDialogState>(
            dialogs.Instance.QuickActionDialog);
        var editTask = page.InvokeAsync(() =>
            dialogs.Instance.ExecuteQuickAction.InvokeAsync(
                quickActions.EditAction));

        dialogHost.WaitForElement(
            "[data-testid='project-structure-task-create-assignee-warning']");
        var workflowSelector =
            $"[data-testid='project-structure-task-create-assignee-workflow-{workflow.Id.Value:N}']";
        dialogHost.WaitForElement(workflowSelector);
        Assert.Empty(dialogHost.FindAll(
            $"[data-testid='project-structure-task-create-assignee-person-{primaryPersonId:N}']"));
        Assert.Empty(dialogHost.FindAll(
            $"[data-testid='project-structure-task-create-assignee-agent-{supportingAgentId:N}']"));
        Assert.Single(dialogHost.FindAll(
            "[data-testid='project-structure-task-create-assignee-warning']"));
        Assert.Empty(dialogHost.FindAll(
            "[data-testid='project-structure-task-edit-assignee-readonly']"));
        dialogHost.Find("[data-testid='project-structure-task-create-title']")
            .Input("Implement Canvas interactions safely");
        dialogHost.Find(workflowSelector).Click();
        dialogHost.Find("[data-testid='project-structure-task-create-submit']")
            .Click();

        await editTask.WaitAsync(TimeSpan.FromSeconds(20));

        var persistedSurface = await workbenchService.GetStructureAsync(projectId);
        var persistedTask = Assert.Single(
            persistedSurface.Nodes,
            node => node.Id == task.Id);
        Assert.Equal("Implement Canvas interactions safely", persistedTask.Title);
        Assert.Equal(
            revisionBefore,
            ReadDirectAssignmentRevision(persistedSurface, task.Id));
        var assignmentsAfter = await ReadDirectAssignmentsAsync(
            partyBridge,
            projectId,
            task.Id);
        Assert.True(
            ProjectStructureTaskAssigneeSelectionPolicy.HasSameDirectAssignments(
                assignmentsBefore,
                assignmentsAfter));
        var workflowNode = Assert.Single(
            persistedSurface.Nodes,
            node =>
                string.Equals(node.ParentId, task.Id, StringComparison.Ordinal) &&
                node.ObjectType == ProjectObjectType.WorkflowDefinition);
        var workflowMetadata =
            ProjectObjectMetadataSerializer.Parse(workflowNode.MetadataJson)
                .Workflow;
        Assert.NotNull(workflowMetadata);
        Assert.Equal(workflow.Id, workflowMetadata!.WorkflowId);
        Assert.Equal(workflow.VersionId, workflowMetadata.WorkflowVersionId);
    }

    [Fact]
    public async Task Canonical_task_edit_assigns_CRM_person_without_creating_child_node()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var partyBridge = harness.Context.Services.GetRequiredService<IProjectPartyIntegrationBridge>();
        var projectId = await CreateProjectAsync(projectsService, TaskCreateEntryPoint.CreateActionInvoked);
        var joeId = await CreateJoeDoeAsync(partyDirectoryService);
        var task = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Prepare CRM handoff",
                "Delivery",
                string.Empty,
                $"project:{projectId}",
                420,
                260,
                ObjectSubtype: "task",
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    WorkItem = new ProjectWorkItemMetadata
                    {
                        WorkItemKind = ProjectWorkItemKind.Task
                    }
                })));
        var dialogHost = harness.Context.Render<DialogHost>();
        var page = harness.Context.Render<ProjectStructurePage>(
            parameters => parameters.Add(component => component.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(page);

        await page.InvokeAsync(() => canvasWorkbench.Instance.NodeOpened.InvokeAsync(task.Id));
        page.WaitForElement("[data-testid='project-structure-node-quick-actions']");
        var dialogs = page.FindComponents<ProjectStructureCanvasDialogs>()
            .Single(component => component.Instance.QuickActionDialog is not null);
        var quickActions = Assert.IsType<ProjectStructureQuickActionDialogState>(dialogs.Instance.QuickActionDialog);
        var editTask = page.InvokeAsync(() =>
            dialogs.Instance.ExecuteQuickAction.InvokeAsync(quickActions.EditAction));

        dialogHost.WaitForElement("[data-testid='project-structure-task-create-title']");
        var joeCardSelector = $"[data-testid='project-structure-task-create-assignee-person-{joeId:N}']";
        dialogHost.WaitForElement(joeCardSelector);
        dialogHost.Find(joeCardSelector).Click();
        dialogHost.Find("[data-testid='project-structure-task-create-submit']").Click();

        await editTask.WaitAsync(TimeSpan.FromSeconds(20));

        var persistedSurface = await workbenchService.GetStructureAsync(projectId);
        var refreshedTask = Assert.Single(persistedSurface.Nodes, node => node.Id == task.Id);
        Assert.Equal(
            "Joe Doe",
            ProjectObjectMetadataSerializer.Parse(refreshedTask.MetadataJson).WorkItem?.AssigneePartyDisplayName);
        Assert.Null(refreshedTask.NodeReferences?.WorkItemAssigneeNodeId);

        var assignment = Assert.Single(
            await partyBridge.ListAssignmentsDetailedAsync(projectId),
            item => item.NodeKey == task.Id && item.Role == ProjectPartyAssignmentRole.WorkItemAssignee);
        Assert.Equal(joeId, assignment.PartyId);
        Assert.True(assignment.IsPrimary);
        Assert.DoesNotContain(
            persistedSurface.Nodes,
            node => node.ParentId == task.Id &&
                (node.ObjectType == ProjectObjectType.Participant || node.Title == "Joe Doe"));
        Assert.DoesNotContain(
            persistedSurface.Links,
            link => link.SourceId == task.Id && link.Kind == ProjectObjectLinkKind.Uses);
    }

    [Theory]
    [InlineData(TaskCreateEntryPoint.CreateActionInvoked)]
    [InlineData(TaskCreateEntryPoint.ContextActionRequested)]
    public async Task Task_create_entry_point_assigns_CRM_person_without_creating_graph_identity_nodes(
        TaskCreateEntryPoint entryPoint)
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var partyBridge = harness.Context.Services.GetRequiredService<IProjectPartyIntegrationBridge>();
        var projectId = await CreateProjectAsync(projectsService, entryPoint);
        var joeId = await CreateJoeDoeAsync(partyDirectoryService);
        var dialogHost = harness.Context.Render<DialogHost>();
        var page = harness.Context.Render<ProjectStructurePage>(
            parameters => parameters.Add(component => component.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(page);
        var projectRoot = Assert.Single(
            canvasWorkbench.Instance.Surface.Nodes,
            node => node.Id.StartsWith("project:", StringComparison.Ordinal));
        var taskTitle = $"Assign Joe through {entryPoint}";

        var callbackTask = InvokeTaskCreateAsync(page, canvasWorkbench, entryPoint, projectRoot);

        dialogHost.WaitForElement("[data-testid='project-structure-task-create-title']");
        var joeCardSelector = $"[data-testid='project-structure-task-create-assignee-person-{joeId:N}']";
        dialogHost.WaitForElement(joeCardSelector);
        dialogHost.Find("[data-testid='project-structure-task-create-title']").Input(taskTitle);
        dialogHost.Find(joeCardSelector).Click();
        dialogHost.Find("[data-testid='project-structure-task-create-submit']").Click();

        await callbackTask.WaitAsync(TimeSpan.FromSeconds(20));

        var persistedSurface = await workbenchService.GetStructureAsync(projectId);
        var task = Assert.Single(
            persistedSurface.Nodes,
            node => string.Equals(node.Title, taskTitle, StringComparison.Ordinal));
        Assert.Equal(ProjectObjectType.WorkItem, task.ObjectType);
        Assert.Equal("task", task.ObjectSubtype);
        Assert.Equal(
            "Joe Doe",
            ProjectObjectMetadataSerializer.Parse(task.MetadataJson).WorkItem?.AssigneePartyDisplayName);
        Assert.Null(task.NodeReferences?.WorkItemAssigneeNodeId);

        var assignment = Assert.Single(
            await partyBridge.ListAssignmentsDetailedAsync(projectId),
            item => string.Equals(item.NodeKey, task.Id, StringComparison.Ordinal) &&
                item.Role == ProjectPartyAssignmentRole.WorkItemAssignee);
        Assert.Equal(joeId, assignment.PartyId);
        Assert.True(assignment.IsPrimary);

        Assert.DoesNotContain(
            persistedSurface.Nodes,
            node => string.Equals(node.ParentId, task.Id, StringComparison.Ordinal) &&
                (node.ObjectType == ProjectObjectType.Participant ||
                 string.Equals(node.Title, "Joe Doe", StringComparison.Ordinal)));
        Assert.DoesNotContain(
            persistedSurface.Links,
            link => string.Equals(link.SourceId, task.Id, StringComparison.Ordinal) &&
                link.Kind == ProjectObjectLinkKind.Uses);
    }

    private static Task InvokeTaskCreateAsync(
        IRenderedComponent<ProjectStructurePage> page,
        IRenderedComponent<CanvasWorkbench> canvasWorkbench,
        TaskCreateEntryPoint entryPoint,
        CanvasWorkbenchNode projectRoot)
        => entryPoint switch
        {
            TaskCreateEntryPoint.CreateActionInvoked => page.InvokeAsync(() =>
                canvasWorkbench.Instance.OnCreateAction(JsonSerializer.Serialize(
                    new CanvasWorkbenchCreateActionRequest(
                        ProjectStructureTaskActionIds.Create,
                        projectRoot.Id,
                        projectRoot.X,
                        projectRoot.Y,
                        projectRoot.Id,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        "child",
                        ProjectStructureTaskActionIds.CreateMode,
                        "task",
                        null)))),
            TaskCreateEntryPoint.ContextActionRequested => page.InvokeAsync(() =>
                canvasWorkbench.Instance.OnContextActionRequest(JsonSerializer.Serialize(
                    new CanvasWorkbenchContextActionRequest(
                        projectRoot.Id,
                        ProjectStructureTaskActionIds.Create,
                        projectRoot.X,
                        projectRoot.Y,
                        "node")))),
            _ => throw new ArgumentOutOfRangeException(nameof(entryPoint), entryPoint, null)
        };

    private static async Task<Guid> CreateProjectAsync(
        ProjectsService projectsService,
        TaskCreateEntryPoint entryPoint)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = $"Task assignment page callback {entryPoint} {Guid.NewGuid():N}",
            Description = "Page-level direct task assignee regression coverage.",
            Objective = "Keep CRM person assignment in the canonical relation.",
            CurrentPhase = "Delivery"
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<Guid> CreateJoeDoeAsync(PartyDirectoryService partyDirectoryService)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = PartyType.Person,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = "Joe Doe",
            Summary = "CRM person used for project task assignment regression coverage.",
            LastChangedBy = "component-tests",
            Roles =
            [
                new PartyRoleAssignmentEditorModel
                {
                    RoleKind = PartyRoleKind.Employee,
                    Title = "Employee",
                    IsPrimary = true
                }
            ]
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName,
        PartyType partyType)
    {
        var result = await partyDirectoryService.SavePartyAsync(
            new PartyEditorModel
            {
                PartyType = partyType,
                LifecycleStatus = PartyLifecycleStatus.Active,
                DisplayName = displayName,
                Summary = $"{displayName} multi-assignment attachment test.",
                LastChangedBy = "component-tests"
            });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static ProjectPartyAssignmentUpsertRequest CreateAssignment(
        Guid projectId,
        string taskNodeId,
        Guid partyId,
        bool isPrimary)
        => new()
        {
            ProjectId = projectId,
            PartyId = partyId,
            Role = ProjectPartyAssignmentRole.WorkItemAssignee,
            NodeKey = taskNodeId,
            IsPrimary = isPrimary,
            Source = "canvas-task-resource-test"
        };

    private static async Task<IReadOnlyList<ProjectPartyAssignmentDetail>>
        ReadDirectAssignmentsAsync(
            IProjectPartyIntegrationBridge partyBridge,
            Guid projectId,
            string taskNodeId)
        => (await partyBridge.ListAssignmentsDetailedAsync(projectId))
            .Where(assignment =>
                assignment.Role ==
                    ProjectPartyAssignmentRole.WorkItemAssignee &&
                string.Equals(
                    assignment.NodeKey,
                    taskNodeId,
                    StringComparison.Ordinal))
            .OrderBy(static assignment => assignment.Id)
            .ToArray();

    private static long ReadDirectAssignmentRevision(
        ProjectStructureSurface surface,
        string taskNodeId)
        => ProjectObjectMetadataSerializer.Parse(
                surface.Nodes.Single(node => node.Id == taskNodeId).MetadataJson)
            .WorkItem?
            .DirectAssignmentRevision ?? 0;

    private static Task<WorkflowDefinition> CreateWorkflowAsync(
        IWorkflowCatalogService workflowCatalogService,
        string name)
    {
        var start = new WorkflowNodeId("start");
        var end = new WorkflowNodeId("end");
        return workflowCatalogService.SaveDefinitionAsync(
            new WorkflowDefinitionSaveRequest(
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

    private static WorkflowNode CreateWorkflowNode(
        WorkflowNodeId id,
        WorkflowNodeKind kind)
        => new(
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

    private static IRenderedComponent<CanvasWorkbench> WaitForCanvasWorkbench(IRenderedComponent<IComponent> page)
    {
        IRenderedComponent<CanvasWorkbench>? canvasWorkbench = null;
        page.WaitForAssertion(() => canvasWorkbench = page.FindComponent<CanvasWorkbench>());
        return canvasWorkbench ?? throw new InvalidOperationException("Canvas workbench did not render.");
    }

    public enum TaskCreateEntryPoint
    {
        CreateActionInvoked,
        ContextActionRequested
    }
}
