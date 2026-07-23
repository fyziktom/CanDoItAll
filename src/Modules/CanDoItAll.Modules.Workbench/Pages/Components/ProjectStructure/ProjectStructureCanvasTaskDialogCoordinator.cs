using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public delegate Task<ProjectStructureNode?> ProjectStructureCanvasTaskNodeCreator(
    CanvasWorkbenchCreateActionRequest request,
    Func<ProjectObjectCreateRequest, ProjectObjectCreateRequest> configureRequest);

public sealed record ProjectStructureCanvasTaskDialogContext(
    Guid ProjectId,
    IReadOnlyList<CanvasWorkbenchInputOption> RepositoryOptions,
    ProjectStructureCanvasTaskNodeCreator CreateTaskNodeAsync,
    Func<string?, Task> ReloadAuthoritativeProject);

public sealed class ProjectStructureCanvasTaskDialogCoordinator(
    ProjectStructureWorkItemAssigneeService assigneeService,
    ProjectStructureTaskResourceCostService resourceCostService,
    ProjectStructureTaskApplicationService taskApplicationService,
    ProjectWorkbenchService projectWorkbenchService,
    DialogService dialogService,
    NotificationService notificationService,
    ILogger<ProjectStructureCanvasTaskDialogCoordinator> logger)
{
    private const string AssignmentSource = "project-structure-task-dialog";

    public async Task OpenCreateAsync(
        ProjectStructureCanvasTaskDialogContext context,
        CanvasWorkbenchCreateActionRequest createRequest,
        CancellationToken cancellationToken = default)
    {
        ValidateContext(context);
        ArgumentNullException.ThrowIfNull(createRequest);

        IReadOnlyList<ProjectStructureTaskResourceOption> assigneeOptions = [];
        IReadOnlyList<string> assigneeWarnings = [];
        try
        {
            assigneeOptions = await assigneeService.ListOptionsAsync(
                context.ProjectId,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            assigneeWarnings =
                ["People and agents could not be loaded. You can still create the task without an assignee."];
            logger.LogWarning(
                exception,
                "Failed to load canvas task assignees for project {ProjectId}.",
                context.ProjectId);
        }

        var result = await dialogService.OpenAsync<ProjectStructureTaskCreateDialog>(
            "Add task",
            new Dictionary<string, object?>
            {
                [nameof(ProjectStructureTaskCreateDialog.ProjectId)] = context.ProjectId,
                [nameof(ProjectStructureTaskCreateDialog.CreateRequest)] = createRequest,
                [nameof(ProjectStructureTaskCreateDialog.RepositoryOptions)] = context.RepositoryOptions,
                [nameof(ProjectStructureTaskCreateDialog.AssigneeOptions)] = assigneeOptions,
                [nameof(ProjectStructureTaskCreateDialog.AssigneeWarnings)] = assigneeWarnings,
                [nameof(ProjectStructureTaskCreateDialog.QuoteResolver)] =
                    new Func<ProjectStructureTaskResourceCostRequest, CancellationToken, Task<ProjectStructureTaskResourceCostQuote>>(
                        resourceCostService.GetQuoteAsync)
            },
            new DialogOptions
            {
                Eyebrow = "Project structure",
                Subtitle = "Create the task at the selected canvas location and optionally assign a CRM person or synchronized AI agent directly to it.",
                Size = ModalSize.Wide,
                DenseChrome = true,
                TestId = "project-structure-task-create-dialog",
                AriaLabel = "Add project structure task",
                ChromeCloseResult = null
            },
            cancellationToken);

        if (result is ProjectStructureTaskDialogResult submission)
        {
            await CreateAsync(context, submission, cancellationToken);
        }
    }

    public async Task OpenEditAsync(
        ProjectStructureCanvasTaskDialogContext context,
        ProjectStructureNode taskNode,
        CanvasWorkbenchCreateActionRequest editRequest,
        CancellationToken cancellationToken = default)
    {
        ValidateContext(context);
        ArgumentNullException.ThrowIfNull(taskNode);
        ArgumentNullException.ThrowIfNull(editRequest);

        ProjectStructureTaskEditState snapshot;
        try
        {
            snapshot = ProjectStructureCanvasTaskCommitPolicy.Read(taskNode);
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException)
        {
            notificationService.Error("Task could not be opened", exception.Message);
            logger.LogWarning(
                exception,
                "Could not prepare canvas task details for project {ProjectId} and task {TaskNodeId}.",
                context.ProjectId,
                taskNode.Id);
            return;
        }

        IReadOnlyList<ProjectStructureTaskResourceOption> assigneeOptions = [];
        IReadOnlyList<string> assigneeWarnings = [];
        var assigneeResolution = ProjectStructureTaskAssigneeSelectionPolicy.Resolve(
            [],
            taskNode.Id);
        var canChangeDirectAssignee = true;
        var assignmentContextLoaded = false;
        try
        {
            var optionsTask = assigneeService.ListOptionsAsync(
                context.ProjectId,
                cancellationToken);
            var assignmentSnapshotTask = assigneeService.ReadAsync(
                context.ProjectId,
                taskNode.Id,
                cancellationToken);
            await Task.WhenAll(optionsTask, assignmentSnapshotTask);
            assigneeOptions = await optionsTask;
            assigneeResolution = ProjectStructureTaskAssigneeSelectionPolicy.Resolve(
                (await assignmentSnapshotTask).DirectAssignments,
                taskNode.Id);
            assignmentContextLoaded = true;
            canChangeDirectAssignee = assigneeResolution.CanChangeDirectAssignee;
            if (ResolveAssigneeWarning(assigneeResolution.Status) is { } warning)
            {
                assigneeWarnings = [warning];
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            canChangeDirectAssignee = false;
            assigneeWarnings =
                ["People and agents could not be loaded. Existing task fields can still be edited without changing the assignee."];
            logger.LogWarning(
                exception,
                "Failed to load canvas task edit assignees for project {ProjectId} and task {TaskNodeId}.",
                context.ProjectId,
                taskNode.Id);
        }

        var result = await dialogService.OpenAsync<ProjectStructureTaskCreateDialog>(
            "Edit task",
            new Dictionary<string, object?>
            {
                [nameof(ProjectStructureTaskCreateDialog.ProjectId)] = context.ProjectId,
                [nameof(ProjectStructureTaskCreateDialog.CreateRequest)] = editRequest,
                [nameof(ProjectStructureTaskCreateDialog.RepositoryOptions)] = context.RepositoryOptions,
                [nameof(ProjectStructureTaskCreateDialog.AssigneeOptions)] = assigneeOptions,
                [nameof(ProjectStructureTaskCreateDialog.AssigneeWarnings)] = assigneeWarnings,
                [nameof(ProjectStructureTaskCreateDialog.IsEditMode)] = true,
                [nameof(ProjectStructureTaskCreateDialog.InitialAssignee)] = assigneeResolution.Representative,
                [nameof(ProjectStructureTaskCreateDialog.InitialExecution)] = snapshot.Execution,
                [nameof(ProjectStructureTaskCreateDialog.CanChangeDirectAssignee)] = canChangeDirectAssignee,
                [nameof(ProjectStructureTaskCreateDialog.QuoteResolver)] =
                    new Func<ProjectStructureTaskResourceCostRequest, CancellationToken, Task<ProjectStructureTaskResourceCostQuote>>(
                        resourceCostService.GetQuoteAsync)
            },
            new DialogOptions
            {
                Eyebrow = "Project structure task",
                Subtitle = "Edit task details, pure effort, expected cost, execution state, and the direct CRM person or AI agent assignment.",
                Size = ModalSize.Wide,
                DenseChrome = true,
                TestId = "project-structure-task-edit-dialog",
                AriaLabel = "Edit project structure task",
                ChromeCloseResult = null
            },
            cancellationToken);

        if (result is ProjectStructureTaskDialogResult submission)
        {
            await SaveEditAsync(
                context,
                taskNode,
                submission,
                snapshot,
                assigneeResolution,
                assignmentContextLoaded,
                cancellationToken);
        }
    }

    private async Task CreateAsync(
        ProjectStructureCanvasTaskDialogContext context,
        ProjectStructureTaskDialogResult submission,
        CancellationToken cancellationToken)
    {
        if (!ProjectStructureCanvasCatalog.TryResolveCreateDefinition(
                ProjectStructureCanvasCatalog.WorkTaskActionId,
                out _))
        {
            notificationService.Error(
                "Task could not be created",
                "The canonical task definition is unavailable.");
            return;
        }

        ProjectStructureTaskCreateApplicationResult result;
        try
        {
            var estimate = RequireEstimate(submission);
            result = await taskApplicationService.CreateAsync(
                new ProjectStructureTaskCreateApplicationRequest(
                    context.ProjectId,
                    estimate,
                    submission.Assignee,
                    AssignmentSource),
                (pricing, _) => context.CreateTaskNodeAsync(
                    submission.CreateRequest,
                    request =>
                        ProjectStructureCanvasTaskCommitPolicy.ApplyCreate(
                            request,
                            pricing)),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Canvas task creation failed. ProjectId={ProjectId} AssigneeSelected={AssigneeSelected}",
                context.ProjectId,
                submission.Assignee is not null);
            notificationService.Error("Task could not be created", exception.Message);
            return;
        }

        var committedTask = result.Task;
        var pricingFeedback = ProjectStructureTaskPricingFeedback.BuildNotificationSuffix(
            result.Pricing);
        try
        {
            await context.ReloadAuthoritativeProject(committedTask.Id);
            notificationService.Success(
                "Task created",
                submission.Assignee is null
                    ? $"{committedTask.Title} was added to the project structure.{pricingFeedback}"
                    : $"{committedTask.Title} was added with its selected assignee.{pricingFeedback}");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Canvas task was committed but the project structure refresh failed. ProjectId={ProjectId} TaskNodeId={TaskNodeId}",
                context.ProjectId,
                committedTask.Id);
            notificationService.Warning(
                "Task created; refresh required",
                $"{committedTask.Title} was saved.{pricingFeedback} Reload the project structure to see the latest state.");
        }
    }

    private async Task SaveEditAsync(
        ProjectStructureCanvasTaskDialogContext context,
        ProjectStructureNode openedTask,
        ProjectStructureTaskDialogResult submission,
        ProjectStructureTaskEditState openedSnapshot,
        ProjectStructureTaskAssigneeSelectionResult openedAssigneeResolution,
        bool assignmentContextLoaded,
        CancellationToken cancellationToken)
    {
        if (!TryResolveEditAction(
                submission.CreateRequest.ActionId,
                out var createActionId) ||
            !ProjectStructureCanvasCatalog.TryResolveCreateDefinition(
                createActionId,
                out var definition))
        {
            notificationService.Error(
                "Task could not be saved",
                "The task edit definition is no longer available.");
            return;
        }

        ProjectStructureTaskEditApplicationResult<ProjectStructureNode> result;
        try
        {
            var proposedExecution =
                submission.Execution ?? openedSnapshot.Execution;
            var assignmentWasChanged =
                assignmentContextLoaded &&
                submission.Assignee !=
                    openedAssigneeResolution.Representative;
            if (assignmentWasChanged &&
                !openedAssigneeResolution.CanChangeDirectAssignee)
            {
                throw new InvalidOperationException(
                    "This task has direct assignments that are read-only here. Reload the project before changing its assignee.");
            }

            result = await taskApplicationService.EditAsync(
                new ProjectStructureTaskEditApplicationRequest(
                    context.ProjectId,
                    openedTask.Id,
                    openedSnapshot,
                    RequireEstimate(submission),
                    proposedExecution,
                    assignmentWasChanged,
                    submission.Assignee,
                    AssignmentSource),
                async (commit, token) =>
                {
                    var update =
                        ProjectStructureNodeEditor.ComposeUpdate(
                            definition,
                            commit.CurrentTask,
                            submission.CreateRequest);
                    update =
                        ProjectStructureCanvasTaskCommitPolicy.ApplyEdit(
                            commit.CurrentTask,
                            update,
                            commit.ProposedExecution,
                            commit.Pricing,
                            commit.ProposedCostBasis);
                    return await projectWorkbenchService
                        .UpdateObjectIfMetadataAsync(
                            context.ProjectId,
                            commit.CurrentTask.Id,
                            update,
                            metadata =>
                                ProjectStructureCanvasTaskCommitPolicy
                                    .ValidateCurrentMetadata(
                                        metadata,
                                        commit.CurrentState),
                            token)
                        ?? throw new InvalidOperationException(
                            "The selected task is no longer available.");
                },
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Canvas task edit failed before a project structure update was committed. ProjectId={ProjectId} TaskNodeId={TaskNodeId}",
                context.ProjectId,
                openedTask.Id);
            notificationService.Error(
                "Task could not be saved",
                exception is InvalidOperationException or
                    ProjectStructureTaskApplicationException
                    ? exception.Message
                    : "The task update failed before it was committed. Review the values and try again.");
            return;
        }

        try
        {
            await context.ReloadAuthoritativeProject(openedTask.Id);
            notificationService.Success(
                "Task saved",
                $"{submission.CreateRequest.Title} was updated.{ProjectStructureTaskPricingFeedback.BuildNotificationSuffix(result.Pricing)}");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Canvas task was committed but the project structure refresh failed. ProjectId={ProjectId} TaskNodeId={TaskNodeId}",
                context.ProjectId,
                openedTask.Id);
            notificationService.Warning(
                "Task saved; refresh required",
                $"{submission.CreateRequest.Title} was saved.{ProjectStructureTaskPricingFeedback.BuildNotificationSuffix(result.Pricing)} Reload the project structure to see the latest state.");
        }
    }

    private static ProjectTaskEstimate RequireEstimate(
        ProjectStructureTaskDialogResult submission)
        => ProjectTaskEstimatePolicy.ValidateAndNormalize(
            submission.Estimate ??
            throw new InvalidOperationException(
                "The task estimate was not supplied by the task editor."));

    private static string? ResolveAssigneeWarning(
        ProjectStructureTaskAssigneeSelectionStatus status)
        => status switch
        {
            ProjectStructureTaskAssigneeSelectionStatus.MultipleWithPrimary =>
                "This task has multiple direct assignees. Its primary assignee is shown, but direct person or agent changes are disabled to preserve the complete assignment set.",
            ProjectStructureTaskAssigneeSelectionStatus.Ambiguous =>
                "This task has multiple direct assignees without one primary assignee. Direct person or agent changes are disabled to preserve the complete assignment set.",
            ProjectStructureTaskAssigneeSelectionStatus.UnsupportedPartyType =>
                "This task has a direct assignee type that cannot be edited here. Direct person or agent changes are disabled to preserve the complete assignment set.",
            _ => null
        };

    private static bool TryResolveEditAction(
        string actionId,
        out string createActionId)
    {
        createActionId = string.Empty;
        if (!actionId.StartsWith("edit:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        createActionId = actionId["edit:".Length..];
        return !string.IsNullOrWhiteSpace(createActionId);
    }

    private static void ValidateContext(
        ProjectStructureCanvasTaskDialogContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.ProjectId == Guid.Empty)
        {
            throw new ArgumentException(
                "A project is required for canvas task editing.",
                nameof(context));
        }

        ArgumentNullException.ThrowIfNull(context.RepositoryOptions);
        ArgumentNullException.ThrowIfNull(context.CreateTaskNodeAsync);
        ArgumentNullException.ThrowIfNull(context.ReloadAuthoritativeProject);
    }
}
