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
    ProjectStructureAgentContext MutationOwner);

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
            notificationService.Error("Task details unavailable", exception.Message);
            logger.LogWarning(
                "Could not prepare Gantt task details for project {ProjectId}, task {TaskId}; failure type {FailureType}.",
                Mask(context.ProjectId),
                Mask(taskId.Value),
                exception.GetType().Name);
            return;
        }

        var (resourceOptions, resourceWarnings) = await LoadResourceOptionsAsync(
            context,
            taskId.Value,
            assigneeResolution.Representative,
            cancellationToken);
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
        var result = await dialogService.OpenAsync<ProjectStructureGanttTaskDialog>(
            "Edit project task",
            new Dictionary<string, object?>
            {
                [nameof(ProjectStructureGanttTaskDialog.ProjectId)] = context.ProjectId,
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

        if (!TryValidateResourceToAttach(proposed.ResourceToAttach, out var resourceValidationError))
        {
            notificationService.Error(
                "Task resource could not be attached",
                resourceValidationError);
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

        var currentExecution = current.Execution ?? ProjectTaskExecutionSnapshot.Unknown;
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
            proposed.Assignee,
            currentExecution,
            proposed.Execution ?? currentExecution,
            current.ExpectedCostBasis,
            current.DirectAssignmentRevision);
        var mutationCommitted = false;
        ProjectStructureTaskEstimateRefreshResult? committedPricing = null;
        try
        {
            var update = await taskDetailsService.UpdateWithPricingAsync(
                context.ProjectId,
                request,
                cancellationToken);
            committedPricing = update.Pricing;
            mutationCommitted = true;
            if (proposed.ResourceToAttach is not null)
            {
                try
                {
                    var attachmentResult = await taskResourceAttachmentService.AttachAfterTransitionAsync(
                        context.ProjectId,
                        current.TaskId.Value,
                        proposed.ResourceToAttach,
                        currentExecution,
                        proposed.Execution ?? currentExecution,
                        context.MutationOwner,
                        cancellationToken);
                    committedPricing = attachmentResult.Pricing;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    await TryReloadAfterPartialSaveAsync(
                        context.ProjectId,
                        current.TaskId.Value,
                        proposed.ResourceToAttach.Kind,
                        reloadAuthoritativeProject);
                    notificationService.Warning(
                        "Task details partially saved",
                        "The task changes were saved, but resource pricing or attachment was canceled. Any newly created attachment was rolled back; reload before trying again.");
                    logger.LogWarning(
                        "Task details were committed before resource attachment pricing completed. ProjectId={ProjectId} TaskId={TaskId} ResourceKind={ResourceKind}",
                        Mask(context.ProjectId),
                        Mask(current.TaskId.Value),
                        proposed.ResourceToAttach.Kind);
                    return;
                }
                catch (Exception exception)
                {
                    await TryReloadAfterPartialSaveAsync(
                        context.ProjectId,
                        current.TaskId.Value,
                        proposed.ResourceToAttach.Kind,
                        reloadAuthoritativeProject);
                    if (exception is ProjectStructureAgentException
                        {
                            ErrorCode: ProjectStructureTaskResourceAttachmentService.CompensationFailedErrorCode
                        })
                    {
                        notificationService.Error(
                            "Task resource requires attention",
                            exception.Message);
                    }
                    else
                    {
                        notificationService.Warning(
                            "Task details partially saved",
                            "The task changes were saved, but the selected workflow or process could not be priced and attached. Any newly created attachment was rolled back; reload before trying again.");
                    }

                    logger.LogWarning(
                        exception,
                        "Task details were committed but attached-resource pricing did not complete. ProjectId={ProjectId} TaskId={TaskId} ResourceKind={ResourceKind} FailureType={FailureType}",
                        Mask(context.ProjectId),
                        Mask(current.TaskId.Value),
                        proposed.ResourceToAttach.Kind,
                        exception.GetType().Name);
                    return;
                }
            }

            await reloadAuthoritativeProject();
            notificationService.Success(
                "Task details saved",
                proposed.ResourceToAttach is null
                    ? $"{proposed.Title} was saved; {update.Mutation.AffectedTaskIds.Count} project task(s) were affected.{BuildPricingSummary(committedPricing)}"
                    : $"{proposed.Title} was saved with its selected {proposed.ResourceToAttach.Kind.ToString().ToLowerInvariant()}; {update.Mutation.AffectedTaskIds.Count} project task(s) were affected.{BuildPricingSummary(committedPricing)}");
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
                    $"The change was saved, but the authoritative project could not be reloaded.{BuildPricingSummary(committedPricing)} Reload this page before making another change.");
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

    private async Task TryReloadAfterPartialSaveAsync(
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskResourceKind resourceKind,
        Func<Task> reloadAuthoritativeProject)
    {
        try
        {
            await reloadAuthoritativeProject();
        }
        catch (Exception reloadFailure)
        {
            logger.LogError(
                reloadFailure,
                "Task details were partially saved and the authoritative project could not be reloaded. ProjectId={ProjectId} TaskId={TaskId} ResourceKind={ResourceKind}",
                Mask(projectId),
                Mask(taskNodeId),
                resourceKind);
        }
    }

    private async Task<(IReadOnlyList<ProjectStructureTaskResourceOption> Options, IReadOnlyList<string> Warnings)> LoadResourceOptionsAsync(
        ProjectStructureGanttTaskEditContext context,
        string taskNodeId,
        ProjectStructureTaskResourceSelection? assignee,
        CancellationToken cancellationToken)
    {
        try
        {
            var options = await taskResourceService.ListOptionsAsync(context.ProjectId, cancellationToken);
            return (IncludeCurrentAssigneeOption(options, context.Assignments, taskNodeId, assignee), []);
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
                IncludeCurrentAssigneeOption([], context.Assignments, taskNodeId, assignee),
                ["Task resources could not be loaded. Existing task details can still be saved without changing the assignee."]);
        }
    }

    private static bool TryValidateResourceToAttach(
        ProjectStructureTaskResourceSelection? resource,
        out string validationError)
    {
        validationError = string.Empty;
        if (resource is null)
        {
            return true;
        }

        try
        {
            ProjectStructureTaskResourceSelectionPolicy.Validate(resource);
        }
        catch (ProjectStructureAgentException exception)
        {
            validationError = exception.Message;
            return false;
        }

        if (resource.Kind is not (ProjectStructureTaskResourceKind.Workflow or ProjectStructureTaskResourceKind.Process))
        {
            validationError = "Only a workflow or process can be staged as an attached task resource.";
            return false;
        }

        return true;
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

        var assignment = assignments.FirstOrDefault(item =>
            item.Role == ProjectPartyAssignmentRole.WorkItemAssignee &&
            string.Equals(item.NodeKey, taskNodeId, StringComparison.Ordinal) &&
            item.PartyId == assignee.ResourceId &&
            IsCompatibleAssigneeType(item.PartyType, assignee.Kind));
        if (assignment is null)
        {
            return options;
        }

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

    private static bool IsCompatibleAssigneeType(
        ProjectPartyType partyType,
        ProjectStructureTaskResourceKind resourceKind)
        => (partyType, resourceKind) is
            (ProjectPartyType.Person, ProjectStructureTaskResourceKind.Person) or
            (ProjectPartyType.AiAgent, ProjectStructureTaskResourceKind.Agent);

    private static string Mask(Guid value)
    {
        var formatted = value.ToString("N");
        return $"{formatted[..6]}...{formatted[^4..]}";
    }

    private static string Mask(string value)
        => value.Length <= 12 ? value : $"{value[..6]}...{value[^4..]}";
}
