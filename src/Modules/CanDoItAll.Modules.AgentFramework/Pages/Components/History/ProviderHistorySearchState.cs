using System.Collections.Immutable;
using CanDoItAll.AgentFramework.UI.History;
using CanDoItAll.AgentFramework.ProviderHistory;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components.History;

public sealed class ProviderHistorySearchState(IProviderRequestHistory history, ILogger<ProviderHistorySearchState> logger) : IDisposable {
    private const int MaximumPreviousPages = 32;
    private readonly List<string?> previousCursors = [];
    private CancellationTokenSource? active;
    private string? currentCursor;
    private bool disposed;

    public event Action? Changed;

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
        ResetCore();
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
        if (disposed || active is null) {
            return;
        }
        StopRequest();
        WasCanceled = true;
        Changed?.Invoke();
    }

    private void StopRequest() {
        var owner = active;
        active = null;
        IsLoading = false;
        owner?.Cancel();
    }

    public void Reset() {
        ObjectDisposedException.ThrowIf(disposed, this);
        ResetCore();
        Changed?.Invoke();
    }

    private void ResetCore() {
        StopRequest();
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
        StopRequest();
        using var cancellation = new CancellationTokenSource();
        active = cancellation;
        IsLoading = true;
        WasCanceled = false;
        Page = null;
        Error = null;
        Failure = null;
        Changed?.Invoke();
        try {
            var page = await history.SearchAsync(query, cancellation.Token);
            if (!ReferenceEquals(active, cancellation) || cancellation.IsCancellationRequested) {
                return;
            }
            accepted?.Invoke();
            Page = page with { Entries = page.Entries.ToImmutableArray() };
        } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
        } catch (ProviderHistoryException exception) {
            if (ReferenceEquals(active, cancellation)) {
                Failure = exception.Failure;
                Error = HistoryPublicErrors.Message(exception.Failure);
                logger.LogWarning("History search rejected with {Failure}.", exception.Failure);
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
                Changed?.Invoke();
            }
        }
    }

    public HistoryResultsPresentation Presentation(bool draftChanged) => new(
        IsLoading ? HistorySearchPhase.Loading : Error is not null ? HistorySearchPhase.Failed
            : WasCanceled ? HistorySearchPhase.Canceled : Page is not null ? HistorySearchPhase.Ready : HistorySearchPhase.NotRequested,
        AppliedQuery, draftChanged, Failure, Page?.Entries.ToImmutableArray() ?? [], Page?.Coverage, Page?.QueriedAtUtc,
        PageNumber, CanPrevious, CanNext, HasEarlierPages);

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        Changed = null;
        StopRequest();
    }
}
