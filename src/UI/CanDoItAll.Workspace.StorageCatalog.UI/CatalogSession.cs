using System.Collections.Immutable;
using CanDoItAll.Modules.Workspace.StorageCatalog.Contracts;

namespace CanDoItAll.Workspace.StorageCatalog.UI;

public sealed class CatalogSession : IDisposable {
    private readonly IStorageCatalogOwner owner;
    private bool disposed;
    private long mutationRevision;
    public CatalogSession(IStorageCatalogOwner owner, CatalogOperationLedger ledger) {
        this.owner = owner;
        Ledger = ledger;
        Catalog = new([], () => IsCurrent, Notify);
        Secrets = new([], () => IsCurrent, Notify);
        Routes = new([], () => IsCurrent, Notify);
        EditorRead = new(null, () => IsCurrent, Notify);
        Ledger.Changed += ReceiptsChanged;
        New(CatalogProvider.FileSystem);
    }
    public CatalogChoices Choices => owner.Choices;
    public CatalogContext Context => owner.Context;
    public bool IsCurrent => !disposed && owner.IsCurrent;
    public CatalogOperationLedger Ledger { get; }
    public IEnumerable<CatalogReceipt> Receipts => Ledger.Receipts.Where(receipt => receipt.Context == Context).Reverse();
    public CatalogReadLane<ImmutableArray<CatalogRow>> Catalog { get; }
    public CatalogReadLane<ImmutableArray<CatalogSecret>> Secrets { get; }
    public CatalogReadLane<ImmutableArray<CatalogRoute>> Routes { get; }
    public CatalogReadLane<CatalogEdit?> EditorRead { get; }
    public CatalogDraft? Draft { get; private set; }
    public Guid? SelectedId { get; private set; }
    public string Search { get; set; } = string.Empty;
    public string Notice { get; private set; } = string.Empty;
    public bool CanMutate => IsCurrent && Draft is { IsSystemDefault: false, Deleted: false } && !Ledger.Blocks(Context);
    public event Action? Changed;
    public IEnumerable<CatalogRow> Filtered => Catalog.Value.Where(row => string.IsNullOrWhiteSpace(Search) ||
        row.Name.Contains(Search, StringComparison.OrdinalIgnoreCase) || row.EndpointOrRoot.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
        ProviderLabel(row.ProviderKind).Contains(Search, StringComparison.OrdinalIgnoreCase)).OrderBy(row => row.DisplayOrder).ThenBy(row => row.Name);
    public string ProviderLabel(CatalogProvider provider) => Choices.Providers.FirstOrDefault(choice => choice.Value == provider)?.Label ?? $"Unsupported provider ({(int)provider})";
    public string PurposeLabel(CatalogPurpose purpose) => Choices.Purposes.FirstOrDefault(choice => choice.Value == purpose)?.Label ?? $"Unknown purpose ({(int)purpose})";
    public Task LoadAsync() => Task.WhenAll(RefreshCatalogAsync(), RefreshSecretsAsync(), RefreshRoutesAsync());
    public Task RefreshCatalogAsync() => Catalog.ReadAsync(owner.ReadCatalogAsync);
    public Task RefreshSecretsAsync() => Secrets.ReadAsync(owner.ReadSecretsAsync);
    public Task RefreshRoutesAsync() => Routes.ReadAsync(owner.ReadRoutesAsync);

    public void New(CatalogProvider provider) {
        if (!IsCurrent) {
            return;
        }
        var choice = Choices.Providers.FirstOrDefault(item => item.Value == provider);
        if (choice is null) {
            Notice = "This provider has no available template.";
            Notify();
            return;
        }
        EditorRead.Invalidate();
        SelectedId = null;
        Draft = new(choice.Template);
        Notify();
    }

    public Task SelectAsync(Guid id) {
        if (!IsCurrent) {
            return Task.CompletedTask;
        }
        SelectedId = id;
        Draft = null;
        return EditorRead.ReadAsync(token => owner.ReadEditorAsync(id, token), editor => {
            Draft = editor is null || editor.Id != id ? null : new(editor);
        });
    }

