using System.Collections.Frozen;

namespace CanDoItAll.SharedProviders.Abstractions;

public sealed class SharedProviderSourceObservation {
    public Guid ConcurrencyToken { get; }
    public IReadOnlyDictionary<Guid, Guid> ImportVersions { get; }

    public SharedProviderSourceObservation(Guid concurrencyToken, IEnumerable<KeyValuePair<Guid, Guid>> importVersions) {
        if (concurrencyToken == Guid.Empty) {
            throw new ArgumentException("The original source version cannot be empty.", nameof(concurrencyToken));
        }
        ConcurrencyToken = concurrencyToken;
        ImportVersions = importVersions.ToFrozenDictionary();
        if (ImportVersions.Any(item => item.Key == Guid.Empty || item.Value == Guid.Empty)) {
            throw new ArgumentException("Original import identities and versions cannot be empty.", nameof(importVersions));
        }
    }

    public bool Matches(Guid concurrencyToken, IEnumerable<KeyValuePair<Guid, Guid>> importVersions) {
        if (ConcurrencyToken != concurrencyToken) {
            return false;
        }
        var current = importVersions.ToArray();
        return current.Length == ImportVersions.Count && current.All(item =>
            ImportVersions.TryGetValue(item.Key, out var original) && original == item.Value);
    }
}
