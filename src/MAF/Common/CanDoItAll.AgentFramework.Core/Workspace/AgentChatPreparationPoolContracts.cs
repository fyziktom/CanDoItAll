using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record AgentChatPreparationPoolSnapshot(
    int Capacity,
    int PreparedCount,
    long CacheHits,
    long CacheMisses,
    IReadOnlyList<Guid> PreparedAgentIds);

public interface IAgentChatPreparationPool
{
    bool HasPreparedEntries { get; }

    void Configure(FloatingAgentChatSettings settings);

    Task WarmAsync(CancellationToken cancellationToken = default);

    Task<AgentDefinition?> AcquireAsync(
        Guid agentId,
        CancellationToken cancellationToken = default);

    int PruneExpired();

    AgentChatPreparationPoolSnapshot Snapshot();
}
