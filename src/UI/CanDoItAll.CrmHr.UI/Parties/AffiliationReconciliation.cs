using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Parties;

public sealed class AffiliationReconciliation {
    private List<PartyOrganizationAffiliationEditorModel> original = [];
    private List<PartyOrganizationAffiliationEditorModel>? beforeMerge;
    private List<PartyOrganizationAffiliationEditorModel>? reviewDraft;
    private readonly Dictionary<AffiliationInput, long> inputs = [];
    private Dictionary<AffiliationInput, long>? submittedInputs;
    private readonly HashSet<AffiliationInput> pendingDates = [];
    private long inputRevision;
    private long hintRevision;
    private long reviewHint;
    private long reviewInput;
    public List<PartyOrganizationAffiliationEditorModel> Draft { get; } = [];
    public AffiliationMergeReview? Review { get; private set; }
    public bool IsOutdated { get; private set; }
    public bool IsSaving { get; private set; }
    public bool IsUnknown { get; private set; }
    public bool HasValidationErrors { get; private set; }
    public bool HasPendingPicker { get; private set; }
    public bool HasPendingInput => HasValidationErrors || pendingDates.Count > 0 || HasPendingPicker;
    public bool ReviewIsStale => Review is not null && reviewHint != hintRevision;
    public bool CanUndo => beforeMerge is not null;
    public IReadOnlyList<PartyOrganizationAffiliationEditorModel>? UnknownSubmission { get; private set; }
    public bool IsDirty => HasPendingInput || !AffiliationValues.Same(original, Draft);

    public void Reset(IReadOnlyList<PartyOrganizationAffiliationListItemModel> saved) {
        original = saved.Select(AffiliationValues.FromOwner).ToList();
        AffiliationValues.Adopt(Draft, original);
        beforeMerge = null;
        CancelReview();
        IsOutdated = false;
        IsSaving = false;
        IsUnknown = false;
        UnknownSubmission = null;
        pendingDates.Clear();
        inputs.Clear();
        submittedInputs = null;
        inputRevision = 0;
        hintRevision = 0;
        HasValidationErrors = false;
        HasPendingPicker = false;
    }

    public void TakeInput(IReadOnlyList<PartyOrganizationAffiliationEditorModel> values) {
        AffiliationValues.Adopt(Draft, values);
        inputRevision++;
    }

    public void InputChanged(AffiliationInput input) {
        var key = input with { PendingDate = false };
        inputs[key] = ++inputRevision;
        if (input.PendingDate) {
            pendingDates.Add(key);
        }
    }

    public void ReportPicker(bool pending) {
        HasPendingPicker = pending;
        inputRevision++;
    }

    public void ReportValidation(bool hasErrors) {
        HasValidationErrors = hasErrors;
        if (!hasErrors) {
            pendingDates.Clear();
        }
    }

    public void Hint() {
        hintRevision++;
        IsOutdated = true;
    }

    public void Observe(IReadOnlyList<PartyOrganizationAffiliationListItemModel> saved) {
        var current = saved.Select(AffiliationValues.FromOwner).ToList();
        if (SameObservation(current)) {
            IsOutdated = IsUnknown;
            return;
        }
        if (IsDirty || IsSaving || IsUnknown || Review is not null) {
            Hint();
            return;
        }
        original = current;
        AffiliationValues.Adopt(Draft, current);
        IsOutdated = false;
    }

    public List<PartyOrganizationAffiliationEditorModel> BeginSave() {
        if (IsOutdated || IsSaving || IsUnknown || HasPendingInput || Review is not null) {
            throw new InvalidOperationException("Review and validate the affiliations before saving.");
        }
        submittedInputs = new(inputs);
        IsSaving = true;
        return AffiliationValues.Snapshot(Draft);
    }

    public void Rejected(bool conflict) {
        IsSaving = false;
        submittedInputs = null;
        if (conflict) {
            Hint();
        }
    }

    public void Unknown(IReadOnlyList<PartyOrganizationAffiliationEditorModel> submitted) {
        IsSaving = false;
        IsUnknown = true;
        IsOutdated = true;
        UnknownSubmission = AffiliationValues.Snapshot(submitted);
    }

    public void Accepted(IReadOnlyList<PartyOrganizationAffiliationEditorModel> submitted,
        IReadOnlyList<PartyOrganizationAffiliationListItemModel> accepted) {
        var s = submitted.ToDictionary(row => row.Id!.Value);
        var local = Draft.ToDictionary(row => row.Id!.Value);
        var a = accepted.Select(AffiliationValues.FromOwner).ToList();
        var later = inputs.Where(pair => submittedInputs?.GetValueOrDefault(pair.Key) != pair.Value).Select(pair => pair.Key).ToHashSet();
        var result = new List<PartyOrganizationAffiliationEditorModel>();
        foreach (var saved in a) {
            var id = saved.Id!.Value;
            if (local.Remove(id, out var row)) {
                result.Add(s.TryGetValue(id, out var sent) ? AffiliationValues.Rebase(sent, row, saved, later) : saved);
            } else if (!s.ContainsKey(id)) {
                result.Add(saved);
            }
        }
        result.AddRange(local.Values.Where(row => !s.ContainsKey(row.Id!.Value)));
        AffiliationValues.Adopt(Draft, result);
        original = a;
        IsSaving = false;
        IsUnknown = false;
        UnknownSubmission = null;
        submittedInputs = null;
        beforeMerge = null;
        CancelReview();
        var ids = Draft.Select(row => row.Id!.Value).ToHashSet();
        foreach (var key in inputs.Keys.Where(key => !ids.Contains(key.RowId)).ToArray()) {
            inputs.Remove(key);
        }
    }

    public long BeginReview() => hintRevision;

    public void Compare(IReadOnlyList<PartyOrganizationAffiliationListItemModel> current, long revision, DateOnly today) {
        Review = new(original, Review?.CaptureCandidate() ?? Draft, current.Select(AffiliationValues.FromOwner).ToList(), today);
        reviewDraft = AffiliationValues.Snapshot(Draft);
        reviewInput = inputRevision;
        reviewHint = revision;
    }

    public bool Apply() {
        if (IsSaving || IsUnknown || HasPendingInput || Review is null || ReviewIsStale || !Review.CanApply ||
            reviewDraft is null || reviewInput != inputRevision || !AffiliationValues.Same(reviewDraft, Draft)) {
            return false;
        }
        beforeMerge = AffiliationValues.Snapshot(Draft);
        AffiliationValues.Adopt(Draft, Review.Result());
        original = AffiliationValues.Snapshot(Review.Current);
        CancelReview();
        IsOutdated = false;
        return true;
    }

    public void Undo() {
        if (IsSaving || IsUnknown || beforeMerge is null) {
            return;
        }
        foreach (var row in beforeMerge) {
            var saved = original.SingleOrDefault(value => value.Id == row.Id);
            if (saved is not null) {
                row.ExpectedUpdatedAtUtc = saved.ExpectedUpdatedAtUtc;
                row.IsNew = false;
            }
        }
        AffiliationValues.Adopt(Draft, beforeMerge);
        beforeMerge = null;
    }

    public void CancelReview() {
        Review = null;
        reviewDraft = null;
    }

    private bool SameObservation(IReadOnlyList<PartyOrganizationAffiliationEditorModel> current) => AffiliationValues.Same(original, current) &&
        original.All(before => current.Any(row => row.Id == before.Id && row.ExpectedUpdatedAtUtc == before.ExpectedUpdatedAtUtc));
}
