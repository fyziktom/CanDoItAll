using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Crm;

public sealed class ConnectionReconciliation {
    private List<CrmAccountConnectionEditorModel> originalValues = [];
    private readonly Dictionary<ConnectionInput, long> inputs = [];
    private Dictionary<ConnectionInput, long> submittedInputs = [];
    private ConnectionSubmission? pending;
    private long revision;
    private long hints;
    private List<CrmAccountConnectionEditorModel>? beforeMerge;
    private HashSet<Guid> beforeMergePersisted = [];
    public Guid AccountPartyId { get; private set; }
    public long Opening { get; private set; }
    public List<CrmAccountConnectionEditorModel> Draft { get; private set; } = [];
    public CrmAccountConnectionsObservation? Original { get; private set; }
    public bool IsSaving => pending is not null;
    public bool IsUnknown { get; private set; }
    public bool IsOutdated { get; private set; }
    public bool PickerPending { get; private set; }
    public bool Reading { get; private set; }
    public long InputRevision => revision;
    public long HintRevision => hints;
    public ConnectionMergeReview? Review { get; private set; }
    public bool ReviewStale { get; private set; }
    public bool CanApply => !Reading && !IsSaving && !IsUnknown && !PickerPending && !ReviewStale && Review?.CanApply == true;
    public bool CanUndo => beforeMerge is not null && !IsSaving && !IsUnknown && !Reading && !PickerPending && Review is null && beforeMerge.All(row =>
        !beforeMergePersisted.Contains(row.Id!.Value) || Original?.Rows.Any(saved => saved.Id == row.Id) == true);
    public bool IsDirty => !ConnectionValues.Same(originalValues, Draft);
    public ConnectionSubmission? UnknownSubmission { get; private set; }
    public CrmAccountConnectionsSaveReceipt? Accepted { get; private set; }
    public string? ValidationMessage => ConnectionMergeReview.Validate(AccountPartyId, Draft, originalValues, originalValues);
    public bool CanSubmit => Original is not null && !IsSaving && !IsUnknown && !IsOutdated && !PickerPending && !Reading && Review is null &&
        ValidationMessage is null && Draft.All(row => row.Id is not null && row.Id != Guid.Empty && row.RelatedPartyId != Guid.Empty && row.RelatedPartyId != AccountPartyId &&
            row.ProjectIds.All(id => row.ProjectAdmissions.ContainsKey(id)));

    public void Open(CrmAccountWorkspaceModel account) {
        Opening++;
        AccountPartyId = account.AccountPartyId;
        Original = account.ConnectionsOriginal?.Snapshot();
        originalValues = account.ConnectedRecords.Select(ConnectionValues.FromOwner).ToList();
        Draft = ConnectionValues.Snapshot(originalValues);
        pending = null;
        IsUnknown = false;
        IsOutdated = false;
        PickerPending = false;
        UnknownSubmission = null;
        Accepted = null;
        inputs.Clear();
        submittedInputs.Clear();
        revision = 0;
        hints = 0;
        Reading = false;
        Review = null;
        ReviewStale = false;
        beforeMerge = null;
        beforeMergePersisted.Clear();
    }

    public void Clear() {
        Opening++;
        AccountPartyId = Guid.Empty;
        Original = null;
        Draft = [];
        originalValues.Clear();
        inputs.Clear();
        submittedInputs.Clear();
        pending = null;
        UnknownSubmission = null;
        Accepted = null;
        IsUnknown = false;
        IsOutdated = false;
        PickerPending = false;
        Reading = false;
        Review = null;
        ReviewStale = false;
        beforeMerge = null;
        beforeMergePersisted.Clear();
    }

    public void Input(ConnectionInput input) {
        if (!Draft.Any(row => row.Id == input.RowId)) {
            return;
        }
        inputs[input] = ++revision;
        ReviewStale |= Review is not null;
    }
    public void Picker(bool pending) => PickerPending = pending;
    public void Hint() {
        hints++;
        IsOutdated = true;
        ReviewStale |= Review is not null;
    }

    public void ReadingReview(bool pending) {
        Reading = pending;
        if (pending) {
            ReviewStale |= Review is not null;
        }
    }

    public void SetReview(CrmAccountWorkspaceModel current, long hintRevision, long inputRevision) {
        Review = new(AccountPartyId, originalValues, Draft, current, Review);
        ReviewStale = hints != hintRevision || revision != inputRevision;
    }

