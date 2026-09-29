using CanDoItAll.Modules.Security;
using CanDoItAll.Workspace.UI;

namespace CanDoItAll.Modules.Workspace;

public sealed class WorkspaceSecretsController(IWorkspaceSecretsOwner owner) : IWorkspaceSecretsState, IDisposable {
    private readonly SettingsOperationLedger ledger = new();
    private readonly CancellationTokenSource lifetime = new();
    private long selection;
    private bool acquired = true;
    private bool disposed;
    private Task? refresh;
    private string? referenceError;
    private string? editorError;
    public event Action? Changed;
    public WorkspaceSecretDraft Draft { get; private set; } = CreateDraft();
    public IReadOnlyList<SecretListItem> Secrets { get; private set; } = [];
    public IReadOnlyList<SettingsReceipt> Receipts => ledger.Entries;
    public string Search { get; set; } = string.Empty;
    public string? Error { get => editorError ?? referenceError; private set => editorError = value; }
    public bool IsLoading { get; private set; }
    public bool CanEdit => !disposed && owner.IsCurrent && acquired;
    public bool CanMutate => !disposed && owner.IsCurrent && acquired && !ledger.Blocks(Draft.Origin, Draft.Model.Id);

    public Task RefreshAsync() {
        if (disposed || !owner.IsCurrent) {
            return Task.CompletedTask;
        }
        return refresh is { IsCompleted: false } pending ? pending : refresh = RefreshCoreAsync();
    }

    private async Task RefreshCoreAsync() {
        if (disposed || !owner.IsCurrent) {
            return;
        }
        IsLoading = true;
        Notify();
        try {
            var secrets = await owner.ListAsync(lifetime.Token);
            if (!disposed && owner.IsCurrent) {
                Secrets = secrets;
                referenceError = null;
            }
        } catch (Exception) {
            if (!disposed) {
                referenceError = "Secret metadata could not be refreshed. The editor and prior list are retained.";
            }
        } finally {
            IsLoading = false;
            Notify();
        }
    }

    public async Task SelectAsync(Guid id) {
        if (disposed || !owner.IsCurrent || acquired && Draft.Model.Id == id) {
            return;
        }
        var version = ++selection;
        ClearSensitive(Draft.Model);
        Draft = new(new() { Id = id });
        var draft = Draft;
        acquired = false;
        Error = null;
        Notify();
        try {
            var model = await owner.GetAsync(id, lifetime.Token);
            if (!Current(draft) || version != selection) {
                if (model is not null) {
                    ClearSensitive(model);
                }
                return;
            }
            if (model is null) {
                Error = "The selected secret no longer exists. Choose another record or explicitly start a new secret.";
                return;
            }
            Draft = new(model);
            acquired = true;
        } catch (Exception) {
            if (Current(draft) && version == selection) {
                Error = "The exact secret editor could not be loaded. Select it again to retry.";
            }
        } finally {
            Notify();
        }
    }

    public void New() {
        if (disposed || !owner.IsCurrent) {
            return;
        }
        selection++;
        ClearSensitive(Draft.Model);
        Draft = CreateDraft();
        acquired = true;
        Error = null;
        Notify();
    }

    public Task SaveAsync() => MutateAsync(false);
    public Task DeleteAsync() => MutateAsync(true);

    private async Task MutateAsync(bool delete) {
        if (!CanMutate || delete && Draft.Model.Id is null) {
            return;
        }
        var draft = Draft;
        var revision = draft.Revision;
        var command = draft.Model.Copy();
        var action = delete ? SettingsAction.DeleteSecret : SettingsAction.SaveSecret;
        if (ledger.Admit(draft.Origin, action, command.Id) is not { } receipt) {
            ClearSensitive(command);
            return;
        }
        Notify();
        try {
            var result = delete
                ? await owner.DeleteAsync(command.Id!.Value, lifetime.Token)
                : await owner.SaveAsync(command, lifetime.Token);
            SettingsOperationLedger.Complete(receipt, result);
            if (result.State == SettingsWriteState.Refused) {
                return;
            }
            receipt.RecordId = result.Value;
            if (Current(draft)) {
                if (!delete) {
                    draft.Model.Id = result.Value;
                }
                if (draft.Revision == revision) {
                    New();
                } else if (delete) {
                    acquired = false;
                    Error = "This secret was deleted. Newer input is retained; explicitly start a new secret to create another record.";
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
        } catch (SecretMutationUnknownException unknown) {
            receipt.RecordId = unknown.SecretId;
            receipt.Effect = SettingsEffect.Unknown;
            receipt.Diagnostic = SettingsDiagnostic.Unknown;
        } catch (Exception) {
            receipt.Effect = SettingsEffect.Unknown;
            receipt.Diagnostic = SettingsDiagnostic.Unknown;
        } finally {
            ClearSensitive(command);
            receipt.IsReconciling = false;
            Notify();
        }
    }

    public Task ReviewAsync(SettingsReceipt receipt) => receipt.RecordId is { } id ? ReviewIdentityAsync(receipt, id) : Task.CompletedTask;

    public async Task ReviewIdentityAsync(SettingsReceipt receipt, Guid id) {
        if (disposed || !owner.IsCurrent || id == Guid.Empty || receipt.IsReviewing || !Receipts.Contains(receipt) || receipt.Effect == SettingsEffect.Pending) {
            return;
        }
        receipt.IsReviewing = true;
        receipt.ObservationFailed = false;
        Notify();
        try {
            var metadata = await owner.ListAsync(lifetime.Token);
            receipt.ObservedRecordId = id;
            receipt.ObservedExists = metadata.Any(item => item.Id == id);
        } catch (Exception) {
            receipt.ObservationFailed = true;
        } finally {
            receipt.IsReviewing = false;
            Notify();
        }
    }

    private bool Current(WorkspaceSecretDraft draft) => !disposed && owner.IsCurrent && ReferenceEquals(Draft, draft);
    private static WorkspaceSecretDraft CreateDraft() => new(new() { Kind = SecretKind.ApiKey, Scope = "workspace" });
    private static void ClearSensitive(SecretEditorModel model) {
        model.SecretValue = string.Empty;
        model.MetadataJson = string.Empty;
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
        selection++;
        ClearSensitive(Draft.Model);
        Draft = CreateDraft();
        Secrets = [];
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