    public async Task MutateAsync(CatalogEffect effect) {
        if (!CanMutate || Draft is not { } draft || (effect == CatalogEffect.Delete && !draft.Id.HasValue)) {
            return;
        }
        var captured = draft.Capture(effect);
        if (captured is null) {
            Notify();
            return;
        }
        var command = new CatalogCommand(Guid.NewGuid(), draft.Origin, Context, effect, captured.Value);
        var receipt = Ledger.Admit(command);
        if (receipt is null) {
            Notice = "An unresolved operation must be reviewed before another effect can be admitted.";
            Notify();
            return;
        }
        var revision = ++mutationRevision;
        Ledger.Notify(Context);
        CatalogOutcome result;
        try {
            result = await owner.ExecuteAsync(command, CancellationToken.None);
        } catch (Exception) {
            result = new() { Write = CatalogWrite.Unknown, Diagnostic = CatalogDiagnostic.PersistenceFailed };
        }
        receipt.Outcome = result;
        if (!IsCurrent) {
            Ledger.Notify(Context);
            return;
        }
        if (ReferenceEquals(Draft, draft)) {
            if (result.Write == CatalogWrite.Committed && result.CatalogId is { } id) {
                if (effect == CatalogEffect.Delete) {
                    draft.MarkDeleted();
                } else {
                    draft.AcceptIdentity(id);
                    SelectedId = id;
                }
            }
            if (result.Health is { } health) {
                draft.AcceptHealth(health, captured);
            }
        }
        Ledger.Notify(Context);
        if (result.Write == CatalogWrite.Committed) {
            await ObserveAsync(receipt, effect == CatalogEffect.Delete ? null : draft, captured, revision);
        }
    }

    public Task ObserveAsync(CatalogReceipt receipt) => ObserveAsync(receipt, null, null);
    private async Task ObserveAsync(CatalogReceipt receipt, CatalogDraft? original, CatalogSubmission? captured, long? expectedMutation = null) {
        if (!IsCurrent || receipt.IsPending || receipt.ObservationLoading || receipt.Context != Context) {
            return;
        }
        receipt.ObservationLoading = true;
        Ledger.Notify(Context);
        try {
            var id = receipt.Outcome?.CatalogId ?? receipt.OriginalId;
            var observed = id.HasValue ? await owner.ReadEditorAsync(id.Value, CancellationToken.None) : null;
            if (!IsCurrent) {
                return;
            }
            receipt.Observation = id.HasValue
                ? observed is null ? "Original target is currently absent. This observation does not establish operation causality."
                    : "Original target is currently present. This observation does not settle an unknown acknowledgement or replay any effect."
                : "No acknowledged create identity is available. Catalog observation cannot establish which row, if any, this operation created.";
            if (expectedMutation == mutationRevision && original is not null && captured is not null && observed is not null && ReferenceEquals(Draft, original)) {
                original.Merge(observed, captured, receipt.Outcome!.Routing);
            }
            await Task.WhenAll(RefreshCatalogAsync(), RefreshRoutesAsync());
        } catch (Exception) {
            if (IsCurrent) {
                receipt.Observation = "Read-back unavailable. The original effect facts are retained; retry observation only.";
            }
        } finally {
            receipt.ObservationLoading = false;
            Ledger.Notify(Context);
        }
    }

    public void AcknowledgeCorrection(CatalogReceipt receipt) {
        if (!IsCurrent || receipt.Context != Context || receipt.Outcome is not { IsUnknown: false, NeedsReview: true }) {
            return;
        }
        receipt.CorrectionAcknowledged = true;
        Notice = "Partial routing reviewed. A subsequent Save is a separate explicit correction of the currently selected draft.";
        Ledger.Notify(Context);
    }

    private void ReceiptsChanged(CatalogContext context) {
        if (context == Context) {
            Notify();
        }
    }

    private void Notify() {
        if (IsCurrent) {
            Changed?.Invoke();
        }
    }
    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        Ledger.Changed -= ReceiptsChanged;
        Catalog.Dispose();
        Secrets.Dispose();
        Routes.Dispose();
        EditorRead.Dispose();
        Draft = null;
        Changed = null;
    }
}