    public bool ApplyReview() {
        if (!CanApply || Review is not { } review) {
            return false;
        }
        beforeMerge = ConnectionValues.Snapshot(Draft);
        beforeMergePersisted = Original!.Rows.Select(row => row.Id).ToHashSet();
        ConnectionValues.Adopt(Draft, review.Candidate());
        Original = review.CurrentOriginal.Snapshot();
        originalValues = ConnectionValues.Snapshot(review.Current);
        Review = null;
        IsOutdated = false;
        ReviewStale = false;
        inputs.Clear();
        revision++;
        return true;
    }

    public void CancelReview() {
        Review = null;
        ReviewStale = false;
    }

    public void Undo() {
        if (CanUndo && beforeMerge is not null) {
            ConnectionValues.Adopt(Draft, beforeMerge);
            beforeMerge = null;
            beforeMergePersisted.Clear();
            revision++;
        }
    }

    public void Observe(CrmAccountWorkspaceModel account) {
        if (account.AccountPartyId != AccountPartyId) {
            Open(account);
            return;
        }
        if (SameObservation(Original, account.ConnectionsOriginal)) {
            return;
        }
        if (IsDirty || IsSaving || IsUnknown || PickerPending || Review is not null || Reading) {
            Hint();
            return;
        }
        Original = account.ConnectionsOriginal?.Snapshot();
        originalValues = account.ConnectedRecords.Select(ConnectionValues.FromOwner).ToList();
        ConnectionValues.Adopt(Draft, originalValues);
        inputs.Clear();
        IsOutdated = false;
    }

    public ConnectionSubmission? Begin() {
        if (!CanSubmit) {
            return null;
        }
        submittedInputs = new(inputs);
        Accepted = null;
        pending = new(AccountPartyId, Opening, ConnectionValues.Snapshot(Draft), Original!.Snapshot());
        return pending;
    }

    public bool IsCurrent(ConnectionSubmission submitted) => ReferenceEquals(pending, submitted) && submitted.Opening == Opening && submitted.AccountPartyId == AccountPartyId;

    public void Accept(ConnectionSubmission submitted, CrmAccountConnectionsSaveReceipt receipt) {
        if (!IsCurrent(submitted)) {
            return;
        }
        if (receipt.AccountPartyId != AccountPartyId || receipt.Original.AccountPartyId != AccountPartyId ||
            !receipt.Accepted.Select(row => row.Id).ToHashSet().SetEquals(submitted.Rows.Select(row => row.Id))) {
            Unknown(submitted);
            throw new InvalidOperationException("The connection receipt does not identify its complete submitted set.");
        }
        var sent = submitted.Rows.ToDictionary(row => row.Id!.Value);
        var accepted = receipt.Accepted.ToDictionary(row => row.Id!.Value);
        var later = inputs.Where(pair => pair.Value != submittedInputs.GetValueOrDefault(pair.Key)).Select(pair => pair.Key).ToHashSet();
        var values = Draft.Select(row => sent.TryGetValue(row.Id!.Value, out var s)
            ? ConnectionValues.Rebase(s, row, accepted[row.Id.Value], later) : row.Snapshot()).ToArray();
        ConnectionValues.Adopt(Draft, values);
        originalValues = ConnectionValues.Snapshot(receipt.Accepted);
        Original = receipt.Original.Snapshot();
        Accepted = receipt with { Accepted = ConnectionValues.Snapshot(receipt.Accepted), Original = Original };
        beforeMerge = null;
        beforeMergePersisted.Clear();
        inputs.Clear();
        foreach (var input in later.Where(input => Draft.Any(row => row.Id == input.RowId))) {
            inputs[input] = ++revision;
        }
    }

    public void Unknown(ConnectionSubmission submitted) {
        if (IsCurrent(submitted) && Accepted is null) {
            IsUnknown = true;
            UnknownSubmission = submitted;
        }
    }

    public void Complete(ConnectionSubmission submitted) {
        if (IsCurrent(submitted)) {
            pending = null;
        }
    }

    private static bool SameObservation(CrmAccountConnectionsObservation? a, CrmAccountConnectionsObservation? b) =>
        a is not null && b is not null && a.AccountPartyId == b.AccountPartyId && a.Rows.ToHashSet().SetEquals(b.Rows) && a.Projects.ToHashSet().SetEquals(b.Projects);
}

public sealed record ConnectionSubmission(Guid AccountPartyId, long Opening, IReadOnlyList<CrmAccountConnectionEditorModel> Rows, CrmAccountConnectionsObservation Original);
