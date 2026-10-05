using CanDoItAll.Workbench.Planning.UI;
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
    IReadOnlyList<ProjectPartyAssignmentDetail> Assignments,
    ProjectStructureAgentContext MutationOwner) {
    public Func<bool> IsCurrent { get; init; } = static () => true;
}

public sealed class ProjectStructureGanttTaskEditCoordinator(
    ProjectStructureTaskResourceService taskResourceService,
    ProjectStructureTaskResourceCostService taskResourceCostService,
    ProjectStructureTaskDetailsService taskDetailsService,
    ProjectStructureTaskResourceAttachmentService taskResourceAttachmentService,
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
        var admission = ProjectAssignmentAdmission.Require(context.ProjectId, context.MutationOwner.ExpectedProjectAdmission);

        var projectedTask = context.Projection.Tasks.FirstOrDefault(task => task.Id == taskId);
        var taskNode = context.Surface.Nodes.FirstOrDefault(node =>
            string.Equals(node.Id, taskId.Value, StringComparison.Ordinal));
        if (projectedTask is null || taskNode is null)
        {
            if (context.IsCurrent()) {
                notificationService.Error(
                    "Task details unavailable",
                    "The selected task is no longer present in the authoritative project structure. Reload the project and try again.");
            }
            return;
        }

        ProjectTaskEstimate estimate;
        ProjectTaskExecutionSnapshot execution;
        ProjectTaskExpectedCostBasis? expectedCostBasis;
        long directAssignmentRevision;
        ProjectStructureTaskAssigneeSelectionResult assigneeResolution;
        try
        {
            var workItem = ProjectObjectMetadataSerializer.Parse(taskNode.MetadataJson).WorkItem;
            estimate = ReadEstimate(workItem);
            execution = ReadExecution(workItem);
            expectedCostBasis = workItem?.ExpectedCostBasis;
            directAssignmentRevision =
                workItem?.DirectAssignmentRevision ?? 0;
            assigneeResolution = ProjectStructureTaskAssigneeSelectionPolicy.Resolve(
                context.Assignments,
                taskId.Value);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ProjectStructureTaskDetailsException)
        {
            if (context.IsCurrent()) {
                notificationService.Error("Task details unavailable", exception.Message);
            }
            logger.LogWarning(
                "Could not prepare Gantt task details for project {ProjectId}, task {TaskId}; failure type {FailureType}.",
                Mask(context.ProjectId),
                Mask(taskId.Value),
                exception.GetType().Name);
            return;
        }

        var (resourceOptions, resourceWarnings) = await LoadResourceOptionsAsync(
            context,
            assigneeResolution,
            cancellationToken);
        if (!context.IsCurrent()) {
            return;
        }
        var editModel = new ProjectStructureGanttTaskEditModel(
            taskId,
            taskNode.Title,
            projectedTask.Start,
            projectedTask.End,
            taskNode.ProgressPercent,
            estimate,
            assigneeResolution.Representative,
            context.Projection.IsProjectionOnly(projectedTask),
            assigneeResolution.CanChangeDirectAssignee,
            execution,
            expectedCostBasis,
            directAssignmentRevision);
        var session = new ProjectTaskDialogSession(context.IsCurrent, reloadAuthoritativeProject, logger, [taskId.Value]);
        await dialogService.OpenAsync<ProjectStructureGanttTaskDialog>(
            "Edit project task",
            new Dictionary<string, object?>
            {
                [nameof(ProjectStructureGanttTaskDialog.EditSubmitted)] =
                    new Func<ProjectStructureTaskEditDialogResult, Task<PlanningTaskSaveResult>>(draft =>
                        session.SubmitAsync(() => SaveAsync(context, editModel, draft, session))),
                [nameof(ProjectStructureGanttTaskDialog.Readback)] = new Func<Task<PlanningTaskSaveResult>>(session.ReadbackAsync),
                [nameof(ProjectStructureGanttTaskDialog.ProjectId)] = context.ProjectId,
                [nameof(ProjectStructureGanttTaskDialog.QuoteContext)] = new ProjectTaskQuoteContext(
                    admission.DatabaseProfileId,
                    admission.LifetimeId,
                    Guid.NewGuid()),
                [nameof(ProjectStructureGanttTaskDialog.DefaultStartUtc)] = projectedTask.Start,
                [nameof(ProjectStructureGanttTaskDialog.DefaultEndUtc)] = projectedTask.End,
                [nameof(ProjectStructureGanttTaskDialog.DefaultCurrencyCode)] = currencyFormatter.CurrencyCode,
                [nameof(ProjectStructureGanttTaskDialog.EditModel)] = editModel,
                [nameof(ProjectStructureGanttTaskDialog.ResourceOptions)] = resourceOptions,
                [nameof(ProjectStructureGanttTaskDialog.ResourceWarnings)] = resourceWarnings,
                [nameof(ProjectStructureGanttTaskDialog.QuoteResolver)] =
                    new Func<ProjectStructureTaskResourceCostRequest, CancellationToken, Task<ProjectStructureTaskResourceCostQuote>>(
                        taskResourceCostService.GetQuoteAsync)
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

    }

    private async Task SaveAsync(ProjectStructureGanttTaskEditContext context,
        ProjectStructureGanttTaskEditModel current, ProjectStructureTaskEditDialogResult proposed, ProjectTaskDialogSession session) {
        if (proposed.TaskId != current.TaskId) {
            session.Reject("The task submission does not belong to this editor.");
            return;
        }
        if (proposed.ResourceToAttach is { } resource) {
            ProjectStructureTaskResourceSelectionPolicy.ValidateDefinitionAttachment(resource);
        }
        GanttTaskScheduleChangeRequest? scheduleChange = null;
        if (proposed.StartUtc != current.StartUtc || proposed.EndUtc != current.EndUtc) {
            if (current.ScheduleReadOnly) {
                session.Reject("The projected interval is read-only in this editor.");
                return;
            }
            try {
                scheduleChange = GanttSchedulePlanner.PlanInterval(context.Projection.Tasks, context.Projection.Dependencies,
                    current.TaskId, proposed.StartUtc, proposed.EndUtc, minimumTaskDuration: TimeSpan.FromMinutes(15));
            } catch (Exception failure) when (failure is GanttScheduleException or ArgumentOutOfRangeException) {
                session.Reject("The proposed schedule is invalid. Review the interval and dependencies.");
                return;
            }
        }
        if (!current.CanChangeDirectAssignee && proposed.AssigneeChanged) {
            session.Reject("The complete direct-assignment set must be preserved.");
            return;
        }
        var currentExecution = current.Execution ?? ProjectTaskExecutionSnapshot.Unknown;
        var request = new ProjectStructureTaskDetailsUpdateRequest(current.TaskId, current.Title, proposed.Title,
            current.ProgressPercent, proposed.ProgressPercent, current.Estimate, proposed.Estimate, scheduleChange,
            proposed.AssigneeChanged, proposed.Assignee, currentExecution, proposed.Execution ?? currentExecution,
            current.ExpectedCostBasis, current.DirectAssignmentRevision) {
            ExpectedProjectAdmission = context.MutationOwner.ExpectedProjectAdmission, MutationOwner = context.MutationOwner
        };
        var update = await taskDetailsService.UpdateWithPricingAsync(context.ProjectId, request, CancellationToken.None);
        session.TaskCommitted(update.Mutation.AffectedTaskIds.Select(id => id.Value).ToArray(), update.Pricing, proposed.AssigneeChanged);
        if (proposed.ResourceToAttach is not null && context.IsCurrent()) {
            var attachment = await taskResourceAttachmentService.AttachAfterTransitionAsync(context.ProjectId, current.TaskId.Value,
                proposed.ResourceToAttach, currentExecution, proposed.Execution ?? currentExecution, context.MutationOwner, CancellationToken.None);
            session.AttachmentCommitted(attachment);
        } else if (proposed.ResourceToAttach is not null) {
            throw new InvalidOperationException("The task committed, but the original view retired before attachment began.");
        }
        await session.RefreshAfterCommitAsync();
        if (context.IsCurrent()) {
            notificationService.Success("Task details saved", session.Result!.Message);
        }
    }

    private async Task<(IReadOnlyList<ProjectStructureTaskResourceOption> Options, IReadOnlyList<string> Warnings)> LoadResourceOptionsAsync(
        ProjectStructureGanttTaskEditContext context,
        ProjectStructureTaskAssigneeSelectionResult assigneeResolution,
        CancellationToken cancellationToken)
    {
        try
        {
            var options = await taskResourceService.ListOptionsAsync(context.ProjectId, cancellationToken);
            return (
                ProjectStructureTaskAssigneeSelectionPolicy
                    .IncludeRepresentativeOption(options, assigneeResolution),
                []);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Failed to load task resource choices for project {ProjectId}; failure type {FailureType}.",
                Mask(context.ProjectId),
                exception.GetType().Name);
            return (
                ProjectStructureTaskAssigneeSelectionPolicy
                    .IncludeRepresentativeOption([], assigneeResolution),
                ["Task resources could not be loaded. Existing task details can still be saved without changing the assignee."]);
        }
    }

    private static ProjectTaskEstimate ReadEstimate(ProjectWorkItemMetadata? workItem)
    {
        return workItem is null
            ? ProjectTaskEstimate.Empty()
            : ProjectTaskEstimatePolicy.ValidateAndNormalize(new ProjectTaskEstimate(
                workItem.ExpectedEffortHours,
                workItem.ExpectedEffortUnit,
                workItem.ExpectedCostAmount,
                workItem.ExpectedCostCurrencyCode));
    }

    private static ProjectTaskExecutionSnapshot ReadExecution(ProjectWorkItemMetadata? workItem)
        => workItem is null
            ? ProjectTaskExecutionSnapshot.Unknown
            : new ProjectTaskExecutionSnapshot(
                workItem.ExecutionState,
                workItem.ActualStartedAtUtc,
                workItem.ActualEndedAtUtc);

    private static string BuildPricingSummary(ProjectStructureTaskEstimateRefreshResult? pricing)
        => pricing is null
            ? string.Empty
            : ProjectStructureTaskPricingFeedback.BuildNotificationSuffix(pricing);

    private static string Mask(Guid value)
    {
        var formatted = value.ToString("N");
        return $"{formatted[..6]}...{formatted[^4..]}";
    }

    private static string Mask(string value)
        => value.Length <= 12 ? value : $"{value[..6]}...{value[^4..]}";
}
