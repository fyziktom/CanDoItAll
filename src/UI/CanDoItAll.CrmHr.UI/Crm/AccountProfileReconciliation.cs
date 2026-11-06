using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Crm;

public sealed class AccountProfileReconciliation {
    private CrmAccountProfileEditorModel? original;
    private CrmAccountProfileEditorModel? reviewLocal;
    private CrmAccountProfileEditorModel? beforeMerge;
    private readonly long[] revisions = new long[Enum.GetValues<AccountProfileField>().Length];
    private long[]? submittedRevisions;
    private long hintRevision;
    private long reviewHintRevision;
    private long inputRevision;
    private long reviewInputRevision;
    public AccountProfileReview? Review { get; private set; }
    public bool IsOutdated { get; private set; }
    public bool IsSaving { get; private set; }
    public CrmAccountProfileEditorModel? UnknownSubmission { get; private set; }
    public bool IsUnknown => UnknownSubmission is not null;
    public bool ReviewIsStale => Review is not null && reviewHintRevision != hintRevision;
    public bool CanUndo => beforeMerge is not null;
    public long InputRevision => inputRevision;
    public bool IsDirty(CrmAccountProfileEditorModel draft) => original is not null && !AccountProfileReview.SameValues(original, draft);

    public void InputChanged(AccountProfileField field) => revisions[(int)field] = ++inputRevision;

    public void Reset(CrmAccountProfileEditorModel draft) {
        original = draft.Snapshot();
        beforeMerge = null;
        reviewLocal = null;
        Review = null;
        IsOutdated = false;
        IsSaving = false;
        UnknownSubmission = null;
        submittedRevisions = null;
        Array.Clear(revisions);
        inputRevision = 0;
        hintRevision = 0;
    }

    public void Hint() {
        hintRevision++;
        IsOutdated = true;
    }

    public void Observe(CrmAccountProfileEditorModel draft, CrmAccountProfileEditorModel current) {
        if (original is null) {
            throw new InvalidOperationException("Open the account profile observation before refreshing it.");
        }
        if (current.Original == original.Original) {
            IsOutdated = false;
            return;
        }
        if (IsSaving || IsUnknown || Review is not null || IsDirty(draft)) {
            Hint();
            return;
        }
        AccountProfileReview.Apply(draft, current);
        original = current.Snapshot();
        IsOutdated = false;
    }

    public CrmAccountProfileEditorModel BeginSave(CrmAccountProfileEditorModel draft) {
        if (IsSaving || IsUnknown || IsOutdated || Review is not null) {
            throw new InvalidOperationException("Review the account profile before saving again.");
        }
        IsSaving = true;
        submittedRevisions = (long[])revisions.Clone();
        return draft.Snapshot();
    }

    public void Accepted(CrmAccountProfileEditorModel draft, CrmAccountProfileEditorModel submitted, CrmAccountProfileSaveReceipt receipt) {
        var a = receipt.Accepted;
        var values = a.Snapshot();
        values.RelationshipStage = Later(AccountProfileField.RelationshipStage, submitted.RelationshipStage, draft.RelationshipStage, a.RelationshipStage);
        values.CommercialNotes = Later(AccountProfileField.CommercialNotes, submitted.CommercialNotes, draft.CommercialNotes, a.CommercialNotes);
        values.ConstraintNotes = Later(AccountProfileField.ConstraintNotes, submitted.ConstraintNotes, draft.ConstraintNotes, a.ConstraintNotes);
        values.TimingRiskNotes = Later(AccountProfileField.TimingRiskNotes, submitted.TimingRiskNotes, draft.TimingRiskNotes, a.TimingRiskNotes);
        AccountProfileReview.Apply(draft, values);
        original = a.Snapshot();
        IsSaving = false;
        submittedRevisions = null;
        beforeMerge = null;

        T Later<T>(AccountProfileField field, T s, T l, T accepted) =>
            submittedRevisions?[(int)field] != revisions[(int)field] || !EqualityComparer<T>.Default.Equals(s, l) ? l : accepted;
    }

    public void Rejected(bool conflict) {
        IsSaving = false;
        submittedRevisions = null;
        if (conflict) {
            Hint();
        }
    }

    public void Unknown(CrmAccountProfileEditorModel submitted) {
        IsSaving = false;
        UnknownSubmission = submitted.Snapshot();
        Hint();
    }

    public long BeginReview() => hintRevision;
    public void Compare(CrmAccountProfileEditorModel draft, CrmAccountProfileEditorModel current, long observedHint) {
        var local = Review?.Candidate() ?? draft.Snapshot();
        reviewLocal = draft.Snapshot();
        Review = new(original ?? throw new InvalidOperationException("The account profile original is unavailable."), local, current);
        reviewHintRevision = observedHint;
        reviewInputRevision = inputRevision;
    }

    public bool Apply(CrmAccountProfileEditorModel draft) {
        if (IsSaving || IsUnknown || Review?.CanApply != true || ReviewIsStale || reviewInputRevision != inputRevision ||
            reviewLocal is null || !AccountProfileReview.SameValues(reviewLocal, draft)) {
            return false;
        }
        beforeMerge = draft.Snapshot();
        AccountProfileReview.Apply(draft, Review.Candidate());
        original = Review.Current.Snapshot();
        CancelReview();
        IsOutdated = false;
        return true;
    }

    public void CancelReview() {
        Review = null;
        reviewLocal = null;
    }

    public void Undo(CrmAccountProfileEditorModel draft) {
        if (IsSaving || IsUnknown || beforeMerge is null || original is null) {
            return;
        }
        beforeMerge.Original = original.Original;
        beforeMerge.Id = original.Id;
        AccountProfileReview.Apply(draft, beforeMerge);
        beforeMerge = null;
    }
}
