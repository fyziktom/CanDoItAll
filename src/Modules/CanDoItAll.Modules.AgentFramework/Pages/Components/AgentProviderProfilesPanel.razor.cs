using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers.UI;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;
using ProviderMetadata = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderMetadata;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class AgentProviderProfilesPanel : IProviderProfilesView, IDisposable {
    [Inject] public IProviderEditorCommands Commands { get; set; } = default!;
    [Inject] public ProviderEditorRecovery Recovery { get; set; } = default!;
    [Inject] public IProviderProfilesReads Reads { get; set; } = default!;
    [Inject] public NotificationService NotificationService { get; set; } = default!;
    private ProviderProfilesSession session = default!;
    private ProviderEditorOperations operations = default!;
    private long sharingRevision;
    private bool disposed;

    public ProviderProfilesState State => session.State;
    public ProviderEditorDraft Editor => session.Editor;
    public long Activation => session.SelectionVersion;
    public IReadOnlyList<ProviderProfile> Providers => session.Catalog.Providers;
    public IReadOnlyList<ProviderSecretChoice> Secrets => session.Catalog.Secrets.Items
        .Select(secret => new ProviderSecretChoice(ProviderMetadata.CreateSecretReference(secret.Id), secret.Name)).ToArray();
    public ProviderProfile? SelectedProvider => session.SelectedProvider;
    public ProviderProfilesLoadState CatalogLoadState => session.CatalogLoadState;
    public bool CanEdit => session.CanEdit;
    public bool SourceManaged => session.IsSourceManaged;
    public bool IsBusy => operations.IsBusy;
    public bool WritesBlocked => operations.WritesBlocked;
    public bool HasPendingReconciliation => operations.HasPendingReconciliation;
    public bool IsWriteUnconfirmed => operations.IsWriteUnconfirmed;
    public bool HasVerifiedRetry => operations.HasVerifiedRetry;
    public string? Error => session.Error;
    public string? MetadataWarning => session.MetadataWarning;
    public string? SecretMetadataError => session.Catalog.Secrets.Error;

    protected override async Task OnInitializedAsync() {
        session = new(Reads, Recovery);
        operations = new(session, Commands);
        await session.RefreshAsync();
    }
    public void SelectSection(ProviderEditorSection section) => session.SelectSection(section);
    public void SetSharedConnectionsOpen(bool open) => session.SetSharedConnectionsOpen(open);
    public async Task RefreshAsync() {
        await session.RefreshAsync();
        await RenderCurrentAsync();
    }
    public async Task SelectAsync(Guid providerId) {
        await session.SelectAsync(providerId);
        await RenderCurrentAsync();
    }
    public async Task NewAsync() {
        await session.NewAsync();
        await RenderCurrentAsync();
    }
    public async Task ExecuteAsync(ProviderEditorIntent intent) {
        if (!IsCurrent(intent.Target) || intent.Target.ProviderId != State.ProviderId) {
            return;
        }
        var feedback = await (intent.Action switch {
            ProviderEditorAction.Save => operations.SaveAsync(),
            ProviderEditorAction.Delete => operations.DeleteAsync(),
            ProviderEditorAction.Health => operations.CheckHealthAsync(),
            ProviderEditorAction.DiscoverModels => operations.DiscoverModelsAsync(),
            ProviderEditorAction.Reconcile => operations.RetryReconciliationAsync(),
            ProviderEditorAction.Verify => operations.VerifyUnconfirmedAsync(),
            ProviderEditorAction.RetryVerified => operations.RetryVerifiedAsync(),
            _ => throw new ArgumentOutOfRangeException(nameof(intent))
        });
        if (!IsCurrent(intent.Target)) {
            return;
        }
        if (feedback is not null) {
            switch (feedback.Kind) {
                case ProviderFeedbackKind.Success:
                    NotificationService.Success(feedback.Title, feedback.Message);
                    break;
                case ProviderFeedbackKind.Warning:
                    NotificationService.Warning(feedback.Title, feedback.Message);
                    break;
                case ProviderFeedbackKind.Error:
                    NotificationService.Error(feedback.Title, feedback.Message);
                    break;
            }
        }
        await InvokeAsync(StateHasChanged);
    }
    private Task RefreshSharedAsync(ProviderEditorTarget target, SharedProviderChangeDelivery delivery) =>
        delivery.ReconcileAsync(async () => {
            if (!IsCurrent(target)) {
                await session.RefreshMetadataAsync();
                return;
            }
            var change = delivery.Change;
            var result = await session.ReconcileSharedAsync(change);
            if (!result.Completed) {
                throw new InvalidOperationException("The provider workspace reconciliation did not complete.");
            }
            if (session.IsCurrentSelection(target.Activation) && target.ProviderId is { } providerId &&
                change.AffectedProviderProfileIds.Contains(providerId) &&
                change.Kind is not (ProviderManagement.SharedProviderChangeKind.Publication or
                    ProviderManagement.SharedProviderChangeKind.ImportedSettings or ProviderManagement.SharedProviderChangeKind.ImportRetirement)) {
                sharingRevision++;
            }
            await InvokeAsync(StateHasChanged);
        });
    private void CloseConnections(ProviderEditorTarget target) {
        if (IsCurrent(target)) {
            session.SetSharedConnectionsOpen(false);
        }
    }
    private bool IsCurrent(ProviderEditorTarget target) => session.IsCurrentSelection(target.Activation) &&
        ReferenceEquals(target.Context, session.EditContext);
    private Task RenderCurrentAsync() => disposed ? Task.CompletedTask : InvokeAsync(StateHasChanged);
    public void Dispose() {
        disposed = true;
        session?.Dispose();
    }
}
