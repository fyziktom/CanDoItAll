using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers.UI;
using CanDoItAll.AgentFramework.UI.History;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;
using CanDoItAll.Editing;
using ProviderEditingChanges = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderEditingChanges;
using ProviderEditingOpening = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderEditingOpening;
using ProviderMetadata = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderMetadata;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

// The provider and editor section a route-controlled panel shows, for its page to write into the URL.
public readonly record struct ProviderRouteSelection(Guid ProviderId, ProviderEditorSection Section);

public partial class AgentProviderProfilesPanel : IProviderProfilesView, IAsyncDisposable {
    [Inject] public IProviderEditorCommands Commands { get; set; } = default!;
    [Inject] public ProviderEditorRecovery Recovery { get; set; } = default!;
    [Inject] public IProviderProfilesReads Reads { get; set; } = default!;
    [Inject] public NotificationService NotificationService { get; set; } = default!;
    [Inject] public ProviderEditingChanges EditingChanges { get; set; } = default!;
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
    private ProviderEditingOpening? opening;
    private bool admitted;
    private bool admissionAttempted;
    private string? accessError;
    private long changeSequence;
    private long refreshedSequence;

    private bool IsRouteControlled => ProviderRequested.HasDelegate;

    public ProviderProfilesState State => session.State;
    public ProviderEditorDraft Editor => session.Editor;
    public long Activation => session.SelectionVersion;
    public IReadOnlyList<ProviderProfile> Providers => admitted ? session.Catalog.Providers : [];
    public IReadOnlyList<ProviderSecretChoice> Secrets => !admitted ? [] : session.Catalog.Secrets.Items
        .Select(secret => new ProviderSecretChoice(ProviderMetadata.CreateSecretReference(secret.Id), secret.Name)).ToArray();
    public ProviderProfile? SelectedProvider => admitted ? session.SelectedProvider : null;
    public ProviderProfilesLoadState CatalogLoadState => session.CatalogLoadState;
    public bool CanEdit => admitted && session.CanEdit;
    public bool SourceManaged => session.IsSourceManaged;
    public bool IsBusy => operations.IsBusy;
    public bool WritesBlocked => operations.WritesBlocked;
    public bool HasPendingReconciliation => operations.HasPendingReconciliation;
    public bool IsWriteUnconfirmed => operations.IsWriteUnconfirmed;
    public bool CanAcknowledgeDiagnostic => operations.CanAcknowledgeDiagnostic;
    public bool CanReviewCurrent => operations.CanReviewCurrent;
    public ProviderRecoveryReview? RecoveryReview => session.RecoveryReview;
    public bool HasVerifiedRetry => operations.HasVerifiedRetry;
    public bool NeedsComparison => session.NeedsComparison;
    public ProviderEditorComparison? Comparison => session.Comparison;
    public string? Error => accessError ?? session.Error;
    public bool CatalogChanged => changeSequence > refreshedSequence;
    public string? MetadataWarning => session.MetadataWarning;
    public string? SecretMetadataError => session.Catalog.Secrets.Error;

    protected override void OnInitialized() {
        session = new(new AuthorizedReads(Reads, () => opening ?? throw new UnauthorizedAccessException("Open provider access before reading.")), Recovery);
        operations = new(session, Commands);
    }

    protected override async Task OnParametersSetAsync() {
        if (!RendererInfo.IsInteractive || disposed) {
            return;
        }
        if (!admissionAttempted) {
            admissionAttempted = true;
            opening = EditingChanges.Open(Guid.NewGuid(), () => !disposed && accessError is null);
            try {
                await opening.StartAsync(batch => InvokeAsync(() => ReceiveChanges(batch)), () => InvokeAsync(RetireAccess));
                Recovery.Bind(new(opening.Scope, opening.PrincipalStamp));
                admitted = true;
                if (!IsRouteControlled) {
                    await session.RefreshAsync();
                }
            } catch (Exception) {
                RetireAccess();
            }
        }
        if (admitted && IsRouteControlled) {
            await ApplyRouteAsync();
        }
    }

