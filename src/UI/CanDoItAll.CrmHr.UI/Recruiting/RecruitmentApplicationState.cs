using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Recruiting;

public sealed class RecruitmentApplicationState {
    private readonly RecordDraftState<RecruitmentApplicationEditorModel, RecruitmentApplicationField> state =
        new(model => model.Snapshot(), RecruitmentDraftValues.Same, RecruitmentDraftValues.Copy);
    private RecruitmentApplicationEditorModel basis = new();
    private RecruitmentApplicationEditorModel? reviewLocal;
    private (RecruitmentApplicationEditorModel Basis, RecruitmentApplicationEditorModel Local)? undo;
    private long submittedHint;
    private long reviewHint;
    private long reviewInput;
    public RecruitmentApplicationState() => Open(new());
    public RecruitmentApplicationEditorModel Draft => state.Draft;
    public long Opening => state.Opening;
    public long InputRevision => state.InputRevision;
    public long HintRevision { get; private set; }
    public bool IsDirty => state.IsDirty;
    public bool IsSaving => state.IsSaving;
    public bool IsUnknown => state.IsUnknown;
    public RecordSubmission<RecruitmentApplicationEditorModel, RecruitmentApplicationField>? UnknownSubmission => state.UnknownSubmission;
    public RecruitmentApplicationEditorModel? Accepted => state.Accepted;
    public bool HasPendingInput => state.HasPendingInput;
    public bool IsOutdated { get; private set; }
    public bool Missing { get; private set; }
    public bool CandidateInputRetained { get; private set; }
    public RecruitmentApplicationReview? Review { get; private set; }
    public bool ReviewIsStale => Review is not null && reviewHint != HintRevision;
    public bool CanSave => state.CanSave && !Missing && !IsOutdated && Review is null && !CandidateInputRetained;
    public bool CanApply => state.CanSave && !Missing && !CandidateInputRetained && !ReviewIsStale &&
        Review?.CanApply == true && reviewInput == InputRevision && reviewLocal is not null && RecruitmentDraftValues.Same(reviewLocal, Draft);
    public bool CanUndo => undo is not null && state.CanSave && !Missing && Review is null;
    public IReadOnlyList<Guid> ReferenceIds => new Guid?[] {
        basis.TargetUnitPartyId, basis.RecruiterPartyId, basis.HiringManagerPartyId,
        Draft.TargetUnitPartyId, Draft.RecruiterPartyId, Draft.HiringManagerPartyId,
        Review?.Current.TargetUnitPartyId, Review?.Current.RecruiterPartyId, Review?.Current.HiringManagerPartyId
    }.OfType<Guid>().Distinct().ToArray();

    public void Open(RecruitmentApplicationEditorModel model) {
        state.Open(model);
        basis = model.Snapshot();
        Review = null;
        reviewLocal = null;
        undo = null;
        IsOutdated = Missing = CandidateInputRetained = false;
        HintRevision = submittedHint = reviewHint = reviewInput = 0;
    }

    public void Input(RecordInput<RecruitmentApplicationField> input) => state.Input(input);
    public void Hint() {
        HintRevision++;
        IsOutdated = true;
    }
    public void Unavailable() {
        Missing = true;
        Hint();
    }

    public void Observe(RecruitmentApplicationEditorModel current, long readHint, bool explicitPull) {
        if (!SameIdentity(current)) {
            Unavailable();
            return;
        }
        if (IsOlderThanBasis(current)) {
            Hint();
            return;
        }
        if (current.Original == basis.Original) {
            IsOutdated = IsUnknown || readHint != HintRevision;
            return;
        }
        if (!explicitPull || IsSaving || IsUnknown || IsDirty || Review is not null) {
            IsOutdated = true;
            return;
        }
        RecruitmentDraftValues.Copy(Draft, current);
        basis = current.Snapshot();
        state.Rebase(current);
        undo = null;
        IsOutdated = readHint != HintRevision;
    }

    public RecordSubmission<RecruitmentApplicationEditorModel, RecruitmentApplicationField>? Begin() {
        if (!CanSave) {
            return null;
        }
        submittedHint = HintRevision;
        return state.Begin();
    }
    public bool IsCurrent(RecordSubmission<RecruitmentApplicationEditorModel, RecruitmentApplicationField> submission) => state.IsCurrent(submission);

