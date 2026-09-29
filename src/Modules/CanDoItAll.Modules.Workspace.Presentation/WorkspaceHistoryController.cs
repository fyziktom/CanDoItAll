using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.Modules.Workspace.Pages.Components;
using CanDoItAll.Workspace.UI;
using Microsoft.AspNetCore.Components.Forms;
using System.ComponentModel.DataAnnotations;

namespace CanDoItAll.Modules.Workspace;

public sealed class WorkspaceHistoryController(IProviderHistoryPolicyService policies) : IWorkspaceHistoryState, IDisposable {
    private readonly SettingsOperationLedger ledger = new();
    private CancellationTokenSource? active;
    private Guid origin = Guid.NewGuid();
    private bool disposed;
    public event Action? Changed;
    public HistoryPolicySnapshot? Snapshot { get; private set; }
    public ProviderHistoryPolicyDraft Draft { get; private set; } = new();
    public EditContext EditContext { get; private set; } = new(new ProviderHistoryPolicyDraft());
    public PendingRetentionChange? Preview { get; private set; }
    public IReadOnlyList<SettingsReceipt> Receipts => ledger.Entries;
    public bool IsBusy => active is not null;
    public bool CanApply => !disposed && !IsBusy && Snapshot is not null && !ledger.Blocks(origin);
    public string? Error { get; private set; }
    public string? Message { get; private set; }

    public Task LoadAsync() => ExecuteAsync(token => policies.GetAsync(token), value => {
        SetSnapshot(value);
        Message = "Current policy observed. This does not establish the outcome of an earlier request.";
    });

    private void SetSnapshot(HistoryPolicySnapshot value) {
        EditContext.OnFieldChanged -= FieldChanged;
        Snapshot = value;
        Draft = ProviderHistoryPolicyDraft.From(value.Policy);
        EditContext = new(Draft);
        EditContext.OnFieldChanged += FieldChanged;
        Preview = null;
    }

    public void DraftChanged() {
        Preview = null;
        Message = null;
        Notify();
    }

    private void FieldChanged(object? sender, FieldChangedEventArgs args) => DraftChanged();

    private bool ValidateDraft() => EditContext.Validate() && Validator.TryValidateObject(Draft, new(Draft), [], true);

    public Task ApplyFutureAsync() => !CanApply || !ValidateDraft()
        ? Task.CompletedTask
        : ApplyAsync(new(Draft.ToPolicy(), Snapshot!.Version, false));

    public async Task PreviewAsync() {
        if (!CanApply || !ValidateDraft()) {
            return;
        }
        var policy = Draft.ToPolicy();
        var version = Snapshot!.Version;
        Preview = null;
        await ExecuteAsync(token => policies.PreviewShorterRetentionAsync(policy, token), value => {
            if (Snapshot?.Version == version && Draft.ToPolicy() == policy && !EditContext.GetValidationMessages().Any()) {
                Preview = new(policy, version, value);
            }
        });
    }

    public Task ConfirmShorterAsync() {
        if (!CanApply || Preview is not { Preview.ExceedsLimit: false } pending || !ValidateDraft() ||
            pending.Policy != Draft.ToPolicy() || pending.Version != Snapshot?.Version) {
            return Task.CompletedTask;
        }
        return ApplyAsync(new(pending.Policy, pending.Version, true));
    }

    private Task ApplyAsync(HistoryPolicyUpdate update) {
        var receipt = ledger.Admit(origin, update.ApplyShorterRetention ? SettingsAction.ShortenRetention : SettingsAction.UpdatePolicy);
        if (receipt is not null) {
            receipt.PolicyVersion = update.ExpectedVersion;
        }
        return receipt is null ? Task.CompletedTask : ExecuteAsync(token => policies.UpdateAsync(update, token), value => {
            SetSnapshot(value);
            Message = update.ApplyShorterRetention
                ? "Policy saved and shorter retention applied to eligible history. Canonical retention is unchanged."
                : "Policy saved for future requests. Existing expiry dates are unchanged.";
        }, receipt);
    }

    public void ClosePreview() {
        if (!IsBusy) {
            Preview = null;
            Notify();
        }
    }

    private async Task ExecuteAsync<T>(Func<CancellationToken, Task<T>> execute, Action<T> publish, SettingsReceipt? receipt = null) {
        if (IsBusy || disposed) {
            return;
        }
        using var cancellation = new CancellationTokenSource();
        active = cancellation;
        Error = null;
        Message = null;
        Notify();
        try {
            var result = await execute(cancellation.Token);
            if (receipt is not null) {
                receipt.Effect = SettingsEffect.Committed;
            }
            if (ReferenceEquals(active, cancellation) && !cancellation.IsCancellationRequested && !disposed) {
                publish(result);
            }
        } catch (ProviderHistoryException exception) when (exception.Failure is HistoryFailure.Denied or HistoryFailure.InvalidQuery or HistoryFailure.StaleContext or HistoryFailure.Conflict) {
            if (receipt is not null) {
                receipt.Effect = SettingsEffect.Refused;
                receipt.Diagnostic = SettingsDiagnostic.Validation;
            }
            if (ReferenceEquals(active, cancellation)) {
                Error = $"Policy operation refused: {exception.Failure}. Reload to observe the current policy.";
                Preview = null;
                if (exception.Failure is HistoryFailure.Denied or HistoryFailure.StaleContext) {
                    Snapshot = null;
                }
            }
        } catch (Exception) {
            if (receipt is not null) {
                receipt.Effect = SettingsEffect.Unknown;
                receipt.Diagnostic = SettingsDiagnostic.Unknown;
            }
            if (ReferenceEquals(active, cancellation)) {
                Error = receipt is null ? "Policy read failed. Retry Load to check access and current state."
                    : "Policy update outcome is unknown. Reload only observes current state; this operation cannot be replayed.";
                Preview = null;
            }
        } finally {
            if (ReferenceEquals(active, cancellation)) {
                active = null;
            }
            Notify();
        }
    }

    public void Retire() {
        active?.Cancel();
        active = null;
        origin = Guid.NewGuid();
        EditContext.OnFieldChanged -= FieldChanged;
        Snapshot = null;
        Draft = new();
        EditContext = new(Draft);
        Preview = null;
        Error = null;
        Message = null;
        Notify();
    }

    private void Notify() {
        if (!disposed) {
            Changed?.Invoke();
        }
    }

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        Retire();
    }
}
