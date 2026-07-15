using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureTaskResourceService(
    IProjectPartyIntegrationBridge partyIntegrationBridge,
    IWorkflowCatalogService workflowCatalogService,
    ProcessDefinitionCatalogProjectionService processDefinitionCatalogService,
    ProjectStructureWorkflowNodeService workflowNodeService,
    ProjectStructureAgentService agentService,
    ProjectWorkbenchService projectWorkbenchService)
{
    private const string AssignmentSource = "project-structure-gantt";
    private const string TaskSubtype = "task";

    private static readonly IReadOnlyList<ProjectPartyAssignmentRole> WorkItemAssignmentRoles =
        [ProjectPartyAssignmentRole.WorkItemAssignee];

    public async Task<IReadOnlyList<ProjectStructureTaskResourceOption>> ListOptionsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        EnsureProjectId(projectId);

        var partiesTask = partyIntegrationBridge.ListPartyOptionsAsync(projectId, cancellationToken);
        var workflowsTask = workflowCatalogService.ListDefinitionsAsync(cancellationToken);
        var processesTask = LoadProcessCatalogAsync(projectId, cancellationToken);
        await Task.WhenAll(partiesTask, workflowsTask, processesTask);

        var options = new List<ProjectStructureTaskResourceOption>();
        options.AddRange((await partiesTask).Where(IsTaskParty).Select(MapPartyOption));
        options.AddRange((await workflowsTask)
            .Where(workflow => workflow.Status == WorkflowLifecycleStatus.Active)
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

    public async Task AttachAsync(
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskResourceSelection selection,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        EnsureProjectId(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskNodeId);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(agent);
        ValidateSelection(selection);
        await EnsureCanonicalTaskAsync(projectId, taskNodeId, cancellationToken);

        switch (selection.Kind)
        {
            case ProjectStructureTaskResourceKind.Person:
            case ProjectStructureTaskResourceKind.Agent:
                await AttachPartyAsync(projectId, taskNodeId, selection, cancellationToken);
                return;
            case ProjectStructureTaskResourceKind.Workflow:
                await AttachWorkflowAsync(projectId, taskNodeId, selection, agent, cancellationToken);
                return;
            case ProjectStructureTaskResourceKind.Process:
                await AttachProcessAsync(projectId, taskNodeId, selection, agent, cancellationToken);
                return;
            default:
                throw new ProjectStructureAgentException(
                    400,
                    "TaskResourceKindInvalid",
                    $"Task resource kind '{selection.Kind}' is not supported.");
        }
    }

    private async Task AttachPartyAsync(
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskResourceSelection selection,
        CancellationToken cancellationToken)
    {
        var party = (await partyIntegrationBridge.ListPartyOptionsAsync(projectId, cancellationToken))
            .FirstOrDefault(option => option.PartyId == selection.ResourceId);
        if (party is null)
        {
            throw new ProjectStructureAgentException(
                404,
                "TaskResourceNotFound",
                $"Party resource '{selection.ResourceId:D}' is not available for project '{projectId:D}'.");
        }

        var expectedPartyType = selection.Kind switch
        {
            ProjectStructureTaskResourceKind.Person => ProjectPartyType.Person,
            ProjectStructureTaskResourceKind.Agent => ProjectPartyType.AiAgent,
            _ => throw new InvalidOperationException($"Resource kind '{selection.Kind}' is not a party resource.")
        };
        if (party.PartyType != expectedPartyType)
        {
            throw new ProjectStructureAgentException(
                400,
                "TaskResourceTypeMismatch",
                $"Party '{selection.ResourceId:D}' is '{party.PartyType}', not '{expectedPartyType}'.");
        }

        var assignmentResult = await partyIntegrationBridge.ReplaceNodeAssignmentsAsync(
            projectId,
            new ProjectNodeReference(taskNodeId),
            [
                new ProjectPartyAssignmentUpsertRequest
                {
                    ProjectId = projectId,
                    PartyId = party.PartyId,
                    Role = ProjectPartyAssignmentRole.WorkItemAssignee,
                    NodeKey = taskNodeId,
                    IsPrimary = true,
                    Source = AssignmentSource
                }
            ],
            WorkItemAssignmentRoles,
            cancellationToken);
        if (assignmentResult.IsFailure)
        {
            throw new ProjectStructureAgentException(
                422,
                "TaskResourceAssignmentFailed",
                string.Join(" ", assignmentResult.Errors.Select(error => error.Message)),
                assignmentResult.Errors);
        }

        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        var task = surface.Nodes.FirstOrDefault(node => string.Equals(node.Id, taskNodeId, StringComparison.Ordinal));
        if (task is null)
        {
            throw new ProjectStructureAgentException(404, "TaskNotFound", $"Task '{taskNodeId}' was not found.");
        }

        var metadata = ProjectObjectMetadataSerializer.Parse(task.MetadataJson);
        metadata.WorkItem ??= new ProjectWorkItemMetadata
        {
            WorkItemKind = ProjectWorkItemKind.Task
        };
        metadata.WorkItem.AssigneePartyDisplayName = party.DisplayName;
        var updatedTask = await projectWorkbenchService.UpdateObjectMetadataAsync(
            projectId,
            taskNodeId,
            ProjectObjectMetadataSerializer.Serialize(metadata),
            cancellationToken: cancellationToken);
        if (updatedTask is null)
        {
            throw new ProjectStructureAgentException(404, "TaskNotFound", $"Task '{taskNodeId}' was not found while saving its assignee.");
        }
    }

    private Task AttachWorkflowAsync(
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskResourceSelection selection,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken)
    {
        WorkflowVersionId? versionId = selection.VersionId.HasValue
            ? new WorkflowVersionId(selection.VersionId.Value)
            : null;
        return workflowNodeService.CreateAsync(
            projectId,
            taskNodeId,
            new ProjectStructureWorkflowNodeCreateInput(
                new WorkflowId(selection.ResourceId),
                versionId),
            agent,
            cancellationToken);
    }

    private async Task AttachProcessAsync(
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

        await agentService.LinkProcessDefinitionAsync(
            projectId,
            taskNodeId,
            new ProjectStructureProcessDefinitionLinkInput(selection.ResourceId),
            agent,
            cancellationToken);
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

    private static bool IsTaskParty(ProjectPartyOption option)
    {
        return option.PartyType is ProjectPartyType.Person or ProjectPartyType.AiAgent;
    }

    private static ProjectStructureTaskResourceOption MapPartyOption(ProjectPartyOption option)
    {
        var kind = option.PartyType == ProjectPartyType.AiAgent
            ? ProjectStructureTaskResourceKind.Agent
            : ProjectStructureTaskResourceKind.Person;
        var description = option.IsSensitive
            ? string.Empty
            : string.IsNullOrWhiteSpace(option.PrimaryEmail)
                ? option.PrimaryPhone
                : option.PrimaryEmail;
        return new ProjectStructureTaskResourceOption(
            kind,
            option.PartyId,
            VersionId: null,
            option.DisplayName,
            option.PartyTypeLabel,
            description,
            IsFavorite: false,
            option.IsSensitive);
    }

    private static void ValidateSelection(ProjectStructureTaskResourceSelection selection)
    {
        if (!Enum.IsDefined(selection.Kind))
        {
            throw new ProjectStructureAgentException(
                400,
                "TaskResourceKindInvalid",
                $"Task resource kind '{selection.Kind}' is not supported.");
        }

        if (selection.ResourceId == Guid.Empty)
        {
            throw new ProjectStructureAgentException(400, "TaskResourceRequired", "A task resource id is required.");
        }

        if (selection.VersionId == Guid.Empty)
        {
            throw new ProjectStructureAgentException(400, "TaskResourceVersionInvalid", "A resource version id cannot be empty.");
        }

        if (selection.Kind != ProjectStructureTaskResourceKind.Workflow && selection.VersionId.HasValue)
        {
            throw new ProjectStructureAgentException(
                400,
                "TaskResourceVersionNotSupported",
                $"Resource kind '{selection.Kind}' does not support a version id.");
        }
    }

    private static void EnsureProjectId(Guid projectId)
    {
        if (projectId == Guid.Empty)
        {
            throw new ProjectStructureAgentException(400, "ProjectIdRequired", "A project id is required.");
        }
    }
}
