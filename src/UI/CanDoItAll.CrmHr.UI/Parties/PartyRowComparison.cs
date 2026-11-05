using CanDoItAll.Editing;

namespace CanDoItAll.CrmHr.UI.Parties;

internal static class PartyRowComparison {
    internal const int DetailedLimit = 1000;

    internal static IReadOnlyList<KeyedMergeRow<Guid, T>> Compare<T>(IReadOnlyList<T> original, IReadOnlyList<T> local,
        IReadOnlyList<T> current, Func<T, Guid?> key, IEqualityComparer<T> equality, out MergeField<IReadOnlyList<T>>? whole) {
        var compared = new HashSet<Guid>();
        foreach (var values in new[] { original, local, current }) {
            var identities = new HashSet<Guid>();
            foreach (var row in values) {
                if (key(row) is not Guid id || id == Guid.Empty || !identities.Add(id)) {
                    throw new ArgumentException("Each compared row must have a distinct, nonempty identity.");
                }
                compared.Add(id);
            }
        }
        whole = compared.Count > DetailedLimit ? new(original, local, current, new RowsEqual<T>(key, equality)) : null;
        return whole is null ? CollectionMerge.ByKey(original, local, current, row => key(row)!.Value, valueComparer: equality, maximumRows: DetailedLimit) : [];
    }

    private sealed class RowsEqual<T>(Func<T, Guid?> key, IEqualityComparer<T> equality) : IEqualityComparer<IReadOnlyList<T>> {
        public bool Equals(IReadOnlyList<T>? a, IReadOnlyList<T>? b) => a is not null && b is not null && PartyEditorValues.SameRows(a, b, key, equality);
        public int GetHashCode(IReadOnlyList<T> values) => values.Count;
    }
}
