using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.Modules.Workspace.Pages.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Workspace.UI;

public sealed record PendingRetentionChange(HistoryPolicy Policy, long Version, HistoryRetentionPreview Preview);

public interface IWorkspaceHistoryState {
    event Action? Changed;
    HistoryPolicySnapshot? Snapshot { get; }
    ProviderHistoryPolicyDraft Draft { get; }
    EditContext EditContext { get; }
    PendingRetentionChange? Preview { get; }
    IReadOnlyList<SettingsReceipt> Receipts { get; }
    bool IsBusy { get; }
    bool CanApply { get; }
    string? Error { get; }
    string? Message { get; }
    Task LoadAsync();
    Task ApplyFutureAsync();
    Task PreviewAsync();
    Task ConfirmShorterAsync();
    void ClosePreview();
    void DraftChanged();
}
