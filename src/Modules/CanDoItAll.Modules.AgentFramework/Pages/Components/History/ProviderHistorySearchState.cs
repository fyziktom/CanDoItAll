using CanDoItAll.AgentFramework.ProviderHistory;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components.History;

public sealed class ProviderHistorySearchState(IProviderRequestHistory history, ILogger<ProviderHistorySearchState> logger) : IDisposable {
    private const int MaximumPreviousPages = 32;
    private readonly List<string?> previousCursors = [];
    private CancellationTokenSource? active;
    private string? currentCursor;
    private bool disposed;

    public ProviderRequestHistoryQuery? AppliedQuery { get; private set; }
    public HistoryPage? Page { get; private set; }
    public string? Error { get; private set; }
    public HistoryFailure? Failure { get; private set; }
    public bool IsLoading { get; private set; }
    public bool WasCanceled { get; private set; }
    public bool HasRequested => AppliedQuery is not null;
    public int PageNumber { get; private set; } = 1;
    public bool HasEarlierPages { get; private set; }
    public bool CanPrevious => !IsLoading && Page is not null && previousCursors.Count > 0;
    public bool CanNext => !IsLoading && Page?.NextCursor is not null;

    public Task SearchAsync(ProviderRequestHistoryQuery query) {
        ObjectDisposedException.ThrowIf(disposed, this);
        Reset();
        AppliedQuery = query with { Cursor = null };
        return ReadAsync(AppliedQuery);
    }

    public Task NextAsync() {
        if (!CanNext || AppliedQuery is null) {
            return Task.CompletedTask;
        }
        var next = Page!.NextCursor;
        return ReadAsync(AppliedQuery with { Cursor = next }, () => {
            if (previousCursors.Count == MaximumPreviousPages) {
                previousCursors.RemoveAt(0);
                HasEarlierPages = true;
            }
            previousCursors.Add(currentCursor);
            currentCursor = next;
            PageNumber = checked(PageNumber + 1);
        });
    }

    public Task PreviousAsync() {
        if (!CanPrevious || AppliedQuery is null) {
            return Task.CompletedTask;
        }
        var previous = previousCursors[^1];
        return ReadAsync(AppliedQuery with { Cursor = previous }, () => {
            previousCursors.RemoveAt(previousCursors.Count - 1);
            currentCursor = previous;
            PageNumber--;
        });
    }

    public void Cancel() {
        active?.Cancel();
        active = null;
        IsLoading = false;
        WasCanceled = true;
    }

    public void Reset() {
        Cancel();
        AppliedQuery = null;
        Page = null;
        Error = null;
        Failure = null;
        WasCanceled = false;
        previousCursors.Clear();
        currentCursor = null;
        PageNumber = 1;
        HasEarlierPages = false;
    }

    private async Task ReadAsync(ProviderRequestHistoryQuery query, Action? accepted = null) {
        ObjectDisposedException.ThrowIf(disposed, this);
        Cancel();
        using var cancellation = new CancellationTokenSource();
        active = cancellation;
        IsLoading = true;
        WasCanceled = false;
        Page = null;
        Error = null;
        Failure = null;
        try {
            var page = await history.SearchAsync(query, cancellation.Token);
            if (!ReferenceEquals(active, cancellation) || cancellation.IsCancellationRequested) {
                return;
            }
            accepted?.Invoke();
            Page = page;
        } catch (OperationCanceledException) {
            if (ReferenceEquals(active, cancellation)) {
                WasCanceled = true;
            }
        } catch (ProviderHistoryException exception) {
            if (ReferenceEquals(active, cancellation)) {
                Failure = exception.Failure;
                Error = exception.Message;
            }
        } catch (Exception exception) {
            if (ReferenceEquals(active, cancellation)) {
                logger.LogError("History UI search failed with {FailureType}.", exception.GetType().Name);
                Failure = HistoryFailure.Unavailable;
                Error = "History could not be loaded. Retry or narrow the selected interval.";
            }
        } finally {
            if (ReferenceEquals(active, cancellation)) {
                active = null;
                IsLoading = false;
            }
        }
    }

    public void Dispose() {
        Cancel();
        disposed = true;
    }
}
