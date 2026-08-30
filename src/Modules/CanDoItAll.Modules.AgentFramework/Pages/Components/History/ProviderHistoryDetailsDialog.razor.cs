using CanDoItAll.AgentFramework.ProviderHistory;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components.History;

public partial class ProviderHistoryDetailsDialog : IDisposable {
    [Parameter, EditorRequired] public HistoryEntryId EntryId { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }
    [Inject] public IProviderRequestHistory History { get; set; } = default!;
    [Inject] public ILogger<ProviderHistoryDetailsDialog> Logger { get; set; } = default!;

    private readonly CancellationTokenSource lifetime = new();
    private HistoryMetadata? metadata;
    private HistoryDetail? detail;
    private string? error;
    private bool isLoading;
    private bool disposed;

    protected override Task OnInitializedAsync() =>
        ReadAsync(token => History.GetMetadataAsync(EntryId, token), value => metadata = value);

    private Task LoadContentAsync(CanonicalEvidenceReference? owner) {
        detail = null;
        return ReadAsync(token => History.GetDetailAsync(EntryId, owner, token), value => detail = value);
    }

    private void CloseContent() => detail = null;

    private async Task ReadAsync<T>(Func<CancellationToken, Task<T>> read, Action<T> publish) {
        if (isLoading || disposed) {
            return;
        }
        isLoading = true;
        error = null;
        try {
            var result = await read(lifetime.Token);
            if (!lifetime.IsCancellationRequested) {
                publish(result);
            }
        } catch (OperationCanceledException) {
            if (!lifetime.IsCancellationRequested) {
                error = "Evidence loading was canceled. Close and reopen to retry.";
            }
        } catch (ProviderHistoryException exception) {
            if (!lifetime.IsCancellationRequested) {
                error = $"{exception.Failure}: {exception.Message}";
                if (exception.Failure is HistoryFailure.Denied or HistoryFailure.StaleContext) {
                    metadata = null;
                    detail = null;
                }
            }
        } catch (Exception exception) {
            if (!lifetime.IsCancellationRequested) {
                Logger.LogError("History detail UI failed with {FailureType}.", exception.GetType().Name);
                error = "Evidence could not be loaded. Close and reopen to retry.";
            }
        } finally {
            isLoading = false;
        }
    }

    private async Task CloseAsync() {
        lifetime.Cancel();
        metadata = null;
        detail = null;
        await OnClose.InvokeAsync();
    }

    public void Dispose() {
        disposed = true;
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
