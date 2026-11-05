using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Parties;

public sealed class RelationshipReconciliation {
    private List<PartyRelationshipEditorModel> original = [];
    private List<PartyRelationshipEditorModel>? beforeMerge;
    private List<PartyRelationshipEditorModel>? reviewDraft;
    private readonly Dictionary<RelationshipInput, long> inputs = [];
    private Dictionary<RelationshipInput, long>? submittedInputs;
    private readonly HashSet<RelationshipInput> pendingDates = [];
    private long inputRevision;
    private long hintRevision;
    private long reviewHint;
    private long reviewInput;
    public Guid PartyId { get; private set; }
    public List<PartyRelationshipEditorModel> Draft { get; } = [];
    public PartyRelationshipObservation Original => new(original.Select(row => new PartyRelationshipVersion(row.Id!.Value, row.RelatedPartyId, row.ExpectedVersion)).ToArray());
    public RelationshipMergeReview? Review { get; private set; }
    public bool IsOutdated { get; private set; }
    public bool IsSaving { get; private set; }
    public bool IsUnknown { get; private set; }
    public bool HasValidationErrors { get; private set; }
    public bool HasPendingPicker { get; private set; }
    public bool HasPendingInput => HasValidationErrors || pendingDates.Count > 0 || HasPendingPicker;
    public bool ReviewIsStale => Review is not null && reviewHint != hintRevision;
    public bool CanUndo => beforeMerge is not null && beforeMerge.All(row => row.ExpectedVersion == 0 || original.Any(saved => saved.Id == row.Id));
    public IReadOnlyList<PartyRelationshipEditorModel>? UnknownSubmission { get; private set; }
    public bool IsDirty => HasPendingInput || !RelationshipValues.Same(original, Draft);

    public void Reset(Guid partyId, IReadOnlyList<PartyRelationshipListItemModel> saved) {
        PartyId = partyId;
        original = saved.Select(RelationshipValues.FromOwner).ToList();
        RelationshipValues.Adopt(Draft, original);
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

    public void TakeInput(IReadOnlyList<PartyRelationshipEditorModel> values) {
        RelationshipValues.Adopt(Draft, values);
        inputRevision++;
    }

    public void InputChanged(RelationshipInput input) {
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

    public void Observe(IReadOnlyList<PartyRelationshipListItemModel> saved) {
        var current = saved.Select(RelationshipValues.FromOwner).ToList();
        if (SameObservation(current)) {
            IsOutdated = IsUnknown;
            return;
        }
        if (IsDirty || IsSaving || IsUnknown || Review is not null) {
            Hint();
            return;
        }
        original = current;
        RelationshipValues.Adopt(Draft, current);
        IsOutdated = false;
    }

    public List<PartyRelationshipEditorModel> BeginSave() {
        if (IsOutdated || IsSaving || IsUnknown || HasPendingInput || Review is not null || RelationshipMergeReview.Validate(PartyId, Draft) is not null) {
            throw new InvalidOperationException("Review and validate the relationships before saving.");
        }
        submittedInputs = new(inputs);
        IsSaving = true;
        return RelationshipValues.Snapshot(Draft);
    }

    public void Rejected(bool conflict) {
        IsSaving = false;
        submittedInputs = null;
        if (conflict) {
            Hint();
        }
    }

    public void Unknown(IReadOnlyList<PartyRelationshipEditorModel> submitted) {
        IsSaving = false;
        IsUnknown = true;
        IsOutdated = true;
        UnknownSubmission = RelationshipValues.Snapshot(submitted);
    }

    public void Accepted(IReadOnlyList<PartyRelationshipEditorModel> submitted,
        IReadOnlyList<PartyRelationshipListItemModel> accepted) {
        var s = submitted.ToDictionary(row => row.Id!.Value);
        var local = Draft.ToDictionary(row => row.Id!.Value);
        var a = accepted.Select(RelationshipValues.FromOwner).ToList();
        var later = inputs.Where(pair => submittedInputs?.GetValueOrDefault(pair.Key) != pair.Value).Select(pair => pair.Key).ToHashSet();
        var result = new List<PartyRelationshipEditorModel>();
        foreach (var saved in a) {
            var id = saved.Id!.Value;
            if (local.Remove(id, out var row)) {
                result.Add(s.TryGetValue(id, out var sent) ? RelationshipValues.Rebase(sent, row, saved, later) : saved);
            } else if (!s.ContainsKey(id)) {
                result.Add(saved);
            }
        }
        result.AddRange(local.Values.Where(row => !s.ContainsKey(row.Id!.Value)));
        RelationshipValues.Adopt(Draft, result);
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

    public void Compare(IReadOnlyList<PartyRelationshipListItemModel> current, long revision) {
        Review = new(PartyId, original, Review?.CaptureCandidate() ?? Draft, current.Select(RelationshipValues.FromOwner).ToList());
        reviewDraft = RelationshipValues.Snapshot(Draft);
        reviewInput = inputRevision;
        reviewHint = revision;
    }

    public bool Apply() {
        if (IsSaving || IsUnknown || HasPendingInput || Review is null || ReviewIsStale || !Review.CanApply ||
            reviewDraft is null || reviewInput != inputRevision || !RelationshipValues.Same(reviewDraft, Draft)) {
            return false;
        }
        beforeMerge = RelationshipValues.Snapshot(Draft);
        RelationshipValues.Adopt(Draft, Review.Result());
        original = RelationshipValues.Snapshot(Review.Current);
        CancelReview();
        IsOutdated = false;
        return true;
    }

    public void Undo() {
        if (IsSaving || IsUnknown || !CanUndo) {
            return;
        }
        foreach (var row in beforeMerge!) {
            var saved = original.SingleOrDefault(value => value.Id == row.Id);
            if (saved is not null) {
                row.ExpectedVersion = saved.ExpectedVersion;
            }
        }
        RelationshipValues.Adopt(Draft, beforeMerge!);
        beforeMerge = null;
    }

    public void CancelReview() {
        Review = null;
        reviewDraft = null;
    }

    private bool SameObservation(IReadOnlyList<PartyRelationshipEditorModel> current) => RelationshipValues.Same(original, current) &&
        original.All(before => current.Any(row => row.Id == before.Id && row.ExpectedVersion == before.ExpectedVersion));
}
