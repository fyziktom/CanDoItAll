namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    private static readonly TimeSpan RuntimeRefreshInterval = TimeSpan.FromSeconds(4);

    private CancellationTokenSource? runtimeRefreshCts;
    private Task? runtimeRefreshTask;

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        UpdateRuntimeRefreshLoop();
        return base.OnAfterRenderAsync(firstRender);
    }

    private void UpdateRuntimeRefreshLoop()
    {
        if (!ShouldAutoRefreshRuntime())
        {
            StopRuntimeRefreshLoop();
            return;
        }

        if (runtimeRefreshTask is { IsCompleted: false })
        {
            return;
        }

        runtimeRefreshCts?.Dispose();
        runtimeRefreshCts = new CancellationTokenSource();
        runtimeRefreshTask = MonitorRuntimeAsync(runtimeRefreshCts.Token);
    }

    private bool ShouldAutoRefreshRuntime()
    {
        if (!selectedProcessId.HasValue ||
            !string.Equals(detailTab, "runs", StringComparison.Ordinal))
        {
            return false;
        }

        return runs.Any(run => run.Status == ProcessRunStatus.Active) ||
               launchPlans.Any(plan => plan.Status is ProcessLaunchPlanStatus.PendingApproval
                   or ProcessLaunchPlanStatus.Approved
                   or ProcessLaunchPlanStatus.Provisioning
                   or ProcessLaunchPlanStatus.Ready
                   or ProcessLaunchPlanStatus.Executing);
    }

    private async Task MonitorRuntimeAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(RuntimeRefreshInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (!ShouldAutoRefreshRuntime())
                {
                    break;
                }

                await InvokeAsync(() => RefreshRuntimeWorkspaceAsync(cancellationToken));
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            runtimeRefreshTask = null;
        }
    }

    private async Task RefreshRuntimeWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (!selectedProcessId.HasValue)
        {
            return;
        }

        analytics = await ProcessesService.GetAnalyticsAsync(selectedProcessId, ProjectId);
        launchPlans = await ProcessesService.ListLaunchPlansAsync(selectedProcessId, ProjectId);
        selectedLaunchPlanId = ResolveSelectedLaunchPlanId();
        await LoadLaunchPlanDetailsAsync();
        runs = await ProcessesService.ListRunsAsync(selectedProcessId, ProjectId);
        activeRunSummaries = await RunDetailsLoader.LoadActiveRunSummariesAsync(runs, cancellationToken);

        var nextSelectedRunId = ResolveSelectedRunId();
        if (nextSelectedRunId != selectedRunId)
        {
            selectedCanvasNodeId = null;
            ResetRuntimeCanvasState();
        }

        selectedRunId = nextSelectedRunId;
        await LoadRunDetailsAsync();
        RefreshCanvasSurface();
        StateHasChanged();

        if (!ShouldAutoRefreshRuntime())
        {
            StopRuntimeRefreshLoop();
        }
    }

    private void StopRuntimeRefreshLoop()
    {
        if (runtimeRefreshCts is null)
        {
            return;
        }

        runtimeRefreshCts.Cancel();
        runtimeRefreshCts.Dispose();
        runtimeRefreshCts = null;
        runtimeRefreshTask = null;
    }
}
