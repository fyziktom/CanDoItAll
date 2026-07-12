using System.Globalization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class WorkflowAnalyticsPanel : IDisposable
{
    private const int RecentRunCount = 8;
    private const string LoadFailureMessage = "Workflow analytics are temporarily unavailable. Retry the query.";
    private WorkflowAnalyticsSnapshot? snapshot;
    private WorkflowAnalyticsScopeKind scopeKind = WorkflowAnalyticsScopeKind.All;
    private Guid? selectedWorkflowValue;
    private bool isLoading;
    private bool loadAttempted;
    private string loadError = string.Empty;
    private long appliedRefreshVersion = long.MinValue;
    private long queryVersion;
    private CancellationTokenSource? activeQueryCancellation;

    [Parameter, EditorRequired]
    public IWorkflowAnalyticsQueryService QueryService { get; set; } = default!;

    [Inject]
    public ILogger<WorkflowAnalyticsPanel> Logger { get; set; } = default!;

    [Parameter]
    public IReadOnlyList<WorkflowCatalogItem> Workflows { get; set; } = [];

    [Parameter]
    public bool IsActive { get; set; }

    [Parameter]
    public long RefreshVersion { get; set; }

    private IReadOnlyList<WorkflowCatalogItem> OrderedWorkflows
        => Workflows
            .OrderBy(workflow => workflow.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(workflow => workflow.Id.Value)
            .ToArray();

    private IReadOnlyList<WorkflowAnalyticsCountRow> DistributionRows
        => snapshot is null
            ? []
            : snapshot.RunsByState
                .OrderBy(pair => pair.Key)
                .Select(pair => new WorkflowAnalyticsCountRow(
                    "State",
                    pair.Key.ToString(),
                    pair.Value,
                    ResolveStateTone(pair.Key)))
                .Concat(snapshot.RunsByBackend
                    .OrderBy(pair => pair.Key)
                    .Select(pair => new WorkflowAnalyticsCountRow(
                        "Backend",
                        pair.Key.ToString(),
                        pair.Value,
                        "info")))
                .ToArray();

    private IReadOnlyList<WorkflowProviderModelAnalyticsRow> ProviderModelRows
        => snapshot?.ProviderModels
            .OrderByDescending(row => row.Usage.TotalTokens)
            .ThenBy(row => row.ProviderName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Model, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

    private IReadOnlyList<WorkflowRunSnapshot> RecentRuns => snapshot?.RecentRuns ?? [];

    private string ScopeDescription
        => scopeKind switch
        {
            WorkflowAnalyticsScopeKind.All => "All workflows",
            WorkflowAnalyticsScopeKind.SelectedWorkflow => OrderedWorkflows
                .FirstOrDefault(workflow => workflow.Id.Value == selectedWorkflowValue)?.Name ?? "Selected workflow",
            _ => throw new InvalidOperationException($"Unsupported analytics scope '{scopeKind}'.")
        };

    protected override async Task OnParametersSetAsync()
    {
        NormalizeScope();
        if (!IsActive || loadAttempted && appliedRefreshVersion == RefreshVersion)
        {
            return;
        }

        await LoadSnapshotAsync();
    }

    public Task RefreshAsync()
        => IsActive ? LoadSnapshotAsync() : Task.CompletedTask;

    private async Task HandleScopeChangedAsync(WorkflowAnalyticsScopeKind value)
    {
        scopeKind = value;
        NormalizeScope();
        await LoadSnapshotAsync();
    }

    private async Task HandleSelectedWorkflowChangedAsync(Guid? value)
    {
        selectedWorkflowValue = value;
        NormalizeScope();
        await LoadSnapshotAsync();
    }

    private async Task LoadSnapshotAsync()
    {
        var requestVersion = Interlocked.Increment(ref queryVersion);
        using var queryCancellation = new CancellationTokenSource();
        var previousCancellation = Interlocked.Exchange(ref activeQueryCancellation, queryCancellation);
        previousCancellation?.Cancel();
        var workflowId = ResolveWorkflowId();
        var requestedRefreshVersion = RefreshVersion;
        var requestedScope = scopeKind;
        loadAttempted = true;
        appliedRefreshVersion = requestedRefreshVersion;
        isLoading = true;
        loadError = string.Empty;

        try
        {
            var result = await QueryService.QueryAsync(
                new WorkflowAnalyticsQuery(
                    WorkflowId: workflowId,
                    RecentTake: RecentRunCount),
                queryCancellation.Token);
            if (!IsCurrentQuery(requestVersion, queryCancellation))
            {
                return;
            }

            snapshot = result;
        }
        catch (OperationCanceledException) when (queryCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (!IsCurrentQuery(requestVersion, queryCancellation))
            {
                return;
            }

            snapshot = null;
            loadError = LoadFailureMessage;
            Logger.LogError(
                "Workflow analytics query failed for scope {ScopeKind}, workflow {WorkflowId}, refresh version {RefreshVersion}, request version {RequestVersion}, and failure type {FailureType}.",
                requestedScope,
                workflowId?.Value,
                requestedRefreshVersion,
                requestVersion,
                exception.GetType().Name);
        }
        finally
        {
            if (IsCurrentQuery(requestVersion, queryCancellation))
            {
                isLoading = false;
                Interlocked.CompareExchange(ref activeQueryCancellation, null, queryCancellation);
            }
        }
    }

    private bool IsCurrentQuery(long requestVersion, CancellationTokenSource cancellation)
        => requestVersion == Interlocked.Read(ref queryVersion) && !cancellation.IsCancellationRequested;

    public void Dispose()
    {
        Interlocked.Increment(ref queryVersion);
        Interlocked.Exchange(ref activeQueryCancellation, null)?.Cancel();
        GC.SuppressFinalize(this);
    }

    private void NormalizeScope()
    {
        if (Workflows.Count == 0)
        {
            scopeKind = WorkflowAnalyticsScopeKind.All;
            selectedWorkflowValue = null;
            return;
        }

        if (selectedWorkflowValue.HasValue &&
            Workflows.Any(workflow => workflow.Id.Value == selectedWorkflowValue.Value))
        {
            return;
        }

        selectedWorkflowValue = OrderedWorkflows[0].Id.Value;
    }

    private WorkflowId? ResolveWorkflowId()
        => scopeKind == WorkflowAnalyticsScopeKind.SelectedWorkflow && selectedWorkflowValue.HasValue
            ? new WorkflowId(selectedWorkflowValue.Value)
            : null;

    private string FormatRunDuration(WorkflowRunId runId)
    {
        var duration = snapshot?.Runs.FirstOrDefault(row => row.Run.RunId == runId)?.Duration;
        return duration.HasValue ? FormatDuration(duration.Value) : "Unavailable";
    }

    private static string FormatObservationCompleteness(WorkflowUsageAnalyticsTotals usage)
    {
        if (usage.ObservationCount == 0)
        {
            return "0 / 0 (not available)";
        }

        var percentage = (decimal)usage.UsageKnownObservationCount / usage.ObservationCount * 100m;
        return $"{FormatCount(usage.UsageKnownObservationCount)} / {FormatCount(usage.ObservationCount)} ({percentage.ToString("F1", CultureInfo.InvariantCulture)}%)";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
        {
            return $"{(int)duration.TotalDays}d {duration:hh\\:mm\\:ss}";
        }

        return duration.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
    }

    private static string FormatCost(decimal costUsd)
        => $"${costUsd.ToString("F6", CultureInfo.InvariantCulture)}";

    private static string FormatCount(long value)
        => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string FormatShortId(Guid value)
        => value.ToString("N", CultureInfo.InvariantCulture)[..8];

    private static string ResolveStateTone(WorkflowRunState state)
        => state switch
        {
            WorkflowRunState.Completed => "success",
            WorkflowRunState.Failed => "danger",
            WorkflowRunState.Running => "info",
            WorkflowRunState.WaitingForInput => "warning",
            WorkflowRunState.Cancelled => "neutral",
            WorkflowRunState.Idle => "secondary",
            WorkflowRunState.NotStarted => "neutral",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unsupported workflow run state.")
        };

    private enum WorkflowAnalyticsScopeKind
    {
        All,
        SelectedWorkflow
    }

    private sealed record WorkflowAnalyticsCountRow(
        string Dimension,
        string Label,
        int Count,
        string Tone);
}
