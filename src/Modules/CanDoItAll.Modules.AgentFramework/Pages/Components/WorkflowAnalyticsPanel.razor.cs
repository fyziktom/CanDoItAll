using CanDoItAll.AgentFramework.Core;
using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Workflows.UI;
using System.Globalization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class WorkflowAnalyticsPanel : IDisposable {
    private const int RecentRunCount = 8;
    private const string LoadFailureMessage = "Workflow analytics are temporarily unavailable. Retry the query.";
    private WorkflowAnalyticsSnapshot? snapshot;
    private WorkflowAnalyticsScopeKind scopeKind = WorkflowAnalyticsScopeKind.All;
    private Guid? selectedWorkflowValue;
    private bool isLoading;
    private bool disposed;
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
        => scopeKind switch {
            WorkflowAnalyticsScopeKind.All => "All workflows",
            WorkflowAnalyticsScopeKind.SelectedWorkflow => OrderedWorkflows
                .FirstOrDefault(workflow => workflow.Id.Value == selectedWorkflowValue)?.Name ?? "Selected workflow",
            _ => throw new InvalidOperationException($"Unsupported analytics scope '{scopeKind}'.")
        };

    protected override async Task OnParametersSetAsync() {
        var previousWorkflow = ResolveWorkflowId();
        NormalizeScope();
        if (previousWorkflow != ResolveWorkflowId()) {
            loadAttempted = false;
        }

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

    private async Task HandleScopeChangedAsync(WorkflowAnalyticsScopeKind value) {
        scopeKind = value;
        NormalizeScope();
        await LoadSnapshotAsync();
    }

    private async Task HandleSelectedWorkflowChangedAsync(Guid? value) {
        selectedWorkflowValue = value;
        NormalizeScope();
        await LoadSnapshotAsync();
    }

    private async Task LoadSnapshotAsync() {
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

        try {
            var result = await QueryService.QueryAsync(
                new WorkflowAnalyticsQuery(
                    WorkflowId: workflowId,
                    RecentTake: RecentRunCount),
                queryCancellation.Token);
            if (!IsCurrentQuery(requestVersion, queryCancellation)) {
                return;
            }

            snapshot = result with {
                DefinitionsByStatus = result.DefinitionsByStatus.ToImmutableDictionary(),
                RunsByState = result.RunsByState.ToImmutableDictionary(),
                RunsByBackend = result.RunsByBackend.ToImmutableDictionary(),
                Runs = result.Runs.ToImmutableArray(),
                ProviderModels = result.ProviderModels.ToImmutableArray(),
                Nodes = result.Nodes.ToImmutableArray(),
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
                "Workflow analytics query failed for scope {ScopeKind}, workflow {WorkflowId}, refresh version {RefreshVersion}, request version {RequestVersion}, and failure type {FailureType}.",
                requestedScope,
                workflowId?.Value,
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

    public void Dispose() {
        disposed = true;
        Interlocked.Increment(ref queryVersion);
        Interlocked.Exchange(ref activeQueryCancellation, null)?.Cancel();
        GC.SuppressFinalize(this);
    }

    private void NormalizeScope() {
        if (Workflows.Count == 0) {
            scopeKind = WorkflowAnalyticsScopeKind.All;
            selectedWorkflowValue = null;
            return;
        }

        if (selectedWorkflowValue.HasValue &&
            Workflows.Any(workflow => workflow.Id.Value == selectedWorkflowValue.Value)) {
            return;
        }

        selectedWorkflowValue = OrderedWorkflows[0].Id.Value;
    }

    private WorkflowId? ResolveWorkflowId()
        => scopeKind == WorkflowAnalyticsScopeKind.SelectedWorkflow && selectedWorkflowValue.HasValue
            ? new WorkflowId(selectedWorkflowValue.Value)
            : null;

    private string FormatRunDuration(WorkflowRunId runId) {
        var duration = snapshot?.Runs.FirstOrDefault(row => row.Run.RunId == runId)?.Duration;
        return duration.HasValue ? FormatDuration(duration.Value) : "Unavailable";
    }

    private static string FormatObservationCompleteness(WorkflowUsageAnalyticsTotals usage) {
        if (usage.ObservationCount == 0) {
            return "0 / 0 (not available)";
        }

        var percentage = (decimal)usage.UsageKnownObservationCount / usage.ObservationCount * 100m;
        return $"{FormatCount(usage.UsageKnownObservationCount)} / {FormatCount(usage.ObservationCount)} ({percentage.ToString("F1", CultureInfo.InvariantCulture)}%)";
    }

    private static string FormatDuration(TimeSpan duration) {
        if (duration.TotalDays >= 1) {
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


    private sealed record WorkflowAnalyticsCountRow(
        string Dimension,
        string Label,
        int Count,
        string Tone);
    private WorkflowAnalyticsPresentation Presentation => new() {
        Revision = queryVersion,
        State = !IsActive ? WorkflowQueryState.Inactive : isLoading ? WorkflowQueryState.Loading
            : loadError.Length > 0 ? WorkflowQueryState.Failed : WorkflowQueryState.Ready,
        Error = loadError,
        RuntimeMetrics = snapshot is null ? [] : [
            new("Definitions", FormatCount(snapshot.DefinitionCount), "workflow-analytics-definition-count"),
            new("Active definitions", FormatCount(snapshot.ActiveDefinitionCount), "workflow-analytics-active-definition-count"),
            new("Runs", FormatCount(snapshot.RunCount), "workflow-analytics-run-count"),
            new("Running", FormatCount(snapshot.RunningRunCount), "workflow-analytics-running-count"),
            new("Waiting for input", FormatCount(snapshot.WaitingForInputRunCount), "workflow-analytics-waiting-count"),
            new("Failed", FormatCount(snapshot.FailedRunCount), "workflow-analytics-failed-count")
        ],
        UsageMetrics = snapshot is null ? [] : [
            new("Total tokens", FormatCount(snapshot.Usage.TotalTokens), "workflow-analytics-total-tokens"),
            new("Input tokens", FormatCount(snapshot.Usage.InputTokens), "workflow-analytics-input-tokens"),
            new("Cached input", FormatCount(snapshot.Usage.CachedInputTokens), "workflow-analytics-cached-tokens"),
            new("Output tokens", FormatCount(snapshot.Usage.OutputTokens), "workflow-analytics-output-tokens"),
            new("Reasoning tokens", FormatCount(snapshot.Usage.ReasoningTokens), "workflow-analytics-reasoning-tokens"),
            new("Known cost", FormatCost(snapshot.Usage.KnownCostUsd), "workflow-analytics-known-cost"),
            new("Unknown pricing", FormatCount(snapshot.Usage.PricingUnknownObservationCount), "workflow-analytics-unknown-pricing"),
            new("Known-priced observations", FormatCount(snapshot.Usage.PricingKnownObservationCount), "workflow-analytics-known-pricing"),
            new("Usage completeness", FormatObservationCompleteness(snapshot.Usage), "workflow-analytics-completeness"),
            new("Unknown usage", FormatCount(snapshot.Usage.UsageUnknownObservationCount), "workflow-analytics-unknown-usage")
        ],
        DurationMetrics = snapshot is null ? [] : [
            new("Total", FormatDuration(snapshot.Duration.Total), "workflow-analytics-duration-total"),
            new("Average", FormatDuration(snapshot.Duration.Average), "workflow-analytics-duration-average"),
            new("Minimum", FormatDuration(snapshot.Duration.Minimum), "workflow-analytics-duration-minimum"),
            new("Maximum", FormatDuration(snapshot.Duration.Maximum), "workflow-analytics-duration-maximum"),
            new("Final durations", FormatCount(snapshot.Duration.FinalRunCount), "workflow-analytics-duration-final"),
            new("Active durations", FormatCount(snapshot.Duration.ActiveRunCount), "workflow-analytics-duration-active")
        ],
        AsOf = snapshot is null ? "" : WorkflowPresentationTime.Format(snapshot.AsOfUtc),
        Scope = scopeKind,
        SelectedWorkflow = selectedWorkflowValue,
        ScopeDescription = ScopeDescription,
        Workflows = OrderedWorkflows.Select(row => new WorkflowChoice(row.Id.Value, row.Name)).ToImmutableArray(),
        Distribution = DistributionRows.Select(row => new WorkflowDistributionView(row.Dimension, row.Label, FormatCount(row.Count), row.Tone)).ToImmutableArray(),
        Providers = ProviderModelRows.Select(row => new WorkflowProviderUsageView(row.ProviderName, row.Model, row.ProviderKind?.ToString(),
            FormatCount(row.Usage.ObservationCount), FormatCount(row.Usage.TotalTokens), FormatCost(row.Usage.KnownCostUsd), FormatCount(row.Usage.PricingUnknownObservationCount))).ToImmutableArray(),
        RecentRuns = RecentRuns.Select(row => new WorkflowRunView(row.RunId.Value, row.State.ToString(), ResolveStateTone(row.State),
            row.Backend.ToString(), WorkflowPresentationTime.Format(row.UpdatedAtUtc), WorkflowEventPresentationPolicy.RunSummary(row.State, row.Summary), Duration: FormatRunDuration(row.RunId))).ToImmutableArray()
    };

    private async Task HandleIntentAsync(WorkflowQueryIntent intent) {
        if (!IsActive || intent.Revision != queryVersion) {
            return;
        }

        switch (intent.Action) {
            case WorkflowQueryAction.Refresh:
                await RefreshAsync();
                break;
            case WorkflowQueryAction.Scope:
                await HandleScopeChangedAsync(intent.Scope);
                break;
            case WorkflowQueryAction.Workflow when Workflows.Any(row => row.Id.Value == intent.WorkflowId):
                await HandleSelectedWorkflowChangedAsync(intent.WorkflowId);
                break;
        }
    }
}
