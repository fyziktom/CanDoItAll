using System.Collections.Frozen;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.AgentFramework.SharedProviders.UI;

public enum SharedProviderSourceAvailability { NeverSynchronized, Available, SourceOffline, AuthorizationFailed, SourceIdentityMismatch, IncompatibleContract }
public enum SharedProviderSourceAction { Edit, Test, Discover, Synchronize, ToggleEnabled, Delete }
public sealed record SharedProviderSourceOrigin(Guid ViewId, Guid SnapshotId, Guid SourceId, Guid ConcurrencyToken);
public sealed record SharedProviderSourceIntent(SharedProviderSourceOrigin Origin, SharedProviderSourceAction Action);
public sealed record SharedProviderCredentialChoice(Guid Id, string Name);
public sealed record SharedProviderSourcePresentation(SharedProviderSourceOrigin Origin, string Name, Uri BaseUri,
    Guid CredentialReference, bool IsEnabled, bool AllowPrivateNetwork, SharedProviderSourceAvailability Status,
    string StatusMessage, int ImportCount);
public sealed record SharedProviderSourceRecoveryPresentation(Guid AttemptId, Guid SourceId, bool DeliveryPending, bool RetryAllowed);
public sealed record SharedProviderSourceConfirmation(Guid Id, SharedProviderSourceOrigin Origin, string SourceName);
public sealed record SharedProviderSourcesPresentation(Guid ViewId, IReadOnlyList<SharedProviderSourcePresentation> Sources,
    IReadOnlyList<SharedProviderCredentialChoice> Credentials, string? MetadataError,
    bool Loading, bool OperationBusy, bool WritesBlocked, string Error, SharedProviderSourceDraft? Editor,
    string EditorError, SharedProviderCatalogSelection? Catalog, SharedProviderSourceRecoveryPresentation? Recovery,
    SharedProviderSourceConfirmation? Confirmation);

public sealed record SharedProviderCatalogSubmission(Guid DialogId, SharedProviderSourceOrigin Origin,
    IReadOnlySet<SharedProviderPublicationId> Selection);

public sealed class SharedProviderCatalogSelection(SharedProviderSourceOrigin origin, string sourceName,
    IReadOnlyList<SharedProviderCatalogPublication> publications, IEnumerable<SharedProviderPublicationId> selected) {
    private readonly HashSet<SharedProviderPublicationId> selection = selected.ToHashSet();
    public Guid Id { get; } = Guid.NewGuid();
    public SharedProviderSourceOrigin Origin { get; private set; } = origin;
    public string SourceName { get; } = sourceName;
    public IReadOnlyList<SharedProviderCatalogPublication> Publications { get; } = publications;
    public int SelectedCount => selection.Count;
    public bool ReadbackConflict { get; set; }
    public bool IsSelected(SharedProviderPublicationId id) => selection.Contains(id);
    public void Select(SharedProviderPublicationId id, bool selected) {
        if (!Publications.Any(publication => publication.PublicationId == id)) {
            return;
        }
        if (selected) {
            selection.Add(id);
        } else {
            selection.Remove(id);
        }
    }
    public SharedProviderCatalogSubmission Capture() => new(Id, Origin, selection.ToFrozenSet());
    public bool IsCurrent(SharedProviderCatalogSubmission submission) => !ReadbackConflict && submission.DialogId == Id &&
        submission.Origin == Origin && selection.SetEquals(submission.Selection);
    public bool Accept(SharedProviderCatalogSubmission submission, SharedProviderSourceOrigin accepted) {
        if (submission.DialogId != Id || submission.Origin != Origin || accepted.SourceId != Origin.SourceId) {
            throw new ArgumentException("The catalog result belongs to another dialog.", nameof(submission));
        }
        var unchanged = selection.SetEquals(submission.Selection);
        Origin = accepted;
        return unchanged;
    }
}

public interface ISharedProviderSourcesView {
    SharedProviderSourcesPresentation Presentation { get; }
    Task CloseAsync(Guid viewId);
    Task RefreshAsync(Guid viewId);
    void NewSource(Guid viewId);
    void CloseEditor(Guid draftId);
    Task SaveAsync(SharedProviderSourceSubmission submission);
    Task ExecuteAsync(SharedProviderSourceIntent intent);
    Task VerifyAsync(Guid attemptId);
    Task RetryVerifiedAsync(Guid attemptId);
    void CloseCatalog(Guid dialogId);
    Task ApplyCatalogAsync(SharedProviderCatalogSubmission submission);
    Task ConfirmDeleteAsync(SharedProviderSourceConfirmation confirmation);
    void CloseConfirmation(SharedProviderSourceConfirmation confirmation);
}
