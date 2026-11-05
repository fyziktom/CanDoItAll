using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Parties;

public sealed class PartyEditorReconciliation {
    private PartyEditorModel? original;
    private PartyEditorModel? beforeMerge;
    private PartyEditorModel? reviewLocal;
    private long reviewRevision;
    private long reviewInputRevision;
    private long hintRevision;
    private readonly Dictionary<PartyEditorFieldKey, long> inputRevisions = [];
    private readonly HashSet<PartyEditorFieldKey> pendingDates = [];
    private Dictionary<PartyEditorFieldKey, long>? submittedRevisions;
    private long inputRevision;
    public string PendingTagInput { get; private set; } = string.Empty;
    public bool HasValidationErrors { get; private set; }
    public bool HasPendingDateInput => pendingDates.Count > 0;
    public bool HasPendingChildInput { get; private set; }
    public PartyMergeReview? Review { get; private set; }
    public bool IsOutdated { get; private set; }
    public bool IsSaving { get; private set; }
    public bool IsUnknown { get; private set; }
    public PartyEditorModel? UnknownSubmission { get; private set; }
    public bool ReviewIsStale => Review is not null && reviewRevision != hintRevision;
    public bool CanUndo => beforeMerge is not null;

    public static PartyEditorModel Capture(PartyEditorViewModel draft) {
        draft.EnsureChildIdentities();
        return draft.ToEditorModel();
    }

    public bool IsDirty(PartyEditorViewModel draft) => HasValidationErrors || HasPendingDateInput || HasPendingChildInput || PendingTagInput.Length > 0 ||
        original is not null && !PartyMergeReview.SameValues(original, Capture(draft));

    public void InputChanged(PartyEditorInputChange change) {
        inputRevisions[change.Key] = ++inputRevision;
        if (change.PendingDateInput) {
            pendingDates.Add(change.Key);
        }
    }

    public void SetPendingTagInput(string value) {
        PendingTagInput = value;
        InputChanged(new(new(PartyEditableField.Tags)));
    }

    public void ReportValidation(bool hasErrors) => HasValidationErrors = hasErrors;
    public void ReportChildInput(bool pending) => HasPendingChildInput = pending;

    public void ConfirmParsedInput() {
        pendingDates.Clear();
        HasValidationErrors = false;
    }

    public void Reset(PartyEditorViewModel draft, PartyEditorModel? owner = null) {
        original = owner?.Snapshot() ?? Capture(draft);
        beforeMerge = null;
        Review = null;
        reviewLocal = null;
        hintRevision = 0;
        IsOutdated = false;
        IsSaving = false;
        IsUnknown = false;
        UnknownSubmission = null;
        inputRevisions.Clear();
        pendingDates.Clear();
        submittedRevisions = null;
        PendingTagInput = string.Empty;
        HasValidationErrors = false;
        HasPendingChildInput = false;
    }

    public void Hint() {
        hintRevision++;
        IsOutdated = true;
    }

    public void Observe(PartyEditorViewModel draft, PartyEditorModel owner) {
        if (original is null) {
            throw new InvalidOperationException("Open the party observation before refreshing it.");
        }
        if (owner.UpdatedAtUtc == original.UpdatedAtUtc) {
            IsOutdated = false;
            return;
        }
        if (IsSaving || IsDirty(draft) || Review is not null || IsUnknown) {
            Hint();
            return;
        }
        draft.Apply(owner);
        original = owner.Snapshot();
        IsOutdated = false;
    }

    public PartyEditorModel BeginSave(PartyEditorViewModel draft) {
        if (IsSaving || IsUnknown || IsOutdated || HasValidationErrors || HasPendingDateInput || HasPendingChildInput || Review is not null) {
            throw new InvalidOperationException("Review or observe the party before saving again.");
        }
        IsSaving = true;
        submittedRevisions = new(inputRevisions);
        return Capture(draft);
    }

    public void Rejected(bool conflict) {
        IsSaving = false;
        submittedRevisions = null;
        if (conflict) {
            Hint();
        }
    }

    public void Unknown(PartyEditorModel submitted) {
        IsSaving = false;
        IsUnknown = true;
        IsOutdated = true;
        UnknownSubmission = submitted.Snapshot();
    }

    public void Accepted(PartyEditorViewModel draft, PartyEditorModel submitted, PartySaveReceipt receipt) {
        var later = inputRevisions.Where(pair => submittedRevisions?.GetValueOrDefault(pair.Key) != pair.Value).Select(pair => pair.Key).ToHashSet();
        draft.Apply(PartyEditorValues.Rebase(submitted, Capture(draft), receipt.Accepted, later));
        original = receipt.Accepted.Snapshot();
        IsSaving = false;
        IsUnknown = false;
        UnknownSubmission = null;
        beforeMerge = null;
        Review = null;
        reviewLocal = null;
        submittedRevisions = null;
    }

    public long BeginReview() => hintRevision;

    public void Compare(PartyEditorViewModel draft, PartyEditorModel current, long observedRevision) {
        var local = Review?.CaptureCandidate() ?? Capture(draft);
        reviewLocal = Capture(draft);
        Review = new(original ?? throw new InvalidOperationException("The party original is unavailable."), local, current);
        reviewRevision = observedRevision;
        reviewInputRevision = inputRevision;
    }

    public bool Apply(PartyEditorViewModel draft) {
        if (IsSaving || IsUnknown || HasValidationErrors || HasPendingDateInput || HasPendingChildInput || Review is null || !Review.CanApply || ReviewIsStale || reviewLocal is null || reviewInputRevision != inputRevision ||
            !PartyMergeReview.SameValues(reviewLocal, Capture(draft))) {
            return false;
        }
        beforeMerge = Capture(draft);
        draft.Apply(Review.Result());
        original = Review.Current.Snapshot();
        Review = null;
        reviewLocal = null;
        IsOutdated = false;
        return true;
    }

    public void CancelReview() {
        Review = null;
        reviewLocal = null;
    }

    public void Undo(PartyEditorViewModel draft) {
        if (IsSaving || IsUnknown || beforeMerge is null || original is null) {
            return;
        }
        var values = beforeMerge;
        values.UpdatedAtUtc = original.UpdatedAtUtc;
        draft.Apply(values);
        beforeMerge = null;
    }
}
