using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Crm;

public sealed class OpportunityConversionState {
    private CrmOpportunityConversionEditorModel original = new();
    private long acceptedInputRevision;
    private Guid? operationId;
    public CrmOpportunityConversionEditorModel Draft { get; private set; } = new();
    public long Opening { get; private set; }
    public long InputRevision { get; private set; }
    public bool IsSaving { get; private set; }
    public bool IsUnknown { get; private set; }
    public bool PickerPending { get; private set; }
    public Guid EditorId { get; private set; }
    public long HintRevision { get; private set; }
    public bool IsOutdated { get; private set; }
    public bool ReviewStale { get; private set; }
    public CrmOpportunityDetailModel? Basis { get; private set; }
    public OpportunityConversionReview? Review { get; private set; }
    public CrmOpportunityConversionResult? Accepted { get; private set; }
    public bool CanSubmit => !IsSaving && !IsUnknown && !PickerPending && !IsOutdated && Review is null && Accepted is null &&
        (!Draft.LinkExistingProject || Draft.ExpectedProjectBasis is not null);
    public bool HasChanges => !SameValues(original, Draft) || Accepted is not null && InputRevision != acceptedInputRevision;

    public void Open(CrmOpportunityConversionEditorModel model, CrmOpportunityDetailModel? basis = null) {
        Opening++;
        Draft = model.Snapshot();
        original = Draft.Snapshot();
        InputRevision = 0;
        IsSaving = false;
        IsUnknown = false;
        PickerPending = false;
        Accepted = null;
        EditorId = Guid.NewGuid();
        operationId = null;
        HintRevision = 0;
        IsOutdated = false;
        ReviewStale = false;
        Review = null;
        Basis = basis;
    }

    public void Clear() => Open(new());

    public void Input(long opening) {
        if (opening == Opening) {
            InputRevision++;
            ReviewStale |= Review is not null;
        }
    }

    public void Picker(long opening, bool pending) {
        if (opening == Opening) {
            PickerPending = pending;
        }
    }

    public OpportunityConversionSubmission? Begin(CrmOpportunityConversionEditorModel model) {
        if (!CanSubmit) {
            return null;
        }
        IsSaving = true;
        operationId = Guid.NewGuid();
        return new(Opening, InputRevision, operationId.Value, model.Snapshot());
    }

    public bool IsCurrent(OpportunityConversionSubmission submission) => submission.Opening == Opening;

    public bool HasLaterInput(OpportunityConversionSubmission submission) =>
        InputRevision != submission.InputRevision || !SameValues(submission.Model, Draft);

    public void Accept(OpportunityConversionSubmission submission, CrmOpportunityConversionResult result) {
        if (!IsCurrent(submission)) {
            return;
        }
        if (result.OpportunityId != submission.Model.OpportunityId || result.ProjectId == Guid.Empty) {
            IsUnknown = true;
            throw new InvalidOperationException("The conversion receipt does not identify its submitted opportunity and project.");
        }
        Accepted = result;
        original = submission.Model.Snapshot();
        acceptedInputRevision = submission.InputRevision;
    }

    public void Fail(OpportunityConversionSubmission submission) {
        if (IsCurrent(submission) && Accepted is null) {
            IsUnknown = true;
        }
    }

    public void Complete(OpportunityConversionSubmission submission) {
        if (IsCurrent(submission)) {
            IsSaving = false;
        }
    }

    public bool IsOwn(Guid? editor, Guid? operation) => editor == EditorId && operation.HasValue && operation == operationId;

    public void Hint() {
        HintRevision++;
        IsOutdated = true;
        ReviewStale |= Review is not null;
    }

    public void SetReview(CrmOpportunityDetailModel current, CrmOpportunityConversionProjectBasis? project,
        bool targetUnavailable, long hintRevision, long inputRevision) {
        if (Basis is null || current.Id != Draft.OpportunityId) {
            throw new InvalidOperationException("The current conversion basis belongs to another opportunity.");
        }
        Review = new(Basis, current, Draft.LinkExistingProject ? Draft.ExpectedProjectBasis : null, project, targetUnavailable);
        ReviewStale = HintRevision != hintRevision || InputRevision != inputRevision;
    }

    public bool CanApplyReview => Review is { Current.Stage: OpportunityStage.Won } && !ReviewStale && !IsSaving && !IsUnknown && Accepted is null;

    public bool ApplyReview() {
        if (!CanApplyReview || Review is not { } review) {
            return false;
        }
        Basis = review.Current;
        Draft.ExpectedUpdatedAtUtc = review.Current.UpdatedAtUtc;
        Draft.ExpectedProjectBasis = review.CurrentProject;
        Review = null;
        ReviewStale = false;
        IsOutdated = false;
        InputRevision++;
        return true;
    }

    public void CancelReview() {
        Review = null;
        ReviewStale = false;
    }

    public void InvalidateReview() => ReviewStale = Review is not null;

    private static bool SameValues(CrmOpportunityConversionEditorModel left, CrmOpportunityConversionEditorModel right) =>
        left.OpportunityId == right.OpportunityId && left.ExpectedUpdatedAtUtc == right.ExpectedUpdatedAtUtc &&
        left.LinkExistingProject == right.LinkExistingProject && left.ExistingProjectId == right.ExistingProjectId &&
        left.ExpectedProjectAdmission == right.ExpectedProjectAdmission && left.ProjectName == right.ProjectName &&
        left.ProjectDescription == right.ProjectDescription && left.ProjectObjective == right.ProjectObjective &&
        left.CurrentPhase == right.CurrentPhase;
}

public sealed record OpportunityConversionSubmission(long Opening, long InputRevision, Guid OperationId, CrmOpportunityConversionEditorModel Model);

public sealed record OpportunityConversionReview(CrmOpportunityDetailModel Original, CrmOpportunityDetailModel Current,
    CrmOpportunityConversionProjectBasis? OriginalProject, CrmOpportunityConversionProjectBasis? CurrentProject, bool TargetUnavailable);
