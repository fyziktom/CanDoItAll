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
            (!string.Equals(detailTab, DetailTabRuns, StringComparison.Ordinal) &&
                !string.Equals(detailTab, DetailTabAnalytics, StringComparison.Ordinal)))
        {
            return false;
        }

        if (runs.Any(run => run.Status == ProcessRunStatus.Active))
        {
            return true;
        }

        return string.Equals(detailTab, DetailTabRuns, StringComparison.Ordinal) &&
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

        await LoadRuntimePaneDataAsync(forceRefresh: true, cancellationToken);
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
        if (runtimeRefreshTask is null || runtimeRefreshTask.IsCompleted)
        {
            runtimeRefreshCts.Dispose();
            runtimeRefreshCts = null;
            runtimeRefreshTask = null;
        }
    }

    private async Task StopRuntimeRefreshLoopAsync()
    {
        var refreshCts = runtimeRefreshCts;
        var refreshTask = runtimeRefreshTask;
        if (refreshCts is null && refreshTask is null)
        {
            return;
        }

        refreshCts?.Cancel();
        try
        {
            if (refreshTask is not null)
            {
                await refreshTask;
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            refreshCts?.Dispose();
            if (ReferenceEquals(runtimeRefreshCts, refreshCts))
            {
                runtimeRefreshCts = null;
            }

            if (ReferenceEquals(runtimeRefreshTask, refreshTask))
            {
                runtimeRefreshTask = null;
            }
        }
    }
}
