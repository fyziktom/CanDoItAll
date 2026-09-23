using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.AgentFramework.Models;

public static class ProviderHistoryPriceMapping {
    public static HistoryPrice From(ProviderExecutionPrice price) => new(price.Kind switch {
        ProviderPriceEvidenceKind.ProviderReported => HistoryPriceState.ProviderReported,
        ProviderPriceEvidenceKind.Calculated => HistoryPriceState.CalculatedAtExecution,
        ProviderPriceEvidenceKind.ExplicitFree => HistoryPriceState.ExplicitFree,
        ProviderPriceEvidenceKind.PartialEstimate => HistoryPriceState.PartialEstimate,
        ProviderPriceEvidenceKind.MissingTariff => HistoryPriceState.MissingTariff,
        ProviderPriceEvidenceKind.MissingUsage => HistoryPriceState.MissingUsage,
        ProviderPriceEvidenceKind.UnsupportedUnit => HistoryPriceState.UnsupportedUnit,
        ProviderPriceEvidenceKind.InvalidEvidence => HistoryPriceState.InvalidEvidence,
        _ => HistoryPriceState.Unpriced
    }, price.Amount, price.Currency, price.ProfileHash, price.Version) { SourceRevision = price.SourceRevision };
}
