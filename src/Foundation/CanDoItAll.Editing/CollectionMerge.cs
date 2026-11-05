namespace CanDoItAll.Editing;

public readonly record struct MergeEntry<T>(bool Exists, T? Value) {
    public static MergeEntry<T> Present(T value) => new(true, value);
    public static MergeEntry<T> Missing => default;
}

public sealed record KeyedMergeRow<TKey, TValue>(TKey Key, MergeField<MergeEntry<TValue>> Field);

public static class CollectionMerge {
    public static IReadOnlyList<T> Membership<T>(IReadOnlyList<T> original, IReadOnlyList<T> local,
        IReadOnlyList<T> current, IEqualityComparer<T>? comparer = null) where T : notnull {
        var equality = comparer ?? EqualityComparer<T>.Default;
        var b = original.ToHashSet(equality);
        var l = local.ToHashSet(equality);
        var r = current.ToHashSet(equality);
        return local.Concat(current).Distinct(equality)
            .Where(key => new MergeField<bool>(b.Contains(key), l.Contains(key), r.Contains(key)).Result)
            .ToArray();
    }

    public static IReadOnlyList<KeyedMergeRow<TKey, TValue>> ByKey<TKey, TValue>(IReadOnlyList<TValue> original,
        IReadOnlyList<TValue> local, IReadOnlyList<TValue> current, Func<TValue, TKey> key,
        IEqualityComparer<TKey>? keyComparer = null, IEqualityComparer<TValue>? valueComparer = null,
        int maximumRows = 1000) where TKey : notnull {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumRows, 1);
        if (original.Count > maximumRows || local.Count > maximumRows || current.Count > maximumRows) {
            throw new ArgumentException("The collection exceeds this adapter's merge limit.");
        }
        var b = original.ToDictionary(key, keyComparer);
        var l = local.ToDictionary(key, keyComparer);
        var r = current.ToDictionary(key, keyComparer);
        var equality = new EntryEquality<TValue>(valueComparer ?? EqualityComparer<TValue>.Default);
        return original.Concat(local).Concat(current).Select(key).Distinct(keyComparer)
            .Select(id => new KeyedMergeRow<TKey, TValue>(id, new(
                Find(b, id), Find(l, id), Find(r, id), equality)))
            .ToArray();
    }

    private static MergeEntry<TValue> Find<TKey, TValue>(Dictionary<TKey, TValue> values, TKey key) where TKey : notnull
        => values.TryGetValue(key, out var value) ? MergeEntry<TValue>.Present(value) : MergeEntry<TValue>.Missing;

    private sealed class EntryEquality<T>(IEqualityComparer<T> values) : IEqualityComparer<MergeEntry<T>> {
        public bool Equals(MergeEntry<T> x, MergeEntry<T> y)
            => x.Exists == y.Exists && (!x.Exists || values.Equals(x.Value!, y.Value!));

        public int GetHashCode(MergeEntry<T> value) => value.Exists && value.Value is not null ? values.GetHashCode(value.Value) : 0;
    }
}
