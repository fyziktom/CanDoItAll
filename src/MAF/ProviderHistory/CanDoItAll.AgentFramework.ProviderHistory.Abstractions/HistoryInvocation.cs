namespace CanDoItAll.AgentFramework.ProviderHistory;

public sealed record HistoryOwnerIdentity(HistorySourceKind Kind, HistoryOwnerId OwnerId, HistoryEvidenceId EvidenceId);

public sealed record HistoryInvocationContext(
    ProviderRequestId RequestId,
    HistoryWorkload Workload,
    HistoryCaller Caller,
    HistoryOwnerIdentity? Owner = null,
    HistoryCurrentTurn? CurrentTurn = null,
    string? CorrelationId = null) {
    public HistoryAttemptCollection Attempts { get; } = new();
    public HistoryExternalReference? ExternalReference { get; init; }

    public HistoryInvocationContext CreateChild() {
        var requestId = ProviderRequestId.New();
        return new(requestId, Workload, Caller,
            Owner is null ? null : Owner with { EvidenceId = new(requestId.Value.ToString("N")) },
            CorrelationId: CorrelationId) { ExternalReference = ExternalReference };
    }

    public static HistoryInvocationContext Create(HistoryWorkload workload = HistoryWorkload.Direct,
        HistoryCaller? caller = null, HistoryOwnerIdentity? owner = null, HistoryCurrentTurn? currentTurn = null,
        string? correlationId = null) =>
        new(new(Guid.NewGuid()), workload, caller ?? new(HistoryAuthenticationKind.Unknown), owner, currentTurn, correlationId);
}

public sealed record HistoryInvocation(HistoryProvider Provider, HistoryOperation Operation, HistoryInvocationContext Context);

public interface IProviderHistoryRecorder {
    Task<HistoryAttemptStart> BeginAsync(HistoryInvocation invocation, CancellationToken cancellationToken);
    Task CompleteAsync(HistoryAttemptStart start, HistoryAttemptCompletion completion, string? response,
        CancellationToken cancellationToken);
}

public sealed class HistoryAttemptCollection {
    public const int MaximumAttempts = 1000;
    private readonly object gate = new();
    private readonly List<HistoryAttemptStart> attempts = [];
    private readonly Dictionary<ProviderAttemptId, HistoryAttemptCompletion> completions = [];

    public int Count {
        get {
            lock (gate) {
                return attempts.Count;
            }
        }
    }

    public void Complete(HistoryAttemptStart attempt, HistoryAttemptCompletion completion) {
        lock (gate) {
            if (!attempts.Any(item => item.AttemptId == attempt.AttemptId)) {
                throw new ProviderHistoryException(HistoryFailure.Conflict, "The completed attempt does not belong to this invocation.");
            }
            completions[attempt.AttemptId] = completion;
        }
    }

    public IReadOnlyList<HistoryEntry> EvidenceSnapshot(int offset = 0) {
        lock (gate) {
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, attempts.Count);
            return attempts.Skip(offset).Select(start => HistoryAttemptEvidence.Create(start,
                completions.GetValueOrDefault(start.AttemptId))).ToArray();
        }
    }

    public void Add(HistoryAttemptStart attempt) {
        lock (gate) {
            if (attempts.Count >= MaximumAttempts) {
                throw new ProviderHistoryException(HistoryFailure.Unavailable, "The request exceeded its history attempt limit.");
            }
            attempts.Add(attempt);
        }
    }

    public IReadOnlyList<HistoryAttemptStart> Snapshot() {
        lock (gate) {
            return attempts.ToArray();
        }
    }
}
