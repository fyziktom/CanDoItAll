using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureWorkItemAssigneeService(
    IProjectPartyIntegrationBridge partyIntegrationBridge,
    ProjectWorkbenchService projectWorkbenchService,
    ILogger<ProjectStructureWorkItemAssigneeService> logger)
{
    private const string RollbackSource = "project-structure-work-item-assignee-rollback";

    private static readonly IReadOnlyList<ProjectPartyAssignmentRole> WorkItemAssignmentRoles =
        [ProjectPartyAssignmentRole.WorkItemAssignee];

    public async Task<IReadOnlyList<ProjectStructureTaskResourceOption>> ListOptionsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        EnsureProjectId(projectId);

        return (await partyIntegrationBridge.ListPartyOptionsAsync(projectId, cancellationToken))
            .Where(IsAssignableParty)
            .Select(MapPartyOption)
            .OrderBy(option => option.Kind)
            .ThenBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.ResourceId)
            .ToList();
    }

    public async Task ReplaceAsync(
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskResourceSelection? selection,
        string source,
        CancellationToken cancellationToken = default)
    {
        EnsureProjectId(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskNodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        await GetCanonicalWorkItemAsync(projectId, taskNodeId, cancellationToken);
        var previousAssignments = (await partyIntegrationBridge.ListAssignmentsDetailedAsync(projectId, cancellationToken))
            .Where(assignment =>
                string.Equals(assignment.NodeKey, taskNodeId, StringComparison.Ordinal) &&
                assignment.Role == ProjectPartyAssignmentRole.WorkItemAssignee)
            .ToList();
        var party = selection is null
            ? null
            : await ResolvePartyAsync(projectId, selection, cancellationToken);
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments = party is null
            ? []
            :
            [
                new ProjectPartyAssignmentUpsertRequest
                {
                    ProjectId = projectId,
                    PartyId = party.PartyId,
                    Role = ProjectPartyAssignmentRole.WorkItemAssignee,
                    NodeKey = taskNodeId,
                    IsPrimary = true,
                    Source = source
                }
            ];

        var assignmentResult = await partyIntegrationBridge.ReplaceNodeAssignmentsAsync(
            projectId,
            new ProjectNodeReference(taskNodeId),
            desiredAssignments,
            WorkItemAssignmentRoles,
            cancellationToken);
        if (assignmentResult.IsFailure)
        {
            throw BuildAssignmentException(assignmentResult.Errors);
        }

        try
        {
            var workItem = await GetCanonicalWorkItemAsync(projectId, taskNodeId, cancellationToken);
            var workItemKind = ProjectNodeKindRegistry.ResolveWorkItemKind(workItem.ObjectSubtype);
            var updatedTask = await projectWorkbenchService.MutateObjectMetadataAsync(
                projectId,
                taskNodeId,
                metadata =>
                {
                    metadata.WorkItem ??= new ProjectWorkItemMetadata
                    {
                        WorkItemKind = workItemKind
                    };
                    metadata.WorkItem.AssigneePartyDisplayName = party?.DisplayName ?? string.Empty;
                },
                cancellationToken: cancellationToken);
            if (updatedTask is null)
            {
                throw new InvalidOperationException($"Work item '{taskNodeId}' disappeared while its assignee display snapshot was being saved.");
            }
        }
        catch (OperationCanceledException cancellationFailure) when (cancellationToken.IsCancellationRequested)
        {
            await RestoreAssignmentsAsync(
                projectId,
                taskNodeId,
                previousAssignments,
                cancellationFailure);
            throw;
        }
        catch (Exception metadataFailure)
        {
            var rollbackFailure = await RestoreAssignmentsAsync(
                projectId,
                taskNodeId,
                previousAssignments,
                metadataFailure);
            throw new ProjectStructureAgentException(
                500,
                rollbackFailure is null
                    ? "TaskAssigneeMetadataSyncFailed"
                    : "TaskAssigneeMetadataSyncRollbackFailed",
                rollbackFailure is null
                    ? "The work-item assignee could not be projected into metadata. The canonical assignment was restored."
                    : "The work-item assignee metadata update and canonical assignment rollback both failed.",
                rollbackFailure is null
                    ? metadataFailure.GetType().Name
                    : new[] { metadataFailure.GetType().Name, rollbackFailure.GetType().Name });
        }
    }

    private async Task<ProjectPartyOption> ResolvePartyAsync(
        Guid projectId,
        ProjectStructureTaskResourceSelection selection,
        CancellationToken cancellationToken)
    {
        ValidateSelection(selection);
        var party = await partyIntegrationBridge.GetPartyOptionAsync(selection.ResourceId, cancellationToken);
        if (party is null)
        {
            throw new ProjectStructureAgentException(
                404,
                "TaskAssigneeNotFound",
                $"Party assignee '{selection.ResourceId:D}' is not available for project '{projectId:D}'.");
        }

        var expectedPartyType = selection.Kind switch
        {
            ProjectStructureTaskResourceKind.Person => ProjectPartyType.Person,
            ProjectStructureTaskResourceKind.Agent => ProjectPartyType.AiAgent,
            _ => throw new InvalidOperationException($"Resource kind '{selection.Kind}' is not a task assignee.")
        };
        if (party.PartyType != expectedPartyType)
        {
            throw new ProjectStructureAgentException(
                400,
                "TaskAssigneeTypeMismatch",
                $"Party '{selection.ResourceId:D}' is '{party.PartyType}', not '{expectedPartyType}'.");
        }

        return party;
    }

    private async Task<ProjectStructureNode> GetCanonicalWorkItemAsync(
        Guid projectId,
        string taskNodeId,
        CancellationToken cancellationToken)
    {
        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        var task = surface.Nodes.FirstOrDefault(node => string.Equals(node.Id, taskNodeId, StringComparison.Ordinal));
        if (task is null)
        {
            throw new ProjectStructureAgentException(404, "WorkItemNotFound", $"Work item '{taskNodeId}' was not found.");
        }

        if (task.IsSystemManaged ||
            task.ObjectType != ProjectObjectType.WorkItem)
        {
            throw new ProjectStructureAgentException(
                400,
                "CanonicalWorkItemRequired",
                $"Node '{taskNodeId}' is not a canonical editable WorkItem node.");
        }

        return task;
    }

    private async Task<Exception?> RestoreAssignmentsAsync(
        Guid projectId,
        string taskNodeId,
        IReadOnlyList<ProjectPartyAssignmentDetail> previousAssignments,
        Exception metadataFailure)
    {
        try
        {
            var rollback = await partyIntegrationBridge.ReplaceNodeAssignmentsAsync(
                projectId,
                new ProjectNodeReference(taskNodeId),
                previousAssignments.Select(assignment => new ProjectPartyAssignmentUpsertRequest
                {
                    ProjectId = projectId,
                    PartyId = assignment.PartyId,
                    Role = assignment.Role,
                    NodeKey = taskNodeId,
                    IsPrimary = assignment.IsPrimary,
                    AllocationPercent = assignment.AllocationPercent,
                    StartsOn = ToDateOnly(assignment.StartsAtUtc),
                    EndsOn = ToDateOnly(assignment.EndsAtUtc),
                    Source = RollbackSource,
                    Notes = assignment.Notes
                }).ToList(),
                WorkItemAssignmentRoles,
                CancellationToken.None);
            if (rollback.IsFailure)
            {
                return new InvalidOperationException(string.Join(" ", rollback.Errors.Select(error => error.Message)));
            }

            logger.LogWarning(
                metadataFailure,
                "Restored canonical work-item assignments after metadata synchronization failed. ProjectId={ProjectId} WorkItemNodeId={WorkItemNodeId}",
                projectId,
                taskNodeId);
            return null;
        }
        catch (Exception rollbackFailure)
        {
            logger.LogError(
                rollbackFailure,
                "Failed to restore canonical work-item assignments. ProjectId={ProjectId} WorkItemNodeId={WorkItemNodeId} MetadataFailureType={MetadataFailureType}",
                projectId,
                taskNodeId,
                metadataFailure.GetType().Name);
            return rollbackFailure;
        }
    }

    private static DateOnly? ToDateOnly(DateTimeOffset? value)
        => value.HasValue ? DateOnly.FromDateTime(value.Value.UtcDateTime) : null;

    private static bool IsAssignableParty(ProjectPartyOption option)
        => option.PartyType is ProjectPartyType.Person or ProjectPartyType.AiAgent;

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
        if (selection.Kind is not (ProjectStructureTaskResourceKind.Person or ProjectStructureTaskResourceKind.Agent))
        {
            throw new ProjectStructureAgentException(
                400,
                "TaskAssigneeKindInvalid",
                $"Resource kind '{selection.Kind}' cannot be assigned directly to a task.");
        }

        if (selection.ResourceId == Guid.Empty)
        {
            throw new ProjectStructureAgentException(400, "TaskAssigneeRequired", "A task assignee id is required.");
        }

        if (selection.VersionId.HasValue)
        {
            throw new ProjectStructureAgentException(
                400,
                "TaskAssigneeVersionNotSupported",
                "Person and agent task assignees do not use a resource version.");
        }
    }

    private static ProjectStructureAgentException BuildAssignmentException(IReadOnlyList<Error> errors)
        => new(
            422,
            "TaskAssigneeAssignmentFailed",
            string.Join(" ", errors.Select(error => error.Message)),
            errors);

    private static void EnsureProjectId(Guid projectId)
    {
        if (projectId == Guid.Empty)
        {
            throw new ProjectStructureAgentException(400, "ProjectIdRequired", "A project id is required.");
        }
    }
}
