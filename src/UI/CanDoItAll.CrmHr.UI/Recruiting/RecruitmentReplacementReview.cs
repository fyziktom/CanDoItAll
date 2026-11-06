using CanDoItAll.Editing;

namespace CanDoItAll.CrmHr.UI.Recruiting;

public sealed class RecruitmentReplacementReview<TModel, TField, TReview>(Func<TModel, TModel> snapshot,
    Func<TModel, TModel, bool> same, Action<TModel, TModel> copy, Func<TModel, TModel, bool> sameOriginal,
    Func<TModel, TModel, TModel, TReview> compare, Action<TReview, TReview> preserve,
    Func<TReview, bool> resolved, Func<TReview, TModel> candidate)
    where TModel : class where TField : struct, Enum where TReview : class {
    private TModel basis = default!;
    private TModel? reviewLocal;
    private TModel? reviewCurrent;
    private (TModel Basis, TModel Local)? undo;
    private long reviewedHint;
    private long reviewedInput;
    public RecordDraftState<TModel, TField> State { get; } = new(snapshot, same, copy);
    public TReview? Review { get; private set; }
    public long HintRevision { get; private set; }
    public bool IsOutdated { get; private set; }
    public bool IsStale => Review is not null && reviewedHint != HintRevision;
    public bool CanSave => State.CanSave && !IsOutdated && Review is null;
    public bool CanApply => State.CanSave && Review is { } review && resolved(review) && !IsStale &&
        reviewedInput == State.InputRevision && reviewLocal is not null && same(reviewLocal, State.Draft);
    public bool CanUndo => undo is not null && State.CanSave && Review is null;
    public IReadOnlyList<Guid> ReferenceIds(Func<TModel, IEnumerable<Guid?>> select) =>
        new[] { basis, State.Draft, reviewCurrent }.OfType<TModel>().SelectMany(select).OfType<Guid>().Distinct().ToArray();

    public void Open(TModel model) {
        State.Open(model);
        basis = snapshot(model);
        Review = null;
        reviewLocal = reviewCurrent = null;
        undo = null;
        IsOutdated = false;
        HintRevision = reviewedHint = reviewedInput = 0;
    }
    public void Hint() {
        HintRevision++;
        IsOutdated = true;
    }
    public void Observe(TModel current, long readHint, bool explicitPull) {
        if (sameOriginal(basis, current) && same(basis, current)) {
            IsOutdated = State.IsUnknown || readHint != HintRevision;
            return;
        }
        if (!explicitPull || State.IsDirty || !State.CanSave || Review is not null || readHint != HintRevision) {
            IsOutdated = true;
            return;
        }
        copy(State.Draft, current);
        basis = snapshot(current);
        State.Rebase(current);
        undo = null;
        IsOutdated = false;
    }
    public void Compare(TModel current, long readHint) {
        var next = compare(basis, State.Draft, current);
        if (Review is { } previous) {
            preserve(previous, next);
        }
        Review = next;
        reviewLocal = snapshot(State.Draft);
        reviewCurrent = snapshot(current);
        reviewedHint = readHint;
        reviewedInput = State.InputRevision;
    }
    public bool Apply() {
        if (!CanApply || Review is not { } review || reviewCurrent is null) {
            return false;
        }
        undo = (snapshot(basis), snapshot(State.Draft));
        copy(State.Draft, candidate(review));
        basis = snapshot(reviewCurrent);
        State.Rebase(basis);
        IsOutdated = false;
        Review = null;
        return true;
    }
    public void Undo() {
        if (!CanUndo || undo is not { } previous) {
            return;
        }
        basis = previous.Basis;
        copy(State.Draft, previous.Local);
        State.Rebase(basis);
        undo = null;
        Hint();
    }
    public void Accept(TModel accepted, long submittedHint) {
        basis = snapshot(accepted);
        IsOutdated = submittedHint != HintRevision;
        Review = null;
        undo = null;
    }
    public void Cancel() => Review = null;
}
