using System.Collections.Frozen;
using CanDoItAll.Editing;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.AgentFramework.SharedProviders.UI;

public enum SharedProviderSourceAvailability { NeverSynchronized, Available, SourceOffline, AuthorizationFailed, SourceIdentityMismatch, IncompatibleContract }
public enum SharedProviderSourceAction { Edit, Test, Discover, Synchronize, ToggleEnabled, Delete }
public sealed record SharedProviderSourceOrigin(Guid ViewId, Guid SnapshotId, Guid SourceId, Guid ConcurrencyToken) {
    public SharedProviderSourceObservation? Observation { get; init; }
}
public sealed record SharedProviderSourceIntent(SharedProviderSourceOrigin Origin, SharedProviderSourceAction Action);
public sealed record SharedProviderCredentialChoice(Guid Id, string Name);
public sealed record SharedProviderSourcePresentation(SharedProviderSourceOrigin Origin, string Name, Uri BaseUri,
    Guid CredentialReference, bool IsEnabled, bool AllowPrivateNetwork, SharedProviderSourceAvailability Status,
    string StatusMessage, int ImportCount);
public sealed record SharedProviderSourceRecoveryPresentation(Guid AttemptId, Guid SourceId, bool DeliveryPending, bool CanAcknowledge,
    bool CanReview = false, bool CanReviewCatalog = false);
public sealed record SharedProviderSourceConfirmation(Guid Id, SharedProviderSourceOrigin Origin, string SourceName);
public sealed record SharedProviderSourcesPresentation(Guid ViewId, IReadOnlyList<SharedProviderSourcePresentation> Sources,
    IReadOnlyList<SharedProviderCredentialChoice> Credentials, string? MetadataError,
    bool Loading, bool OperationBusy, bool WritesBlocked, string Error, SharedProviderSourceDraft? Editor,
    string EditorError, SharedProviderCatalogSelection? Catalog, SharedProviderSourceRecoveryPresentation? Recovery,
    SharedProviderSourceConfirmation? Confirmation) {
    public bool CatalogChanged { get; init; }
}

public sealed record SharedProviderCatalogSubmission(Guid DialogId, SharedProviderSourceOrigin Origin,
    IReadOnlySet<SharedProviderPublicationId> Selection) {
    public long InputRevision { get; init; }
}

public sealed class SharedProviderCatalogComparison {
    public SharedProviderSourceOrigin Current { get; }
    public MergeField<FrozenSet<SharedProviderPublicationId>> Selection { get; }
    public bool IsStale { get; private set; }
    public bool CanApply => !IsStale && Selection.IsResolved;
    public SharedProviderCatalogComparison(SharedProviderSourceOrigin current, IEnumerable<SharedProviderPublicationId> original,
        IEnumerable<SharedProviderPublicationId> local, IEnumerable<SharedProviderPublicationId> saved) {
        Current = current;
        var equality = EqualityComparer<FrozenSet<SharedProviderPublicationId>>.Create(
            (left, right) => ReferenceEquals(left, right) || left is not null && right is not null && left.SetEquals(right),
            values => values.Aggregate(0, (hash, id) => hash ^ id.GetHashCode()));
        Selection = new(original.ToFrozenSet(), local.ToFrozenSet(), saved.ToFrozenSet(), equality);
        Selection.RequireChoice();
    }
    public void Invalidate() => IsStale = true;
}

