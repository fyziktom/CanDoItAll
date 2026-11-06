using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Workforce;

public sealed record WorkforceProfileSubmission(long Opening, WorkforceProfileEditorModel Model);

public sealed class WorkforceProfileReconciliation {
    private WorkforceProfileEditorModel? original;
    private WorkforceProfileEditorModel? beforeMerge;
    private WorkforceProfileEditorModel? reviewLocal;
    private WorkforceProfileSubmission? active;
    private readonly long[] inputs = new long[Enum.GetValues<WorkforceProfileField>().Length];
    private long[]? submittedInputs;
    private readonly HashSet<WorkforceProfileField> pending = [];
    private long cleanInput;
    private long submittedInput;
    private long hintRevision;
    private long submittedHint;
    private long reviewHint;
    private long reviewInput;
    public long Opening { get; private set; }
    public long InputRevision { get; private set; }
    public WorkforceProfileEditorModel Draft { get; private set; } = new();
    public WorkforceProfileReview? Review { get; private set; }
    public WorkforceProfileSubmission? UnknownSubmission { get; private set; }
    public WorkforceSaveReceipt<WorkforceProfileEditorModel>? Accepted { get; private set; }
    public bool IsSaving { get; private set; }
    public bool IsUnknown => UnknownSubmission is not null;
    public bool IsOutdated { get; private set; }
    public bool Missing { get; private set; }
    public bool PickerPending { get; private set; }
    public bool HasPendingInput => PickerPending || pending.Count > 0;
    public bool IsDirty => original is not null && (pending.Count > 0 || PickerPending || InputRevision != cleanInput || !Same(original, Draft));
    public bool ReviewIsStale => Review is not null && reviewHint != hintRevision;
    public bool CanSave => original is not null && !IsSaving && !IsUnknown && !IsOutdated && !Missing && !PickerPending && pending.Count == 0 && Review is null;
    public bool CanApply => !IsSaving && !IsUnknown && !Missing && !PickerPending && pending.Count == 0 &&
        !ReviewIsStale && reviewInput == InputRevision && Review?.CanApply == true && reviewLocal is not null && Same(reviewLocal, Draft);
    public bool CanUndo => beforeMerge is not null && !IsSaving && !IsUnknown && !Missing && !PickerPending && pending.Count == 0 && Review is null;

    public void Open(WorkforceProfileEditorModel model) {
        Clear();
        original = model.Snapshot();
        Draft = model.Snapshot();
    }

    public void Clear() {
        Opening++;
        Draft = new();
        original = null;
        beforeMerge = null;
        reviewLocal = null;
        active = null;
        Review = null;
        UnknownSubmission = null;
        Accepted = null;
        IsSaving = false;
        IsOutdated = false;
        Missing = false;
        PickerPending = false;
        InputRevision = cleanInput = submittedInput = hintRevision = submittedHint = reviewHint = reviewInput = 0;
        submittedInputs = null;
        Array.Clear(inputs);
        pending.Clear();
    }

    public void InputChanged(WorkforceProfileInput input) {
        inputs[(int)input.Field] = ++InputRevision;
        if (input.Pending) {
            pending.Add(input.Field);
        } else {
            pending.Remove(input.Field);
        }
    }

    public void ReportPicker(bool open) {
        PickerPending = open;
    }

    public void Hint() {
        hintRevision++;
        IsOutdated = true;
    }

    public void Unavailable() {
        Missing = true;
        Hint();
    }

    public void Observe(WorkforceProfileEditorModel current) {
        RequireTarget(current);
        if (Replaced(current)) {
            Unavailable();
            return;
        }
        if (current.Original == original!.Original) {
            IsOutdated = IsUnknown;
            return;
        }
        if (IsSaving || IsUnknown || IsDirty || Review is not null) {
            Hint();
            return;
        }
        Copy(Draft, current);
        original = current.Snapshot();
        beforeMerge = null;
        IsOutdated = false;
    }

    public WorkforceProfileSubmission? Begin() {
        if (!CanSave) {
            return null;
        }
        IsSaving = true;
        Accepted = null;
        submittedInputs = (long[])inputs.Clone();
        submittedInput = InputRevision;
        submittedHint = hintRevision;
        active = new(Opening, Draft.Snapshot());
        return active;
    }

    public bool IsCurrent(WorkforceProfileSubmission submission) => submission.Opening == Opening && ReferenceEquals(active, submission);

