using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;

namespace CanDoItAll.Workspace.ApiAccess.UI;

public enum ApiTargetKind { Issuance, Token, Account, NewAccount }
public readonly record struct ApiWriteTarget(ApiTargetKind Kind, Guid Id = default, string? UserName = null);
public sealed record ApiOperationReceipt(Guid OperationId, ApiWriteTarget Target, ApiWriteAction Action, ApiWriteResult Result);

public sealed class ApiOperationLedger(int capacity = 32) {
    private readonly Lock sync = new();
    private readonly List<ApiOperationReceipt> receipts = [];

    public IReadOnlyList<ApiOperationReceipt> Receipts {
        get {
            lock (sync) {
                return receipts.ToArray();
            }
        }
    }

    public bool IsBlocked(ApiWriteTarget target) {
        lock (sync) {
            return receipts.Any(receipt => receipt.Target == target && IsUnresolved(receipt));
        }
    }

    public Guid? Begin(ApiWriteTarget target, ApiWriteAction action) {
        lock (sync) {
            if (capacity < 1 || receipts.Any(receipt => receipt.Target == target && IsUnresolved(receipt))) {
                return null;
            }
            if (receipts.Count >= capacity) {
                var resolved = receipts.FindIndex(receipt => !IsUnresolved(receipt));
                if (resolved < 0) {
                    return null;
                }
                receipts.RemoveAt(resolved);
            }
            var id = Guid.NewGuid();
            receipts.Add(new(id, target, action, new(ApiWriteState.Pending)));
            return id;
        }
    }

    public void Complete(Guid operationId, ApiWriteResult result) {
        lock (sync) {
            var index = receipts.FindIndex(receipt => receipt.OperationId == operationId);
            if (index >= 0 && receipts[index].Result.State == ApiWriteState.Pending && result.State != ApiWriteState.Pending) {
                receipts[index] = receipts[index] with { Result = result };
            }
        }
    }

    private static bool IsUnresolved(ApiOperationReceipt receipt) => receipt.Result.State is ApiWriteState.Pending or ApiWriteState.Unknown;
}
