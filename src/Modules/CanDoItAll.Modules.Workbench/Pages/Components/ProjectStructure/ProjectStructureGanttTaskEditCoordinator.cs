using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Gantt;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public sealed record ProjectStructureGanttTaskEditContext(
    Guid ProjectId,
    ProjectStructureSurface Surface,
    ProjectStructureGanttProjectionResult Projection,
    IReadOnlyList<ProjectPartyAssignmentDetail> Assignments);

public sealed class ProjectStructureGanttTaskEditCoordinator(
    ProjectStructureTaskResourceService taskResourceService,
    ProjectStructureTaskDetailsService taskDetailsService,
    DialogService dialogService,
    NotificationService notificationService,
    ICurrencyFormatter currencyFormatter,
    ILogger<ProjectStructureGanttTaskEditCoordinator> logger)
{
    public async Task OpenAsync(
        ProjectStructureGanttTaskEditContext context,
        GanttTaskId taskId,
        Func<Task> reloadAuthoritativeProject,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(reloadAuthoritativeProject);

        var projectedTask = context.Projection.Tasks.FirstOrDefault(task => task.Id == taskId);
        var taskNode = context.Surface.Nodes.FirstOrDefault(node =>
            string.Equals(node.Id, taskId.Value, StringComparison.Ordinal));
        if (projectedTask is null || taskNode is null)
        {
            notificationService.Error(
                "Task details unavailable",
                "The selected task is no longer present in the authoritative project structure. Reload the project and try again.");
            return;
        }

        ProjectTaskEstimate estimate;
        ProjectStructureTaskResourceSelection? assignee;
        try
        {
            estimate = ReadEstimate(taskNode);
            assignee = ResolveAssignee(context.Assignments, taskId.Value);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ProjectStructureTaskDetailsException)
        {
            notificationService.Error("Task details unavailable", exception.Message);
            logger.LogWarning(
                "Could not prepare Gantt task details for project {ProjectId}, task {TaskId}; failure type {FailureType}.",
                Mask(context.ProjectId),
                Mask(taskId.Value),
                exception.GetType().Name);
            return;
        }

        var (resourceOptions, resourceWarnings) = await LoadAssigneeOptionsAsync(
            context,
            taskId.Value,
            assignee,
            cancellationToken);
        var editModel = new ProjectStructureGanttTaskEditModel(
            taskId,
            taskNode.Title,
            projectedTask.Start,
            projectedTask.End,
            Math.Clamp(taskNode.ProgressPercent, 0, 100),
            estimate,
            assignee,
            context.Projection.IsProjectionOnly(projectedTask));
        var result = await dialogService.OpenAsync<ProjectStructureGanttTaskDialog>(
            "Edit project task",
            new Dictionary<string, object?>
            {
                [nameof(ProjectStructureGanttTaskDialog.DefaultStartUtc)] = projectedTask.Start,
                [nameof(ProjectStructureGanttTaskDialog.DefaultEndUtc)] = projectedTask.End,
                [nameof(ProjectStructureGanttTaskDialog.DefaultCurrencyCode)] = currencyFormatter.CurrencyCode,
                [nameof(ProjectStructureGanttTaskDialog.EditModel)] = editModel,
                [nameof(ProjectStructureGanttTaskDialog.ResourceOptions)] = resourceOptions,
                [nameof(ProjectStructureGanttTaskDialog.ResourceWarnings)] = resourceWarnings
            },
            new DialogOptions
            {
                Eyebrow = "Gantt task details",
                Subtitle = "Delivery dates, actual progress, pure effort, expected cost, and direct assignee are edited from their authoritative project task.",
                Size = ModalSize.Wide,
                DenseChrome = true,
                TestId = "project-structure-gantt-task-edit-dialog",
                AriaLabel = "Edit project task",
                ChromeCloseResult = null
            },
            cancellationToken);

        if (result is ProjectStructureTaskEditDialogResult editResult)
        {
            await SaveAsync(context, editModel, editResult, reloadAuthoritativeProject, cancellationToken);
        }
    }

    private async Task SaveAsync(
        ProjectStructureGanttTaskEditContext context,
        ProjectStructureGanttTaskEditModel current,
        ProjectStructureTaskEditDialogResult proposed,
        Func<Task> reloadAuthoritativeProject,
        CancellationToken cancellationToken)
    {
        if (proposed.TaskId != current.TaskId)
        {
            notificationService.Error(
                "Task details could not be saved",
                "The task detail result does not belong to the selected task.");
            return;
        }

        GanttTaskScheduleChangeRequest? scheduleChange = null;
        if (proposed.StartUtc != current.StartUtc || proposed.EndUtc != current.EndUtc)
        {
            try
            {
                scheduleChange = GanttSchedulePlanner.PlanInterval(
                    context.Projection.Tasks,
                    context.Projection.Dependencies,
                    current.TaskId,
                    proposed.StartUtc,
                    proposed.EndUtc,
                    minimumTaskDuration: TimeSpan.FromMinutes(15));
            }
            catch (Exception exception) when (exception is GanttScheduleException or ArgumentOutOfRangeException)
            {
                notificationService.Error("Task schedule change rejected", exception.Message);
                return;
            }
        }

        var request = new ProjectStructureTaskDetailsUpdateRequest(
            current.TaskId,
            current.Title,
            proposed.Title,
            current.ProgressPercent,
            proposed.ProgressPercent,
            current.Estimate,
            proposed.Estimate,
            scheduleChange,
            proposed.AssigneeChanged,
            proposed.Assignee);
        var mutationCommitted = false;
        try
        {
            var result = await taskDetailsService.UpdateAsync(context.ProjectId, request, cancellationToken);
            mutationCommitted = true;
            await reloadAuthoritativeProject();
            notificationService.Success(
                "Task details saved",
                $"{proposed.Title} was saved; {result.AffectedTaskIds.Count} project task(s) were affected.");
        }
        catch (ProjectStructureGanttMutationException exception)
        {
            notificationService.Error("Task details change rejected", exception.Message);
            logger.LogWarning(
                "Rejected Gantt task detail update for project {ProjectId}, task {TaskId}, with code {ErrorCode}.",
                Mask(context.ProjectId),
                Mask(current.TaskId.Value),
                exception.Code);
        }
        catch (ProjectStructureTaskDetailsException exception)
        {
            notificationService.Error("Task details could not be saved", exception.Message);
            logger.LogWarning(
                "Rejected Gantt task detail orchestration for project {ProjectId}, task {TaskId}, with code {ErrorCode}.",
                Mask(context.ProjectId),
                Mask(current.TaskId.Value),
                exception.Code);
        }
        catch (ProjectStructureAgentException exception)
        {
            notificationService.Error("Task details could not be saved", exception.Message);
            logger.LogWarning(
                "Rejected Gantt task detail update for project {ProjectId}, task {TaskId}, with application error {ErrorCode}.",
                Mask(context.ProjectId),
                Mask(current.TaskId.Value),
                exception.ErrorCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (mutationCommitted)
            {
                notificationService.Warning(
                    "Task details saved; reload required",
                    "The change was saved, but the authoritative project could not be reloaded. Reload this page before making another change.");
            }
            else
            {
                notificationService.Error(
                    "Task details save failed",
                    "The task details could not be saved. The project remains unchanged.");
            }

            logger.LogError(
                "Gantt task detail update failed for project {ProjectId}, task {TaskId}, after commit state {MutationCommitted}; failure type {FailureType}.",
                Mask(context.ProjectId),
                Mask(current.TaskId.Value),
                mutationCommitted,
                exception.GetType().Name);
        }
    }

    private async Task<(IReadOnlyList<ProjectStructureTaskResourceOption> Options, IReadOnlyList<string> Warnings)> LoadAssigneeOptionsAsync(
        ProjectStructureGanttTaskEditContext context,
        string taskNodeId,
        ProjectStructureTaskResourceSelection? assignee,
        CancellationToken cancellationToken)
    {
        try
        {
            var options = (await taskResourceService.ListOptionsAsync(context.ProjectId, cancellationToken))
                .Where(static option => option.Kind is ProjectStructureTaskResourceKind.Person or ProjectStructureTaskResourceKind.Agent)
                .ToArray();
            return (IncludeCurrentAssigneeOption(options, context.Assignments, taskNodeId, assignee), []);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Failed to load task assignee choices for project {ProjectId}; failure type {FailureType}.",
                Mask(context.ProjectId),
                exception.GetType().Name);
            return (
                IncludeCurrentAssigneeOption([], context.Assignments, taskNodeId, assignee),
                ["People and agents could not be loaded. Existing task details can still be saved without changing the assignee."]);
        }
    }

    private static ProjectTaskEstimate ReadEstimate(ProjectStructureNode taskNode)
    {
        var workItem = ProjectObjectMetadataSerializer.Parse(taskNode.MetadataJson).WorkItem;
        return workItem is null
            ? ProjectTaskEstimate.Empty()
            : ProjectTaskEstimatePolicy.ValidateAndNormalize(new ProjectTaskEstimate(
                workItem.ExpectedEffortHours,
                workItem.ExpectedEffortUnit,
                workItem.ExpectedCostAmount,
                workItem.ExpectedCostCurrencyCode));
    }

    private static ProjectStructureTaskResourceSelection? ResolveAssignee(
        IEnumerable<ProjectPartyAssignmentDetail> assignments,
        string taskNodeId)
    {
        var taskAssignments = assignments
            .Where(assignment =>
                assignment.Role == ProjectPartyAssignmentRole.WorkItemAssignee &&
                string.Equals(assignment.NodeKey, taskNodeId, StringComparison.Ordinal))
            .ToArray();
        if (taskAssignments.Length > 1)
        {
            throw new ProjectStructureTaskDetailsException(
                ProjectStructureTaskDetailsErrorCode.AssignmentConflict,
                "The task has multiple direct assignees. Resolve the assignment conflict before editing it from the Gantt chart.");
        }

        if (taskAssignments.Length == 0)
        {
            return null;
        }

        var assignment = taskAssignments[0];
        var kind = assignment.PartyType switch
        {
            ProjectPartyType.Person => ProjectStructureTaskResourceKind.Person,
            ProjectPartyType.AiAgent => ProjectStructureTaskResourceKind.Agent,
            _ => throw new ProjectStructureTaskDetailsException(
                ProjectStructureTaskDetailsErrorCode.AssignmentConflict,
                "The task has an unsupported direct assignee type.")
        };
        return new ProjectStructureTaskResourceSelection(kind, assignment.PartyId);
    }

    private static IReadOnlyList<ProjectStructureTaskResourceOption> IncludeCurrentAssigneeOption(
        IReadOnlyList<ProjectStructureTaskResourceOption> options,
        IReadOnlyList<ProjectPartyAssignmentDetail> assignments,
        string taskNodeId,
        ProjectStructureTaskResourceSelection? assignee)
    {
        if (assignee is null || options.Any(option =>
                option.Kind == assignee.Kind && option.ResourceId == assignee.ResourceId))
        {
            return options;
        }

        var assignment = assignments.Single(item =>
            item.Role == ProjectPartyAssignmentRole.WorkItemAssignee &&
            string.Equals(item.NodeKey, taskNodeId, StringComparison.Ordinal));
        return options
            .Append(new ProjectStructureTaskResourceOption(
                assignee.Kind,
                assignee.ResourceId,
                VersionId: null,
                assignment.PartyDisplayName,
                assignment.PartyTypeLabel,
                string.Empty,
                IsFavorite: false,
                IsSensitive: false))
            .OrderBy(static option => option.Kind)
            .ThenBy(static option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string Mask(Guid value)
    {
        var formatted = value.ToString("N");
        return $"{formatted[..6]}...{formatted[^4..]}";
    }

    private static string Mask(string value)
        => value.Length <= 12 ? value : $"{value[..6]}...{value[^4..]}";
}
