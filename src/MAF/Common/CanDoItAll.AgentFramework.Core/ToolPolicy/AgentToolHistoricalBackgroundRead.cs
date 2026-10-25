using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentToolHistoricalBackgroundSourcePolicy {
    string SourceKind { get; }
    ValueTask<AgentToolBackgroundSourceObservation> ObserveHistoricalReadAsync(ExecutionRunRecord execution,
        AgentHistoricalReceiptReadFence currentFence, CancellationToken cancellationToken = default);
}