using CanDoItAll.Workspace.UI;

namespace CanDoItAll.Modules.Workspace;

public sealed class WorkspaceFilesController(IWorkspaceFilesOwner owner) : IWorkspaceFilesState, IDisposable {
    private readonly SettingsOperationLedger ledger = new();
    private readonly CancellationTokenSource lifetime = new();
    private bool disposed;
    private Task? refresh;
    private string? referenceError;
    private string? editorError;
    public event Action? Changed;
    public WorkspaceFileDraft Draft { get; private set; } = new();
    public IReadOnlyList<WorkspaceFilePreference> Preferences { get; private set; } = [];
    public IReadOnlyList<SettingsReceipt> Receipts => ledger.Entries;
    public bool CanMutate => !disposed && !ledger.Blocks(Draft.Origin, extension: Draft.Selected);
    public bool IsLoading { get; private set; }
    public string? Error { get => editorError ?? referenceError; private set => editorError = value; }

    public Task RefreshAsync() {
        if (disposed) {
            return Task.CompletedTask;
        }
        return refresh is { IsCompleted: false } pending ? pending : refresh = RefreshCoreAsync();
    }

    private async Task RefreshCoreAsync() {
        if (disposed) {
            return;
        }
        IsLoading = true;
        Notify();
        try {
            var preferences = await owner.ListAsync(lifetime.Token);
            if (!disposed) {
                Preferences = preferences;
                referenceError = null;
            }
        } catch (Exception) {
            if (!disposed) {
                referenceError = "Preferred applications could not be refreshed. The previous list and draft are retained.";
            }
        } finally {
            IsLoading = false;
            Notify();
        }
    }

    public void Select(WorkspaceFilePreference preference) {
        if (disposed || Draft.Selected == preference.Extension) {
            return;
        }
        Draft = new() { Selected = preference.Extension, Extension = preference.Extension.Value, ExecutablePath = preference.ExecutablePath };
        Error = null;
        Notify();
    }

    public void New() {
        if (disposed) {
            return;
        }
        Draft = new();
        Error = null;
        Notify();
    }

    public Task SaveAsync() => MutateAsync(false);
    public Task DeleteAsync() => MutateAsync(true);

    private async Task MutateAsync(bool delete) {
        if (!CanMutate || delete && Draft.Selected is null) {
            return;
        }
        var draft = Draft;
        var revision = draft.Revision;
        WorkspaceFileCommand command;
        try {
            command = delete ? new(draft.Selected!.Value, string.Empty) : owner.Capture(draft.Extension, draft.ExecutablePath);
        } catch (ArgumentException) {
            Error = "Enter a single file extension and an existing absolute executable path.";
            Notify();
            return;
        }
        if (ledger.Admit(draft.Origin, delete ? SettingsAction.DeleteFile : SettingsAction.SaveFile, extension: command.Extension) is not { } receipt) {
            return;
        }
        Error = null;
        Notify();
        try {
            var result = delete
                ? await owner.DeleteAsync(command.Extension, lifetime.Token)
                : await owner.SaveAsync(command, lifetime.Token);
            SettingsOperationLedger.Complete(receipt, result);
            if (result.State != SettingsWriteState.Refused && !disposed) {
                if (ReferenceEquals(Draft, draft) && draft.Revision == revision) {
                    if (delete) {
                        New();
                    } else {
                        draft.Selected = result.Value;
                        draft.Extension = result.Value.Value;
                    }
                }
                receipt.IsReconciling = true;
                Notify();
                if (refresh is { IsCompleted: false } prior) {
                    await prior;
                }
                await RefreshAsync();
                if (referenceError is not null && receipt.Effect == SettingsEffect.Committed) {
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
        if (disposed || receipt.IsReviewing || !Receipts.Contains(receipt) || receipt.Extension is not { } extension || receipt.Effect == SettingsEffect.Pending) {
            return;
        }
        receipt.IsReviewing = true;
        receipt.ObservationFailed = false;
        Notify();
        try {
            var preferences = await owner.ListAsync(lifetime.Token);
            receipt.ObservedExists = preferences.Any(item => item.Extension == extension);
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
