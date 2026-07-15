using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Gantt;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructureGanttPanel : ComponentBase, IAsyncDisposable
{
    private static readonly TimeSpan DefaultTaskDuration = TimeSpan.FromHours(8);
    private readonly CancellationTokenSource lifetimeCancellation = new();
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
    private ILogger<ProjectStructureGanttPanel> Logger { get; set; } = default!;

    [Inject]
    private NotificationService NotificationService { get; set; } = default!;

    [Parameter]
    public Guid ProjectId { get; set; }

    [Parameter, EditorRequired]
    public ProjectStructureSurface Surface { get; set; } = default!;

    [Parameter, EditorRequired]
    public EventCallback MutationCommitted { get; set; }

    private bool CanInsertTask =>
        projection is { IsValid: true, Dependencies.Count: > 0 } &&
        !mutationInFlight;

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
            var assignments = await ProjectPartyIntegrationBridge.ListAssignmentsDetailedAsync(
                ProjectId,
                lifetimeCancellation.Token);
            projection = ProjectionAdapter.Build(
                Surface,
                assignments,
                new ProjectStructureGanttProjectionOptions(projectionOriginUtc, DefaultTaskDuration));
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

    private async Task ExecuteMutationAsync(
        string operation,
        Func<CancellationToken, Task<ProjectStructureGanttMutationResult>> mutation,
        bool renewInsertionCandidate = false)
    {
        if (!MutationCommitted.HasDelegate)
        {
            NotificationService.Error(
                "Project schedule change unavailable",
                "The schedule host is not configured to reload authoritative project data, so the change was not attempted.");
            return;
        }

        if (mutationInFlight)
        {
            NotificationService.Warning(
                "Project schedule change in progress",
                "Another project schedule change is still being saved.");
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
            const string reloadRequired = "The change was saved, but the authoritative project schedule could not be reloaded. Reload this page before making another change.";
            const string saveFailed = "The project schedule change could not be saved. The chart remains unchanged.";
            if (mutationCommitted)
            {
                NotificationService.Warning("Project schedule saved; reload required", reloadRequired);
            }
            else
            {
                NotificationService.Error("Project schedule save failed", saveFailed);
            }
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

    private static bool IsScheduleProjectionIssue(ProjectStructureGanttProjectionIssueCode code)
        => code is ProjectStructureGanttProjectionIssueCode.ScheduleSynthesized or
            ProjectStructureGanttProjectionIssueCode.ScheduleStartSynthesized or
            ProjectStructureGanttProjectionIssueCode.ScheduleEndSynthesized;

    private static string Mask(Guid value)
    {
        var formatted = value.ToString("N");
        return $"{formatted[..6]}...{formatted[^4..]}";
    }

    public ValueTask DisposeAsync()
    {
        lifetimeCancellation.Cancel();
        lifetimeCancellation.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
