using CanDoItAll.Modules.Workspace;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Workspace.UI;

public sealed class WorkspaceFileDraft {
    public Guid Origin { get; } = Guid.NewGuid();
    public WorkspaceFileExtension? Selected { get; set; }
    public string Extension { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public long Revision { get; private set; }
    public EditContext EditContext { get; }
    public WorkspaceFileDraft() => EditContext = new(this);
    public void Edited() => Revision++;
}

public interface IWorkspaceFilesState {
    event Action? Changed;
    WorkspaceFileDraft Draft { get; }
    IReadOnlyList<WorkspaceFilePreference> Preferences { get; }
    IReadOnlyList<SettingsReceipt> Receipts { get; }
    bool CanMutate { get; }
    bool IsLoading { get; }
    string? Error { get; }
    Task RefreshAsync();
    Task SaveAsync();
    Task DeleteAsync();
    Task ReviewAsync(SettingsReceipt receipt);
    void Select(WorkspaceFilePreference preference);
    void New();
}
