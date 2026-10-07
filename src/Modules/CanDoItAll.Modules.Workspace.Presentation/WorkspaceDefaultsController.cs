using CanDoItAll.Workspace.UI;

namespace CanDoItAll.Modules.Workspace;

public sealed class WorkspaceDefaultsController(IWorkspaceDefaultsOwner owner) : IWorkspaceDefaultsState, IDisposable {
    private readonly SettingsOperationLedger ledger = new();
    private readonly CancellationTokenSource lifetime = new();
    private bool disposed;
    private bool acquired;
    private Task? refresh;
    private string? defaultsError;
    private string? providerError;
    public event Action? Changed;
    public WorkspaceDefaultsDraft Draft { get; } = new();
    public IReadOnlyList<WorkspaceProviderOption> Providers { get; private set; } = [];
    public IReadOnlyList<SettingsReceipt> Receipts => ledger.Entries;
    public bool CanSave => !disposed && owner.IsCurrent && acquired && !ledger.Blocks(Draft.Origin);
    public bool IsLoading { get; private set; }
    public string? Error => defaultsError ?? providerError;

    public Task RefreshAsync() {
        if (disposed || !owner.IsCurrent) {
            return Task.CompletedTask;
        }
        return refresh is { IsCompleted: false } pending ? pending : refresh = RefreshCoreAsync();
    }

    private async Task RefreshCoreAsync() {
        IsLoading = true;
        Notify();
        try {
            await Task.WhenAll(ReadDefaultsAsync(), ReadProvidersAsync());
        } finally {
            IsLoading = false;
            Notify();
        }
    }

    private async Task ReadDefaultsAsync() {
        if (acquired) {
            return;
        }
        var versions = Draft.CaptureVersions();
        try {
            var model = await owner.ReadAsync(lifetime.Token);
            if (!disposed && owner.IsCurrent) {
                Draft.Reconcile(model, versions);
                acquired = true;
                defaultsError = null;
            }
        } catch (Exception) {
            if (!disposed) {
                defaultsError = "Defaults could not be loaded. Your input is retained.";
            }
        }
    }

    private async Task ReadProvidersAsync() {
        try {
            var providers = await owner.ProvidersAsync(lifetime.Token);
            if (!disposed && owner.IsCurrent) {
                Providers = providers;
                providerError = null;
                Notify();
            }
        } catch (Exception) {
            if (!disposed) {
                providerError = "Provider choices could not be refreshed. Existing references and input are retained.";
            }
        }
    }

    public async Task SaveAsync() {
        if (!CanSave || ledger.Admit(Draft.Origin, SettingsAction.SaveDefaults) is not { } receipt) {
            return;
        }
        var command = Draft.Model.Copy();
        var versions = Draft.CaptureVersions();
        Notify();
        try {
            var result = await owner.SaveAsync(command, lifetime.Token);
            SettingsOperationLedger.Complete(receipt, result);
            if (result.State != SettingsWriteState.Refused && !disposed && owner.IsCurrent) {
                Draft.Reconcile(result.Value, versions);
                receipt.IsReconciling = true;
                Notify();
                if (refresh is { IsCompleted: false } prior) {
                    await prior;
                }
                await RefreshAsync();
                if (Error is not null && receipt.Effect == SettingsEffect.Committed) {
                    receipt.Effect = SettingsEffect.CommittedWarning;
                    receipt.Diagnostic = SettingsDiagnostic.ReadBackPending;
                }
            }
        } catch (Exception) {
            receipt.Effect = SettingsEffect.Unknown;
            receipt.Diagnostic = SettingsDiagnostic.Unknown;
        } finally {
            receipt.IsReconciling = false;
            Notify();
        }
    }

    public async Task ReviewAsync(SettingsReceipt receipt) {
        if (disposed || !owner.IsCurrent || receipt.IsReviewing || !Receipts.Contains(receipt) || receipt.Effect == SettingsEffect.Pending) {
            return;
        }
        receipt.IsReviewing = true;
        receipt.ObservationFailed = false;
        Notify();
        try {
            await owner.ReadAsync(lifetime.Token);
            receipt.ObservedExists = true;
        } catch (Exception) {
            receipt.ObservationFailed = true;
        } finally {
            receipt.IsReviewing = false;
            Notify();
        }
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
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
