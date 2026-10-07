using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Gantt;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench.AgentContext;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using System.Globalization;
using CanDoItAll.Workbench.Planning.UI;

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
    private GanttPreview? mermaidPreview;
    private Guid viewOrigin = Guid.NewGuid();
    private Guid? mutationOperation;
    private bool readbackRequired;
    private int activeOperations;
    private readonly Dictionary<Guid, GanttMutationReceipt> mutationReceipts = [];

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

    [Parameter]
    public EventCallback OpenAgentCatalog { get; set; }

    /// <summary>
    /// Publishes the bounded visible Gantt observation whenever the projection
    /// state changes. The observation is model context only; it never grants
    /// product access.
    /// </summary>
    [Parameter]
    public EventCallback<ProjectStructureGanttObservation?> ObservationChanged { get; set; }

    private string? lastPublishedObservationFingerprint;
    private bool disposed;

    private bool IsCurrent(Guid origin) => !disposed && origin == viewOrigin;

    private async Task AcceptAsync(Guid origin, Func<Task> action) {
        if (!IsCurrent(origin) || isLoading) {
            return;
        }
        activeOperations++;
        try {
            await action();
        } finally {
            ReleaseOperation();
        }
    }

    private void ReleaseOperation() {
        activeOperations--;
        if (disposed && activeOperations == 0) {
            lifetimeCancellation.Dispose();
        }
    }

    private GanttPresentation BuildPresentation() => new(viewOrigin) {
        ProjectName = loadedSurface?.ProjectName ?? string.Empty,
        HasProjection = projection is not null,
        IsLoading = isLoading,
        CanMutate = CanMutate,
        LoadError = loadError,
        Tasks = projection?.Tasks ?? [],
        Dependencies = projection?.Dependencies ?? [],
        ProjectionOnlyTaskIds = projection?.ProjectionOnlyTaskIds ?? new HashSet<GanttTaskId>(),
        Warnings = NonScheduleWarnings.Select(issue => issue.Message).ToArray(),
        Errors = ProjectionErrors.Select(issue => issue.Message).ToArray(),
        ExpectedCostTotals = projection?.ExpectedCostTotals.Select(FormatExpectedCost).ToArray() ?? [],
        TotalExpectedEffortHours = TotalExpectedEffortHours,
        InsertionCandidate = insertionCandidate,
        Preview = mermaidPreview,
        Receipt = readbackRequired ? mutationReceipts.Values.LastOrDefault(receipt => receipt.Origin == viewOrigin) : null
    };

    private Task PublishObservationAsync()
    {
        if (disposed || !ObservationChanged.HasDelegate || ProjectId == Guid.Empty)
        {
            return Task.CompletedTask;
        }

        var observation = ProjectStructureGanttObservationFactory.FromProjection(
            ProjectId,
            projection,
            isLoading,
            loadError,
            selectedTaskNodeId: null,
            DateTimeOffset.UtcNow,
            loadedSurface?.ExpectedProjectAdmission is { } admission && admission.ProjectId == ProjectId
                ? new(admission.DatabaseProfileId, admission.ProjectId, admission.LifetimeId)
                : null);
        if (string.Equals(
                lastPublishedObservationFingerprint,
                observation.ContentFingerprint,
                StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        lastPublishedObservationFingerprint = observation.ContentFingerprint;
        return ObservationChanged.InvokeAsync(observation);
    }

    private bool CanMutate => !disposed && !mutationInFlight && !isLoading && !readbackRequired;

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

    protected override async Task OnParametersSetAsync()
    {
        if (disposed) {
            return;
        }
        activeOperations++;
        try {
            await LoadProjectionAsync();
        } finally {
            ReleaseOperation();
        }
    }

    private async Task LoadProjectionAsync() {
        if (ProjectId == Guid.Empty)
        {
            loadError = "A project is required before its Gantt schedule can be displayed.";
            projection = null;
            RetireView();
            return;
        }

        if (Surface is null || Surface.ProjectId != ProjectId)
        {
            loadError = "The project schedule cannot be built because the supplied structure does not match this project.";
            projection = null;
            RetireView();
            await PublishObservationAsync();
            return;
        }

        if (ReferenceEquals(loadedSurface, Surface))
        {
            return;
        }

        var capturedSurface = Surface;
        var previousAdmission = loadedSurface?.ExpectedProjectAdmission;
        loadedSurface = capturedSurface;
        RetireView();
        if (projectionProjectId != ProjectId || previousAdmission != capturedSurface.ExpectedProjectAdmission)
        {
            projectionProjectId = ProjectId;
            projection = null;
            projectionOriginUtc = ResolveProjectionOriginUtc(capturedSurface);
            insertionCandidate = CreateInsertionCandidate(projectionOriginUtc);
        }

        isLoading = true;
        loadError = null;
        // Publish the explicit loading observation before the projection is
        // built so a turn admitted mid-load sees partial facts, never stale
        // Canvas or Gantt content.
        await PublishObservationAsync();
        if (disposed || !ReferenceEquals(loadedSurface, capturedSurface) || !ReferenceEquals(Surface, capturedSurface)) {
            return;
        }
        try
        {
            var assignmentsTask = ProjectPartyIntegrationBridge.ListAssignmentsDetailedAsync(
                ProjectId,
                lifetimeCancellation.Token);
            var viewStateTask = ProjectWorkbenchService.LoadGanttViewStateAsync(
                ProjectId,
                lifetimeCancellation.Token);
            await Task.WhenAll(assignmentsTask, viewStateTask);
            if (disposed || !ReferenceEquals(loadedSurface, capturedSurface) || !ReferenceEquals(Surface, capturedSurface) ||
                    ProjectId != capturedSurface.ProjectId) {
                return;
            }
            var assignments = await assignmentsTask;
            loadedAssignments = assignments;
            var viewState = await viewStateTask;
            projection = ProjectionAdapter.Build(
                capturedSurface,
                assignments,
                new ProjectStructureGanttProjectionOptions(
                    projectionOriginUtc,
                    DefaultTaskDuration,
                    viewState.OrderedTaskNodeIds));
            readbackRequired = false;
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (disposed || !ReferenceEquals(loadedSurface, capturedSurface) || !ReferenceEquals(Surface, capturedSurface)) {
                return;
            }
            projection = null;
            loadError = "The project schedule could not be loaded. The project structure remains unchanged.";
            Logger.LogError(
                "Failed to build the Gantt projection for project {ProjectId}; failure type {FailureType}.",
                Mask(ProjectId),
                exception.GetType().Name);
        }
        finally
        {
            if (!disposed && ReferenceEquals(loadedSurface, capturedSurface) && ReferenceEquals(Surface, capturedSurface)) {
                isLoading = false;
            }
        }

        if (disposed || !ReferenceEquals(loadedSurface, capturedSurface) || !ReferenceEquals(Surface, capturedSurface)) {
            return;
        }

        await PublishObservationAsync();
    }

    private void RetireView() {
        viewOrigin = Guid.NewGuid();
        mermaidPreview = null;
        mutationInFlight = false;
        mutationOperation = null;
    }

    private Task OpenMermaidPreviewAsync()
    {
        if (projection is not { IsValid: true, Tasks.Count: > 0 })
        {
            return Task.CompletedTask;
        }

        mermaidPreview = new(viewOrigin, loadedSurface!.ProjectName,
            ProjectStructureGanttMermaidExporter.Build(loadedSurface.ProjectName, projection),
            projection.Tasks.Count, projection.Dependencies.Count);
        return Task.CompletedTask;
    }

    private Task CloseMermaidPreviewAsync()
    {
        mermaidPreview = null;
        return Task.CompletedTask;
    }

    private async Task ApplyTitleAsync(GanttTaskTitleChangeRequest request)
    {
        if (projection?.Tasks.Any(task => task.Id == request.TaskId && task.Title == request.CurrentTitle) != true) {
            return;
        }
        var owner = CaptureRenderedMutationOwner(loadedSurface ?? Surface);
        if (owner is null) {
            return;
        }
        await ExecuteMutationAsync(
            "title",
            cancellationToken => MutationService.ApplyTitleAsync(owner.ExpectedProjectAdmission!.ProjectId, request, cancellationToken, owner));
    }

    private async Task ApplyScheduleAsync(GanttTaskScheduleChangeRequest request)
    {
        if (!EnsureMutationHostAvailable()) {
            return;
        }
        var origin = viewOrigin;
        var renderedSurface = loadedSurface ?? Surface;
        var owner = CaptureRenderedMutationOwner(renderedSurface);
        if (owner is null) {
            return;
        }
        var renderedProjection = projection
            ?? throw new InvalidOperationException("The Gantt projection is unavailable.");
        var frozenRequest = ProjectStructureGanttScheduleMutationFactory.Create(request, renderedSurface, renderedProjection.Tasks);
        var pendingProjection = renderedProjection.WithScheduleChanges(request.AffectedTasks);
        projection = pendingProjection;

        var committed = await ExecuteMutationAsync(
            "schedule",
            cancellationToken => MutationService.ApplyScheduleAsync(
                owner.ExpectedProjectAdmission!.ProjectId,
                frozenRequest,
                cancellationToken, owner));
        if (committed == PlanningCommitState.Rejected && IsCurrent(origin) && ReferenceEquals(projection, pendingProjection))
        {
            projection = renderedProjection;
        }
    }

    private async Task ApplyDependencyAsync(GanttDependencyMutationRequest request)
    {
        var owner = CaptureRenderedMutationOwner(loadedSurface ?? Surface);
        if (owner is null) {
            return;
        }
        await ExecuteMutationAsync(
            "dependency",
            cancellationToken => MutationService.ApplyDependencyAsync(owner.ExpectedProjectAdmission!.ProjectId, request, cancellationToken, owner));
    }

    private async Task ApplyInsertionAsync(GanttTaskInsertionRequest request)
    {
        var owner = CaptureRenderedMutationOwner(loadedSurface ?? Surface);
        if (owner is null) {
            return;
        }
        await ExecuteMutationAsync(
            "insertion",
            cancellationToken => MutationService.ApplyInsertionAsync(owner.ExpectedProjectAdmission!.ProjectId, request, cancellationToken, owner),
            renewInsertionCandidate: true);
    }

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

        var origin = viewOrigin;
        var operation = Guid.NewGuid();
        var reload = MutationCommitted;
        mutationOperation = operation;
        mutationInFlight = true;
        try
        {
            await TaskEditCoordinator.OpenAsync(
                new ProjectStructureGanttTaskEditContext(
                    ProjectId,
                    loadedSurface,
                    projection,
                    loadedAssignments,
                    uiMutationOwner with { ExpectedProjectAdmission = loadedSurface.ExpectedProjectAdmission }) {
                        IsCurrent = () => IsCurrent(origin)
                    },
                taskId,
                () => IsCurrent(origin) ? reload.InvokeAsync() : Task.CompletedTask,
                lifetimeCancellation.Token);
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            EndMutation(operation);
        }
    }

    private async Task OpenTaskDialogAsync(DateTimeOffset startUtc, string? afterTaskNodeId) {
        if (!EnsureMutationHostAvailable()) {
            return;
        }
        var origin = viewOrigin;
        var operation = Guid.NewGuid();
        var reload = MutationCommitted;
        mutationOperation = operation;
        mutationInFlight = true;
        try {
            await OpenTaskDialogCoreAsync(startUtc, afterTaskNodeId, origin, operation, reload);
        } finally {
            EndMutation(operation);
        }
    }

    private void EndMutation(Guid operation) {
        if (mutationOperation == operation) {
            mutationOperation = null;
            mutationInFlight = false;
        }
    }

    private async Task OpenTaskDialogCoreAsync(DateTimeOffset startUtc, string? afterTaskNodeId, Guid origin, Guid operation, EventCallback reload) {
        var openedProjectId = ProjectId;
        var openedAdmission = loadedSurface?.ExpectedProjectAdmission
            ?? throw new InvalidOperationException("Reload the project structure before creating a task.");
        var openedOwner = uiMutationOwner with { ExpectedProjectAdmission = openedAdmission };
        IReadOnlyList<ProjectStructureTaskResourceOption> resourceOptions = [];
        IReadOnlyList<string> resourceWarnings = [];
        try
        {
            resourceOptions = await TaskResourceService.ListOptionsAsync(openedProjectId, lifetimeCancellation.Token);
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

        if (!IsCurrent(origin)) {
            return;
        }
        var normalizedStart = startUtc.ToUniversalTime();
        var session = new ProjectTaskDialogSession(() => IsCurrent(origin), () => reload.InvokeAsync(), Logger);
        await DialogService.OpenAsync<ProjectStructureGanttTaskDialog>(
            "Add project task",
            new Dictionary<string, object?>
            {
                [nameof(ProjectStructureGanttTaskDialog.CreateSubmitted)] =
                    new Func<ProjectStructureTaskCreateRequest, Task<PlanningTaskSaveResult>>(draft =>
                        session.SubmitAsync(() => CreateTaskAsync(openedProjectId,
                            draft with { ExpectedProjectAdmission = openedAdmission, AfterTaskNodeId = afterTaskNodeId },
                            openedOwner, origin, operation, session))),
                [nameof(ProjectStructureGanttTaskDialog.Readback)] = new Func<Task<PlanningTaskSaveResult>>(session.ReadbackAsync),
                [nameof(ProjectStructureGanttTaskDialog.ProjectId)] = openedProjectId,
                [nameof(ProjectStructureGanttTaskDialog.QuoteContext)] = new ProjectTaskQuoteContext(
                    openedAdmission.DatabaseProfileId, openedAdmission.LifetimeId, Guid.NewGuid()),
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

    }

    private async Task CreateTaskAsync(Guid openedProjectId, ProjectStructureTaskCreateRequest request,
        ProjectStructureAgentContext openedOwner, Guid origin, Guid operation, ProjectTaskDialogSession session) {
        var result = await TaskCreationService.CreateAsync(openedProjectId, request, openedOwner, CancellationToken.None);
        session.TaskCommitted([result.TaskNodeId], result.Pricing,
            request.Resource?.Kind is ProjectStructureTaskResourceKind.Person or ProjectStructureTaskResourceKind.Agent,
            rowOrderChanged: true);
        if (request.Resource?.Kind is ProjectStructureTaskResourceKind.Workflow or ProjectStructureTaskResourceKind.Process) {
            session.AttachmentCommitted(new(request.Resource, result.Pricing, result.ResourceNodeId) {
                LinkTargetNodeId = result.ResourceLinkTargetNodeId
            });
        }
        mutationReceipts[operation] = new(origin, operation, PlanningCommitState.Committed,
            session.Result!.Message, [new(result.TaskNodeId)]);
        await session.RefreshAfterCommitAsync();
        if (IsCurrent(origin)) {
            NotificationService.Success("Project task created", session.Result.Message);
        }
    }

    private async Task ApplyTaskOrderAsync(GanttTaskOrderChangeRequest request) {
        var owner = CaptureRenderedMutationOwner(loadedSurface ?? Surface);
        if (owner is null) {
            return;
        }
        var placement = request.Placement switch {
            GanttTaskOrderPlacement.Before => ProjectStructureGanttRowPlacement.Before,
            GanttTaskOrderPlacement.After => ProjectStructureGanttRowPlacement.After,
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Placement, "Unknown Gantt row placement.")
        };
        var move = new ProjectStructureGanttRowMoveRequest(request.TaskId.Value, request.AnchorTaskId.Value, placement);
        await ExecuteMutationAsync("task order", async cancellationToken => {
            try {
                await RowOrderService.MoveAsync(owner.ExpectedProjectAdmission!.ProjectId, move, owner, cancellationToken);
                return new([request.TaskId, request.AnchorTaskId], 0, 0);
            } catch (ProjectStructureGanttRowOrderConflictException) {
                throw new ProjectStructureGanttMutationException(ProjectStructureGanttMutationErrorCode.StaleTask,
                    "The task rows changed before this order request could be applied. Read the current project before trying again.");
            }
        });
    }

    private async Task<PlanningCommitState> ExecuteMutationAsync(
        string operation,
        Func<CancellationToken, Task<ProjectStructureGanttMutationResult>> mutation,
        bool renewInsertionCandidate = false)
    {
        if (!EnsureMutationHostAvailable())
        {
            return PlanningCommitState.Rejected;
        }

        var origin = viewOrigin;
        var operationId = Guid.NewGuid();
        var projectId = ProjectId;
        var reload = MutationCommitted;
        mutationOperation = operationId;
        mutationInFlight = true;
        var mutationCommitted = false;
        try
        {
            var result = await mutation(CancellationToken.None);
            mutationCommitted = true;
            mutationReceipts[operationId] = new(origin, operationId, PlanningCommitState.Committed,
                BuildMutationStatus(operation, result), result.AffectedTaskIds.ToArray());
            if (!IsCurrent(origin)) {
                return PlanningCommitState.Committed;
            }
            if (renewInsertionCandidate)
            {
                insertionCandidate = CreateInsertionCandidate(projectionOriginUtc);
            }

            await reload.InvokeAsync();
            if (!IsCurrent(origin)) {
                return PlanningCommitState.Committed;
            }
            NotificationService.Success(
                "Project schedule saved",
                BuildMutationStatus(operation, result));
            return PlanningCommitState.Committed;
        }
        catch (ProjectStructureGanttMutationException exception)
        {
            if (IsCurrent(origin)) {
                NotificationService.Error("Project schedule change rejected", exception.Message);
            }
            Logger.LogWarning(
                "Rejected Gantt {Operation} mutation for project {ProjectId} with code {ErrorCode}.",
                operation,
                Mask(projectId),
                exception.Code);
            return PlanningCommitState.Rejected;
        }
        catch (ProjectWriteAdmissionRejectedException) {
            if (IsCurrent(origin)) {
                readbackRequired = true;
                NotificationService.Error("Project schedule change rejected", "The original project lifetime is no longer available. Read the project again.");
            }
            return PlanningCommitState.Rejected;
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
            return mutationCommitted ? PlanningCommitState.Committed : PlanningCommitState.Unknown;
        }
        catch (Exception exception)
        {
            if (IsCurrent(origin)) {
                readbackRequired = true;
                if (!mutationCommitted) {
                    mutationReceipts[operationId] = new(origin, operationId, PlanningCommitState.Unknown,
                        "The save outcome could not be confirmed. Read the project again before making another change.", []);
                }
                NotifyUnexpectedMutationFailure(operation, mutationCommitted);
            }
            Logger.LogError(
                "Gantt {Operation} processing failed for project {ProjectId} after commit state {MutationCommitted}; failure type {FailureType}.",
                operation,
                Mask(projectId),
                mutationCommitted,
                exception.GetType().Name);
            return mutationCommitted ? PlanningCommitState.Committed : PlanningCommitState.Unknown;
        }
        finally
        {
            if (mutationOperation == operationId) {
                mutationInFlight = false;
                mutationOperation = null;
            }
        }
    }

    private ProjectStructureAgentContext? CaptureRenderedMutationOwner(ProjectStructureSurface surface) {
        if (surface.ExpectedProjectAdmission is not { } expected || surface.ProjectId != ProjectId || expected.ProjectId != surface.ProjectId) {
            NotificationService.Error("Refresh project schedule", "The displayed project scope is no longer available. Reload it before editing.");
            return null;
        }
        return uiMutationOwner with { ExpectedProjectAdmission = expected };
    }

    private bool EnsureMutationHostAvailable()
    {
        if (disposed || readbackRequired || loadError is not null) {
            return false;
        }
        if (!MutationCommitted.HasDelegate)
        {
            NotificationService.Error(
                "Project schedule change unavailable",
                "The schedule host is not configured to reload authoritative project data, so the change was not attempted.");
            return false;
        }

        if (isLoading)
        {
            NotificationService.Warning(
                "Project schedule refresh in progress",
                "Wait for the authoritative project schedule to finish refreshing before making another change.");
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

    private void NotifyUnexpectedMutationFailure(
        string operation,
        bool mutationCommitted,
        string committedDetails = "")
    {
        if (mutationCommitted)
        {
            NotificationService.Warning(
                "Project schedule saved; reload required",
                $"The change was saved, but the authoritative project schedule could not be reloaded.{committedDetails} Reload this page before making another change.");
            return;
        }

        NotificationService.Warning(
            "Project schedule outcome unconfirmed",
            $"The {operation} save could not be confirmed. Read the project again before making another change.");
    }

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
        if (disposed) {
            return ValueTask.CompletedTask;
        }
        disposed = true;
        lifetimeCancellation.Cancel();
        if (activeOperations == 0) {
            lifetimeCancellation.Dispose();
        }
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
