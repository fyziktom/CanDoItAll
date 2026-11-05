using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.CrmHr.UI.Crm;

public sealed class OpportunityReconciliation {
    private CrmOpportunityEditorModel original = new();
    private CrmOpportunityEditorModel? beforeMerge;
    private CrmOpportunityEditorModel? reviewDraft;
    private readonly long[] inputs = new long[Enum.GetValues<OpportunityField>().Length];
    private long[]? submittedInputs;
    private readonly HashSet<OpportunityField> pending = [];
    private long hintRevision;
    private long reviewHint;
    private long reviewInput;
    public CrmOpportunityEditorModel Draft { get; private set; } = new();
    public OpportunityReview? Review { get; private set; }
    public CrmOpportunityEditorModel? UnknownSubmission { get; private set; }
    public bool IsUnknown => UnknownSubmission is not null;
    public bool IsSaving { get; private set; }
    public bool IsOutdated { get; private set; }
    public bool Missing { get; private set; }
    public bool HasPendingPicker { get; private set; }
    public bool HasPendingInput => pending.Count > 0 || HasPendingPicker;
    public bool IsDirty => HasPendingInput || !OpportunityValues.Same(original, Draft);
    public bool ReviewIsStale => Review is not null && reviewHint != hintRevision;
    public bool CanUndo {
        get {
            if (beforeMerge is null || OpportunityValues.ProjectWasReplaced(beforeMerge, original)) {
                return false;
            }
            var currentIds = original.Parties.Select(row => row.Id!.Value).ToHashSet();
            return beforeMerge.Parties.All(row => !persistedMergeRows.Contains(row.Id!.Value) || currentIds.Contains(row.Id.Value));
        }
    }
    private readonly HashSet<Guid> persistedMergeRows = [];
    public long InputRevision { get; private set; }
    public bool CanSave => !IsSaving && !IsUnknown && !IsOutdated && !Missing && !HasPendingInput && Review is null;
    public bool CanApply => !IsSaving && !IsUnknown && !HasPendingInput && !Missing && !ReviewIsStale &&
        Review?.CanApply == true && reviewInput == InputRevision && reviewDraft is not null && OpportunityValues.Same(reviewDraft, Draft);

    public void Reset(CrmOpportunityEditorModel value) {
        var saved = value.Snapshot();
        OpportunityValues.EnsureKeys(saved);
        Draft = saved.Snapshot();
        original = saved.Snapshot();
        beforeMerge = null;
        CancelReview();
        IsSaving = false;
        IsOutdated = false;
        Missing = false;
        UnknownSubmission = null;
        HasPendingPicker = false;
        pending.Clear();
        persistedMergeRows.Clear();
        Array.Clear(inputs);
        submittedInputs = null;
        InputRevision = 0;
        hintRevision = 0;
    }

    public void InputChanged(OpportunityInput input) {
        inputs[(int)input.Field] = ++InputRevision;
        if (input.Pending) {
            pending.Add(input.Field);
        } else {
            pending.Remove(input.Field);
        }
    }

    public void ReportPicker(bool isOpen) {
        HasPendingPicker = isOpen;
        InputRevision++;
    }

    public void Hint() {
        hintRevision++;
        IsOutdated = true;
    }

    public void Unavailable() {
        Missing = true;
        Hint();
    }

    public void Observe(CrmOpportunityEditorModel current) {
        if (current.Id != Draft.Id || current.AccountPartyId != Draft.AccountPartyId) {
            throw new InvalidOperationException("The opportunity observation belongs to a different editor.");
        }
        if (current.ExpectedUpdatedAtUtc == original.ExpectedUpdatedAtUtc && OpportunityValues.Same(original, current)) {
            IsOutdated = IsUnknown;
            return;
        }
        if (OpportunityValues.ProjectWasReplaced(original, current)) {
            Hint();
            return;
        }
        if (IsDirty || IsSaving || IsUnknown || Review is not null) {
            Hint();
            return;
        }
        OpportunityValues.Apply(Draft, current);
        original = current.Snapshot();
        IsOutdated = false;
    }

    public CrmOpportunityEditorModel BeginSave() {
        if (!CanSave) {
            throw new InvalidOperationException("Review and validate the opportunity before saving.");
        }
        submittedInputs = (long[])inputs.Clone();
        IsSaving = true;
        return Draft.Snapshot();
    }

    public void Accepted(CrmOpportunityEditorModel submitted, CrmOpportunityEditorModel accepted) {
        var later = Enum.GetValues<OpportunityField>().Where(field => submittedInputs?[(int)field] != inputs[(int)field]).ToHashSet();
        OpportunityValues.Apply(Draft, OpportunityValues.Rebase(submitted, Draft, accepted, later));
        original = accepted.Snapshot();
        IsSaving = false;
        submittedInputs = null;
        beforeMerge = null;
        persistedMergeRows.Clear();
        CancelReview();
    }

    public void Rejected(bool conflict) {
        IsSaving = false;
        submittedInputs = null;
        if (conflict) {
            Hint();
        }
    }

    public void Unknown(CrmOpportunityEditorModel submitted) {
        IsSaving = false;
        UnknownSubmission = submitted.Snapshot();
        Hint();
    }

    public long BeginReview() => hintRevision;
    public void Compare(CrmOpportunityEditorModel current, long revision) {
        if (current.Id != original.Id || current.AccountPartyId != original.AccountPartyId) {
            throw new InvalidOperationException("The comparison belongs to a different opportunity.");
        }
        Review = Review?.Refresh(current, Draft) ?? new(original, Draft, current);
        reviewDraft = Draft.Snapshot();
        reviewHint = revision;
        reviewInput = InputRevision;
    }

    public bool Apply() {
        if (!CanApply) {
            return false;
        }
        var review = Review!;
        beforeMerge = Draft.Snapshot();
        persistedMergeRows.Clear();
        persistedMergeRows.UnionWith(original.Parties.Concat(review.Current.Parties).Select(row => row.Id!.Value));
        OpportunityValues.Apply(Draft, review.Candidate());
        original = review.Current.Snapshot();
        CancelReview();
        IsOutdated = false;
        return true;
    }

    public void Undo() {
        if (IsSaving || IsUnknown || !CanUndo) {
            return;
        }
        beforeMerge!.ExpectedUpdatedAtUtc = original.ExpectedUpdatedAtUtc;
        OpportunityValues.Apply(Draft, beforeMerge);
        beforeMerge = null;
    }

    public void CancelReview() {
        Review = null;
        reviewDraft = null;
    }

    public (HashSet<Guid> Parties, HashSet<ProjectWriteAdmission> Projects) MetadataReferences() {
        CrmOpportunityEditorModel?[] snapshots = [original, Draft, beforeMerge, reviewDraft, UnknownSubmission, Review?.Current, Review?.Candidate()];
        var parties = new HashSet<Guid>();
        var projects = new HashSet<ProjectWriteAdmission>();
        foreach (var model in snapshots) {
            if (model is null) {
                continue;
            }
            parties.Add(model.AccountPartyId);
            parties.Add(model.OwnerPartyId);
            if (model.DeliveryUnitPartyId is Guid unitId) {
                parties.Add(unitId);
            }
            parties.UnionWith(model.Parties.Select(row => row.PartyId));
            if (model.LinkedProjectAdmission is { } project) {
                projects.Add(project);
            }
        }
        return (parties, projects);
    }
}