    public void Accept(RecordSubmission<RecruitmentApplicationEditorModel, RecruitmentApplicationField> submission,
        RecruitmentSaveReceipt<RecruitmentApplicationEditorModel> receipt) {
        if (!IsCurrent(submission)) {
            return;
        }
        if (receipt.Id != receipt.Accepted.Id || receipt.Accepted.Original?.Id != receipt.Id ||
            receipt.Accepted.Original.PartyId != receipt.Accepted.PartyId || submission.Model.Id is { } id && id != receipt.Id ||
            submission.Model.PartyId is { } partyId && partyId != receipt.Accepted.PartyId) {
            throw new InvalidOperationException("The receipt does not identify the accepted recruitment application.");
        }
        var local = Draft.Snapshot();
        var accepted = receipt.Accepted.Snapshot();
        if (submission.Model.Id.HasValue) {
            CopyCandidateDetails(accepted, local);
        }
        state.Accept(submission, accepted, () => {
            RecruitmentDraftValues.Copy(Draft, accepted);
            foreach (var field in Enum.GetValues<RecruitmentApplicationField>()) {
                if (state.Changed(submission, field) || RecruitmentDraftValues.Changed(submission.Model, local, field)) {
                    RecruitmentDraftValues.CopyField(Draft, local, field);
                    CandidateInputRetained |= !submission.Model.Id.HasValue && IsCandidateField(field);
                }
            }
        });
        basis = accepted;
        IsOutdated = HintRevision != submittedHint;
        Review = null;
        undo = null;
    }

    public void Unknown(RecordSubmission<RecruitmentApplicationEditorModel, RecruitmentApplicationField> submission) {
        state.Unknown(submission);
        if (state.IsUnknown) {
            Hint();
        }
    }
    public void Complete(RecordSubmission<RecruitmentApplicationEditorModel, RecruitmentApplicationField> submission) => state.Complete(submission);

    public void Compare(RecruitmentApplicationEditorModel current, long readHint) {
        if (!SameIdentity(current)) {
            Unavailable();
            return;
        }
        if (IsOlderThanBasis(current)) {
            Hint();
            return;
        }
        var review = new RecruitmentApplicationReview(basis, Draft, current);
        if (Review is { } previous) {
            review.PreserveChoices(previous);
        }
        Review = review;
        reviewHint = readHint;
        reviewInput = InputRevision;
        reviewLocal = Draft.Snapshot();
    }
    public bool Apply() {
        if (!CanApply || Review is not { } review) {
            return false;
        }
        undo = (basis.Snapshot(), Draft.Snapshot());
        var candidate = review.Candidate(Draft);
        RecruitmentDraftValues.Copy(Draft, candidate);
        basis = review.Current.Snapshot();
        state.Rebase(basis);
        IsOutdated = false;
        Review = null;
        return true;
    }
    public void Undo() {
        if (!CanUndo || undo is not { } before) {
            return;
        }
        basis = before.Basis;
        RecruitmentDraftValues.Copy(Draft, before.Local);
        state.Rebase(basis);
        undo = null;
        Hint();
    }
    public void CancelReview() => Review = null;
    public void DiscardLocal() {
        if (IsSaving || IsUnknown) {
            return;
        }
        state.Open(basis);
        Review = null;
        reviewLocal = null;
        undo = null;
        CandidateInputRetained = false;
    }
    public void UseSavedCandidateDetails() {
        CopyCandidateDetails(Draft, basis);
        CandidateInputRetained = false;
    }
    private bool SameIdentity(RecruitmentApplicationEditorModel model) => model.Id == Draft.Id && model.PartyId == basis.PartyId;
    public bool IsOlderThanBasis(RecruitmentApplicationEditorModel current) => SameIdentity(current) &&
        current.Original is { } observed && basis.Original is { } original && observed.UpdatedAtUtc < original.UpdatedAtUtc;
    private static bool IsCandidateField(RecruitmentApplicationField field) => field is RecruitmentApplicationField.CandidateParty or
        RecruitmentApplicationField.CandidateName or RecruitmentApplicationField.CandidateEmail or RecruitmentApplicationField.CandidatePhone or RecruitmentApplicationField.CandidateSummary;
    private static void CopyCandidateDetails(RecruitmentApplicationEditorModel target, RecruitmentApplicationEditorModel source) {
        target.PartyId = source.PartyId;
        target.CandidateName = source.CandidateName;
        target.CandidateEmail = source.CandidateEmail;
        target.CandidatePhone = source.CandidatePhone;
        target.CandidateSummary = source.CandidateSummary;
    }
}
