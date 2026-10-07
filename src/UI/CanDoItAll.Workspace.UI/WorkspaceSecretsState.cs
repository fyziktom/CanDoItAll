using CanDoItAll.Modules.Security;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Workspace.UI;

public sealed class WorkspaceSecretDraft(SecretEditorModel model) {
    public Guid Origin { get; } = Guid.NewGuid();
    public SecretEditorModel Model { get; } = model;
    public EditContext EditContext { get; } = new(model);
    public long Revision { get; private set; }
    public void Edited() => Revision++;
}

public interface IWorkspaceSecretsState {
    event Action? Changed;
    WorkspaceSecretDraft Draft { get; }
    IReadOnlyList<SecretListItem> Secrets { get; }
    IReadOnlyList<SettingsReceipt> Receipts { get; }
    string Search { get; set; }
    bool CanMutate { get; }
    bool CanEdit { get; }
    bool IsLoading { get; }
    string? Error { get; }
    Task RefreshAsync();
    Task SelectAsync(Guid id);
    Task SaveAsync();
    Task DeleteAsync();
    Task ReviewAsync(SettingsReceipt receipt);
    Task ReviewIdentityAsync(SettingsReceipt receipt, Guid id);
    void New();
}
