using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private const string TaskCreateAssignmentSource = "project-structure-task-dialog";

    [Inject]
    private DialogService DialogService { get; set; } = default!;

    [Inject]
    private NotificationService NotificationService { get; set; } = default!;

    [Inject]
    private ProjectStructureWorkItemAssigneeService WorkItemAssigneeService { get; set; } = default!;

    [Inject]
    private ProjectStructureTaskResourceCostService TaskResourceCostService { get; set; } = default!;

    private Task ToggleSelectionWindowAsync()
        => ToggleWindowAsync(SelectionWindowKey);

    private Task ToggleHealthWindowAsync()
        => ToggleWindowAsync(HealthWindowKey);

    private Task ToggleToolboxWindowAsync()
        => ToggleWindowAsync(ToolboxWindowKey);

    private Task ToggleObjectIndexWindowAsync()
        => ToggleWindowAsync(ObjectIndexWindowKey);

    private Task HandleToolboxActionSelectedAsync(string actionId)
    {
        var action = ToolboxCreateGroups
            .SelectMany(group => group.Actions)
            .FirstOrDefault(candidate => string.Equals(candidate.ActionId, actionId, StringComparison.Ordinal));
        return action is null
            ? Task.CompletedTask
            : OpenCreateDialogAsync(action);
    }

    private Task HandleProjectHierarchySelectionChangedAsync(Guid? selectedProjectId)
    {
        HandleProjectHierarchySelectionChanged(new ChangeEventArgs
        {
            Value = selectedProjectId?.ToString()
        });
        return Task.CompletedTask;
    }

    private Task HandleTranscriptProviderChangedAsync(Guid? selectedProviderId)
    {
        HandleTranscriptProviderChanged(new ChangeEventArgs
        {
            Value = selectedProviderId?.ToString()
        });
        return Task.CompletedTask;
    }

    private Task HandleSummaryStatusChangedAsync((string NodeId, string Status) request)
    {
        return ChangeSummaryStatusAsync(
            request.NodeId,
            new ChangeEventArgs
            {
                Value = request.Status
            });
    }

    private async Task OpenTaskCreateDialogAsync(CanvasWorkbenchCreateActionRequest createRequest)
    {
        IReadOnlyList<ProjectStructureTaskResourceOption> assigneeOptions = [];
        IReadOnlyList<string> assigneeWarnings = [];
        try
        {
            assigneeOptions = await WorkItemAssigneeService.ListOptionsAsync(ProjectId, deferredCompletionCts.Token);
        }
        catch (OperationCanceledException) when (deferredCompletionCts.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            assigneeWarnings = ["People and agents could not be loaded. You can still create the task without an assignee."];
            Logger.LogWarning(
                exception,
                "Failed to load task assignees for project {ProjectId}.",
                ProjectId);
        }

        var result = await DialogService.OpenAsync<ProjectStructureTaskCreateDialog>(
            "Add task",
            new Dictionary<string, object?>
            {
                [nameof(ProjectStructureTaskCreateDialog.ProjectId)] = ProjectId,
                [nameof(ProjectStructureTaskCreateDialog.CreateRequest)] = createRequest,
                [nameof(ProjectStructureTaskCreateDialog.RepositoryOptions)] = BuildNodeOptions(ProjectObjectType.Repository),
                [nameof(ProjectStructureTaskCreateDialog.AssigneeOptions)] = assigneeOptions,
                [nameof(ProjectStructureTaskCreateDialog.AssigneeWarnings)] = assigneeWarnings,
                [nameof(ProjectStructureTaskCreateDialog.QuoteResolver)] =
                    new Func<ProjectStructureTaskResourceCostRequest, CancellationToken, Task<ProjectStructureTaskResourceCostQuote>>(
                        TaskResourceCostService.GetQuoteAsync)
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
            deferredCompletionCts.Token);

        if (result is ProjectStructureTaskDialogResult submission)
        {
            await CreateTaskFromDialogAsync(submission);
        }
    }

    private async Task OpenTaskEditDialogAsync(
        ProjectStructureNode taskNode,
        ProjectStructureNodeEditModel editModel)
    {
        IReadOnlyList<ProjectStructureTaskResourceOption> assigneeOptions = [];
        IReadOnlyList<string> assigneeWarnings = [];
        ProjectStructureTaskResourceSelection? initialAssignee = null;
        try
        {
            var optionsTask = WorkItemAssigneeService.ListOptionsAsync(ProjectId, deferredCompletionCts.Token);
            var assignmentsTask = ProjectPartyIntegrationBridge.ListAssignmentsDetailedAsync(
                ProjectId,
                [ProjectPartyAssignmentRole.WorkItemAssignee],
                deferredCompletionCts.Token);
            await Task.WhenAll(optionsTask, assignmentsTask);
            assigneeOptions = await optionsTask;
            initialAssignee = ResolveTaskAssignee(await assignmentsTask, taskNode.Id);
        }
        catch (OperationCanceledException) when (deferredCompletionCts.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            assigneeWarnings = ["People and agents could not be loaded. Existing task fields can still be edited without changing the assignee."];
            Logger.LogWarning(
                exception,
                "Failed to load task edit assignees for project {ProjectId} and task {TaskNodeId}.",
                ProjectId,
                taskNode.Id);
        }

        var result = await DialogService.OpenAsync<ProjectStructureTaskCreateDialog>(
            "Edit task",
            new Dictionary<string, object?>
            {
                [nameof(ProjectStructureTaskCreateDialog.ProjectId)] = ProjectId,
                [nameof(ProjectStructureTaskCreateDialog.CreateRequest)] = editModel.Request,
                [nameof(ProjectStructureTaskCreateDialog.RepositoryOptions)] = BuildNodeOptions(ProjectObjectType.Repository),
                [nameof(ProjectStructureTaskCreateDialog.AssigneeOptions)] = assigneeOptions,
                [nameof(ProjectStructureTaskCreateDialog.AssigneeWarnings)] = assigneeWarnings,
                [nameof(ProjectStructureTaskCreateDialog.IsEditMode)] = true,
                [nameof(ProjectStructureTaskCreateDialog.InitialAssignee)] = initialAssignee,
                [nameof(ProjectStructureTaskCreateDialog.QuoteResolver)] =
                    new Func<ProjectStructureTaskResourceCostRequest, CancellationToken, Task<ProjectStructureTaskResourceCostQuote>>(
                        TaskResourceCostService.GetQuoteAsync)
            },
            new DialogOptions
            {
                Eyebrow = "Project structure task",
                Subtitle = "Edit task details, pure effort, expected cost, and the direct CRM person or AI agent assignment.",
                Size = ModalSize.Wide,
                DenseChrome = true,
                TestId = "project-structure-task-edit-dialog",
                AriaLabel = "Edit project structure task",
                ChromeCloseResult = null
            },
            deferredCompletionCts.Token);

        if (result is ProjectStructureTaskDialogResult submission)
        {
            await SaveTaskEditDialogAsync(taskNode, submission, initialAssignee);
        }
    }

    private async Task SaveTaskEditDialogAsync(
        ProjectStructureNode taskNode,
        ProjectStructureTaskDialogResult submission,
        ProjectStructureTaskResourceSelection? initialAssignee)
    {
        if (!TryResolveEditAction(submission.CreateRequest.ActionId, out var createActionId) ||
            !ProjectStructureCanvasCatalog.TryResolveCreateDefinition(createActionId, out var definition))
        {
            NotificationService.Error("Task could not be saved", "The task edit definition is no longer available.");
            return;
        }

        ProjectStructureNode? updated;
        try
        {
            var update = ProjectStructureNodeEditor.ComposeUpdate(definition, taskNode, submission.CreateRequest);
            updated = await ProjectWorkbenchService.UpdateObjectAsync(ProjectId, taskNode.Id, update);
        }
        catch (OperationCanceledException) when (deferredCompletionCts.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            Logger.LogWarning(
                exception,
                "Task edit failed before a project structure update was committed. ProjectId={ProjectId} TaskNodeId={TaskNodeId}",
                ProjectId,
                taskNode.Id);
            NotificationService.Error(
                "Task could not be saved",
                "The task update failed before it was committed. Review the values and try again.");
            return;
        }

        if (updated is null)
        {
            NotificationService.Error("Task could not be saved", "The selected task is no longer available.");
            return;
        }

        var assigneeChanged = submission.Assignee != initialAssignee;
        if (assigneeChanged)
        {
            try
            {
                await WorkItemAssigneeService.ReplaceAsync(
                    ProjectId,
                    taskNode.Id,
                    submission.Assignee,
                    TaskCreateAssignmentSource,
                    deferredCompletionCts.Token);
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    exception,
                    "Task fields were committed but assignee update failed. ProjectId={ProjectId} TaskNodeId={TaskNodeId}",
                    ProjectId,
                    taskNode.Id);
                try
                {
                    await ReloadSurfaceAsync(taskNode.Id);
                    NotificationService.Warning(
                        "Task saved; assignee not changed",
                        "The task fields were saved, but the person or agent assignment could not be changed. Authoritative project data was reloaded.");
                }
                catch (Exception reloadFailure)
                {
                    Logger.LogError(
                        reloadFailure,
                        "Task fields were committed, assignee update failed, and the project structure refresh failed. ProjectId={ProjectId} TaskNodeId={TaskNodeId}",
                        ProjectId,
                        taskNode.Id);
                    NotificationService.Warning(
                        "Task saved; assignee not changed",
                        "The task fields were saved, but the assignee was not changed and the view could not refresh. Reload the project structure.");
                }

                return;
            }
        }

        try
        {
            await ReloadSurfaceAsync(taskNode.Id);
            NotificationService.Success("Task saved", $"{submission.CreateRequest.Title} was updated.");
        }
        catch (Exception exception)
        {
            Logger.LogError(
                exception,
                "Task was committed but the project structure refresh failed. ProjectId={ProjectId} TaskNodeId={TaskNodeId}",
                ProjectId,
                taskNode.Id);
            NotificationService.Warning(
                "Task saved; refresh required",
                $"{submission.CreateRequest.Title} was saved. Reload the project structure to see the latest state.");
        }
    }

    private static ProjectStructureTaskResourceSelection? ResolveTaskAssignee(
        IReadOnlyList<ProjectPartyAssignmentDetail> assignments,
        string taskNodeId)
    {
        var taskAssignments = assignments
            .Where(assignment => string.Equals(assignment.NodeKey, taskNodeId, StringComparison.Ordinal))
            .ToArray();
        if (taskAssignments.Length > 1)
        {
            throw new InvalidOperationException("The task has multiple direct assignees. Resolve the conflict before editing it.");
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
            _ => throw new InvalidOperationException("The task has an unsupported direct assignee type.")
        };
        return new ProjectStructureTaskResourceSelection(kind, assignment.PartyId);
    }

    private async Task CreateTaskFromDialogAsync(ProjectStructureTaskDialogResult submission)
    {
        if (!ProjectStructureCanvasCatalog.TryResolveCreateDefinition(
                ProjectStructureCanvasCatalog.WorkTaskActionId,
                out var definition))
        {
            NotificationService.Error("Task could not be created", "The canonical task definition is unavailable.");
            return;
        }

        ProjectStructureNode? createdTask = null;
        try
        {
            createdTask = await CreateObjectAsync(definition, submission.CreateRequest);
            if (createdTask is null)
            {
                throw new InvalidOperationException("Task creation completed without returning the created node.");
            }

            if (submission.Assignee is not null)
            {
                await WorkItemAssigneeService.ReplaceAsync(
                    ProjectId,
                    createdTask.Id,
                    submission.Assignee,
                    TaskCreateAssignmentSource,
                    deferredCompletionCts.Token);
            }
        }
        catch (OperationCanceledException) when (deferredCompletionCts.IsCancellationRequested)
        {
            if (createdTask is not null && submission.Assignee is not null)
            {
                await CompensateTaskCreateFailureAsync(createdTask, "The task creation was canceled while assigning its resource.");
            }

            return;
        }
        catch (Exception exception)
        {
            Logger.LogWarning(
                exception,
                "Project structure task creation failed. ProjectId={ProjectId} TaskNodeId={TaskNodeId} AssigneeSelected={AssigneeSelected}",
                ProjectId,
                createdTask?.Id ?? "not-created",
                submission.Assignee is not null);
            if (createdTask is not null && submission.Assignee is not null)
            {
                await CompensateTaskCreateFailureAsync(createdTask, exception.Message);
                return;
            }

            NotificationService.Error("Task could not be created", exception.Message);
            return;
        }

        var committedTask = createdTask ?? throw new InvalidOperationException(
            "Task creation completed without a committed node.");

        try
        {
            await ReloadSurfaceAsync(committedTask.Id);
            NotificationService.Success(
                "Task created",
                submission.Assignee is null
                    ? $"{committedTask.Title} was added to the project structure."
                    : $"{committedTask.Title} was added with its selected assignee.");
        }
        catch (Exception exception)
        {
            Logger.LogError(
                exception,
                "Task was committed but the project structure refresh failed. ProjectId={ProjectId} TaskNodeId={TaskNodeId}",
                ProjectId,
                committedTask.Id);
            NotificationService.Warning(
                "Task created; refresh required",
                $"{committedTask.Title} was saved. Reload the project structure to see the latest state.");
        }
    }

    private async Task CompensateTaskCreateFailureAsync(ProjectStructureNode createdTask, string failureMessage)
    {
        try
        {
            var deletedCount = await ProjectWorkbenchService.DeleteObjectAsync(ProjectId, createdTask.Id, CancellationToken.None);
            await ReloadSurfaceAsync();
            NotificationService.Error(
                "Task could not be created",
                deletedCount > 0
                    ? $"{failureMessage} The partially created task was removed."
                    : $"{failureMessage} The task could not be found during cleanup.");
        }
        catch (Exception compensationFailure)
        {
            Logger.LogError(
                compensationFailure,
                "Failed to remove a task after assignee creation failed. ProjectId={ProjectId} TaskNodeId={TaskNodeId}",
                ProjectId,
                createdTask.Id);
            NotificationService.Error(
                "Task requires attention",
                $"{failureMessage} The partially created task could not be removed.");
        }
    }
}
