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
    ProjectStructureTaskEditingSessions editingSessions,
    ProjectStructureTaskResourceAttachmentService taskResourceAttachmentService,
    DialogService dialogService,
    NotificationService notificationService,
    ICurrencyFormatter currencyFormatter,
    ILogger<ProjectStructureGanttTaskEditCoordinator> logger)
{
    public async Task OpenAsync(
        ProjectStructureGanttTaskEditContext context,
        GanttTaskId taskId,
        Func<Func<CancellationToken, ValueTask>, Task> reloadAuthoritativeProject,
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

        var options = new ProjectStructureGanttProjectionOptions(projectedTask.Start, TimeSpan.FromHours(8),
            context.Projection.Tasks.Select(task => task.Id.Value).ToArray());
        await using var editing = editingSessions.Create(admission, taskNode, context.IsCurrent, currencyFormatter.CurrencyCode, options);
        try {
            await editing.OpenAsync(cancellationToken);
        } catch (Exception failure) when (failure is InvalidOperationException or UnauthorizedAccessException or FormatException) {
            if (context.IsCurrent()) {
                notificationService.Error("Task details unavailable", "The original Task, current dependencies and Structure access could not be verified. Refresh before reopening.");
            }
            logger.LogWarning("Could not open Gantt Task comparison authority; failure type {FailureType}.", failure.GetType().Name);
            return;
        }
        var assigneeResolution = editing.Baseline.Assignees;
        await editing.AuthorizeReadAsync(cancellationToken);
        var (resourceOptions, resourceWarnings) = await LoadResourceOptionsAsync(
            context,
            assigneeResolution,
            cancellationToken);
        await editing.AuthorizeReadAsync(cancellationToken);
        if (!context.IsCurrent()) {
            return;
        }
        var editModel = ProjectStructureTaskForm.GanttModel(editing.Baseline);
        var session = new ProjectTaskDialogSession(context.IsCurrent, () => reloadAuthoritativeProject(editing.AuthorizeReadAsync), logger, [taskId.Value]);
        editing.CanAccept = () => session.CanAcceptComparison;
        editing.Accepted = session.AcceptComparison;
        await dialogService.OpenAsync<ProjectStructureGanttTaskDialog>(
            "Edit project task",
            new Dictionary<string, object?>
            {
                [nameof(ProjectStructureGanttTaskDialog.EditSubmitted)] =
                    new Func<ProjectStructureTaskEditDialogResult, Task<PlanningTaskSaveResult>>(draft =>
                        session.SubmitAsync(() => SaveAsync(context, editing, draft, session))),
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
                [nameof(ProjectStructureGanttTaskDialog.ComparisonOwner)] = editing,
                [nameof(ProjectStructureGanttTaskDialog.ResourceOptions)] = resourceOptions,
                [nameof(ProjectStructureGanttTaskDialog.ResourceWarnings)] = resourceWarnings,
                [nameof(ProjectStructureGanttTaskDialog.QuoteResolver)] =
                    new Func<ProjectStructureTaskResourceCostRequest, CancellationToken, Task<ProjectStructureTaskResourceCostQuote>>(
                        (request, token) => editing.ReadPrivateAsync(current => taskResourceCostService.GetQuoteAsync(request, current), token))
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
        ProjectStructureTaskEditingSession editing, ProjectStructureTaskEditDialogResult proposed, ProjectTaskDialogSession session) {
        await editing.AuthorizeWriteAsync(CancellationToken.None);
        var current = ProjectStructureTaskForm.GanttModel(editing.Baseline);
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
                scheduleChange = GanttSchedulePlanner.PlanInterval(editing.Baseline.Schedule!.Tasks, editing.Baseline.Schedule.Dependencies,
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
        var update = await taskDetailsService.UpdateWithPricingAsync(context.ProjectId, request, CancellationToken.None, editing.AuthorizeWriteAsync, editing.Baseline.Task.RecordId);
        session.TaskCommitted(update.Mutation.AffectedTaskIds.Select(id => id.Value).ToArray(), update.Pricing, proposed.AssigneeChanged);
        if (proposed.ResourceToAttach is not null && context.IsCurrent()) {
            var attachment = await taskResourceAttachmentService.AttachAfterTransitionAsync(context.ProjectId, current.TaskId.Value,
                proposed.ResourceToAttach, currentExecution, proposed.Execution ?? currentExecution, context.MutationOwner, CancellationToken.None, editing.AuthorizeWriteAsync);
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

    private static string Mask(Guid value)
    {
        var formatted = value.ToString("N");
        return $"{formatted[..6]}...{formatted[^4..]}";
    }

    private static string Mask(string value)
        => value.Length <= 12 ? value : $"{value[..6]}...{value[^4..]}";
}
