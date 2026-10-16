using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers.UI;
using CanDoItAll.AgentFramework.UI.History;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;
using ProviderMetadata = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderMetadata;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

// The provider and editor section a route-controlled panel shows, for its page to write into the URL.
public readonly record struct ProviderRouteSelection(Guid ProviderId, ProviderEditorSection Section);

public partial class AgentProviderProfilesPanel : IProviderProfilesView, IDisposable {
    [Inject] public IProviderEditorCommands Commands { get; set; } = default!;
    [Inject] public ProviderEditorRecovery Recovery { get; set; } = default!;
    [Inject] public IProviderProfilesReads Reads { get; set; } = default!;
    [Inject] public NotificationService NotificationService { get; set; } = default!;
    [Parameter] public Guid? ProviderId { get; set; }
    [Parameter] public ProviderEditorSection Section { get; set; } = ProviderEditorSection.Connection;
    [Parameter] public bool ConnectionsOpen { get; set; }
    [Parameter] public ProviderHistoryLocation? HistoryLocation { get; set; }
    [Parameter] public EventCallback<ProviderRouteSelection> ProviderRequested { get; set; }
    [Parameter] public EventCallback<ProviderRouteSelection> ProviderPublished { get; set; }
    [Parameter] public EventCallback NewDraftRequested { get; set; }
    [Parameter] public EventCallback<ProviderEditorSection> SectionRequested { get; set; }
    [Parameter] public EventCallback<bool> ConnectionsRequested { get; set; }
    [Parameter] public EventCallback<ProviderHistoryLocationChange> HistoryLocationChanged { get; set; }
    private ProviderProfilesSession session = default!;
    private ProviderEditorOperations operations = default!;
    private long sharingRevision;
    private bool disposed;
    private bool routeApplied;
    private Guid? appliedProviderId;
    private ProviderEditorSection? appliedSection;

    private bool IsRouteControlled => ProviderRequested.HasDelegate;

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
        if (!IsRouteControlled) {
            await session.RefreshAsync();
        }
    }

    protected override Task OnParametersSetAsync() => IsRouteControlled ? ApplyRouteAsync() : Task.CompletedTask;

    // A location applies its section when the section changes or the location names another provider, never on a mere
    // render: a new draft has no provider in its URL and so no section there either, and keeps the one it is on.
    private async Task ApplyRouteAsync() {
        var initial = !routeApplied;
        if (initial || appliedProviderId != ProviderId || Section != appliedSection) {
            appliedSection = Section;
            session.SelectSection(Section);
        }
        session.SetSharedConnectionsOpen(ConnectionsOpen);
        if (!initial && appliedProviderId == ProviderId) {
            return;
        }
        routeApplied = true;
        appliedProviderId = ProviderId;
        if (initial && !await session.RefreshCatalogAsync()) {
            return;
        }
        if (ProviderId is { } providerId) {
            await session.SelectAsync(providerId);
            return;
        }
        var fallback = session.Catalog.Providers.FirstOrDefault()?.Id;
        await session.SelectAsync(fallback);
        if (fallback is { } selected && appliedProviderId is null) {
            appliedProviderId = selected;
            await ProviderPublished.InvokeAsync(new(selected, State.Section));
        }
    }

    public void SelectSection(ProviderEditorSection section) {
        session.SelectSection(section);
        if (IsRouteControlled) {
            _ = SectionRequested.InvokeAsync(section);
        }
    }

    public void SetSharedConnectionsOpen(bool open) {
        session.SetSharedConnectionsOpen(open);
        if (IsRouteControlled) {
            _ = ConnectionsRequested.InvokeAsync(open);
        }
    }

    public async Task RefreshAsync() {
        await session.RefreshAsync();
        await RenderCurrentAsync();
    }

    public async Task SelectAsync(Guid providerId) {
        if (IsRouteControlled) {
            routeApplied = true;
            appliedProviderId = providerId;
            await ProviderRequested.InvokeAsync(new(providerId, State.Section));
        }
        await session.SelectAsync(providerId);
        await RenderCurrentAsync();
    }

    // A draft has no identity to put in the URL, so New leaves the provider's location: Back returns to that provider,
    // and a reload or a shared link opens the catalog's default selection rather than an unsaved draft.
    public async Task NewAsync() {
        if (IsRouteControlled) {
            routeApplied = true;
            appliedProviderId = null;
            await NewDraftRequested.InvokeAsync();
        }
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
        await PublishCommittedIdentityAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task PublishCommittedIdentityAsync() {
        if (!IsRouteControlled || State.ProviderId is not { } providerId || providerId == appliedProviderId) {
            return;
        }
        appliedProviderId = providerId;
        await ProviderPublished.InvokeAsync(new(providerId, State.Section));
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
            SetSharedConnectionsOpen(false);
        }
    }

    private EventCallback<ProviderHistoryLocationChange> HistoryChanged => IsRouteControlled ? HistoryLocationChanged : default;

    private bool IsCurrent(ProviderEditorTarget target) => session.IsCurrentSelection(target.Activation) &&
        ReferenceEquals(target.Context, session.EditContext);
    private Task RenderCurrentAsync() => disposed ? Task.CompletedTask : InvokeAsync(StateHasChanged);
    public void Dispose() {
        disposed = true;
        session?.Dispose();
    }
}
