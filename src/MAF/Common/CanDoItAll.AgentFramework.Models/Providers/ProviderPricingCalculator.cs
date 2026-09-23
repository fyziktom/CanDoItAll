namespace CanDoItAll.AgentFramework.Models;

public static class ProviderPricingCalculator {
    public static bool TryCalculate(ProviderModelTokenPrice price, long inputTokens, long cachedInputTokens,
        long cacheWriteTokens, long outputTokens, out ProviderTokenCost cost) =>
        ProviderTokenCostCalculator.TryCalculate(price, inputTokens, cachedInputTokens, cacheWriteTokens, outputTokens, out cost);

    public static bool TryCalculate(
        AgentRunMetric metric,
        ProviderProfile provider,
        out ProviderRunCostResult cost) {
        ArgumentNullException.ThrowIfNull(metric);
        ArgumentNullException.ThrowIfNull(provider);

        return TryCalculate(
            provider.Name,
            metric.Model,
            metric.InputTokens,
            metric.CachedInputTokens,
            metric.CacheWriteTokens,
            metric.OutputTokens,
            provider.ModelPrices,
            out cost);
    }

    public static bool TryCalculate(
        string providerName,
        string model,
        int inputTokens,
        int cachedInputTokens,
        int outputTokens,
        IEnumerable<ProviderModelTokenPrice>? modelPrices,
        out ProviderRunCostResult cost) {
        return TryCalculate(
            providerName,
            model,
            inputTokens,
            cachedInputTokens,
            cacheWriteTokens: 0,
            outputTokens,
            modelPrices,
            out cost);
    }

    public static bool TryCalculate(
        string providerName,
        string model,
        int inputTokens,
        int cachedInputTokens,
        int cacheWriteTokens,
        int outputTokens,
        IEnumerable<ProviderModelTokenPrice>? modelPrices,
        out ProviderRunCostResult cost) {
        cost = default!;
        if (!ProviderPricingDefaults.TryFindPrice(modelPrices, model, out var price)) {
            return false;
        }

        var normalizedInputTokens = Math.Max(0, inputTokens);
        var normalizedCachedInputTokens = Math.Clamp(cachedInputTokens, 0, normalizedInputTokens);
        var normalizedCacheWriteTokens = Math.Clamp(
            cacheWriteTokens,
            0,
            normalizedInputTokens - normalizedCachedInputTokens);
        if (!TryCalculate(price, normalizedInputTokens, normalizedCachedInputTokens,
            normalizedCacheWriteTokens, Math.Max(0, outputTokens), out var tokenCost)) {
            return false;
        }
        cost = new ProviderRunCostResult(
            providerName,
            price.Model,
            normalizedInputTokens,
            normalizedCachedInputTokens,
            Math.Max(0, outputTokens),
            tokenCost.InputCostUsd,
            tokenCost.CachedInputCostUsd,
            tokenCost.OutputCostUsd,
            tokenCost.TotalUsd) {
            CacheWriteTokens = normalizedCacheWriteTokens,
            CacheWriteCostUsd = tokenCost.CacheWriteCostUsd
        };
        return true;
    }

    public static bool TryResolveMetricCost(
        AgentRunMetric metric,
        IEnumerable<ProviderProfile> providers,
        out decimal costUsd) {
        ArgumentNullException.ThrowIfNull(metric);
        ArgumentNullException.ThrowIfNull(providers);

        if (metric.CostUsd > 0m) {
            costUsd = metric.CostUsd;
            return true;
        }

        var provider = providers.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, metric.ProviderName, StringComparison.OrdinalIgnoreCase));
        if (provider is not null && TryCalculate(metric, provider, out var calculatedCost)) {
            costUsd = calculatedCost.TotalUsd;
            return true;
        }

        costUsd = 0m;
        return false;
    }

    public static bool TryResolveObservationCost(
        ProviderUsageObservation observation,
        IEnumerable<ProviderProfile> providers,
        out decimal costUsd) {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(providers);

        if (!IsKnownUsageStatus(observation.UsageStatus)) {
            costUsd = 0m;
            return false;
        }

        if (observation.ProviderCostUsd is >= 0m) {
            costUsd = observation.ProviderCostUsd.Value;
            return true;
        }

        if (observation.CalculatedCostUsd is >= 0m) {
            costUsd = observation.CalculatedCostUsd.Value;
            return true;
        }

        var provider = providers.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, observation.ProviderName, StringComparison.OrdinalIgnoreCase));
        var billableOutputTokens = ResolveBillableOutputTokens(
            observation.InputTokens,
            observation.OutputTokens,
            observation.TotalTokens);
        if (provider is not null &&
            TryCalculate(
                provider.Name,
                observation.Model,
                observation.InputTokens,
                observation.CachedInputTokens,
                observation.CacheWriteTokens,
                billableOutputTokens,
                provider.ModelPrices,
                out var calculatedCost)) {
            costUsd = calculatedCost.TotalUsd;
            return true;
        }

        costUsd = 0m;
        return false;
    }

    public static ProviderUsageSummary SummarizeUsage(
        IEnumerable<ProviderUsageObservation> observations,
        IEnumerable<ProviderProfile> providers) {
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(providers);

        var items = observations.ToList();
        var knownItems = items.Where(item => IsKnownUsageStatus(item.UsageStatus)).ToList();
        var knownCost = knownItems
            .Select(item => TryResolveObservationCost(item, providers, out var costUsd) ? costUsd : 0m)
            .Sum();

        return new ProviderUsageSummary(
            ObservationCount: items.Count,
            KnownObservationCount: knownItems.Count,
            UnknownObservationCount: items.Count - knownItems.Count,
            InputTokens: knownItems.Sum(item => item.InputTokens),
            CachedInputTokens: knownItems.Sum(item => item.CachedInputTokens),
            OutputTokens: knownItems.Sum(item => item.OutputTokens),
            ReasoningTokens: knownItems.Sum(item => item.ReasoningTokens),
            TotalTokens: knownItems.Sum(item => ResolveTotalTokens(item.InputTokens, item.OutputTokens, item.TotalTokens)),
            KnownCostUsd: decimal.Round(knownCost, 6, MidpointRounding.AwayFromZero)) {
            CacheWriteTokens = knownItems.Sum(item => item.CacheWriteTokens)
        };
    }

    public static decimal SumKnownCosts(IEnumerable<AgentRunMetric> metrics) {
        ArgumentNullException.ThrowIfNull(metrics);

        return metrics.Sum(metric => metric.CostUsd);
    }

    public static bool IsKnownUsageStatus(ProviderUsageObservationStatus status) {
        return status is ProviderUsageObservationStatus.Observed
            or ProviderUsageObservationStatus.ObservedFromMetric;
    }

    public static int ResolveBillableOutputTokens(int inputTokens, int outputTokens, int totalTokens) {
        var normalizedOutputTokens = Math.Max(0, outputTokens);
        if (totalTokens <= 0) {
            return normalizedOutputTokens;
        }

        return Math.Max(normalizedOutputTokens, Math.Max(0, totalTokens - Math.Max(0, inputTokens)));
    }

    private static int ResolveTotalTokens(int inputTokens, int outputTokens, int totalTokens)
        => totalTokens > 0
            ? totalTokens
            : Math.Max(0, inputTokens) + Math.Max(0, outputTokens);
}
