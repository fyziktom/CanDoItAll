namespace CanDoItAll.Editing;

public readonly record struct RecordInput<TField>(TField Field, bool Pending = false) where TField : struct, Enum;
public sealed record RecordSubmission<TModel, TField>(long Opening, TModel Model, IReadOnlyDictionary<TField, long> Inputs)
    where TModel : class where TField : struct, Enum;

public sealed class RecordDraftState<TModel, TField>(Func<TModel, TModel> snapshot, Func<TModel, TModel, bool> same,
    Action<TModel, TModel> copy) where TModel : class where TField : struct, Enum {
    private TModel initial = default!;
    private readonly Dictionary<TField, long> inputs = [];
    private readonly HashSet<TField> pending = [];
    private RecordSubmission<TModel, TField>? active;
    private long cleanRevision;
    public TModel Draft { get; private set; } = default!;
    public long Opening { get; private set; }
    public long InputRevision { get; private set; }
    public bool IsDirty => InputRevision != cleanRevision || pending.Count > 0 || !same(initial, Draft);
    public bool HasPendingInput => pending.Count > 0;
    public bool IsPending(TField field) => pending.Contains(field);
    public bool IsSaving => active is not null;
    public bool IsUnknown => UnknownSubmission is not null;
    public bool CanSave => !IsSaving && !IsUnknown && !HasPendingInput;
    public RecordSubmission<TModel, TField>? UnknownSubmission { get; private set; }
    public TModel? Accepted { get; private set; }

    public void Open(TModel model) {
        Opening++;
        Draft = snapshot(model);
        initial = snapshot(model);
        active = null;
        Accepted = default;
        UnknownSubmission = null;
        inputs.Clear();
        pending.Clear();
        InputRevision = cleanRevision = 0;
    }

    public void Input(RecordInput<TField> input) {
        inputs[input.Field] = ++InputRevision;
        if (input.Pending) {
            pending.Add(input.Field);
        } else {
            pending.Remove(input.Field);
        }
    }

    public RecordSubmission<TModel, TField>? Begin() {
        if (!CanSave) {
            return null;
        }
        Accepted = default;
        active = new(Opening, snapshot(Draft), new Dictionary<TField, long>(inputs));
        return active;
    }

    public bool IsCurrent(RecordSubmission<TModel, TField> submission) => ReferenceEquals(active, submission) && submission.Opening == Opening;

    public bool Changed(RecordSubmission<TModel, TField> submission, TField field) =>
        submission.Inputs.GetValueOrDefault(field) != inputs.GetValueOrDefault(field);

    public bool HasLaterInput(RecordSubmission<TModel, TField> submission) =>
        Enum.GetValues<TField>().Any(field => Changed(submission, field)) || !same(submission.Model, Draft);

    public void Accept(RecordSubmission<TModel, TField> submission, TModel accepted, Action reconcile) {
        if (!IsCurrent(submission)) {
            return;
        }
        var later = HasLaterInput(submission);
        Accepted = snapshot(accepted);
        reconcile();
        initial = snapshot(accepted);
        if (!later) {
            cleanRevision = InputRevision;
        }
    }

    public void ResetAcceptedCreate(RecordSubmission<TModel, TField> submission, TModel accepted, TModel empty) {
        if (!IsCurrent(submission)) {
            return;
        }
        Accept(submission, accepted, () => {
            if (!HasLaterInput(submission)) {
                copy(Draft, empty);
            }
        });
        initial = snapshot(empty);
    }

    public void Unknown(RecordSubmission<TModel, TField> submission) {
        if (IsCurrent(submission) && Accepted is null) {
            UnknownSubmission = submission;
        }
    }

    public void Complete(RecordSubmission<TModel, TField> submission) {
        if (IsCurrent(submission)) {
            active = null;
        }
    }

    public void Rebase(TModel current) {
        initial = snapshot(current);
        cleanRevision = InputRevision;
    }
}
