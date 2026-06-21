namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    private async Task LoadProcessGraphsAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        processGraphsLoadRequested = true;
        processGraphsError = string.Empty;

        if (!selectedProcessId.HasValue)
        {
            processGraphsSnapshot = null;
            return;
        }

        if (!forceRefresh &&
            processGraphsSnapshot is not null &&
            processGraphsLoadedProcessId == selectedProcessId &&
            processGraphsLoadedWindow == processGraphsWindow)
        {
            return;
        }

        processGraphsLoading = true;
        StateHasChanged();
        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                componentLifetimeCts.Token,
                cancellationToken);
            processGraphsSnapshot = await ProcessObservationService.GetLiveSnapshotAsync(
                new ProcessLiveObservationQuery(
                    ProjectId,
                    processGraphsWindow,
                    ForceRefresh: forceRefresh,
                    ProcessDefinitionId: selectedProcessId.Value),
                linkedCts.Token);
            processGraphsLoadedProcessId = selectedProcessId;
            processGraphsLoadedWindow = processGraphsWindow;
        }
        catch (OperationCanceledException) when (componentLifetimeCts.IsCancellationRequested || cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            processGraphsSnapshot = null;
            processGraphsError = exception.Message;
        }
        finally
        {
            processGraphsLoading = false;
        }
    }

    private async Task EnsureSelectedRunGraphsLoadedAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        selectedRunGraphsError = string.Empty;

        if (!selectedRunId.HasValue)
        {
            selectedRunGraphsSnapshot = null;
            return;
        }

        if (!forceRefresh &&
            selectedRunGraphsSnapshot is not null &&
            selectedRunGraphsLoadedRunId == selectedRunId)
        {
            return;
        }

        selectedRunGraphsLoading = true;
        StateHasChanged();
        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                componentLifetimeCts.Token,
                cancellationToken);
            selectedRunGraphsSnapshot = await ProcessObservationService.GetLiveSnapshotAsync(
                new ProcessLiveObservationQuery(
                    ProjectId,
                    ProcessLiveHistoryWindow.All,
                    ProcessRunId: selectedRunId.Value,
                    ForceRefresh: forceRefresh),
                linkedCts.Token);
            selectedRunGraphsLoadedRunId = selectedRunId;
        }
        catch (OperationCanceledException) when (componentLifetimeCts.IsCancellationRequested || cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            selectedRunGraphsSnapshot = null;
            selectedRunGraphsError = exception.Message;
        }
        finally
        {
            selectedRunGraphsLoading = false;
        }
    }

    private void SetProcessGraphsWindow(ProcessLiveHistoryWindow window)
    {
        var normalizedWindow = ProcessGraphWindows.Contains(window)
            ? window
            : ProcessLiveHistoryWindow.ThirtyDays;

        if (processGraphsWindow == normalizedWindow)
        {
            return;
        }

        processGraphsWindow = normalizedWindow;
        processGraphsSnapshot = null;
        processGraphsLoadRequested = false;
        processGraphsError = string.Empty;
        processGraphsLoadedProcessId = null;
        processGraphsLoadedWindow = null;
    }

    private void ResetGraphPaneData()
    {
        processGraphsSnapshot = null;
        processGraphsLoadRequested = false;
        processGraphsLoading = false;
        processGraphsError = string.Empty;
        processGraphsLoadedProcessId = null;
        processGraphsLoadedWindow = null;
        ResetSelectedRunGraphData();
    }

    private void ResetSelectedRunGraphData()
    {
        selectedRunGraphsSnapshot = null;
        selectedRunGraphsLoading = false;
        selectedRunGraphsError = string.Empty;
        selectedRunGraphsLoadedRunId = null;
    }

    private static string ResolveProcessGraphWindowLabel(ProcessLiveHistoryWindow window)
    {
        return window switch
        {
            ProcessLiveHistoryWindow.OneDay => "1 day",
            ProcessLiveHistoryWindow.SevenDays => "1 week",
            ProcessLiveHistoryWindow.ThirtyDays => "1 month",
            ProcessLiveHistoryWindow.ThreeMonths => "3 months",
            ProcessLiveHistoryWindow.OneYear => "1 year",
            ProcessLiveHistoryWindow.All => "All",
            _ => "1 month"
        };
    }
}
