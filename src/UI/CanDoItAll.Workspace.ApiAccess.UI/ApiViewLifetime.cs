using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;

namespace CanDoItAll.Workspace.ApiAccess.UI;

public sealed class ApiViewLifetime : IDisposable {
    private readonly CancellationTokenSource cancellation = new();
    private readonly CancellationTokenRegistration parentRegistration;

    public ApiViewLifetime(ApiViewLifetime? parent = null) {
        if (parent is not null) {
            if (parent.IsActive) {
                parentRegistration = parent.Token.Register(Dispose);
            } else {
                Dispose();
            }
        }
    }
    public bool IsActive { get; private set; } = true;
    public CancellationToken Token => cancellation.Token;
    public event Action? Retired;

    public void Dispose() {
        if (!IsActive) {
            return;
        }
        IsActive = false;
        cancellation.Cancel();
        Retired?.Invoke();
        Retired = null;
        cancellation.Dispose();
        parentRegistration.Dispose();
    }
}

public static class ApiOutcomeText {
    public static string Describe(ApiWriteResult result) => result.State switch {
        ApiWriteState.Pending => "Operation is pending. Closing this view does not undo an admitted write.",
        ApiWriteState.Committed => "The change was saved.",
        ApiWriteState.CommittedWithWarning => "The change was saved, but its diagnostic notification failed. Do not repeat the write.",
        ApiWriteState.Unknown => "The durable acknowledgement is unknown. Do not repeat this operation. An exact observation shows current state only, not whether this request committed.",
        _ => result.Failure switch {
            ApiFailure.Denied => "Administration access was denied. Reopen or retry access with the authorized operator.",
            ApiFailure.Conflict => "The account changed or its username is already in use. Reload the exact account before editing.",
            ApiFailure.Missing => "The selected record no longer exists.",
            ApiFailure.Invalid => "The request was refused. Check the fields, allowed scopes and current limits.",
            _ => "The owner is unavailable. No write was admitted. Retry explicitly."
        }
    };
}
