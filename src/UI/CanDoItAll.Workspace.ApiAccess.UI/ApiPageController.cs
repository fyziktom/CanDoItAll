using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;

namespace CanDoItAll.Workspace.ApiAccess.UI;

public enum ApiPageQueryCause { Operator, Owner }

public readonly record struct ApiPageQueryAcceptance(ApiPageQuery Query, ApiPageQueryCause Cause);

public sealed class ApiPageController<T>(Func<ApiPageQuery, CancellationToken, Task<ApiPage<T>>> read, ApiViewLifetime authority, Action? accessDenied = null) : IDisposable {
    private CancellationTokenSource? reading;
    private bool disposed;
    private string desiredSearch = string.Empty;
    public event Action? Changed;
    public event Action<ApiPageQueryAcceptance>? QueryAccepted;
    public long Revision { get; private set; }
    public string DesiredSearch {
        get => desiredSearch;
        set {
            if (desiredSearch == value) {
                return;
            }
            desiredSearch = value;
            Invalidate();
            Notify();
        }
    }
    public ApiPageQuery? AcceptedQuery { get; private set; }
    public ApiPage<T>? Page { get; private set; }
    public bool IsLoading { get; private set; }
    public string? Error { get; private set; }
    public bool IsStale => Page is not null && (Error is not null || IsLoading || AcceptedQuery?.Search != DesiredSearch);
    public bool CanPrevious => !IsLoading && AcceptedQuery?.Search == DesiredSearch && AcceptedQuery.Offset > 0;
    public bool CanNext => !IsLoading && AcceptedQuery?.Search == DesiredSearch && AcceptedQuery.Offset + ApiPageQuery.PageSize < Page?.TotalCount;

    public Task SearchAsync() => LoadAsync(new(DesiredSearch), ApiPageQueryCause.Operator);
    public Task RefreshAsync() => LoadAsync(new(DesiredSearch, AcceptedQuery?.Search == DesiredSearch ? AcceptedQuery.Offset : 0), ApiPageQueryCause.Owner);
    public Task PreviousAsync() => CanPrevious ? LoadAsync(AcceptedQuery! with { Offset = AcceptedQuery.Offset - ApiPageQuery.PageSize }, ApiPageQueryCause.Operator) : Task.CompletedTask;
    public Task NextAsync() => CanNext ? LoadAsync(AcceptedQuery! with { Offset = AcceptedQuery.Offset + ApiPageQuery.PageSize }, ApiPageQueryCause.Operator) : Task.CompletedTask;
    public Task RefreshIfCurrentAsync(long revision) => revision == Revision ? RefreshAsync() : Task.CompletedTask;

    public Task OpenAsync(ApiPageQuery query) {
        ArgumentNullException.ThrowIfNull(query);
        desiredSearch = query.Search;
        return LoadAsync(query, ApiPageQueryCause.Owner);
    }

    private async Task LoadAsync(ApiPageQuery query, ApiPageQueryCause cause) {
        if (disposed || !authority.IsActive) {
            return;
        }
        Invalidate();
        var origin = Revision;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(authority.Token);
        reading = cancellation;
        IsLoading = true;
        Error = null;
        Notify();
        var accepted = false;
        try {
            var page = await read(query, cancellation.Token);
            if (!Current(origin)) {
                return;
            }
            if (query.Offset > 0 && query.Offset >= page.TotalCount) {
                query = query with { Offset = Math.Max(0, (page.TotalCount - 1) / ApiPageQuery.PageSize * ApiPageQuery.PageSize) };
                page = await read(query, cancellation.Token);
            }
            if (Current(origin)) {
                Page = page;
                AcceptedQuery = query;
                accepted = true;
            }
        } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
        } catch (UnauthorizedAccessException) {
            if (Current(origin)) {
                (accessDenied ?? authority.Dispose)();
            }
        } catch (Exception) {
            if (Current(origin)) {
                Error = "The list could not be refreshed. Retry the read; no mutation is repeated.";
            }
        } finally {
            if (ReferenceEquals(reading, cancellation)) {
                reading = null;
                IsLoading = false;
            }
            if (Current(origin)) {
                Notify();
            }
        }
        if (accepted && Current(origin)) {
            QueryAccepted?.Invoke(new(query, cause));
        }
    }

    private bool Current(long origin) => !disposed && authority.IsActive && origin == Revision;
    private void Invalidate() {
        Revision++;
        reading?.Cancel();
        reading = null;
        IsLoading = false;
    }
    private void Notify() => Changed?.Invoke();
    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        Invalidate();
        Changed = null;
        QueryAccepted = null;
    }
}
