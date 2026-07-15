using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
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
                [nameof(ProjectStructureTaskCreateDialog.CreateRequest)] = createRequest,
                [nameof(ProjectStructureTaskCreateDialog.RepositoryOptions)] = BuildNodeOptions(ProjectObjectType.Repository),
                [nameof(ProjectStructureTaskCreateDialog.AssigneeOptions)] = assigneeOptions,
                [nameof(ProjectStructureTaskCreateDialog.AssigneeWarnings)] = assigneeWarnings
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
