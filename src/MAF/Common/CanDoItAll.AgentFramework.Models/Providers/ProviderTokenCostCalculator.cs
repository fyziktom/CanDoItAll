namespace CanDoItAll.AgentFramework.Models;

public sealed record ProviderTokenCost(
    decimal InputCostUsd, decimal CachedInputCostUsd, decimal CacheWriteCostUsd, decimal OutputCostUsd) {
    public decimal TotalUsd => InputCostUsd + CachedInputCostUsd + CacheWriteCostUsd + OutputCostUsd;
}

internal static class ProviderTokenCostCalculator {
    public static bool HasOnlyZeroRates(ProviderModelTokenPrice price) =>
        price.InputPerMillionTokensUsd == 0 && price.CachedInputPerMillionTokensUsd == 0
        && price.OutputPerMillionTokensUsd == 0 && price.CacheWritePerMillionTokensUsd is null or 0
        && price.LongContextInputPerMillionTokensUsd is null or 0
        && price.LongContextCachedInputPerMillionTokensUsd is null or 0
        && price.LongContextCacheWritePerMillionTokensUsd is null or 0
        && price.LongContextOutputPerMillionTokensUsd is null or 0;

    public static bool TryCalculate(ProviderModelTokenPrice price, long input, long cached, long written, long output,
        out ProviderTokenCost cost) {
        cost = default!;
        if (input < 0 || cached < 0 || written < 0 || output < 0 || cached > input || written > input - cached
            || !Enum.IsDefined(price.TariffKind) || price.LongContextThresholdTokens is <= 0) {
            return false;
        }
        var longContext = price.LongContextThresholdTokens is { } threshold && input > threshold;
        var inputRate = longContext ? price.LongContextInputPerMillionTokensUsd : price.InputPerMillionTokensUsd;
        var cachedRate = longContext ? price.LongContextCachedInputPerMillionTokensUsd : price.CachedInputPerMillionTokensUsd;
        var writeRate = longContext ? price.LongContextCacheWritePerMillionTokensUsd : price.CacheWritePerMillionTokensUsd;
        var outputRate = longContext ? price.LongContextOutputPerMillionTokensUsd : price.OutputPerMillionTokensUsd;
        if (inputRate is null or < 0 || cachedRate is null or < 0 || outputRate is null or < 0
            || writeRate is < 0 || written > 0 && writeRate is null) {
            return false;
        }
        try {
            const decimal units = 1_000_000m;
            cost = new((input - cached - written) / units * inputRate.Value,
                cached / units * cachedRate.Value, written / units * (writeRate ?? 0m),
                output / units * outputRate.Value);
            _ = cost.TotalUsd;
            return true;
        } catch (OverflowException) {
            cost = default!;
            return false;
        }
    }
}
