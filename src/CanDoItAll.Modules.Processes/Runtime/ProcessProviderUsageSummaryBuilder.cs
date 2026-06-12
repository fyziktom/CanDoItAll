using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessProviderUsageSummaryBuilder
{
    public static ProviderUsageSummary Build(
        IReadOnlyList<ProviderUsageObservation> usageObservations,
        IReadOnlyList<AgentRunMetric> legacyMetricsWithoutUsageObservations,
        IReadOnlyList<ProviderProfile> providers)
    {
        ArgumentNullException.ThrowIfNull(usageObservations);
        ArgumentNullException.ThrowIfNull(legacyMetricsWithoutUsageObservations);
        ArgumentNullException.ThrowIfNull(providers);

        var usageSummary = ProviderPricingCalculator.SummarizeUsage(usageObservations, providers);
        var legacyMetricCosts = legacyMetricsWithoutUsageObservations
            .Where(HasProviderActivity)
            .Select(metric => new LegacyMetricUsage(
                metric,
                ProviderPricingCalculator.TryResolveMetricCost(metric, providers, out var costUsd),
                costUsd))
            .ToArray();
        var knownLegacyMetrics = legacyMetricCosts
            .Where(metric => metric.HasKnownCost)
            .ToArray();
        var unknownLegacyMetrics = legacyMetricCosts
            .Where(metric => !metric.HasKnownCost)
            .ToArray();

        return new ProviderUsageSummary(
            usageSummary.ObservationCount + knownLegacyMetrics.Length + unknownLegacyMetrics.Length,
            usageSummary.KnownObservationCount + knownLegacyMetrics.Length,
            usageSummary.UnknownObservationCount + unknownLegacyMetrics.Length,
            usageSummary.InputTokens + legacyMetricCosts.Sum(item => item.Metric.InputTokens),
            usageSummary.CachedInputTokens + legacyMetricCosts.Sum(item => item.Metric.CachedInputTokens),
            usageSummary.OutputTokens + legacyMetricCosts.Sum(item => item.Metric.OutputTokens),
            usageSummary.ReasoningTokens,
            usageSummary.TotalTokens + legacyMetricCosts.Sum(item => Math.Max(0, item.Metric.InputTokens) + Math.Max(0, item.Metric.OutputTokens)),
            decimal.Round(
                usageSummary.KnownCostUsd + knownLegacyMetrics.Sum(item => item.CostUsd),
                6,
                MidpointRounding.AwayFromZero));
    }

    public static bool HasProviderActivity(AgentRunMetric metric)
    {
        ArgumentNullException.ThrowIfNull(metric);

        return metric.InputTokens > 0 ||
               metric.OutputTokens > 0 ||
               metric.CachedInputTokens > 0 ||
               metric.ToolCalls > 0 ||
               metric.DurationMs > 0;
    }

    private sealed record LegacyMetricUsage(
        AgentRunMetric Metric,
        bool HasKnownCost,
        decimal CostUsd);
}
