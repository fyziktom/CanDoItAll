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

        if (string.Equals(detailTab, DetailTabAnalytics, StringComparison.Ordinal))
        {
            analytics = await ProcessesService.GetAnalyticsAsync(selectedProcessId, ProjectId);
        }

        runtimeStateOverview = await RuntimeStateOverviewService.GetOverviewAsync(
            definitions.Select(definition => definition.Id).ToList(),
            ProjectId,
            forceRefresh: true,
            cancellationToken);
        await LoadRuntimePaneDataAsync(cancellationToken);
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
