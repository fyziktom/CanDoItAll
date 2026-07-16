using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureTaskDetailsService(
    ProjectStructureGanttMutationService ganttMutationService,
    ProjectStructureWorkItemAssigneeService assigneeService,
    IProjectPartyIntegrationBridge partyIntegrationBridge,
    ProjectWorkbenchService projectWorkbenchService,
    ILogger<ProjectStructureTaskDetailsService> logger)
{
    private const string AssignmentSource = "project-structure-task-details";

    private static readonly IReadOnlyList<ProjectPartyAssignmentRole> WorkItemAssignmentRoles =
        [ProjectPartyAssignmentRole.WorkItemAssignee];

    public async Task<ProjectStructureGanttMutationResult> UpdateAsync(
        Guid projectId,
        ProjectStructureTaskDetailsUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(projectId, request);

        var previousAssignee = request.AssigneeChanged
            ? await LoadCurrentAssigneeAsync(projectId, request.TaskId.Value, cancellationToken)
            : null;
        var assignmentWasChanged = request.AssigneeChanged && previousAssignee!.Selection != request.ProposedAssignee;
        if (assignmentWasChanged)
        {
            await assigneeService.ReplaceAsync(
                projectId,
                request.TaskId.Value,
                request.ProposedAssignee,
                AssignmentSource,
                cancellationToken);
        }

        try
        {
            return await ganttMutationService.ApplyTaskDetailsAsync(projectId, request, cancellationToken);
        }
        catch (Exception updateFailure) when (assignmentWasChanged)
        {
            try
            {
                await RestoreAssigneeAsync(
                    projectId,
                    request.TaskId.Value,
                    previousAssignee!,
                    CancellationToken.None);
            }
            catch (Exception compensationFailure)
            {
                logger.LogError(
                    compensationFailure,
                    "Task detail update failed and the previous assignee could not be restored. ProjectId={ProjectId} TaskId={TaskId} UpdateFailureType={UpdateFailureType}",
                    Mask(projectId),
                    Mask(request.TaskId.Value),
                    updateFailure.GetType().Name);
                throw new ProjectStructureTaskDetailsException(
                    ProjectStructureTaskDetailsErrorCode.AssignmentCompensationFailed,
                    "The task details were not saved and its previous assignee could not be restored. Reload the project before making another change.",
                    new AggregateException(updateFailure, compensationFailure));
            }

            logger.LogWarning(
                "Restored the previous task assignee after a task detail update failed. ProjectId={ProjectId} TaskId={TaskId} FailureType={FailureType}",
                Mask(projectId),
                Mask(request.TaskId.Value),
                updateFailure.GetType().Name);
            throw;
        }
    }

    private async Task<AssigneeSnapshot> LoadCurrentAssigneeAsync(
        Guid projectId,
        string taskNodeId,
        CancellationToken cancellationToken)
    {
        var assignments = (await partyIntegrationBridge.ListAssignmentsDetailedAsync(
                projectId,
                WorkItemAssignmentRoles,
                cancellationToken))
            .Where(assignment =>
                string.Equals(assignment.NodeKey, taskNodeId, StringComparison.Ordinal))
            .ToArray();
        if (assignments.Length > 1)
        {
            throw new ProjectStructureTaskDetailsException(
                ProjectStructureTaskDetailsErrorCode.AssignmentConflict,
                "The task has multiple direct assignees. Resolve the assignment conflict before editing it from the Gantt chart.");
        }

        if (assignments.Length == 0)
        {
            return new AssigneeSnapshot(null, null);
        }

        var assignment = assignments[0];
        var kind = assignment.PartyType switch
        {
            ProjectPartyType.Person => ProjectStructureTaskResourceKind.Person,
            ProjectPartyType.AiAgent => ProjectStructureTaskResourceKind.Agent,
            _ => throw new ProjectStructureTaskDetailsException(
                ProjectStructureTaskDetailsErrorCode.AssignmentConflict,
                "The task has an unsupported direct assignee type.")
        };
        return new AssigneeSnapshot(
            new ProjectStructureTaskResourceSelection(kind, assignment.PartyId),
            assignment);
    }

    private async Task RestoreAssigneeAsync(
        Guid projectId,
        string taskNodeId,
        AssigneeSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments = snapshot.Assignment is null
            ? []
            :
            [
                new ProjectPartyAssignmentUpsertRequest
                {
                    ProjectId = projectId,
                    PartyId = snapshot.Assignment.PartyId,
                    Role = snapshot.Assignment.Role,
                    NodeKey = taskNodeId,
                    IsPrimary = snapshot.Assignment.IsPrimary,
                    AllocationPercent = snapshot.Assignment.AllocationPercent,
                    StartsOn = ToDateOnly(snapshot.Assignment.StartsAtUtc),
                    EndsOn = ToDateOnly(snapshot.Assignment.EndsAtUtc),
                    Source = snapshot.Assignment.Source,
                    Notes = snapshot.Assignment.Notes
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
            throw new InvalidOperationException(
                $"The canonical task assignee could not be restored: {string.Join(" ", assignmentResult.Errors.Select(error => error.Message))}");
        }

        var updatedTask = await projectWorkbenchService.MutateObjectMetadataAsync(
            projectId,
            taskNodeId,
            metadata =>
            {
                metadata.WorkItem ??= new ProjectWorkItemMetadata
                {
                    WorkItemKind = ProjectWorkItemKind.Task
                };
                metadata.WorkItem.AssigneePartyDisplayName = snapshot.Assignment?.PartyDisplayName ?? string.Empty;
            },
            cancellationToken);
        if (updatedTask is null)
        {
            throw new InvalidOperationException(
                $"Task '{taskNodeId}' disappeared while its previous assignee display snapshot was being restored.");
        }
    }

    private static void Validate(Guid projectId, ProjectStructureTaskDetailsUpdateRequest request)
    {
        if (projectId == Guid.Empty)
        {
            throw new ProjectStructureTaskDetailsException(
                ProjectStructureTaskDetailsErrorCode.InvalidRequest,
                "A project is required to update task details.");
        }

        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.CurrentTitle) || string.IsNullOrWhiteSpace(request.ProposedTitle))
        {
            throw new ProjectStructureTaskDetailsException(
                ProjectStructureTaskDetailsErrorCode.InvalidRequest,
                "Task titles cannot be empty.");
        }

        if ((request.CurrentProgressPercent != ProjectProgressPolicy.UntrackedPercent &&
             !ProjectProgressPolicy.IsTrackedPercent(request.CurrentProgressPercent)) ||
            !ProjectProgressPolicy.IsTrackedPercent(request.ProposedProgressPercent))
        {
            throw new ProjectStructureTaskDetailsException(
                ProjectStructureTaskDetailsErrorCode.InvalidRequest,
                "Current task progress must be untracked (-1) or between 0 and 100 percent; proposed progress must be between 0 and 100 percent.");
        }

        if (request.ScheduleChange is not null && request.ScheduleChange.TaskId != request.TaskId)
        {
            throw new ProjectStructureTaskDetailsException(
                ProjectStructureTaskDetailsErrorCode.InvalidRequest,
                "The schedule change does not belong to the edited task.");
        }

        if (!request.AssigneeChanged || request.ProposedAssignee is null)
        {
            return;
        }

        if (request.ProposedAssignee.Kind is not (ProjectStructureTaskResourceKind.Person or ProjectStructureTaskResourceKind.Agent) ||
            request.ProposedAssignee.ResourceId == Guid.Empty ||
            request.ProposedAssignee.VersionId.HasValue)
        {
            throw new ProjectStructureTaskDetailsException(
                ProjectStructureTaskDetailsErrorCode.InvalidRequest,
                "Only a person or agent can be assigned directly to a task.");
        }
    }

    private static string Mask(Guid value)
    {
        var formatted = value.ToString("N");
        return $"{formatted[..6]}...{formatted[^4..]}";
    }

    private static string Mask(string value)
        => value.Length <= 12 ? value : $"{value[..6]}...{value[^4..]}";

    private static DateOnly? ToDateOnly(DateTimeOffset? value)
        => value.HasValue ? DateOnly.FromDateTime(value.Value.UtcDateTime) : null;

    private sealed record AssigneeSnapshot(
        ProjectStructureTaskResourceSelection? Selection,
        ProjectPartyAssignmentDetail? Assignment);
}
