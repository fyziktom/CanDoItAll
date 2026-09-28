using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;

namespace CanDoItAll.AgentFramework.Core;

public interface IBoundedAgentProviderUsageEvidenceStore : IAgentProviderUsageEvidenceStore {
    Task<AgentProviderUsageWindowEvidence> LoadProviderUsageWindowAsync(ProviderUsageWindow window, CancellationToken cancellationToken = default);
}

public sealed record AgentProviderUsageWindowEvidence(
    AgentProviderUsageEvidence Evidence,
    ProviderUsageSourceState State,
    DateTimeOffset? CoverageVerifiedAtUtc,
    IReadOnlyList<string> ObservationIdentities,
    ProviderUsageSourceError? Error = null);
