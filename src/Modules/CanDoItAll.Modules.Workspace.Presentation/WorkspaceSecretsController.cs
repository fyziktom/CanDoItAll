using CanDoItAll.Modules.Security;
using CanDoItAll.Workspace.UI;

namespace CanDoItAll.Modules.Workspace;

public enum WorkspaceSelectionCause { Operator, Owner }

public readonly record struct WorkspaceSecretSelection(Guid? SecretId, WorkspaceSelectionCause Cause);

public sealed class WorkspaceSecretsController(IWorkspaceSecretsOwner owner) : IWorkspaceSecretsState, IDisposable {
    private readonly SettingsOperationLedger ledger = new();
    private readonly CancellationTokenSource lifetime = new();
    private long selection;
    private bool acquired = true;
    private bool disposed;
    private Task? refresh;
    private WorkspaceSecretDraft? readingComparison;
    private string? referenceError;
    private string? editorError;
    private Guid? publishedSelection;
    public event Action? Changed;
    public event Action<WorkspaceSecretSelection>? SelectionChanged;
    public WorkspaceSecretDraft Draft { get; private set; } = CreateDraft();
    public Guid? SelectedSecretId => Draft.Model.Id;
    public IReadOnlyList<SecretListItem> Secrets { get; private set; } = [];
    public IReadOnlyList<SettingsReceipt> Receipts => ledger.Entries;
    public string Search { get; set; } = string.Empty;
    public string? Error { get => editorError ?? referenceError; private set => editorError = value; }
    public bool IsLoading { get; private set; }
    public bool CanEdit => !disposed && owner.IsCurrent && acquired;
    public bool CanMutate => CanEdit && !RequiresComparison && !ledger.Blocks(Draft.Origin, Draft.Model.Id);
    public bool CanCompare => CanEdit && RequiresComparison && Draft.Baseline is not null && Draft.Model.Id.HasValue &&
        !ReferenceEquals(readingComparison, Draft) && !ledger.Blocks(Draft.Origin, Draft.Model.Id);
    public bool RequiresComparison { get; private set; }
    public WorkspaceSecretReview? Comparison { get; private set; }

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
        RetireDraft();
        Draft = new(new() { Id = id });
        var draft = Draft;
        acquired = false;
        Error = null;
        PublishSelection(WorkspaceSelectionCause.Operator);
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

    public void New() => Reset(WorkspaceSelectionCause.Operator);

    public async Task CompareAsync() {
        if (!CanCompare || Draft.Model.Id is not { } id) {
            return;
        }
        var draft = Draft;
        readingComparison = draft;
        Notify();
        try {
            var current = await owner.GetAsync(id, lifetime.Token);
            if (Current(draft)) {
                if (current?.Id == id && current.ExpectedUpdatedAtUtc.HasValue) {
                    Comparison = new(draft, current);
                    Error = null;
                } else {
                    Error = "The original secret is unavailable. Your draft remains open.";
                }
            }
        } catch (Exception) {
            if (Current(draft)) {
                Error = "Saved secret metadata could not be read. Your draft remains open.";
            }
        } finally {
            if (ReferenceEquals(readingComparison, draft)) {
                readingComparison = null;
            }
            Notify();
        }
    }

    public void ApplyComparison() {
        if (!CanCompare || Comparison is not { } review || !review.CanApply(Draft)) {
            return;
        }
        Draft.Apply(review);
        RequiresComparison = false;
        CloseComparison();
    }

    public void CloseComparison() {
        Comparison = null;
        Notify();
    }

    private void Reset(WorkspaceSelectionCause cause) {
        if (disposed || !owner.IsCurrent) {
            return;
        }
        selection++;
        RetireDraft();
        Draft = CreateDraft();
        acquired = true;
        Error = null;
        PublishSelection(cause);
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
        var replacementRevision = draft.ReplacementRevision;
        var command = draft.Model.Copy();
        var action = delete ? SettingsAction.DeleteSecret : SettingsAction.SaveSecret;
        if (ledger.Admit(draft.Origin, action, command.Id) is not { } receipt) {
            ClearSensitive(command);
            return;
        }
        Notify();
        try {
            var result = delete
                ? await owner.DeleteAsync(new(command.Id!.Value, command.ExpectedUpdatedAtUtc), lifetime.Token)
                : await owner.SaveAsync(command, lifetime.Token);
            SettingsOperationLedger.Complete(receipt, result);
            if (result.State == SettingsWriteState.Refused) {
                if (Current(draft) && result.Diagnostic == SettingsDiagnostic.Conflict) {
                    RequiresComparison = true;
                    Error = "The secret changed after you opened it. Compare the saved metadata before retrying.";
                }
                return;
            }
            receipt.RecordId = result.Value;
            if (Current(draft)) {
                var hasLaterInput = draft.Revision != revision;
                if (!delete) {
                    draft.Accept(result.Value, command, replacementRevision);
                }
                if (!hasLaterInput) {
                    Reset(WorkspaceSelectionCause.Owner);
                } else {
                    if (delete) {
                        acquired = false;
                        Error = "This secret was deleted. Newer input is retained; explicitly start a new secret to create another record.";
                    }
                    PublishSelection(WorkspaceSelectionCause.Owner);
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
    private void RetireDraft() {
        Draft.Retire();
        Comparison = null;
        RequiresComparison = false;
    }

    private static void ClearSensitive(SecretEditorModel model) {
        model.SecretValue = string.Empty;
        model.MetadataJson = string.Empty;
    }

    private void PublishSelection(WorkspaceSelectionCause cause) {
        var current = Draft.Model.Id;
        if (disposed || publishedSelection == current) {
            return;
        }
        publishedSelection = current;
        SelectionChanged?.Invoke(new(current, cause));
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
        RetireDraft();
        Draft = CreateDraft();
        Secrets = [];
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
