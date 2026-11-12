using CanDoItAll.Plugins.UI;

namespace CanDoItAll.Modules.Plugins.Presentation;

public sealed class PluginWorkspaceOperations(PluginsWorkspaceView view, IPluginWorkspaceOwner owner, Action changed, Action<PluginPackageProgress> acceptPackageProgress) {
    private readonly object reconciliation = new();
    public async Task RunAsync<T>(PluginOperationTarget target, Func<Task<PluginWriteReceipt<T>>> write,
        Action<T> accept, Func<Task<bool>> refresh, string success, Func<bool> visible,
        Action<PluginOperationState>? admitted = null) {
        if (view.IsDisposed || view.IsBlocked(target)) {
            return;
        }
        foreach (var completed in view.Operations.Where(pair => !pair.Value.PreventsReplay &&
            pair.Key is not PluginOperationTarget.Connection && pair.Value.PackageProgress is null).Select(pair => pair.Key).ToArray()) {
            view.Operations.Remove(completed);
        }
        if (view.Operations.Count >= 256 && !view.Operations.ContainsKey(target)) {
            view.Notice = "Review unresolved operations before starting another operation.";
            view.NoticeStatus = PluginMutationStatus.Refused;
            changed();
            return;
        }
        var operation = new PluginOperationState();
        view.Operations[target] = operation;
        admitted?.Invoke(operation);
        changed();
        PluginWriteReceipt<T> receipt;
        try {
            receipt = await write();
        } catch (Exception exception) {
            owner.ReportFailure("mutation", exception);
            receipt = PluginWriteReceipt<T>.Unknown();
        }
        if (view.IsDisposed || !view.Operations.TryGetValue(target, out var current) || !ReferenceEquals(current, operation)) {
            return;
        }
        var status = receipt.Status;
        operation.PackageProgress = receipt.PackageProgress;
        operation.OAuthProgress = receipt.OAuthProgress;
        operation.HasCommittedValue = receipt.Committed;
        lock (reconciliation) {
            if (receipt.PackageProgress is { } progress) {
                acceptPackageProgress(progress);
            }
            if (receipt.Committed && receipt.Value is { } value) {
                accept(value);
            }
        }
        if (receipt.Committed) {
            changed();
            bool refreshed;
            try {
                refreshed = await refresh();
            } catch (Exception exception) {
                owner.ReportFailure("post-commit refresh", exception);
                refreshed = false;
            }
            if (!refreshed || receipt.Status == PluginMutationStatus.SavedWithWarning) {
                status = PluginMutationStatus.SavedWithWarning;
            }
        }
        if (view.IsDisposed || !view.Operations.TryGetValue(target, out current) || !ReferenceEquals(current, operation)) {
            return;
        }
        operation.Status = status;
        operation.Message = status switch {
            PluginMutationStatus.Saved => success,
            PluginMutationStatus.SavedWithWarning => success + " A follow-up step is unavailable. Refresh reads only; do not repeat the operation.",
            PluginMutationStatus.Refused => "The operation was refused. Review the settings and permissions.",
            _ => "Outcome unknown. Review stored state before explicitly resolving this operation; automatic replay is blocked."
        };
        if (visible()) {
            view.Notice = operation.Message;
            view.NoticeStatus = operation.Status;
        }
        changed();
    }
}
