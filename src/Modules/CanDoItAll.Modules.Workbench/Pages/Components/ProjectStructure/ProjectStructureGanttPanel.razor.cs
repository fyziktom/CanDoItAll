using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Gantt;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructureGanttPanel : ComponentBase, IAsyncDisposable
{
    private static readonly TimeSpan DefaultTaskDuration = TimeSpan.FromHours(8);
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private readonly ProjectStructureAgentContext uiMutationOwner = CreateUiMutationOwner();
    private IReadOnlyList<ProjectPartyAssignmentDetail> loadedAssignments = [];
    private ProjectStructureSurface? loadedSurface;
    private ProjectStructureGanttProjectionResult? projection;
    private GanttTask insertionCandidate = CreateInsertionCandidate(DateTimeOffset.UtcNow);
    private DateTimeOffset projectionOriginUtc;
    private Guid projectionProjectId;
    private bool isLoading;
    private bool mutationInFlight;
    private string? loadError;

    [Inject]
    private IProjectPartyIntegrationBridge ProjectPartyIntegrationBridge { get; set; } = default!;

    [Inject]
    private ProjectStructureGanttProjectionAdapter ProjectionAdapter { get; set; } = default!;

    [Inject]
    private ProjectStructureGanttMutationService MutationService { get; set; } = default!;

    [Inject]
    private ProjectStructureTaskCreationService TaskCreationService { get; set; } = default!;

    [Inject]
    private ProjectStructureGanttTaskEditCoordinator TaskEditCoordinator { get; set; } = default!;

    [Inject]
    private ProjectStructureTaskResourceService TaskResourceService { get; set; } = default!;

    [Inject]
    private ProjectStructureTaskResourceCostService TaskResourceCostService { get; set; } = default!;

    [Inject]
    private ProjectStructureGanttRowOrderService RowOrderService { get; set; } = default!;

    [Inject]
    private ProjectWorkbenchService ProjectWorkbenchService { get; set; } = default!;

    [Inject]
    private ILogger<ProjectStructureGanttPanel> Logger { get; set; } = default!;

    [Inject]
    private NotificationService NotificationService { get; set; } = default!;

    [Inject]
    private DialogService DialogService { get; set; } = default!;

    [Inject]
    private ICurrencyFormatter CurrencyFormatter { get; set; } = default!;

    [Parameter]
    public Guid ProjectId { get; set; }

    [Parameter, EditorRequired]
    public ProjectStructureSurface Surface { get; set; } = default!;

    [Parameter, EditorRequired]
    public EventCallback MutationCommitted { get; set; }

    private bool CanInsertTask =>
        projection is { IsValid: true, Dependencies.Count: > 0 } &&
        !mutationInFlight;

    private decimal? TotalExpectedEffortHours
    {
        get
        {
            var efforts = projection?.Tasks
                .Where(static task => task.ExpectedEffort.HasValue)
                .Select(static task => (decimal)task.ExpectedEffort!.Value.TotalHours)
                .ToArray() ?? [];
            return efforts.Length == 0 ? null : efforts.Sum();
        }
    }

    private IReadOnlyList<ProjectStructureGanttProjectionIssue> ProjectionErrors =>
        projection?.Issues
            .Where(static issue => issue.Severity == ProjectStructureGanttProjectionIssueSeverity.Error)
            .ToArray() ?? [];

    private IReadOnlyList<ProjectStructureGanttProjectionIssue> NonScheduleWarnings =>
        projection?.Issues
            .Where(issue =>
                issue.Severity == ProjectStructureGanttProjectionIssueSeverity.Warning &&
                !IsScheduleProjectionIssue(issue.Code))
            .ToArray() ?? [];

    private const string ExportFileName = "project-schedule-gantt.png";

    protected override async Task OnParametersSetAsync()
    {
        if (ProjectId == Guid.Empty)
        {
            loadError = "A project is required before its Gantt schedule can be displayed.";
            projection = null;
            return;
        }

        if (Surface is null || Surface.ProjectId != ProjectId)
        {
            loadError = "The project schedule cannot be built because the supplied structure does not match this project.";
            projection = null;
            return;
        }

        if (ReferenceEquals(loadedSurface, Surface))
        {
            return;
        }

        loadedSurface = Surface;
        if (projectionProjectId != ProjectId)
        {
            projectionProjectId = ProjectId;
            projection = null;
            projectionOriginUtc = ResolveProjectionOriginUtc(Surface);
            insertionCandidate = CreateInsertionCandidate(projectionOriginUtc);
        }

        isLoading = true;
        loadError = null;
        try
        {
            var assignmentsTask = ProjectPartyIntegrationBridge.ListAssignmentsDetailedAsync(
                ProjectId,
                lifetimeCancellation.Token);
            var viewStateTask = ProjectWorkbenchService.LoadGanttViewStateAsync(
                ProjectId,
                lifetimeCancellation.Token);
            await Task.WhenAll(assignmentsTask, viewStateTask);
            var assignments = await assignmentsTask;
            loadedAssignments = assignments;
            var viewState = await viewStateTask;
            projection = ProjectionAdapter.Build(
                Surface,
                assignments,
                new ProjectStructureGanttProjectionOptions(
                    projectionOriginUtc,
                    DefaultTaskDuration,
                    viewState.OrderedTaskNodeIds));
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            projection = null;
            loadError = "The project schedule could not be loaded. The project structure remains unchanged.";
            Logger.LogError(
                "Failed to build the Gantt projection for project {ProjectId}; failure type {FailureType}.",
                Mask(ProjectId),
                exception.GetType().Name);
        }
        finally
        {
            isLoading = false;
        }
    }

    private Task ApplyTitleAsync(GanttTaskTitleChangeRequest request)
        => ExecuteMutationAsync(
            "title",
            cancellationToken => MutationService.ApplyTitleAsync(ProjectId, request, cancellationToken));

    private Task ApplyScheduleAsync(GanttTaskScheduleChangeRequest request)
        => ExecuteMutationAsync(
            "schedule",
            cancellationToken => MutationService.ApplyScheduleAsync(ProjectId, request, cancellationToken));

    private Task ApplyDependencyAsync(GanttDependencyMutationRequest request)
        => ExecuteMutationAsync(
            "dependency",
            cancellationToken => MutationService.ApplyDependencyAsync(ProjectId, request, cancellationToken));

    private Task ApplyInsertionAsync(GanttTaskInsertionRequest request)
        => ExecuteMutationAsync(
            "insertion",
            cancellationToken => MutationService.ApplyInsertionAsync(ProjectId, request, cancellationToken),
            renewInsertionCandidate: true);

    private Task OpenGeneralTaskDialogAsync()
    {
        var startUtc = projection is { Tasks.Count: > 0 }
            ? projection.Tasks.Max(static task => task.End)
            : projectionOriginUtc;
        return OpenTaskDialogAsync(startUtc, afterTaskNodeId: null);
    }

    private Task OpenTimelineTaskDialogAsync(GanttTimelineDoubleClickEventArgs args)
        => OpenTaskDialogAsync(args.ClickedAtUtc, args.RowTaskId.Value);

    private async Task OpenTaskDetailsDialogAsync(GanttTaskId taskId)
    {
        if (!EnsureMutationHostAvailable() || projection is null || loadedSurface is null)
        {
            return;
        }

        mutationInFlight = true;
        try
        {
            await TaskEditCoordinator.OpenAsync(
                new ProjectStructureGanttTaskEditContext(
                    ProjectId,
                    loadedSurface,
                    projection,
                    loadedAssignments,
                    uiMutationOwner),
                taskId,
                () => MutationCommitted.InvokeAsync(),
                lifetimeCancellation.Token);
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            mutationInFlight = false;
        }
    }

    private async Task OpenTaskDialogAsync(DateTimeOffset startUtc, string? afterTaskNodeId)
    {
        if (mutationInFlight)
        {
            NotificationService.Warning(
                "Project schedule change in progress",
                "Wait for the current schedule change to finish before adding a task.");
            return;
        }

        IReadOnlyList<ProjectStructureTaskResourceOption> resourceOptions = [];
        IReadOnlyList<string> resourceWarnings = [];
        try
        {
            resourceOptions = await TaskResourceService.ListOptionsAsync(ProjectId, lifetimeCancellation.Token);
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            resourceWarnings = ["Resources could not be loaded. You can still create the task without an assignment."];
            Logger.LogWarning(
                "Failed to load task resource choices for project {ProjectId}; failure type {FailureType}.",
                Mask(ProjectId),
                exception.GetType().Name);
        }

        var normalizedStart = startUtc.ToUniversalTime();
        var result = await DialogService.OpenAsync<ProjectStructureGanttTaskDialog>(
            "Add project task",
            new Dictionary<string, object?>
            {
                [nameof(ProjectStructureGanttTaskDialog.ProjectId)] = ProjectId,
                [nameof(ProjectStructureGanttTaskDialog.DefaultStartUtc)] = normalizedStart,
                [nameof(ProjectStructureGanttTaskDialog.DefaultEndUtc)] = normalizedStart + DefaultTaskDuration,
                [nameof(ProjectStructureGanttTaskDialog.DefaultEstimate)] = new ProjectTaskEstimate(
                    (decimal)DefaultTaskDuration.TotalHours,
                    ProjectWorkItemEffortUnit.Hours,
                    null,
                    string.Empty),
                [nameof(ProjectStructureGanttTaskDialog.DefaultCurrencyCode)] = CurrencyFormatter.CurrencyCode,
                [nameof(ProjectStructureGanttTaskDialog.AfterTaskNodeId)] = afterTaskNodeId,
                [nameof(ProjectStructureGanttTaskDialog.ResourceOptions)] = resourceOptions,
                [nameof(ProjectStructureGanttTaskDialog.ResourceWarnings)] = resourceWarnings,
                [nameof(ProjectStructureGanttTaskDialog.QuoteResolver)] =
                    new Func<ProjectStructureTaskResourceCostRequest, CancellationToken, Task<ProjectStructureTaskResourceCostQuote>>(
                        TaskResourceCostService.GetQuoteAsync)
            },
            new DialogOptions
            {
                Eyebrow = afterTaskNodeId is null ? "Project schedule" : "Gantt timeline",
                Subtitle = afterTaskNodeId is null
                    ? "Create a task at the end of the current Gantt row order."
                    : "Create a task directly below the row you double-clicked.",
                Size = ModalSize.Wide,
                DenseChrome = true,
                TestId = "project-structure-gantt-task-dialog",
                AriaLabel = "Add project task",
                ChromeCloseResult = null
            },
            lifetimeCancellation.Token);

        if (result is ProjectStructureTaskCreateRequest request)
        {
            await CreateTaskAsync(request);
        }
    }

    private async Task CreateTaskAsync(ProjectStructureTaskCreateRequest request)
    {
        if (!EnsureMutationHostAvailable())
        {
            return;
        }

        mutationInFlight = true;
        var mutationCommitted = false;
        try
        {
            var result = await TaskCreationService.CreateAsync(
                ProjectId,
                request,
                uiMutationOwner,
                lifetimeCancellation.Token);
            mutationCommitted = true;
            await MutationCommitted.InvokeAsync();
            NotificationService.Success(
                "Project task created",
                result.AttachedResource is null
                    ? $"{request.Title} was added to Main."
                    : $"{request.Title} was added to Main with its selected resource.");
        }
        catch (ProjectStructureTaskCreationException exception)
        {
            NotificationService.Error("Project task could not be created", exception.Message);
            Logger.LogWarning(
                "Rejected Gantt task creation for project {ProjectId} with code {ErrorCode}.",
                Mask(ProjectId),
                exception.Code);
        }
        catch (ProjectStructureAgentException exception)
        {
            NotificationService.Error("Project task could not be created", exception.Message);
            Logger.LogWarning(
                "Rejected Gantt task creation for project {ProjectId} with application error {ErrorCode}.",
                Mask(ProjectId),
                exception.ErrorCode);
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            NotifyUnexpectedMutationFailure("task creation", mutationCommitted);
            Logger.LogError(
                "Gantt task creation failed for project {ProjectId} after commit state {MutationCommitted}; failure type {FailureType}.",
                Mask(ProjectId),
                mutationCommitted,
                exception.GetType().Name);
        }
        finally
        {
            mutationInFlight = false;
        }
    }

    private async Task ApplyTaskOrderAsync(GanttTaskOrderChangeRequest request)
    {
        if (!EnsureMutationHostAvailable())
        {
            return;
        }

        var placement = request.Placement switch
        {
            GanttTaskOrderPlacement.Before => ProjectStructureGanttRowPlacement.Before,
            GanttTaskOrderPlacement.After => ProjectStructureGanttRowPlacement.After,
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Placement, "Unknown Gantt row placement.")
        };

        mutationInFlight = true;
        var mutationCommitted = false;
        try
        {
            await RowOrderService.MoveAsync(
                ProjectId,
                new ProjectStructureGanttRowMoveRequest(
                    request.TaskId.Value,
                    request.AnchorTaskId.Value,
                    placement),
                uiMutationOwner,
                lifetimeCancellation.Token);
            mutationCommitted = true;
            await MutationCommitted.InvokeAsync();
            NotificationService.Success("Task order saved", "The Gantt row order was updated.");
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (ProjectStructureGanttRowOrderConflictException exception)
        {
            NotificationService.Error(
                "Task order change rejected",
                "The task rows changed before this order request could be applied. Try again.");
            Logger.LogWarning(
                "Rejected stale Gantt row order change for project {ProjectId}, task {TaskNodeId}, anchor {AnchorTaskNodeId}, placement {Placement}.",
                Mask(ProjectId),
                exception.TaskNodeId,
                exception.AnchorTaskNodeId,
                exception.Placement);
        }
        catch (Exception exception)
        {
            NotifyUnexpectedMutationFailure("task order", mutationCommitted);
            Logger.LogError(
                "Gantt row order change failed for project {ProjectId} after commit state {MutationCommitted}; failure type {FailureType}.",
                Mask(ProjectId),
                mutationCommitted,
                exception.GetType().Name);
        }
        finally
        {
            mutationInFlight = false;
        }
    }

    private async Task ExecuteMutationAsync(
        string operation,
        Func<CancellationToken, Task<ProjectStructureGanttMutationResult>> mutation,
        bool renewInsertionCandidate = false)
    {
        if (!EnsureMutationHostAvailable())
        {
            return;
        }

        mutationInFlight = true;
        var mutationCommitted = false;
        try
        {
            var result = await mutation(lifetimeCancellation.Token);
            mutationCommitted = true;
            if (renewInsertionCandidate)
            {
                insertionCandidate = CreateInsertionCandidate(projectionOriginUtc);
            }

            await MutationCommitted.InvokeAsync();
            NotificationService.Success(
                "Project schedule saved",
                BuildMutationStatus(operation, result));
        }
        catch (ProjectStructureGanttMutationException exception)
        {
            NotificationService.Error(
                "Project schedule change rejected",
                exception.Message);
            Logger.LogWarning(
                "Rejected Gantt {Operation} mutation for project {ProjectId} with code {ErrorCode}.",
                operation,
                Mask(ProjectId),
                exception.Code);
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            NotifyUnexpectedMutationFailure(operation, mutationCommitted);
            Logger.LogError(
                "Gantt {Operation} processing failed for project {ProjectId} after commit state {MutationCommitted}; failure type {FailureType}.",
                operation,
                Mask(ProjectId),
                mutationCommitted,
                exception.GetType().Name);
        }
        finally
        {
            mutationInFlight = false;
        }
    }

    private bool EnsureMutationHostAvailable()
    {
        if (!MutationCommitted.HasDelegate)
        {
            NotificationService.Error(
                "Project schedule change unavailable",
                "The schedule host is not configured to reload authoritative project data, so the change was not attempted.");
            return false;
        }

        if (!mutationInFlight)
        {
            return true;
        }

        NotificationService.Warning(
            "Project schedule change in progress",
            "Another project schedule change is still being saved.");
        return false;
    }

    private void NotifyUnexpectedMutationFailure(string operation, bool mutationCommitted)
    {
        if (mutationCommitted)
        {
            NotificationService.Warning(
                "Project schedule saved; reload required",
                "The change was saved, but the authoritative project schedule could not be reloaded. Reload this page before making another change.");
            return;
        }

        NotificationService.Error(
            "Project schedule save failed",
            $"The {operation} change could not be saved. The chart remains unchanged.");
    }

    private bool IsProjectionOnly(GanttTask task)
        => projection?.IsProjectionOnly(task) == true;

    private static GanttDependencyId CreateDependencyId(GanttTaskId predecessorId, GanttTaskId successorId)
        => ProjectStructureGanttMutationConventions.CreatePendingDependencyId();

    private static GanttTask CreateInsertionCandidate(DateTimeOffset projectionOriginUtc)
    {
        var start = projectionOriginUtc.ToUniversalTime();
        return new GanttTask(
            ProjectStructureGanttMutationConventions.CreateCustomTaskId(),
            "New task",
            start,
            start + DefaultTaskDuration);
    }

    private static DateTimeOffset ResolveProjectionOriginUtc(ProjectStructureSurface surface)
    {
        var earliestPersistedStart = surface.Nodes
            .Where(static node => node.StartUtc.HasValue)
            .Select(static node => node.StartUtc!.Value.ToUniversalTime())
            .DefaultIfEmpty()
            .Min();
        if (earliestPersistedStart != default)
        {
            return earliestPersistedStart;
        }

        var utcToday = DateTime.UtcNow.Date;
        return new DateTimeOffset(utcToday, TimeSpan.Zero);
    }

    private static string BuildMutationStatus(
        string operation,
        ProjectStructureGanttMutationResult result)
    {
        var dependencySummary = result.AddedDependencyCount == 0 && result.RemovedDependencyCount == 0
            ? string.Empty
            : $", {result.AddedDependencyCount} dependency link(s) added and {result.RemovedDependencyCount} removed";
        return $"The {operation} change was saved for {result.AffectedTaskIds.Count} task(s){dependencySummary}.";
    }

    private string FormatExpectedCost(ProjectStructureGanttExpectedCostTotal expectedCost)
    {
        var value = string.Equals(
            expectedCost.CurrencyCode,
            CurrencyFormatter.CurrencyCode,
            StringComparison.OrdinalIgnoreCase)
            ? CurrencyFormatter.Format(expectedCost.Amount)
            : $"{expectedCost.CurrencyCode} {expectedCost.Amount.ToString("0.##", CultureInfo.InvariantCulture)}";
        return $"{value} expected";
    }

    private static bool IsScheduleProjectionIssue(ProjectStructureGanttProjectionIssueCode code)
        => code is ProjectStructureGanttProjectionIssueCode.ScheduleSynthesized or
            ProjectStructureGanttProjectionIssueCode.ScheduleStartSynthesized or
            ProjectStructureGanttProjectionIssueCode.ScheduleEndSynthesized;

    private static string Mask(Guid value)
    {
        var formatted = value.ToString("N");
        return $"{formatted[..6]}...{formatted[^4..]}";
    }

    private static ProjectStructureAgentContext CreateUiMutationOwner()
    {
        var ownerId = Guid.NewGuid().ToString("N");
        return new ProjectStructureAgentContext(
            $"project-structure-gantt-ui-{ownerId}",
            "Project structure Gantt UI",
            Environment.MachineName,
            string.Empty,
            string.Empty,
            $"gantt-panel-{ownerId}");
    }

    public ValueTask DisposeAsync()
    {
        lifetimeCancellation.Cancel();
        lifetimeCancellation.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
