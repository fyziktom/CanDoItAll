using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public static class SharedProviderPriceMapper {
    public static SharedProviderCatalogPrice ToCatalog(ProviderModelTokenPrice price) => new(
        price.InputPerMillionTokensUsd,
        price.CachedInputPerMillionTokensUsd,
        price.OutputPerMillionTokensUsd) {
        IsExplicitlyFree = price.TariffKind == ProviderTariffKind.ExplicitFree,
        CacheWritePerMillionTokensUsd = price.CacheWritePerMillionTokensUsd,
        LongContextThresholdTokens = price.LongContextThresholdTokens,
        LongContextInputPerMillionTokensUsd = price.LongContextInputPerMillionTokensUsd,
        LongContextCachedInputPerMillionTokensUsd = price.LongContextCachedInputPerMillionTokensUsd,
        LongContextCacheWritePerMillionTokensUsd = price.LongContextCacheWritePerMillionTokensUsd,
        LongContextOutputPerMillionTokensUsd = price.LongContextOutputPerMillionTokensUsd
    };

    public static ProviderModelTokenPrice ToRuntime(string model, SharedProviderCatalogPrice price) => new(
        model,
        price.InputPerMillionTokensUsd,
        price.CachedInputPerMillionTokensUsd,
        price.OutputPerMillionTokensUsd) {
        TariffKind = price.IsExplicitlyFree ? ProviderTariffKind.ExplicitFree : ProviderTariffKind.Unspecified,
        CacheWritePerMillionTokensUsd = price.CacheWritePerMillionTokensUsd,
        LongContextThresholdTokens = price.LongContextThresholdTokens,
        LongContextInputPerMillionTokensUsd = price.LongContextInputPerMillionTokensUsd,
        LongContextCachedInputPerMillionTokensUsd = price.LongContextCachedInputPerMillionTokensUsd,
        LongContextCacheWritePerMillionTokensUsd = price.LongContextCacheWritePerMillionTokensUsd,
        LongContextOutputPerMillionTokensUsd = price.LongContextOutputPerMillionTokensUsd
    };
}
