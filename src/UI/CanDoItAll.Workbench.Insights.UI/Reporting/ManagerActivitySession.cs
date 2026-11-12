using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Workbench.Insights.UI;

public sealed class ManagerActivitySession(IManagerActivitySource source) : IDisposable {
    public const int PageSize = 20;
    private CancellationTokenSource? load;
    private bool disposed;
    public event Action? Changed;
    public ManagerActivityQuery Query { get; private set; } = new(ProjectManagerActivityKind.Conversation, ProjectManagerActivityStatusFilter.All, 0);
    public ManagerActivityPresentation? Page { get; private set; }
    public string? Error { get; private set; }
    public bool IsLoading => load is not null;
    public bool CanMovePrevious => Query.PageIndex > 0 && !IsLoading;
    public bool CanMoveNext => Page?.HasNext == true && !IsLoading;

    public Task SetKindAsync(ManagerActivityQuery expected, ProjectManagerActivityKind kind) => expected == Query
        ? ChangeAsync(expected with { Kind = kind, PageIndex = 0 }) : Task.CompletedTask;
    public Task SetStatusAsync(ManagerActivityQuery expected, ProjectManagerActivityStatusFilter status) => expected == Query
        ? ChangeAsync(expected with { Status = status, PageIndex = 0 }) : Task.CompletedTask;
    public Task PreviousAsync(ManagerActivityQuery expected) => expected == Query && CanMovePrevious
        ? ChangeAsync(expected with { PageIndex = expected.PageIndex - 1 }) : Task.CompletedTask;
    public Task NextAsync(ManagerActivityQuery expected) => expected == Query && CanMoveNext
        ? ChangeAsync(expected with { PageIndex = expected.PageIndex + 1 }) : Task.CompletedTask;

    private Task ChangeAsync(ManagerActivityQuery query) {
        if (disposed) {
            return Task.CompletedTask;
        }
        Query = query;
        Page = null;
        return LoadAsync();
    }

    public async Task LoadAsync() {
        if (disposed) {
            return;
        }
        RetireLoad();
        using var operation = new CancellationTokenSource();
        load = operation;
        var query = Query;
        Error = null;
        Changed?.Invoke();
        try {
            var page = await source.ReadAsync(query, operation.Token);
            if (IsCurrent(operation)) {
                Page = page;
            }
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
        source.Dispose();
        Changed = null;
    }
}
