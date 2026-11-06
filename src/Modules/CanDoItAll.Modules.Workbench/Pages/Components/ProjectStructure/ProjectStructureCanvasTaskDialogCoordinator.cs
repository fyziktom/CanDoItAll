using CanDoItAll.Workbench.Planning.UI;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public delegate Task<ProjectStructureNode?> ProjectStructureCanvasTaskNodeCreator(
    CanvasWorkbenchCreateActionRequest request,
    Func<ProjectObjectCreateRequest, ProjectObjectCreateRequest> configureRequest,
    Func<CancellationToken, ValueTask> authorizeCommit);

public sealed record ProjectStructureCanvasTaskDialogContext(
    Guid ProjectId,
    IReadOnlyList<CanvasWorkbenchInputOption> RepositoryOptions,
    ProjectStructureCanvasTaskNodeCreator CreateTaskNodeAsync,
    Func<string?, Func<CancellationToken, ValueTask>, Task> ReloadAuthoritativeProject,
    ProjectStructureAgentContext MutationOwner) {
    public Func<bool> IsCurrent { get; init; } = static () => true;
}

public sealed class ProjectStructureCanvasTaskDialogCoordinator(
    ProjectStructureWorkItemAssigneeService assigneeService,
    ProjectStructureTaskResourceService taskResourceService,
    ProjectStructureTaskResourceCostService resourceCostService,
    ProjectStructureTaskResourceAttachmentService taskResourceAttachmentService,
    ProjectStructureTaskApplicationService taskApplicationService,
    ProjectWorkbenchService projectWorkbenchService,
    ProjectStructureTaskEditingSessions editingSessions,
    ICurrencyFormatter currencyFormatter,
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

        await using var access = editingSessions.CreateAccess(context.MutationOwner.ExpectedProjectAdmission!, context.IsCurrent);
        try {
            await access.OpenAsync(cancellationToken);
        } catch (Exception failure) when (failure is InvalidOperationException or UnauthorizedAccessException) {
            ReportCreateAccessFailure(context, failure);
            return;
        }

        IReadOnlyList<ProjectStructureTaskResourceOption> assigneeOptions = [];
        IReadOnlyList<string> assigneeWarnings = [];
        try
        {
            assigneeOptions = await access.ReadPrivateAsync(token => assigneeService.ListOptionsAsync(context.ProjectId, token), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception failure) when (failure is UnauthorizedAccessException or ProjectWriteAdmissionRejectedException) {
            ReportCreateAccessFailure(context, failure);
            return;
        }
        catch (Exception exception)
        {
            assigneeWarnings =
                ["People and agents could not be loaded. You can still create the task without an assignee."];
            logger.LogWarning(
                "Failed to load canvas task assignees for project {ProjectId}; failure type {FailureType}.",
                Mask(context.ProjectId), exception.GetType().Name);
        }

        if (!context.IsCurrent()) {
            return;
        }
        var session = new ProjectTaskDialogSession(context.IsCurrent, () => context.ReloadAuthoritativeProject(null, access.AuthorizeReadAsync), logger);
        await dialogService.OpenAsync<ProjectStructureTaskCreateDialog>(
            "Add task",
            new Dictionary<string, object?>
            {
                [nameof(ProjectStructureTaskCreateDialog.Submitted)] =
                    new Func<ProjectStructureTaskDialogResult, Task<PlanningTaskSaveResult>>(draft =>
                        session.SubmitAsync(() => CreateAsync(context, draft, access, session))),
                [nameof(ProjectStructureTaskCreateDialog.Readback)] = new Func<Task<PlanningTaskSaveResult>>(session.ReadbackAsync),
                [nameof(ProjectStructureTaskCreateDialog.ProjectId)] = context.ProjectId,
                [nameof(ProjectStructureTaskCreateDialog.QuoteContext)] = new ProjectTaskQuoteContext(
                    context.MutationOwner.ExpectedProjectAdmission!.DatabaseProfileId,
                    context.MutationOwner.ExpectedProjectAdmission.LifetimeId,
                    Guid.NewGuid()),
                [nameof(ProjectStructureTaskCreateDialog.CreateRequest)] = createRequest,
                [nameof(ProjectStructureTaskCreateDialog.RepositoryOptions)] = context.RepositoryOptions,
                [nameof(ProjectStructureTaskCreateDialog.ResourceOptions)] = assigneeOptions,
                [nameof(ProjectStructureTaskCreateDialog.ResourceWarnings)] = assigneeWarnings,
                [nameof(ProjectStructureTaskCreateDialog.QuoteResolver)] =
                    new Func<ProjectStructureTaskResourceCostRequest, CancellationToken, Task<ProjectStructureTaskResourceCostQuote>>(
                        (request, token) => access.ReadPrivateAsync(current => resourceCostService.GetQuoteAsync(request, current), token))
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

        await using var editing = editingSessions.Create(context.MutationOwner.ExpectedProjectAdmission!, taskNode,
            context.IsCurrent, currencyFormatter.CurrencyCode);
        try {
            await editing.OpenAsync(cancellationToken);
        } catch (Exception failure) when (failure is InvalidOperationException or UnauthorizedAccessException or FormatException) {
            if (context.IsCurrent()) {
                notificationService.Error("Task could not be opened", "The original Task and current Structure access could not be verified. Refresh before opening it again.");
            }
            logger.LogWarning("Could not open Task comparison authority; failure type {FailureType}.", failure.GetType().Name);
            return;
        }
        taskNode = editing.Baseline.Task;
        editRequest = ProjectStructureTaskForm.StructureRequest(editing.Baseline, editRequest);
        var snapshot = editing.Baseline.State;
        var assigneeResolution = editing.Baseline.Assignees;
        await editing.AuthorizeReadAsync(cancellationToken);
        var resources = await LoadEditResourceOptionsAsync(context.ProjectId, cancellationToken);
        await editing.AuthorizeReadAsync(cancellationToken);
        var canChangeDirectAssignee = resources.Loaded && assigneeResolution.CanChangeDirectAssignee;
        var resourceOptions = ProjectStructureTaskAssigneeSelectionPolicy.IncludeRepresentativeOption(resources.Options, assigneeResolution);
        IReadOnlyList<string> resourceWarnings = [.. resources.Warnings,
            .. ResolveAssigneeWarning(assigneeResolution.Status) is { } warning ? new[] { warning } : Array.Empty<string>()];

        if (!context.IsCurrent()) {
            return;
        }
        var session = new ProjectTaskDialogSession(context.IsCurrent, () => context.ReloadAuthoritativeProject(taskNode.Id, editing.AuthorizeReadAsync), logger, [taskNode.Id]);
        editing.CanAccept = () => session.CanAcceptComparison;
        editing.Accepted = session.AcceptComparison;
        await dialogService.OpenAsync<ProjectStructureTaskCreateDialog>(
            "Edit task",
            new Dictionary<string, object?>
            {
                [nameof(ProjectStructureTaskCreateDialog.Submitted)] =
                    new Func<ProjectStructureTaskDialogResult, Task<PlanningTaskSaveResult>>(draft =>
                        session.SubmitAsync(() => SaveEditAsync(context, editing.Baseline, draft, editing, session))),
                [nameof(ProjectStructureTaskCreateDialog.Readback)] = new Func<Task<PlanningTaskSaveResult>>(session.ReadbackAsync),
                [nameof(ProjectStructureTaskCreateDialog.ProjectId)] = context.ProjectId,
                [nameof(ProjectStructureTaskCreateDialog.QuoteContext)] = new ProjectTaskQuoteContext(
                    context.MutationOwner.ExpectedProjectAdmission!.DatabaseProfileId,
                    context.MutationOwner.ExpectedProjectAdmission.LifetimeId,
                    Guid.NewGuid()),
                [nameof(ProjectStructureTaskCreateDialog.CreateRequest)] = editRequest,
                [nameof(ProjectStructureTaskCreateDialog.RepositoryOptions)] = context.RepositoryOptions,
                [nameof(ProjectStructureTaskCreateDialog.ResourceOptions)] = resourceOptions,
                [nameof(ProjectStructureTaskCreateDialog.ResourceWarnings)] = resourceWarnings,
                [nameof(ProjectStructureTaskCreateDialog.ComparisonOwner)] = editing,
                [nameof(ProjectStructureTaskCreateDialog.IsEditMode)] = true,
                [nameof(ProjectStructureTaskCreateDialog.InitialAssignee)] = assigneeResolution.Representative,
                [nameof(ProjectStructureTaskCreateDialog.InitialExecution)] = snapshot.Execution,
                [nameof(ProjectStructureTaskCreateDialog.CanChangeDirectAssignee)] = canChangeDirectAssignee,
                [nameof(ProjectStructureTaskCreateDialog.QuoteResolver)] =
                    new Func<ProjectStructureTaskResourceCostRequest, CancellationToken, Task<ProjectStructureTaskResourceCostQuote>>(
                        (request, token) => editing.ReadPrivateAsync(current => resourceCostService.GetQuoteAsync(request, current), token))
            },
            new DialogOptions
            {
                Eyebrow = "Project structure task",
                Subtitle = "Edit task details, pure effort, expected cost, execution state, direct assignee, and additive workflow or process resources.",
                Size = ModalSize.Wide,
                DenseChrome = true,
                TestId = "project-structure-task-edit-dialog",
                AriaLabel = "Edit project structure task",
                ChromeCloseResult = null
            },
            cancellationToken);

    }

    private async Task CreateAsync(ProjectStructureCanvasTaskDialogContext context,
        ProjectStructureTaskDialogResult submission, ProjectStructureTaskEditorAccess access, ProjectTaskDialogSession session) {
        await access.AuthorizeWriteAsync(CancellationToken.None);
        if (submission.ResourceToAttach is not null || submission.Assignee?.Kind is
            ProjectStructureTaskResourceKind.Workflow or ProjectStructureTaskResourceKind.Process) {
            session.Reject("General task creation accepts only a direct person or agent assignee.");
            return;
        }
        var result = await taskApplicationService.CreateAsync(
            new(context.ProjectId, RequireEstimate(submission), submission.Assignee, AssignmentSource, context.MutationOwner),
            (pricing, _) => {
                if (!context.IsCurrent()) {
                    throw new InvalidOperationException("The original task editor retired before creation began.");
                }
                return context.CreateTaskNodeAsync(submission.CreateRequest, request =>
                    ProjectStructureCanvasTaskCommitPolicy.ApplyCreate(request, pricing) with {
                        ExpectedProjectAdmission = context.MutationOwner.ExpectedProjectAdmission,
                        ProcessMutationAdmission = context.MutationOwner.ProcessMutationAdmission
                    }, access.AuthorizeWriteAsync);
            }, CancellationToken.None, access.AuthorizeWriteAsync);
        session.TaskCommitted([result.Task.Id], result.Pricing, submission.Assignee is not null);
        await session.RefreshAfterCommitAsync();
        if (context.IsCurrent()) {
            notificationService.Success("Task created", session.Result!.Message);
        }
    }

    private void ReportCreateAccessFailure(ProjectStructureCanvasTaskDialogContext context, Exception failure) {
        if (context.IsCurrent()) {
            notificationService.Error("Task creation unavailable", "The original Project and current Structure access could not be verified. Reopen the creator with current access.");
        }
        logger.LogWarning("Could not open or read Task creation choices for project {ProjectId}; failure type {FailureType}.",
            Mask(context.ProjectId), failure.GetType().Name);
    }

    private async Task SaveEditAsync(ProjectStructureCanvasTaskDialogContext context,
        ProjectStructureTaskSnapshot baseline, ProjectStructureTaskDialogResult submission,
        ProjectStructureTaskEditingSession editing, ProjectTaskDialogSession session) {
        await editing.AuthorizeWriteAsync(CancellationToken.None);
        var openedTask = baseline.Task;
        var openedSnapshot = baseline.State;
        var openedAssigneeResolution = baseline.Assignees;
        if (!TryResolveEditAction(submission.CreateRequest.ActionId, out var actionId) ||
            !ProjectStructureCanvasCatalog.TryResolveCreateDefinition(actionId, out var definition)) {
            session.Reject("The task edit definition is no longer available.", readback: true);
            return;
        }
        if (submission.ResourceToAttach is { } resource) {
            ProjectStructureTaskResourceSelectionPolicy.ValidateDefinitionAttachment(resource);
        }
        var changedAssignee = submission.Assignee != openedAssigneeResolution.Representative;
        if (changedAssignee && !openedAssigneeResolution.CanChangeDirectAssignee) {
            session.Reject("The complete direct-assignment set must be preserved.");
            return;
        }
        var proposedExecution = submission.Execution ?? openedSnapshot.Execution;
        var result = await taskApplicationService.EditAsync(
            new(context.ProjectId, openedTask.Id, openedSnapshot, RequireEstimate(submission), proposedExecution,
                changedAssignee, submission.Assignee, AssignmentSource, context.MutationOwner),
            async (commit, token) => {
                if (!context.IsCurrent()) {
                    throw new InvalidOperationException("The original editor retired before task persistence began.");
                }
                var update = ProjectStructureNodeEditor.ComposeUpdate(definition, commit.CurrentTask, submission.CreateRequest with {
                    InputValues = submission.CreateRequest.InputValues?.Where(input => input.Key == ProjectTaskInputKeys.DueUtc).ToArray()
                });
                update = ProjectStructureCanvasTaskCommitPolicy.ApplyEdit(commit.CurrentTask, update,
                    commit.ProposedExecution, commit.Pricing, commit.ProposedCostBasis) with {
                    ExpectedProjectAdmission = context.MutationOwner.ExpectedProjectAdmission,
                    ProcessMutationAdmission = context.MutationOwner.ProcessMutationAdmission,
                    ExpectedNode = ProjectStructureCanvasTaskCommitPolicy.ExpectedAfterAssignment(openedTask, commit.CurrentTask)
                };
                return await projectWorkbenchService.UpdateObjectIfMetadataAsync(context.ProjectId, commit.CurrentTask.Id, update,
                    metadata => ProjectStructureCanvasTaskCommitPolicy.ValidateCurrentMetadata(metadata, commit.CurrentState), token, editing.AuthorizeWriteAsync)
                    ?? throw new InvalidOperationException("The selected task is no longer available.");
            }, CancellationToken.None, editing.AuthorizeWriteAsync, openedTask);
        session.TaskCommitted([result.PersistenceResult.Id], result.Pricing, changedAssignee);
        if (submission.ResourceToAttach is not null && context.IsCurrent()) {
            var attachment = await taskResourceAttachmentService.AttachAfterTransitionAsync(context.ProjectId, openedTask.Id,
                submission.ResourceToAttach, openedSnapshot.Execution, proposedExecution, context.MutationOwner, CancellationToken.None, editing.AuthorizeWriteAsync);
            session.AttachmentCommitted(attachment);
        } else if (submission.ResourceToAttach is not null) {
            throw new InvalidOperationException("The task committed, but its original view retired before attachment began.");
        }
        await session.RefreshAfterCommitAsync();
        if (context.IsCurrent()) {
            notificationService.Success("Task saved", session.Result!.Message);
        }
    }

    private async Task<(
        IReadOnlyList<ProjectStructureTaskResourceOption> Options,
        bool Loaded,
        IReadOnlyList<string> Warnings)> LoadEditResourceOptionsAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        try
        {
            return (
                await taskResourceService.ListOptionsAsync(
                    projectId,
                    cancellationToken),
                Loaded: true,
                []);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Failed to load canvas task resources for project {ProjectId}; failure type {FailureType}.",
                Mask(projectId), exception.GetType().Name);
            return (
                [],
                Loaded: false,
                ["Task resources could not be loaded. Existing task fields can still be edited without changing the assignee or attaching a workflow or process."]);
        }
    }

    private static ProjectTaskEstimate RequireEstimate(
        ProjectStructureTaskDialogResult submission)
        => ProjectTaskEstimatePolicy.ValidateAndNormalize(
            submission.Estimate ??
            throw new InvalidOperationException(
                "The task estimate was not supplied by the task editor."));

    private static string Mask(Guid value) => Mask(value.ToString("N"));
    private static string Mask(string value) => value.Length <= 12 ? value : $"{value[..6]}...{value[^4..]}";

    private static string? ResolveAssigneeWarning(
        ProjectStructureTaskAssigneeSelectionStatus status)
        => status switch
        {
            ProjectStructureTaskAssigneeSelectionStatus.MultipleWithPrimary =>
                "This task has multiple direct assignees. Its primary assignee remains unchanged, and direct person or agent changes are disabled to preserve the complete assignment set.",
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
        ArgumentNullException.ThrowIfNull(context.MutationOwner);
        ProjectAssignmentAdmission.Require(context.ProjectId, context.MutationOwner.ExpectedProjectAdmission);
    }
}
