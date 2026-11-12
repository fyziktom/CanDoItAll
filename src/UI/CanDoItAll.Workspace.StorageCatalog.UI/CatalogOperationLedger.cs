using CanDoItAll.Modules.Workspace.StorageCatalog.Contracts;

namespace CanDoItAll.Workspace.StorageCatalog.UI;

public sealed class CatalogReceipt(CatalogCommand command) {
    public Guid OperationId { get; } = command.OperationId;
    public Guid Origin { get; } = command.Origin;
    public CatalogContext Context { get; } = command.Context;
    public CatalogEffect Effect { get; } = command.Effect;
    public Guid? OriginalId { get; } = command.Draft.Id;
    public CatalogOutcome? Outcome { get; internal set; }
    public bool CorrectionAcknowledged { get; internal set; }
    public bool IsPending => Outcome is null;
    public bool Blocks => IsPending || Outcome!.IsUnknown || (Outcome.NeedsReview && !CorrectionAcknowledged);
    public bool ObservationLoading { get; internal set; }
    public string Observation { get; internal set; } = string.Empty;
}

public sealed class CatalogOperationLedger {
    public const int Capacity = 32;
    private readonly List<CatalogReceipt> receipts = [];
    internal event Action<CatalogContext>? Changed;
    internal void Notify(CatalogContext context) => Changed?.Invoke(context);
    public IReadOnlyList<CatalogReceipt> Receipts => receipts;
    public bool Blocks(CatalogContext context) => receipts.Any(receipt => receipt.Context == context && receipt.Blocks);
    public CatalogReceipt? Admit(CatalogCommand command) {
        if (Blocks(command.Context)) {
            return null;
        }
        if (receipts.Count == Capacity) {
            var removable = receipts.FindIndex(receipt => !receipt.Blocks);
            if (removable < 0) {
                return null;
            }
            receipts.RemoveAt(removable);
        }
        var receipt = new CatalogReceipt(command);
        receipts.Add(receipt);
        return receipt;
    }
}
