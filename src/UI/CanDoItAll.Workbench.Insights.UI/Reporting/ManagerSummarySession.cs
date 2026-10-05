using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Workbench.Insights.UI;

public sealed class ManagerSummarySession(IManagerSummarySource source, ProjectManagerSummaryOptions? options = null,
    ManagerReportPresentation? snapshot = null) : IDisposable {
    private CancellationTokenSource? load;
    private bool disposed;
    public event Action? Changed;
    public ProjectManagerSummaryOptions Options { get; private set; } = options ?? new();
    public ManagerReportPresentation? Snapshot { get; private set; } = snapshot;
    public ManagerScopePresentation? PendingScope { get; private set; }
    public ProjectManagerSummaryLoadProgress? Progress { get; private set; }
    public string? Error { get; private set; }
    public bool IsLoading => load is not null;
    public ManagerActivitySession? Activity { get; private set; }
    public ManagerReportPresentation? WarningReport { get; private set; }

    public void UpdateOptions(ProjectManagerSummaryOptions expected, ProjectManagerSummaryOptions options) {
        if (disposed || expected != Options || options == Options) {
            return;
        }
        RetireLoad();
        Options = options;
        PendingScope = null;
        Error = null;
        source.Retain(Options, Snapshot);
        Changed?.Invoke();
    }

    public Task LoadAsync(ProjectManagerSummaryOptions expected) => expected == Options ? RunAsync(null) : Task.CompletedTask;

    public Task ContinueAsync(ManagerScopePresentation scope) => !disposed && ReferenceEquals(scope, PendingScope)
        ? RunAsync(scope) : Task.CompletedTask;

    public void CancelConfirmation(ManagerScopePresentation scope) {
        if (!disposed && ReferenceEquals(scope, PendingScope)) {
            PendingScope = null;
            Changed?.Invoke();
        }
    }

    private async Task RunAsync(ManagerScopePresentation? confirmed) {
        if (disposed) {
            return;
        }
        RetireLoad();
        using var operation = new CancellationTokenSource();
        load = operation;
        var submitted = Options;
        PendingScope = null;
        Progress = new("Resolving project scope", "Checking the selected project and descendant hierarchy.", 0, 3);
        Error = null;
        Changed?.Invoke();
        try {
            var scope = confirmed ?? await source.ResolveScopeAsync(submitted, operation.Token);
            if (!IsCurrent(operation)) {
                return;
            }
            if (confirmed is null && scope.RequiresConfirmation) {
                PendingScope = scope;
                return;
            }
            var report = await source.LoadAsync(scope.Id, submitted, next => {
                if (IsCurrent(operation)) {
                    Progress = next;
                    Changed?.Invoke();
                }
                return ValueTask.CompletedTask;
            }, operation.Token);
            if (!IsCurrent(operation)) {
                return;
            }
            if (report.Options != submitted) {
                throw new InvalidOperationException("The report did not preserve the submitted options.");
            }
            source.Retain(Options, report);
            Snapshot = report;
            WarningReport = null;
            CloseActivity();
        } catch (OperationCanceledException) when (operation.IsCancellationRequested) {
        } catch (Exception exception) {
            if (IsCurrent(operation)) {
                Error = source.DescribeFailure(exception);
            }
        } finally {
            if (ReferenceEquals(load, operation)) {
                load = null;
                Changed?.Invoke();
            }
        }
    }

    public async Task OpenActivityAsync(ManagerReportPresentation report) {
        if (disposed || !ReferenceEquals(report, Snapshot)) {
            return;
        }
        CloseActivity();
        var activity = new ManagerActivitySession(source.OpenActivity(report.Id));
        Activity = activity;
        Changed?.Invoke();
        await activity.LoadAsync();
    }

    public void CloseActivity(ManagerActivitySession? expected = null) {
        if (expected is not null && !ReferenceEquals(Activity, expected)) {
            return;
        }
        Activity?.Dispose();
        Activity = null;
        Changed?.Invoke();
    }

    public void OpenWarnings(ManagerReportPresentation report) {
        if (!disposed && ReferenceEquals(report, Snapshot)) {
            WarningReport = report;
            Changed?.Invoke();
        }
    }

    public void CloseWarnings(ManagerReportPresentation report) {
        if (!disposed && ReferenceEquals(report, WarningReport)) {
            WarningReport = null;
            Changed?.Invoke();
        }
    }

    private bool IsCurrent(CancellationTokenSource operation) => !disposed && ReferenceEquals(load, operation) && !operation.IsCancellationRequested;

    private void RetireLoad() {
        var previous = load;
        load = null;
        previous?.Cancel();
    }

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        RetireLoad();
        Activity?.Dispose();
        Activity = null;
        WarningReport = null;
        PendingScope = null;
        Changed = null;
    }
}
