using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.SharedProviders.Abstractions;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SharedProviderCatalogPrice(
    [property: JsonPropertyName("inputPerMillionTokensUsd")] decimal InputPerMillionTokensUsd,
    [property: JsonPropertyName("cachedInputPerMillionTokensUsd")] decimal CachedInputPerMillionTokensUsd,
    [property: JsonPropertyName("outputPerMillionTokensUsd")] decimal OutputPerMillionTokensUsd) {
    [JsonPropertyName("cacheWritePerMillionTokensUsd")]
    public decimal? CacheWritePerMillionTokensUsd { get; init; }

    [JsonPropertyName("longContextThresholdTokens")]
    public int? LongContextThresholdTokens { get; init; }

    [JsonPropertyName("longContextInputPerMillionTokensUsd")]
    public decimal? LongContextInputPerMillionTokensUsd { get; init; }

    [JsonPropertyName("longContextCachedInputPerMillionTokensUsd")]
    public decimal? LongContextCachedInputPerMillionTokensUsd { get; init; }

    [JsonPropertyName("longContextCacheWritePerMillionTokensUsd")]
    public decimal? LongContextCacheWritePerMillionTokensUsd { get; init; }

    [JsonPropertyName("longContextOutputPerMillionTokensUsd")]
    public decimal? LongContextOutputPerMillionTokensUsd { get; init; }

    internal void Validate() {
        if (InputPerMillionTokensUsd < 0 || CachedInputPerMillionTokensUsd < 0 ||
            OutputPerMillionTokensUsd < 0 || CacheWritePerMillionTokensUsd < 0 ||
            LongContextInputPerMillionTokensUsd < 0 || LongContextCachedInputPerMillionTokensUsd < 0 ||
            LongContextCacheWritePerMillionTokensUsd < 0 || LongContextOutputPerMillionTokensUsd < 0) {
            throw new JsonException("Shared-provider model prices cannot be negative.");
        }

        var hasLongContext = LongContextThresholdTokens.HasValue ||
            LongContextInputPerMillionTokensUsd.HasValue || LongContextCachedInputPerMillionTokensUsd.HasValue ||
            LongContextCacheWritePerMillionTokensUsd.HasValue || LongContextOutputPerMillionTokensUsd.HasValue;
        if (hasLongContext && (LongContextThresholdTokens is not > 0 ||
            LongContextInputPerMillionTokensUsd is null || LongContextCachedInputPerMillionTokensUsd is null ||
            LongContextOutputPerMillionTokensUsd is null)) {
            throw new JsonException("Shared-provider long-context pricing requires a positive threshold and all token rates.");
        }
    }
}
