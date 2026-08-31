using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.AgentFramework.Runtime.Abstractions;

public sealed class AgentHistoryCancellationException(OperationCanceledException inner, HistoryCanonicalInvocation evidence)
    : OperationCanceledException(inner.Message, inner, inner.CancellationToken) {
    public HistoryCanonicalInvocation HistoryEvidence { get; } = evidence;
}
