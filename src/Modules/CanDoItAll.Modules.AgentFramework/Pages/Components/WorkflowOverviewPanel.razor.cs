using CanDoItAll.AgentFramework.Core;
using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Workflows.UI;
using System.Globalization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class WorkflowOverviewPanel : IDisposable {
    private const int RecentRunCount = 6;
    private const int TopWorkflowCount = 5;
    private const string LoadFailureMessage = "Workflow dashboard data is temporarily unavailable. Retry the query.";

    private static readonly WorkflowLifecycleStatus[] LifecycleStatuses = Enum.GetValues<WorkflowLifecycleStatus>();

    private WorkflowOverviewSnapshot? snapshot;
    private bool isLoading;
    private bool disposed;
    private bool loadAttempted;
    private string loadError = string.Empty;
    private long appliedRefreshVersion = long.MinValue;
    private long queryVersion;
    private CancellationTokenSource? activeQueryCancellation;

    [Parameter, EditorRequired]
    public IWorkflowOverviewQueryService QueryService { get; set; } = default!;

    [Parameter]
    public bool IsActive { get; set; }

    [Parameter]
    public long RefreshVersion { get; set; }

    [Inject]
    public ILogger<WorkflowOverviewPanel> Logger { get; set; } = default!;



    protected override async Task OnParametersSetAsync() {
        if (!IsActive) {
            Interlocked.Increment(ref queryVersion);
            Interlocked.Exchange(ref activeQueryCancellation, null)?.Cancel();
            if (isLoading) {
                loadAttempted = false;
            }

            isLoading = false;
            return;
        }

        if (disposed || loadAttempted && appliedRefreshVersion == RefreshVersion) {
            return;
        }

        await LoadSnapshotAsync();
    }

    public Task RefreshAsync()
        => !disposed && IsActive ? LoadSnapshotAsync() : Task.CompletedTask;

    public void Dispose() {
        disposed = true;
        Interlocked.Increment(ref queryVersion);
        Interlocked.Exchange(ref activeQueryCancellation, null)?.Cancel();
        GC.SuppressFinalize(this);
    }

    private async Task LoadSnapshotAsync() {
        var requestVersion = Interlocked.Increment(ref queryVersion);
        using var queryCancellation = new CancellationTokenSource();
        var previousCancellation = Interlocked.Exchange(ref activeQueryCancellation, queryCancellation);
        previousCancellation?.Cancel();
        var requestedRefreshVersion = RefreshVersion;
        loadAttempted = true;
        appliedRefreshVersion = requestedRefreshVersion;
        isLoading = true;
        loadError = string.Empty;

        try {
            var result = await QueryService.QueryAsync(
                new WorkflowOverviewQuery(
                    RecentTake: RecentRunCount,
                    TopWorkflowTake: TopWorkflowCount),
                queryCancellation.Token);
            if (!IsCurrentQuery(requestVersion, queryCancellation)) {
                return;
            }

            snapshot = result with {
                DefinitionsByStatus = result.DefinitionsByStatus.ToImmutableDictionary(),
                RunsByState = result.RunsByState.ToImmutableDictionary(),
                RunsByBackend = result.RunsByBackend.ToImmutableDictionary(),
                TopWorkflows = result.TopWorkflows.ToImmutableArray(),
                RecentlyUpdatedDefinitions = result.RecentlyUpdatedDefinitions.ToImmutableArray(),
                RecentRuns = result.RecentRuns.ToImmutableArray()
            };
        } catch (OperationCanceledException) when (queryCancellation.IsCancellationRequested) {
        } catch (Exception exception) {
            if (!IsCurrentQuery(requestVersion, queryCancellation)) {
                return;
            }

            snapshot = null;
            loadError = LoadFailureMessage;
            Logger.LogError(
                "Workflow overview query failed for refresh version {RefreshVersion}, request version {RequestVersion}, and failure type {FailureType}.",
                requestedRefreshVersion,
                requestVersion,
                exception.GetType().Name);
        } finally {
            if (IsCurrentQuery(requestVersion, queryCancellation)) {
                isLoading = false;
                Interlocked.CompareExchange(ref activeQueryCancellation, null, queryCancellation);
            }
        }
    }

    private bool IsCurrentQuery(long requestVersion, CancellationTokenSource cancellation)
        => !disposed && IsActive && requestVersion == Interlocked.Read(ref queryVersion) && !cancellation.IsCancellationRequested;

    private static string FormatSuccessRate(decimal? value)
        => value.HasValue
            ? $"{value.Value.ToString("F1", CultureInfo.InvariantCulture)}%"
            : "Not available";

    private static string FormatFailureCount(int value)
        => value == 1 ? "1 failure" : $"{FormatCount(value)} failures";

    private static string FormatCount(int value)
        => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string FormatRunState(WorkflowRunState state)
        => state switch {
            WorkflowRunState.NotStarted => "Not started",
            WorkflowRunState.WaitingForInput => "Waiting for input",
            _ => state.ToString()
        };

    private static string FormatBackend(WorkflowRuntimeBackendKind backend)
        => backend switch {
            WorkflowRuntimeBackendKind.InProcess => "In process",
            WorkflowRuntimeBackendKind.DurableTask => "Durable task",
            WorkflowRuntimeBackendKind.AzureFunctions => "Azure Functions",
            _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, "Unsupported workflow backend.")
        };

    private static string ResolveLifecycleTone(WorkflowLifecycleStatus? status)
        => status switch {
            WorkflowLifecycleStatus.Active => "success",
            WorkflowLifecycleStatus.Draft => "info",
            WorkflowLifecycleStatus.Suspended => "warning",
            WorkflowLifecycleStatus.Archived => "neutral",
            null => "danger",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported workflow lifecycle status.")
        };

    private static string ResolveRunTone(WorkflowRunState state)
        => state switch {
            WorkflowRunState.Completed => "success",
            WorkflowRunState.Failed => "danger",
            WorkflowRunState.Running => "info",
            WorkflowRunState.WaitingForInput => "warning",
            WorkflowRunState.Cancelled => "neutral",
            WorkflowRunState.Idle => "secondary",
            WorkflowRunState.NotStarted => "neutral",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unsupported workflow run state.")
        };
    private WorkflowOverviewPresentation Presentation => new() {
        Revision = queryVersion,
        State = !IsActive ? WorkflowQueryState.Inactive : isLoading ? WorkflowQueryState.Loading
            : loadError.Length > 0 ? WorkflowQueryState.Failed : WorkflowQueryState.Ready,
        Error = loadError,
        Metrics = snapshot is null ? [] : [
            new("Definitions", FormatCount(snapshot.DefinitionCount), "workflow-overview-definition-count"),
            new("Active", FormatCount(snapshot.ActiveDefinitionCount), "workflow-overview-active-count"),
            new("Runs", FormatCount(snapshot.RunCount), "workflow-overview-run-count"),
            new("Running", FormatCount(snapshot.RunningRunCount), "workflow-overview-running-count"),
            new("Waiting", FormatCount(snapshot.WaitingForInputRunCount), "workflow-overview-waiting-count"),
            new("Completed", FormatCount(snapshot.CompletedRunCount), "workflow-overview-completed-count"),
            new("Failed", FormatCount(snapshot.FailedRunCount), "workflow-overview-failed-count"),
            new("Success rate", FormatSuccessRate(snapshot.SuccessRatePercent), "workflow-overview-success-rate")
        ],
        AsOf = snapshot?.AsOfUtc.LocalDateTime.ToString("g") ?? "",
        TopWorkflows = snapshot?.TopWorkflows.Select(row => new WorkflowRankedView(row.Name, row.Status?.ToString() ?? "Deleted",
            ResolveLifecycleTone(row.Status), row.LastRunAtUtc.LocalDateTime.ToString("g"), FormatCount(row.RunCount), FormatFailureCount(row.FailedRunCount))).ToImmutableArray() ?? [],
        RecentRuns = snapshot?.RecentRuns.Select(row => new WorkflowActivityView(row.WorkflowName,
            WorkflowFailureDisplayFormatter.ToUserMessage(row.Run.Summary), row.Run.UpdatedAtUtc.LocalDateTime.ToString("g"), FormatRunState(row.Run.State), ResolveRunTone(row.Run.State))).ToImmutableArray() ?? [],
        RecentlyUpdatedDefinitions = snapshot?.RecentlyUpdatedDefinitions.Select(row => new WorkflowActivityView(row.Name,
            row.Description, row.UpdatedAtUtc.LocalDateTime.ToString("g"), row.Status.ToString(), ResolveLifecycleTone(row.Status), FormatBackend(row.PreferredBackend))).ToImmutableArray() ?? [],
        Lifecycle = snapshot is null ? [] : LifecycleStatuses.Select(status => new WorkflowBadge(
            $"{status}: {FormatCount(snapshot.DefinitionsByStatus.GetValueOrDefault(status))}", ResolveLifecycleTone(status))).ToImmutableArray(),
        RunStates = snapshot?.RunsByState.Where(pair => pair.Value > 0).OrderBy(pair => pair.Key)
            .Select(pair => new WorkflowChartPointView(FormatRunState(pair.Key), pair.Value)).ToImmutableArray() ?? [],
        Backends = snapshot?.RunsByBackend.Where(pair => pair.Value > 0).OrderBy(pair => pair.Key)
            .Select(pair => new WorkflowChartPointView(FormatBackend(pair.Key), pair.Value)).ToImmutableArray() ?? []
    };

    private async Task HandleIntentAsync(WorkflowQueryIntent intent) {
        if (!IsActive || intent.Revision != queryVersion) {
            return;
        }

        await RefreshAsync();
    }
}
