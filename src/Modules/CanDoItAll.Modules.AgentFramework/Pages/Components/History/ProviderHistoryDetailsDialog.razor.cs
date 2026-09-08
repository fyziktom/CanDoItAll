using System.Collections.Immutable;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.UI.History;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components.History;

public partial class ProviderHistoryDetailsDialog : IDisposable {
    [Parameter, EditorRequired] public HistoryEntryId EntryId { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }
    [Inject] public IProviderRequestHistory History { get; set; } = default!;
    [Inject] public ILogger<ProviderHistoryDetailsDialog> Logger { get; set; } = default!;

    private CancellationTokenSource? active;
    private HistoryEntryId? desiredEntry;
    private HistoryMetadata? metadata;
    private HistoryDetail? detail;
    private string? error;
    private bool isLoading;
    private bool disposed;
    private bool closed;

    protected override Task OnParametersSetAsync() {
        if (disposed || closed || desiredEntry == EntryId) {
            return Task.CompletedTask;
        }
        desiredEntry = EntryId;
        metadata = null;
        detail = null;
        var target = EntryId;
        return ReadAsync(token => History.GetMetadataAsync(target, token), value => {
            if (value is not null && value.Entry.Id != target) {
                throw new ProviderHistoryException(HistoryFailure.StaleContext, "The metadata target changed.");
            }
            metadata = value is null ? null : value with { Owners = value.Owners.ToImmutableArray() };
        });
    }

    private Task LoadContentAsync(CanonicalEvidenceReference? owner) {
        if (isLoading || metadata is null) {
            return Task.CompletedTask;
        }
        detail = null;
        var target = EntryId;
        return ReadAsync(token => History.GetDetailAsync(target, owner, token), value => {
            if (value.EntryId != target) {
                throw new ProviderHistoryException(HistoryFailure.StaleContext, "The content target changed.");
            }
            detail = value with { Sections = value.Sections.ToImmutableArray() };
        });
    }

    private void CloseContent() => detail = null;

    private async Task ReadAsync<T>(Func<CancellationToken, Task<T>> read, Action<T> publish) {
        if (disposed || closed) {
            return;
        }
        var previous = active;
        using var owner = new CancellationTokenSource();
        active = owner;
        isLoading = true;
        error = null;
        previous?.Cancel();
        try {
            if (!IsCurrent(owner)) {
                return;
            }
            var result = await read(owner.Token);
            if (IsCurrent(owner)) {
                publish(result);
            }
        } catch (OperationCanceledException) {
            if (IsCurrent(owner)) {
                error = "Evidence loading was canceled. Close and reopen to retry.";
            }
        } catch (ProviderHistoryException exception) {
            if (IsCurrent(owner)) {
                Logger.LogWarning("History evidence read rejected with {Failure}.", exception.Failure);
                error = HistoryPublicErrors.Message(exception.Failure);
                if (exception.Failure is HistoryFailure.Denied or HistoryFailure.StaleContext) {
                    metadata = null;
                    detail = null;
                }
            }
        } catch (Exception exception) {
            if (IsCurrent(owner)) {
                Logger.LogError("History detail UI failed with {FailureType}.", exception.GetType().Name);
                error = HistoryPublicErrors.Message(HistoryFailure.Unavailable);
            }
        } finally {
            if (IsCurrent(owner)) {
                active = null;
                isLoading = false;
            }
        }
    }

    private bool IsCurrent(CancellationTokenSource owner)
        => !disposed && !closed && ReferenceEquals(active, owner) && !owner.IsCancellationRequested;

    private void Cancel() {
        var owner = active;
        active = null;
        isLoading = false;
        owner?.Cancel();
    }

    private async Task CloseAsync() {
        if (closed || disposed) {
            return;
        }
        closed = true;
        Cancel();
        metadata = null;
        detail = null;
        await OnClose.InvokeAsync();
    }

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        Cancel();
    }
}
