using System.Text.Json;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructurePageTaskAssigneeCreationTests
{
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
        var dialogHost = harness.Context.RenderComponent<DialogHost>();
        var page = harness.Context.RenderComponent<ProjectStructurePage>(
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

    private static IRenderedComponent<CanvasWorkbench> WaitForCanvasWorkbench(IRenderedFragment page)
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
