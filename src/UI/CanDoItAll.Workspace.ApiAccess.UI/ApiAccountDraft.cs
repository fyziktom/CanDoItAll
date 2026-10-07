using System.Text.Json.Serialization;
using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Workspace.ApiAccess.UI;

public sealed class ApiAccountDraft {
    private string userName;
    private string displayName;
    private string scopeText;
    private string password = string.Empty;
    private bool enabled;

    public ApiAccountDraft(ApiWriteAction action, ApiAccountMetadata? account = null) {
        Action = action;
        Id = account?.Id;
        Version = account?.Version;
        userName = account?.UserName ?? string.Empty;
        displayName = account?.DisplayName ?? string.Empty;
        scopeText = account is null ? string.Empty : string.Join(' ', account.Scopes);
        enabled = account?.Enabled ?? true;
        Form = new(this);
    }

    public Guid Origin { get; } = Guid.NewGuid();
    public Guid? Id { get; private set; }
    public long? Version { get; private set; }
    public ApiWriteAction Action { get; private set; }
    public long Revision { get; private set; }
    public bool Pending { get; internal set; }
    public bool Unknown { get; internal set; }
    public EditContext Form { get; }
    public string UserName { get => userName; set => Set(ref userName, value); }
    public string DisplayName { get => displayName; set => Set(ref displayName, value); }
    public string ScopeText { get => scopeText; set => Set(ref scopeText, value); }
    public bool Enabled { get => enabled; set => Set(ref enabled, value); }
    [JsonIgnore] public string Password { get => password; set => Set(ref password, value); }
    public bool NeedsPassword => Action is ApiWriteAction.CreateAccount or ApiWriteAction.ResetPassword;
    public bool EditsProfile => Action is ApiWriteAction.CreateAccount or ApiWriteAction.UpdateAccount;

    public ApiAccountIntent Capture() => new(Action, Id, Version, UserName, DisplayName, Enabled, ScopeText, Password);
    public void ClearPassword() => password = string.Empty;
    public void Accept(ApiAccountMetadata account) {
        Id = account.Id;
        Version = account.Version;
        if (Action == ApiWriteAction.CreateAccount) {
            Action = ApiWriteAction.UpdateAccount;
        }
    }
    private void Set<T>(ref T field, T value) {
        if (!EqualityComparer<T>.Default.Equals(field, value)) {
            field = value;
            Revision++;
        }
    }
}
