using System.Security.Cryptography;
using System.Text.Json;

namespace CanDoItAll.AgentFramework.Models;

public enum ProviderTariffKind { Unspecified, Configured, ExplicitFree }
public enum ProviderPriceEvidenceKind {
    LegacyUnavailable, ProviderReported, Calculated, ExplicitFree, PartialEstimate,
    MissingTariff, MissingUsage, UnsupportedUnit, InvalidEvidence
}

public sealed record ProviderExecutionPrice(
    ProviderPriceEvidenceKind Kind, decimal? Amount, string? Currency,
    string? ProfileHash, string? Version, string? SourceRevision);

public sealed record ProviderExecutionTariff(
    ProviderModelTokenPrice? Price, string ProfileHash, string Version, string SourceRevision);

public static class ProviderExecutionPricing {
    public const string Version = "provider-execution-pricing-v1";

    public static ProviderExecutionTariff Freeze(
        Guid providerId, string model, IEnumerable<ProviderModelTokenPrice> prices, string sourceRevision) {
        var exact = prices.SingleOrDefault(price => string.Equals(price.Model, model, StringComparison.Ordinal));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { ProviderId = providerId, Model = model, Price = exact, SourceRevision = sourceRevision });
        return new(exact, Convert.ToHexStringLower(SHA256.HashData(bytes)), Version, sourceRevision);
    }

    public static ProviderExecutionPrice Evaluate(
        ProviderExecutionTariff tariff, long? input, long? cachedInput, long? cacheWrite, long? output,
        bool supportedTokenUnit, decimal? reportedAmount = null, string? reportedCurrency = null) {
        ProviderExecutionPrice Result(ProviderPriceEvidenceKind kind, decimal? amount = null, string? currency = null) =>
            new(kind, amount, currency, tariff.ProfileHash, tariff.Version, tariff.SourceRevision);

        if (reportedAmount.HasValue) {
            return reportedAmount >= 0 && reportedCurrency is { Length: 3 } && reportedCurrency.All(char.IsAsciiLetterUpper)
                ? Result(ProviderPriceEvidenceKind.ProviderReported, reportedAmount, reportedCurrency)
                : Result(ProviderPriceEvidenceKind.InvalidEvidence);
        }
        if (!supportedTokenUnit) {
            return Result(ProviderPriceEvidenceKind.UnsupportedUnit);
        }
        var price = tariff.Price;
        if (price is null || price.TariffKind != ProviderTariffKind.ExplicitFree && ProviderTokenCostCalculator.HasOnlyZeroRates(price)) {
            return Result(ProviderPriceEvidenceKind.MissingTariff);
        }
        if (price.TariffKind == ProviderTariffKind.ExplicitFree) {
            return ProviderTokenCostCalculator.HasOnlyZeroRates(price)
                ? Result(ProviderPriceEvidenceKind.ExplicitFree, 0m, "USD")
                : Result(ProviderPriceEvidenceKind.InvalidEvidence);
        }
        if (input is null || output is null) {
            return Result(input.HasValue || output.HasValue ? ProviderPriceEvidenceKind.PartialEstimate : ProviderPriceEvidenceKind.MissingUsage);
        }
        var longContext = price.LongContextThresholdTokens is { } threshold && input > threshold;
        var inputRate = longContext ? price.LongContextInputPerMillionTokensUsd : price.InputPerMillionTokensUsd;
        var cachedRate = longContext ? price.LongContextCachedInputPerMillionTokensUsd : price.CachedInputPerMillionTokensUsd;
        var writeRate = longContext ? price.LongContextCacheWritePerMillionTokensUsd : price.CacheWritePerMillionTokensUsd;
        if (input > 0 && (cachedInput is null && cachedRate != inputRate || cacheWrite is null && writeRate.HasValue)) {
            return Result(ProviderPriceEvidenceKind.PartialEstimate);
        }
        return ProviderPricingCalculator.TryCalculate(price, input.Value, cachedInput ?? 0, cacheWrite ?? 0, output.Value, out var cost)
            ? Result(ProviderPriceEvidenceKind.Calculated, cost.TotalUsd, "USD")
            : Result(ProviderPriceEvidenceKind.InvalidEvidence);
    }
}