public sealed class SharedProviderCatalogSelection(SharedProviderSourceOrigin origin, string sourceName,
    IReadOnlyList<SharedProviderCatalogPublication> publications, IEnumerable<SharedProviderPublicationId> selected) {
    private readonly HashSet<SharedProviderPublicationId> selection = selected.ToHashSet();
    private FrozenSet<SharedProviderPublicationId> originalSelection = selected.ToFrozenSet();
    private long inputRevision;
    public Guid Id { get; } = Guid.NewGuid();
    public SharedProviderSourceOrigin Origin { get; private set; } = origin;
    public string SourceName { get; } = sourceName;
    public IReadOnlyList<SharedProviderCatalogPublication> Publications { get; } = publications;
    public int SelectedCount => selection.Count;
    public bool ReadbackConflict { get; set; }
    public SharedProviderCatalogComparison? Comparison { get; private set; }
    public bool IsSelected(SharedProviderPublicationId id) => selection.Contains(id);
    public void Select(SharedProviderPublicationId id, bool selected) {
        if (!Publications.Any(publication => publication.PublicationId == id)) {
            return;
        }
        inputRevision++;
        Comparison?.Invalidate();
        if (selected) {
            selection.Add(id);
        } else {
            selection.Remove(id);
        }
    }
    public SharedProviderCatalogSubmission Capture() => new(Id, Origin, selection.ToFrozenSet()) { InputRevision = inputRevision };
    public bool IsCurrent(SharedProviderCatalogSubmission submission) => !ReadbackConflict && Comparison is null && submission.DialogId == Id &&
        submission.Origin == Origin && submission.InputRevision == inputRevision && selection.SetEquals(submission.Selection);
    public bool Accept(SharedProviderCatalogSubmission submission, SharedProviderSourceOrigin accepted) {
        if (submission.DialogId != Id || submission.Origin != Origin || accepted.SourceId != Origin.SourceId) {
            throw new ArgumentException("The catalog result belongs to another dialog.", nameof(submission));
        }
        var unchanged = submission.InputRevision == inputRevision && selection.SetEquals(submission.Selection);
        Origin = accepted;
        originalSelection = submission.Selection.ToFrozenSet();
        ReadbackConflict = false;
        Comparison = null;
        return unchanged;
    }

    public void Observe(SharedProviderSourceOrigin current) {
        if (current.SourceId != Origin.SourceId) {
            throw new ArgumentException("The catalog observation belongs to another source.", nameof(current));
        }
        if (!SameVersion(Origin, current)) {
            ReadbackConflict = true;
        }
        if (Comparison is { } review && !SameVersion(review.Current, current)) {
            review.Invalidate();
        }
    }

    private static bool SameVersion(SharedProviderSourceOrigin left, SharedProviderSourceOrigin right) =>
        left.ConcurrencyToken == right.ConcurrencyToken && left.Observation is { } original && right.Observation is { } current &&
        original.Matches(current.ConcurrencyToken, current.ImportVersions);

    public void Compare(SharedProviderSourceOrigin current, IEnumerable<SharedProviderPublicationId> saved) =>
        Comparison = new(current, originalSelection, selection, saved);

    public bool ApplyComparison() {
        if (Comparison is not { CanApply: true } review) {
            return false;
        }
        selection.Clear();
        selection.UnionWith(review.Selection.Result);
        originalSelection = review.Selection.Current;
        Origin = review.Current;
        inputRevision++;
        ReadbackConflict = false;
        Comparison = null;
        return true;
    }

    public void CancelComparison() => Comparison = null;

    public string DescribeSelection(FrozenSet<SharedProviderPublicationId> values) => values.Count == 0 ? "No providers selected"
        : string.Join(Environment.NewLine, values.OrderBy(id => id.Value).Select(id =>
            Publications.SingleOrDefault(item => item.PublicationId == id)?.DisplayName ?? $"Provider {id.Value:D} unavailable in this discovered catalog"));
}

public interface ISharedProviderSourcesView {
    SharedProviderSourcesPresentation Presentation { get; }
    Task CloseAsync(Guid viewId);
    Task RefreshAsync(Guid viewId);
    void NewSource(Guid viewId);
    void CloseEditor(Guid draftId);
    Task SaveAsync(SharedProviderSourceSubmission submission);
    Task CompareSourceAsync(Guid draftId);
    Task ApplySourceComparisonAsync(Guid draftId);
    void CancelSourceComparison(Guid draftId);
    Task ExecuteAsync(SharedProviderSourceIntent intent);
    Task VerifyAsync(Guid attemptId);
    Task ReviewPendingSourceAsync(Guid attemptId);
    Task ReviewPendingCatalogAsync(Guid attemptId);
    Task AcknowledgeCurrentAsync(Guid attemptId);
    void CloseCatalog(Guid dialogId);
    Task ApplyCatalogAsync(SharedProviderCatalogSubmission submission);
    Task CompareCatalogAsync(Guid dialogId);
    Task ApplyCatalogComparisonAsync(Guid dialogId);
    void CancelCatalogComparison(Guid dialogId);
    Task ConfirmDeleteAsync(SharedProviderSourceConfirmation confirmation);
    void CloseConfirmation(SharedProviderSourceConfirmation confirmation);
}
