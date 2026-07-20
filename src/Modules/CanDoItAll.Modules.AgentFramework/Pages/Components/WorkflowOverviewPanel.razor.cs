using System.Globalization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Components.Charts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class WorkflowOverviewPanel : IDisposable
{
    private const int RecentRunCount = 6;
    private const int TopWorkflowCount = 5;
    private const string LoadFailureMessage = "Workflow dashboard data is temporarily unavailable. Retry the query.";

    private static readonly WorkflowLifecycleStatus[] LifecycleStatuses = Enum.GetValues<WorkflowLifecycleStatus>();

    private static readonly CdaChartOptions RunStateChartOptions = new()
    {
        Type = CdaChartType.Bar,
        XAxisType = CdaChartAxisType.Category,
        Unit = "runs",
        YAxisTitle = "Executions",
        ShowToolbar = false,
        EnableZoom = false,
        ShowLegend = false,
        ValuePrecision = 0,
        TooltipPrecision = 0,
        Palette = CdaChartPalette.Calm
    };

    private static readonly CdaChartOptions BackendChartOptions = new()
    {
        Type = CdaChartType.Donut,
        XAxisType = CdaChartAxisType.Category,
        Unit = "runs",
        ShowToolbar = false,
        EnableZoom = false,
        ShowLegend = true,
        ShowDataLabels = true,
        ValuePrecision = 0,
        TooltipPrecision = 0,
        LegendPosition = CdaChartLegendPosition.Bottom,
        Palette = CdaChartPalette.Energetic
    };

    private WorkflowOverviewSnapshot? snapshot;
    private IReadOnlyList<CdaChartSeries> runStateSeries = [];
    private IReadOnlyList<CdaChartSeries> backendSeries = [];
    private bool isLoading;
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

    private IReadOnlyList<CdaChartSeries> RunStateSeries
        => runStateSeries;

    private IReadOnlyList<CdaChartSeries> BackendSeries
        => backendSeries;

    protected override async Task OnParametersSetAsync()
    {
        if (!IsActive || loadAttempted && appliedRefreshVersion == RefreshVersion)
        {
            return;
        }

        await LoadSnapshotAsync();
    }

    public Task RefreshAsync()
        => IsActive ? LoadSnapshotAsync() : Task.CompletedTask;

    public void Dispose()
    {
        Interlocked.Increment(ref queryVersion);
        Interlocked.Exchange(ref activeQueryCancellation, null)?.Cancel();
        GC.SuppressFinalize(this);
    }

    private async Task LoadSnapshotAsync()
    {
        var requestVersion = Interlocked.Increment(ref queryVersion);
        using var queryCancellation = new CancellationTokenSource();
        var previousCancellation = Interlocked.Exchange(ref activeQueryCancellation, queryCancellation);
        previousCancellation?.Cancel();
        var requestedRefreshVersion = RefreshVersion;
        loadAttempted = true;
        appliedRefreshVersion = requestedRefreshVersion;
        isLoading = true;
        loadError = string.Empty;

        try
        {
            var result = await QueryService.QueryAsync(
                new WorkflowOverviewQuery(
                    RecentTake: RecentRunCount,
                    TopWorkflowTake: TopWorkflowCount),
                queryCancellation.Token);
            if (!IsCurrentQuery(requestVersion, queryCancellation))
            {
                return;
            }

            snapshot = result;
            runStateSeries = BuildSeries(
                result.RunsByState,
                CdaChartType.Bar,
                "Workflow runs",
                static state => FormatRunState(state));
            backendSeries = BuildSeries(
                result.RunsByBackend,
                CdaChartType.Donut,
                "Runtime backends",
                static backend => FormatBackend(backend));
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
            runStateSeries = [];
            backendSeries = [];
            loadError = LoadFailureMessage;
            Logger.LogError(
                "Workflow overview query failed for refresh version {RefreshVersion}, request version {RequestVersion}, and failure type {FailureType}.",
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

    private static IReadOnlyList<CdaChartSeries> BuildSeries<TKey>(
        IReadOnlyDictionary<TKey, int>? values,
        CdaChartType chartType,
        string name,
        Func<TKey, string> formatLabel)
        where TKey : notnull
    {
        if (values is null || values.Count == 0)
        {
            return [];
        }

        var points = values
            .Where(pair => pair.Value > 0)
            .OrderBy(pair => pair.Key)
            .Select(pair => new CdaChartPoint(formatLabel(pair.Key), pair.Value))
            .ToArray();
        return points.Length == 0
            ? []
            :
            [
                new CdaChartSeries
                {
                    Name = name,
                    Type = chartType,
                    Points = points
                }
            ];
    }

    private static string FormatSuccessRate(decimal? value)
        => value.HasValue
            ? $"{value.Value.ToString("F1", CultureInfo.InvariantCulture)}%"
            : "Not available";

    private static string FormatFailureCount(int value)
        => value == 1 ? "1 failure" : $"{FormatCount(value)} failures";

    private static string FormatCount(int value)
        => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string FormatRunState(WorkflowRunState state)
        => state switch
        {
            WorkflowRunState.NotStarted => "Not started",
            WorkflowRunState.WaitingForInput => "Waiting for input",
            _ => state.ToString()
        };

    private static string FormatBackend(WorkflowRuntimeBackendKind backend)
        => backend switch
        {
            WorkflowRuntimeBackendKind.InProcess => "In process",
            WorkflowRuntimeBackendKind.DurableTask => "Durable task",
            WorkflowRuntimeBackendKind.AzureFunctions => "Azure Functions",
            _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, "Unsupported workflow backend.")
        };

    private static string ResolveLifecycleTone(WorkflowLifecycleStatus? status)
        => status switch
        {
            WorkflowLifecycleStatus.Active => "success",
            WorkflowLifecycleStatus.Draft => "info",
            WorkflowLifecycleStatus.Suspended => "warning",
            WorkflowLifecycleStatus.Archived => "neutral",
            null => "danger",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported workflow lifecycle status.")
        };

    private static string ResolveRunTone(WorkflowRunState state)
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
}