    private void ReceiveChanges(EditingChangeBatch batch) {
        if (!admitted || disposed) {
            return;
        }
        var peers = batch.Changes.Where(change => change.OriginEditorId != opening!.EditorId).ToArray();
        if (!batch.RequiresResync && peers.Length == 0) {
            return;
        }
        changeSequence++;
        if (State.ProviderId.HasValue && !session.IsSourceManaged &&
            (batch.RequiresResync || peers.Any(change => change.Resource.ResourceId == State.ProviderId &&
                change.Kind is not (EditingChangeKind.MetadataChanged or EditingChangeKind.LifecycleChanged)))) {
            session.RequireComparison();
        }
        StateHasChanged();
    }

    private void RetireAccess() {
        if (disposed) {
            return;
        }
        admitted = false;
        accessError = "Provider workspace access changed. Reopen it with current access; the local draft is retained.";
        session.MarkTargetUnavailable(accessError);
        StateHasChanged();
    }

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
        var sequence = changeSequence;
        await session.RefreshAsync();
        if (session.CatalogLoadState == ProviderProfilesLoadState.Ready && session.MetadataWarning is null) {
            refreshedSequence = sequence;
        }
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
        if (!admitted || opening is null || !IsCurrent(intent.Target) || intent.Target.ProviderId != State.ProviderId) {
            return;
        }
        if (intent.Action is ProviderEditorAction.Compare or ProviderEditorAction.ApplyComparison or ProviderEditorAction.CancelComparison or ProviderEditorAction.ReviewCurrent or ProviderEditorAction.ApplyCurrentReview) {
            try {
                if (intent.Action == ProviderEditorAction.Compare && !operations.IsBusy && !operations.IsWriteUnconfirmed) {
                    await session.CompareAsync(session.TargetCancellationToken);
                } else if (intent.Action == ProviderEditorAction.ApplyComparison) {
                    await opening.ReadAsync(_ => Task.FromResult(true), session.TargetCancellationToken);
                    session.ApplyComparison();
                } else if (intent.Action == ProviderEditorAction.CancelComparison) {
                    session.CancelComparison();
                } else if (intent.Action == ProviderEditorAction.ReviewCurrent) {
                    await operations.ReviewCurrentAsync();
                } else if (intent.Action == ProviderEditorAction.ApplyCurrentReview) {
                    await opening.ReadAsync(_ => Task.FromResult(true), session.TargetCancellationToken);
                    if (operations.ApplyCurrentReview() is { } recovered) {
                        NotificationService.Warning(recovered.Title, recovered.Message);
                    }
                }
            } catch (OperationCanceledException) when (session.TargetCancellationToken.IsCancellationRequested) {
            } catch (Exception) {
                if (IsCurrent(intent.Target)) {
                    NotificationService.Warning("Provider comparison unavailable", "The current provider could not be read. Your draft is retained.");
                }
            }
            await RenderCurrentAsync();
            return;
        }
        ProviderEditorFeedback? feedback;
        try {
            feedback = await opening.WriteAsync(() => intent.Action switch {
                ProviderEditorAction.Save => operations.SaveAsync(),
                ProviderEditorAction.Delete => operations.DeleteAsync(),
                ProviderEditorAction.Health => operations.CheckHealthAsync(),
                ProviderEditorAction.DiscoverModels => operations.DiscoverModelsAsync(),
                ProviderEditorAction.Reconcile => operations.RetryReconciliationAsync(),
                ProviderEditorAction.Verify => operations.VerifyUnconfirmedAsync(),
                ProviderEditorAction.RetryVerified => operations.RetryVerifiedAsync(),
                ProviderEditorAction.AcknowledgeDiagnostic => operations.AcknowledgeDiagnosticAsync(),
                _ => throw new ArgumentOutOfRangeException(nameof(intent))
            }, () => IsCurrent(intent.Target) && intent.Target.ProviderId == State.ProviderId, session.TargetCancellationToken);
        } catch (UnauthorizedAccessException) {
            RetireAccess();
            return;
        }
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
    public async ValueTask DisposeAsync() {
        disposed = true;
        session?.Dispose();
        if (opening is not null) {
            await opening.DisposeAsync();
        }
    }

    private sealed class AuthorizedReads(IProviderProfilesReads reads, Func<ProviderEditingOpening> original) : IProviderProfilesReads {
        public Task<ProviderProfilesCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default)
            => original().ReadAsync(reads.LoadCatalogAsync, cancellationToken);
        public Task<CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel> LoadEditorAsync(Guid id, CancellationToken cancellationToken = default)
            => original().ReadAsync(token => reads.LoadEditorAsync(id, token), cancellationToken);
    }
}
