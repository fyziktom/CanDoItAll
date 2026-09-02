using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workspace.Pages.Components;

public partial class ProviderHistoryPolicyPanel : IDisposable {
    [Inject] public IProviderHistoryPolicyService Policies { get; set; } = default!;
    [Inject] public IDatabaseSwitchNotificationService ProfileChanges { get; set; } = default!;
    [Inject] public ILogger<ProviderHistoryPolicyPanel> Logger { get; set; } = default!;
    [CascadingParameter] public Task<AuthenticationState>? AuthenticationState { get; set; }

    private static readonly HistoryCaptureMode[] CaptureModes = Enum.GetValues<HistoryCaptureMode>();
    private HistoryPolicySnapshot? snapshot;
    private ProviderHistoryPolicyDraft draft = new();
    private EditContext editContext = default!;
    private PendingRetentionChange? preview;
    private CancellationTokenSource? active;
    private Task<AuthenticationState>? previousAuthentication;
    private string? error;
    private string? message;
    private bool isBusy;
    private bool disposed;

    protected override void OnInitialized() => ProfileChanges.Changed += ProfileChanged;

    protected override void OnParametersSet() {
        if (!ReferenceEquals(previousAuthentication, AuthenticationState)) {
            Reset();
            previousAuthentication = AuthenticationState;
        }
    }

    private Task LoadAsync() => ExecuteAsync(token => Policies.GetAsync(token), SetSnapshot);

    private void SetSnapshot(HistoryPolicySnapshot value) {
        snapshot = value;
        draft = ProviderHistoryPolicyDraft.From(value.Policy);
        editContext = new(draft);
        preview = null;
    }

    private void DraftChanged() {
        preview = null;
        message = null;
    }

    private Task ApplyFutureAsync() => snapshot is null || !editContext.Validate()
        ? Task.CompletedTask
        : ApplyAsync(new(draft.ToPolicy(), snapshot.Version, false));

    private async Task PreviewAsync() {
        if (snapshot is null || !editContext.Validate()) {
            return;
        }
        var policy = draft.ToPolicy();
        var version = snapshot.Version;
        preview = null;
        await ExecuteAsync(token => Policies.PreviewShorterRetentionAsync(policy, token), value => {
            if (snapshot?.Version == version && draft.ToPolicy() == policy) {
                preview = new(policy, version, value);
            }
        });
    }

    private Task ConfirmShorterAsync() {
        if (preview is not { Preview.ExceedsLimit: false } pending) {
            return Task.CompletedTask;
        }
        return ApplyAsync(new(pending.Policy, pending.Version, true));
    }

    private Task ApplyAsync(HistoryPolicyUpdate update) => ExecuteAsync(token => Policies.UpdateAsync(update, token), value => {
        SetSnapshot(value);
        message = update.ApplyShorterRetention
            ? "Policy saved and shorter retention applied to eligible history. Canonical retention is unchanged."
            : "Policy saved for future requests. Existing expiry dates are unchanged.";
    });

    private void ClosePreview() {
        if (!isBusy) {
            preview = null;
        }
    }

    private async Task ExecuteAsync<T>(Func<CancellationToken, Task<T>> execute, Action<T> publish) {
        if (isBusy || disposed) {
            return;
        }
        using var cancellation = new CancellationTokenSource();
        active = cancellation;
        isBusy = true;
        error = null;
        message = null;
        try {
            var result = await execute(cancellation.Token);
            if (ReferenceEquals(active, cancellation) && !cancellation.IsCancellationRequested) {
                publish(result);
            }
        } catch (OperationCanceledException) {
            if (ReferenceEquals(active, cancellation)) {
                error = "Policy operation canceled. Reload the current policy before retrying.";
            }
        } catch (ProviderHistoryException exception) {
            if (ReferenceEquals(active, cancellation)) {
                error = $"{exception.Failure}: {exception.Message}";
                preview = null;
                if (exception.Failure is HistoryFailure.Denied or HistoryFailure.StaleContext) {
                    snapshot = null;
                }
            }
        } catch (Exception exception) {
            if (ReferenceEquals(active, cancellation)) {
                Logger.LogError("History policy UI failed with {FailureType}.", exception.GetType().Name);
                error = "Policy operation failed. Reload the current policy before retrying.";
                preview = null;
            }
        } finally {
            if (ReferenceEquals(active, cancellation)) {
                active = null;
                isBusy = false;
            }
        }
    }

    private void Reset() {
        active?.Cancel();
        active = null;
        isBusy = false;
        snapshot = null;
        preview = null;
        error = null;
        message = null;
    }

    private void ProfileChanged(object? sender, DatabaseProfileChangedNotification notification) {
        if (!disposed) {
            _ = InvokeAsync(() => {
                if (!disposed) {
                    Reset();
                    StateHasChanged();
                }
            });
        }
    }

    public void Dispose() {
        disposed = true;
        ProfileChanges.Changed -= ProfileChanged;
        Reset();
    }

    private sealed record PendingRetentionChange(HistoryPolicy Policy, long Version, HistoryRetentionPreview Preview);
}