    public void Accept(WorkforceProfileSubmission submission, WorkforceSaveReceipt<WorkforceProfileEditorModel> receipt) {
        if (!IsCurrent(submission)) {
            return;
        }
        RequireTarget(receipt.Accepted);
        if (receipt.Id != receipt.Accepted.Id || receipt.Accepted.Original?.ProfileId != receipt.Id) {
            throw new InvalidOperationException("The workforce receipt does not identify its accepted profile.");
        }
        var saved = receipt.Accepted.Snapshot();
        var values = saved.Snapshot();
        Keep(WorkforceProfileField.Kind, model => model.WorkforceKind, (model, value) => model.WorkforceKind = value);
        Keep(WorkforceProfileField.EmployeeCode, model => model.EmployeeCode, (model, value) => model.EmployeeCode = value);
        Keep(WorkforceProfileField.JobTitle, model => model.JobTitle, (model, value) => model.JobTitle = value);
        Keep(WorkforceProfileField.Discipline, model => model.Discipline, (model, value) => model.Discipline = value);
        Keep(WorkforceProfileField.Seniority, model => model.Seniority, (model, value) => model.Seniority = value);
        Keep(WorkforceProfileField.HomeUnit, model => model.HomeUnitPartyId, (model, value) => model.HomeUnitPartyId = value);
        Keep(WorkforceProfileField.Manager, model => model.ManagerPartyId, (model, value) => model.ManagerPartyId = value);
        Keep(WorkforceProfileField.Dates, WorkforceDateRange.From, (model, value) => value.ApplyTo(model));
        Keep(WorkforceProfileField.Location, model => model.Location, (model, value) => model.Location = value);
        Keep(WorkforceProfileField.TimeZone, model => model.TimeZone, (model, value) => model.TimeZone = value);
        Keep(WorkforceProfileField.Rates, WorkforceRates.From, (model, value) => value.ApplyTo(model));
        Keep(WorkforceProfileField.Capacity, model => model.CapacityHoursPerWeek, (model, value) => model.CapacityHoursPerWeek = value);
        Keep(WorkforceProfileField.Status, model => model.Status, (model, value) => model.Status = value);
        Keep(WorkforceProfileField.Notes, model => model.Notes, (model, value) => model.Notes = value);
        Copy(Draft, values);
        original = saved;
        Accepted = receipt;
        beforeMerge = null;
        IsOutdated = hintRevision != submittedHint;
        if (InputRevision == submittedInput) {
            cleanInput = InputRevision;
        }

        void Keep<T>(WorkforceProfileField field, Func<WorkforceProfileEditorModel, T> read, Action<WorkforceProfileEditorModel, T> write) {
            if (submittedInputs?[(int)field] != inputs[(int)field] || !EqualityComparer<T>.Default.Equals(read(submission.Model), read(Draft))) {
                write(values, read(Draft));
            }
        }
    }

    public void Unknown(WorkforceProfileSubmission submission) {
        if (IsCurrent(submission) && Accepted is null) {
            UnknownSubmission = new(Opening, submission.Model.Snapshot());
            Hint();
        }
    }

    public void Complete(WorkforceProfileSubmission submission) {
        if (IsCurrent(submission)) {
            IsSaving = false;
            active = null;
            submittedInputs = null;
        }
    }

    public long BeginReview() => hintRevision;

    public void Compare(WorkforceProfileEditorModel current, long observedHint) {
        RequireTarget(current);
        if (Replaced(current)) {
            Unavailable();
            return;
        }
        var review = new WorkforceProfileReview(original!, Draft, current);
        if (Review is { } previous) {
            review.PreserveChoices(previous);
        }
        Review = review;
        reviewLocal = Draft.Snapshot();
        reviewHint = observedHint;
        reviewInput = InputRevision;
    }

    public bool Apply() {
        if (!CanApply) {
            return false;
        }
        beforeMerge = Draft.Snapshot();
        Copy(Draft, Review!.Candidate());
        original = Review.Current.Snapshot();
        InputRevision++;
        cleanInput = InputRevision;
        CancelReview();
        IsOutdated = false;
        return true;
    }

    public void CancelReview() {
        Review = null;
        reviewLocal = null;
    }

    public void Undo() {
        if (!CanUndo) {
            return;
        }
        WorkforceProfileValues.From(beforeMerge!).ApplyTo(Draft);
        InputRevision++;
        beforeMerge = null;
    }

    private void RequireTarget(WorkforceProfileEditorModel current) {
        if (original is null || current.PartyId != Draft.PartyId || current.Original is null) {
            throw new InvalidOperationException("The workforce observation belongs to another opening or has no original.");
        }
    }

    private bool Replaced(WorkforceProfileEditorModel current) => original!.Original?.PartyType != current.Original?.PartyType ||
        original.Id is not null && original.Id != current.Id;

    private static bool Same(WorkforceProfileEditorModel a, WorkforceProfileEditorModel b) => WorkforceProfileValues.From(a) == WorkforceProfileValues.From(b);

    private static void Copy(WorkforceProfileEditorModel target, WorkforceProfileEditorModel source) {
        WorkforceProfileValues.From(source).ApplyTo(target);
        target.Id = source.Id;
        target.PartyId = source.PartyId;
        target.LastChangedBy = source.LastChangedBy;
        target.Original = source.Original;
    }
}
