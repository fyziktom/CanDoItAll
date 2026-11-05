namespace CanDoItAll.Editing;

public enum MergeChoice { Unresolved, Automatic, Local, Current, Custom }

public sealed class MergeField<T> {
    private readonly IEqualityComparer<T> equality;
    private T result;

    public MergeField(T original, T local, T current, IEqualityComparer<T>? comparer = null) {
        equality = comparer ?? EqualityComparer<T>.Default;
        Original = original;
        Local = local;
        Current = current;
        (result, Choice) = equality.Equals(local, current) || equality.Equals(current, original)
            ? (local, MergeChoice.Automatic)
            : equality.Equals(local, original)
                ? (current, MergeChoice.Automatic)
                : (local, MergeChoice.Unresolved);
    }

    public T Original { get; }
    public T Local { get; }
    public T Current { get; }
    public MergeChoice Choice { get; private set; }
    public bool IsResolved => Choice != MergeChoice.Unresolved;
    public bool LocalChanged => !equality.Equals(Original, Local);
    public bool CurrentChanged => !equality.Equals(Original, Current);
    public T Result => IsResolved ? result : throw new InvalidOperationException("Resolve this field before applying the merge.");

    public void UseLocal() => Set(Local, MergeChoice.Local);
    public void UseCurrent() => Set(Current, MergeChoice.Current);
    public void SetResult(T value) => Set(value, MergeChoice.Custom);
    public void RequireChoice() => Choice = MergeChoice.Unresolved;

    internal void SetAutomatic(T value) => Set(value, MergeChoice.Automatic);

    private void Set(T value, MergeChoice choice) {
        result = value;
        Choice = choice;
    }
}
