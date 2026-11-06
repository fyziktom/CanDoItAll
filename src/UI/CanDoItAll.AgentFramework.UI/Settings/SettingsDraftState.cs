using CanDoItAll.Editing;

namespace CanDoItAll.AgentFramework.UI.Settings;

public abstract class SettingsMergeField<T>(string label) where T : class {
    public string Label { get; } = label;
    public abstract string Original { get; }
    public abstract string Local { get; }
    public abstract string Current { get; }
    public abstract string Result { get; }
    public abstract bool IsResolved { get; }
    public abstract void UseLocal();
    public abstract void UseCurrent();
    public abstract T Apply(T value);
    public abstract T RetainLater(T value);
}

public sealed class SettingsMergeField<T, TValue>(string label, T original, T local, T current,
    Func<T, TValue> get, Func<T, TValue, T> set) : SettingsMergeField<T>(label) where T : class {
    private readonly MergeField<TValue> entry = new(get(original), get(local), get(current));
    public override string Original => Format(entry.Original);
    public override string Local => Format(entry.Local);
    public override string Current => Format(entry.Current);
    public override string Result => entry.IsResolved ? Format(entry.Result) : "Choose a result";
    public override bool IsResolved => entry.IsResolved;
    public override void UseLocal() => entry.UseLocal();
    public override void UseCurrent() => entry.UseCurrent();
    public override T Apply(T value) => set(value, entry.Result);
    public override T RetainLater(T value) => set(value, entry.LocalChanged ? entry.Local : entry.Current);
    private static string Format(TValue value) => value?.ToString() ?? "None";
}

public sealed record SettingsComparison<T>(T Current, long InputRevision, IReadOnlyList<SettingsMergeField<T>> Fields) where T : class;

public sealed class SettingsDraftState<T>(Func<T, T> content,
    Func<T, T, T, IReadOnlyList<SettingsMergeField<T>>> fields) where T : class {
    private T? undo;
    public T? Original { get; private set; }
    public T Draft { get; private set; } = default!;
    public SettingsComparison<T>? Comparison { get; private set; }
    public long InputRevision { get; private set; }
    public bool IsDirty => Original is not null && !EqualityComparer<T>.Default.Equals(content(Original), content(Draft));
    public bool NeedsReview { get; private set; }
    public bool OutcomeUnknown { get; private set; }
    public bool UpdatesAvailable { get; private set; }
    public bool CanUndo => undo is not null;
    public bool CanApply => Comparison is { } review && review.InputRevision == InputRevision && review.Fields.All(entry => entry.IsResolved);

    public void Open(T value) {
        Original = value;
        Draft = value;
        Comparison = null;
        undo = null;
        NeedsReview = OutcomeUnknown = UpdatesAvailable = false;
        InputRevision++;
    }

    public void Input(T value) {
        Draft = value;
        InputRevision++;
    }

    public void Hint() => UpdatesAvailable = true;
    public void Refused() => NeedsReview = true;
    public void Unknown() {
        OutcomeUnknown = true;
        NeedsReview = true;
    }

    public bool Compare(T current, long revision) {
        if (Original is null || revision != InputRevision) {
            return false;
        }
        Comparison = new(current, revision, fields(Original, Draft, current));
        return true;
    }

    public bool Apply(bool reviewedUnknown) {
        if (!CanApply || OutcomeUnknown && !reviewedUnknown) {
            return false;
        }
        var review = Comparison!;
        undo = Draft;
        Draft = review.Fields.Aggregate(Draft, (value, entry) => entry.Apply(value));
        Original = review.Current;
        Comparison = null;
        NeedsReview = OutcomeUnknown = UpdatesAvailable = false;
        InputRevision++;
        return true;
    }

    public void CancelComparison() => Comparison = null;

    public void Undo() {
        if (undo is null) {
            return;
        }
        Draft = undo;
        undo = null;
        Comparison = null;
        NeedsReview = true;
        InputRevision++;
    }

    public void Accept(T submitted, T accepted, long submittedRevision) {
        if (submittedRevision == InputRevision) {
            Draft = fields(submitted, Draft, accepted).Aggregate(Draft, (value, entry) => entry.RetainLater(value));
        }
        Original = accepted;
        Comparison = null;
        undo = null;
        NeedsReview = OutcomeUnknown = false;
        InputRevision++;
    }
}
