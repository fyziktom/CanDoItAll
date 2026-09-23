namespace CanDoItAll.AgentFramework.ProviderHistory;

public sealed record HistoryCanonicalInvocation {
    public HistoryCanonicalInvocation(ProviderRequestId requestId, bool isPrimary, IReadOnlyList<HistoryEntry> attempts) {
        ArgumentNullException.ThrowIfNull(attempts);
        if (requestId.Value == Guid.Empty || attempts.Count > HistoryAttemptCollection.MaximumAttempts ||
            isPrimary != (attempts.Count > 0) || attempts.Any(entry => entry.RequestId != requestId) ||
            attempts.Select(entry => entry.Id).Distinct().Count() != attempts.Count) {
            throw new ArgumentException("Canonical invocation evidence has invalid identity, role or attempts.", nameof(attempts));
        }
        RequestId = requestId;
        IsPrimary = isPrimary;
        Attempts = Array.AsReadOnly(attempts.ToArray());
    }

    public ProviderRequestId RequestId { get; }
    public bool IsPrimary { get; }
    public IReadOnlyList<HistoryEntry> Attempts { get; }

    public static HistoryCanonicalInvocation? Capture(HistoryInvocationContext context) {
        var attempts = context.Attempts.EvidenceSnapshot();
        return attempts.Count == 0 ? null : new(context.RequestId, true, attempts);
    }

    public bool Equals(HistoryCanonicalInvocation? other) =>
        other is not null && RequestId == other.RequestId && IsPrimary == other.IsPrimary &&
        Attempts.SequenceEqual(other.Attempts);

    public override int GetHashCode() {
        var hash = new HashCode();
        hash.Add(RequestId);
        hash.Add(IsPrimary);
        foreach (var attempt in Attempts) {
            hash.Add(attempt);
        }
        return hash.ToHashCode();
    }
}
