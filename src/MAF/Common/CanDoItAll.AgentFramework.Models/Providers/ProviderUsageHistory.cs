using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.AgentFramework.Models;

public static class ProviderUsageHistory {
    public static IReadOnlyList<ProviderUsageObservation> Attach(
        IReadOnlyList<ProviderUsageObservation> observations, HistoryCanonicalInvocation? evidence) {
        if (evidence is null || observations.Count == 0) {
            return observations;
        }
        var primary = true;
        var included = new HistoryCanonicalInvocation(evidence.RequestId, false, []);
        return observations.Select(observation => {
            if (observation.HistoryEvidence is not null) {
                return observation;
            }
            var result = observation with { HistoryEvidence = primary ? evidence : included };
            primary = false;
            return result;
        }).ToArray();
    }
}
