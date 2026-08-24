using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectStructureTaskResourceAttachment(
    ProjectStructureTaskResourceKind Kind,
    string? CreatedNodeId,
    string? LinkTargetNodeId);

public sealed class ProjectStructureTaskResourceService(
    ProjectStructureWorkItemAssigneeService workItemAssigneeService,
    IWorkflowCatalogService workflowCatalogService,
    ProcessDefinitionCatalogProjectionService processDefinitionCatalogService,
    ProjectStructureWorkflowNodeService workflowNodeService,
    ProjectStructureAgentService agentService,
    ProjectWorkbenchService projectWorkbenchService)
{
    private const string AssignmentSource = "project-structure-task-resource";
    private const string TaskSubtype = "task";

    public async Task<IReadOnlyList<ProjectStructureTaskResourceOption>> ListOptionsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        EnsureProjectId(projectId);

        var partiesTask = workItemAssigneeService.ListOptionsAsync(projectId, cancellationToken);
        var workflowsTask = workflowCatalogService.ListDefinitionsAsync(cancellationToken);
        var processesTask = LoadProcessCatalogAsync(projectId, cancellationToken);
        await Task.WhenAll(partiesTask, workflowsTask, processesTask);
        var workflowHeads = await workflowsTask;
        var activeWorkflowDetails = await Task.WhenAll(
            workflowHeads.Select(workflow =>
                workflowCatalogService.GetLatestDefinitionByStatusAsync(
                    workflow.Id,
                    WorkflowLifecycleStatus.Active,
                    cancellationToken)));

        var options = new List<ProjectStructureTaskResourceOption>();
        options.AddRange(await partiesTask);
        options.AddRange(activeWorkflowDetails
            .Where(static detail => detail is not null)
            .Select(static detail => detail!.Definition)
            .Select(workflow => new ProjectStructureTaskResourceOption(
                ProjectStructureTaskResourceKind.Workflow,
                workflow.Id.Value,
                workflow.VersionId.Value,
                workflow.Name,
                "Workflow",
                workflow.Description,
                IsFavorite: false,
                IsSensitive: false)));
        options.AddRange((await processesTask).Select(process => new ProjectStructureTaskResourceOption(
            ProjectStructureTaskResourceKind.Process,
            ProcessDefinitionCatalogProjectionService.CreateDefinitionId(process.Key).Value,
            VersionId: null,
            process.Name,
            "Process",
            process.Summary,
            IsFavorite: false,
            IsSensitive: false)));

        return options
            .OrderBy(option => option.Kind)
            .ThenBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.ResourceId)
            .ToList();
    }

    internal Task<ProjectStructureTaskResourceAttachment> AttachAsync(
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskResourceSelection selection,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
        => AttachAsync(
            projectId,
            taskNodeId,
            selection,
            workflowInputSettings: null,
            agent,
            cancellationToken);

    internal async Task<ProjectStructureTaskResourceAttachment> AttachAsync(
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskResourceSelection selection,
        ProjectStructureWorkflowInputSettings? workflowInputSettings,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        EnsureProjectId(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskNodeId);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(agent);
        ProjectStructureTaskResourceSelectionPolicy.Validate(selection);
        workflowInputSettings =
            ProjectStructureTaskResourceSelectionPolicy.ValidateAndNormalizeWorkflowInputSettings(
                selection,
                workflowInputSettings);
        await EnsureCanonicalTaskAsync(projectId, taskNodeId, cancellationToken);

        switch (selection.Kind)
        {
            case ProjectStructureTaskResourceKind.Person:
            case ProjectStructureTaskResourceKind.Agent:
                await workItemAssigneeService.ReplaceAsync(
                    projectId,
                    taskNodeId,
                    selection,
                    AssignmentSource,
                    cancellationToken);
                return new ProjectStructureTaskResourceAttachment(
                    selection.Kind,
                    CreatedNodeId: null,
                    LinkTargetNodeId: null);
            case ProjectStructureTaskResourceKind.Workflow:
                return await AttachWorkflowAsync(
                    projectId,
                    taskNodeId,
                    selection,
                    workflowInputSettings,
                    agent,
                    cancellationToken);
            case ProjectStructureTaskResourceKind.Process:
                return await AttachProcessAsync(
                    projectId,
                    taskNodeId,
                    selection,
                    agent,
                    cancellationToken);
            default:
                throw new ProjectStructureAgentException(
                    400,
                    "TaskResourceKindInvalid",
                    $"Task resource kind '{selection.Kind}' is not supported.");
        }
    }

    internal async Task DetachAsync(
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskResourceAttachment attachment,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        EnsureProjectId(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskNodeId);
        ArgumentNullException.ThrowIfNull(attachment);
        ArgumentNullException.ThrowIfNull(agent);

        switch (attachment.Kind)
        {
            case ProjectStructureTaskResourceKind.Workflow:
                if (string.IsNullOrWhiteSpace(attachment.CreatedNodeId))
                {
                    throw new InvalidOperationException(
                        "The workflow attachment receipt does not identify its created node.");
                }

                var deletedCount = await agentService.DeleteCanonicalTaskResourceAsync(
                    projectId,
                    attachment.CreatedNodeId,
                    agent,
                    cancellationToken);
                if (deletedCount > 1)
                {
                    throw new InvalidOperationException(
                        $"Workflow attachment rollback removed an unexpected {deletedCount} nodes.");
                }

                return;
            case ProjectStructureTaskResourceKind.Process:
                if (string.IsNullOrWhiteSpace(attachment.LinkTargetNodeId))
                {
                    throw new InvalidOperationException(
                        "The process attachment receipt does not identify its link target.");
                }

                await agentService.UnlinkCanonicalTaskResourceAsync(
                    projectId,
                    new ProjectStructureLinkInput(
                        taskNodeId,
                        attachment.LinkTargetNodeId,
                        ProjectObjectLinkKind.Uses),
                    agent,
                    cancellationToken);
                return;
            default:
                throw new InvalidOperationException(
                    $"Resource kind '{attachment.Kind}' does not support attachment compensation.");
        }
    }

    private async Task<ProjectStructureTaskResourceAttachment> AttachWorkflowAsync(
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskResourceSelection selection,
        ProjectStructureWorkflowInputSettings? workflowInputSettings,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken)
    {
        WorkflowVersionId? versionId = selection.VersionId.HasValue
            ? new WorkflowVersionId(selection.VersionId.Value)
            : null;
        var result = await workflowNodeService.CreateForCanonicalTaskAsync(
            projectId,
            taskNodeId,
            new ProjectStructureWorkflowNodeCreateInput(
                new WorkflowId(selection.ResourceId),
                versionId,
                InputSettings: workflowInputSettings),
            agent,
            cancellationToken);
        return new ProjectStructureTaskResourceAttachment(
            ProjectStructureTaskResourceKind.Workflow,
            result.Node.Id,
            LinkTargetNodeId: null);
    }

    private async Task<ProjectStructureTaskResourceAttachment> AttachProcessAsync(
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskResourceSelection selection,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken)
    {
        var catalog = await LoadProcessCatalogAsync(projectId, cancellationToken);
        var exists = catalog.Any(item =>
            ProcessDefinitionCatalogProjectionService.CreateDefinitionId(item.Key).Value == selection.ResourceId);
        if (!exists)
        {
            throw new ProjectStructureAgentException(
                404,
                "TaskResourceNotFound",
                $"Process definition '{selection.ResourceId:D}' is not available for project '{projectId:D}'.");
        }

        var link = await agentService.LinkCanonicalTaskProcessDefinitionAsync(
            projectId,
            taskNodeId,
            new ProjectStructureProcessDefinitionLinkInput(selection.ResourceId),
            agent,
            cancellationToken);
        return new ProjectStructureTaskResourceAttachment(
            ProjectStructureTaskResourceKind.Process,
            CreatedNodeId: null,
            link.Link.TargetId);
    }

    private async Task EnsureCanonicalTaskAsync(
        Guid projectId,
        string taskNodeId,
        CancellationToken cancellationToken)
    {
        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        var task = surface.Nodes.FirstOrDefault(node => string.Equals(node.Id, taskNodeId, StringComparison.Ordinal));
        if (task is null)
        {
            throw new ProjectStructureAgentException(404, "TaskNotFound", $"Task '{taskNodeId}' was not found.");
        }

        if (task.IsSystemManaged ||
            task.ObjectType != ProjectObjectType.WorkItem ||
            !string.Equals(task.ObjectSubtype, TaskSubtype, StringComparison.OrdinalIgnoreCase))
        {
            throw new ProjectStructureAgentException(
                400,
                "CanonicalTaskRequired",
                $"Node '{taskNodeId}' is not a canonical WorkItem/task node.");
        }
    }

    private Task<IReadOnlyList<ProcessDefinitionCatalogItemProjection>> LoadProcessCatalogAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        return processDefinitionCatalogService.GetCompleteCatalogItemsAsync(
            ProcessWorkspaceShellScope.ForProject(projectId),
            cancellationToken: cancellationToken);
    }

    private static void EnsureProjectId(Guid projectId)
    {
        if (projectId == Guid.Empty)
        {
            throw new ProjectStructureAgentException(400, "ProjectIdRequired", "A project id is required.");
        }
    }
}
