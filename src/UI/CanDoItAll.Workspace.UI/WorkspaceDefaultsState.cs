using CanDoItAll.Modules.Workspace;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Workspace.UI;

public enum DefaultsField { Name, Provider, OutputFormat, Currency, Culture, Notes }

public sealed class WorkspaceDefaultsDraft {
    private readonly Dictionary<DefaultsField, long> versions = [];
    public Guid Origin { get; } = Guid.NewGuid();
    public WorkspaceSettingsModel Model { get; } = new();
    public EditContext EditContext { get; }
    public WorkspaceDefaultsDraft() => EditContext = new(Model);
    public void Edited(DefaultsField field) => versions[field] = versions.GetValueOrDefault(field) + 1;
    public IReadOnlyDictionary<DefaultsField, long> CaptureVersions() => new Dictionary<DefaultsField, long>(versions);

    public void Reconcile(WorkspaceSettingsModel saved, IReadOnlyDictionary<DefaultsField, long> captured) {
        Apply(DefaultsField.Name, () => Model.WorkspaceName = saved.WorkspaceName);
        Apply(DefaultsField.Provider, () => Model.DefaultProviderProfileId = saved.DefaultProviderProfileId);
        Apply(DefaultsField.OutputFormat, () => Model.DefaultPromptOutputFormat = saved.DefaultPromptOutputFormat);
        Apply(DefaultsField.Currency, () => Model.CurrencyCode = saved.CurrencyCode);
        Apply(DefaultsField.Culture, () => Model.CurrencyCultureName = saved.CurrencyCultureName);
        Apply(DefaultsField.Notes, () => Model.Notes = saved.Notes);

        void Apply(DefaultsField field, Action assign) {
            if (versions.GetValueOrDefault(field) == captured.GetValueOrDefault(field)) {
                assign();
            }
        }
    }
}

public interface IWorkspaceDefaultsState {
    event Action? Changed;
    WorkspaceDefaultsDraft Draft { get; }
    IReadOnlyList<WorkspaceProviderOption> Providers { get; }
    IReadOnlyList<SettingsReceipt> Receipts { get; }
    bool CanSave { get; }
    bool IsLoading { get; }
    string? Error { get; }
    Task RefreshAsync();
    Task SaveAsync();
    Task ReviewAsync(SettingsReceipt receipt);
}
