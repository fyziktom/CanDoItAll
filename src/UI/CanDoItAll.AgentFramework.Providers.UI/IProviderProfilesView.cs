using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.AgentFramework.Providers.UI;

public sealed record ProviderSecretChoice(string Reference, string Name);
public sealed record ProviderEditorTarget(Guid? ProviderId, long Activation, EditContext Context);
public sealed record ProviderHostSlotContext(ProviderEditorTarget Target, ProviderEditorSection Section, bool SourceManaged);
public enum ProviderEditorAction { Save, Delete, Health, DiscoverModels, Reconcile, Verify, RetryVerified }
public sealed record ProviderEditorIntent(ProviderEditorTarget Target, ProviderEditorAction Action);

public interface IProviderProfilesView {
    ProviderProfilesState State { get; }
    ProviderEditorDraft Editor { get; }
    long Activation { get; }
    IReadOnlyList<ProviderProfile> Providers { get; }
    IReadOnlyList<ProviderSecretChoice> Secrets { get; }
    ProviderProfile? SelectedProvider { get; }
    ProviderProfilesLoadState CatalogLoadState { get; }
    bool CanEdit { get; }
    bool SourceManaged { get; }
    bool IsBusy { get; }
    bool WritesBlocked { get; }
    bool HasPendingReconciliation { get; }
    bool IsWriteUnconfirmed { get; }
    bool HasVerifiedRetry { get; }
    string? Error { get; }
    string? MetadataWarning { get; }
    string? SecretMetadataError { get; }
    void SelectSection(ProviderEditorSection section);
    void SetSharedConnectionsOpen(bool open);
    Task RefreshAsync();
    Task SelectAsync(Guid providerId);
    Task NewAsync();
    Task ExecuteAsync(ProviderEditorIntent intent);
}
