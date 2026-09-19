using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.SharedProviders.Abstractions;

/// <summary>
/// Public token prices of a shared model, in US dollars per million tokens. Every rate is zero or more; a model marked
/// <c>isExplicitlyFree</c> has only zero rates. A model without a price object is unpriced (cost unknown), which is not
/// the same as free. Invocations keep the prices in effect when they ran; later catalog changes do not rewrite them.
/// </summary>
/// <param name="InputPerMillionTokensUsd">Price of one million input tokens, in US dollars.</param>
/// <param name="CachedInputPerMillionTokensUsd">Price of one million cached input tokens, in US dollars.</param>
/// <param name="OutputPerMillionTokensUsd">Price of one million output tokens, in US dollars.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SharedProviderCatalogPrice(
    [property: JsonPropertyName("inputPerMillionTokensUsd")] decimal InputPerMillionTokensUsd,
    [property: JsonPropertyName("cachedInputPerMillionTokensUsd")] decimal CachedInputPerMillionTokensUsd,
    [property: JsonPropertyName("outputPerMillionTokensUsd")] decimal OutputPerMillionTokensUsd) {
    /// <summary>
    /// True when the publisher declared the model free of charge; all rates are then zero. Omitted when false.
    /// </summary>
    [JsonPropertyName("isExplicitlyFree")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsExplicitlyFree { get; init; }

    /// <summary>
    /// Price of writing one million tokens to the provider's prompt cache, in US dollars; null when not priced.
    /// </summary>
    [JsonPropertyName("cacheWritePerMillionTokensUsd")]
    public decimal? CacheWritePerMillionTokensUsd { get; init; }

    /// <summary>
    /// Input size in tokens above which an invocation is priced with the long-context rates instead of the normal
    /// ones; null when the model has no long-context tariff. When present it is greater than zero and the long-context
    /// input, cached-input and output rates are present too.
    /// </summary>
    [JsonPropertyName("longContextThresholdTokens")]
    public int? LongContextThresholdTokens { get; init; }

    /// <summary>
    /// Price of one million input tokens, in US dollars, for an invocation whose input exceeds
    /// <c>longContextThresholdTokens</c>; null when the model has no long-context tariff.
    /// </summary>
    [JsonPropertyName("longContextInputPerMillionTokensUsd")]
    public decimal? LongContextInputPerMillionTokensUsd { get; init; }

    /// <summary>
    /// Price of one million cached input tokens, in US dollars, for an invocation whose input exceeds
    /// <c>longContextThresholdTokens</c>; null when the model has no long-context tariff.
    /// </summary>
    [JsonPropertyName("longContextCachedInputPerMillionTokensUsd")]
    public decimal? LongContextCachedInputPerMillionTokensUsd { get; init; }

    /// <summary>
    /// Price of writing one million tokens to the prompt cache, in US dollars, for an invocation whose input exceeds
    /// <c>longContextThresholdTokens</c>; null when not priced.
    /// </summary>
    [JsonPropertyName("longContextCacheWritePerMillionTokensUsd")]
    public decimal? LongContextCacheWritePerMillionTokensUsd { get; init; }

    /// <summary>
    /// Price of one million output tokens, in US dollars, for an invocation whose input exceeds
    /// <c>longContextThresholdTokens</c>; null when the model has no long-context tariff.
    /// </summary>
    [JsonPropertyName("longContextOutputPerMillionTokensUsd")]
    public decimal? LongContextOutputPerMillionTokensUsd { get; init; }

    internal void Validate() {
        if (InputPerMillionTokensUsd < 0 || CachedInputPerMillionTokensUsd < 0 ||
            OutputPerMillionTokensUsd < 0 || CacheWritePerMillionTokensUsd < 0 ||
            LongContextInputPerMillionTokensUsd < 0 || LongContextCachedInputPerMillionTokensUsd < 0 ||
            LongContextCacheWritePerMillionTokensUsd < 0 || LongContextOutputPerMillionTokensUsd < 0) {
            throw new JsonException("Shared-provider model prices cannot be negative.");
        }

        if (IsExplicitlyFree && (InputPerMillionTokensUsd != 0 || CachedInputPerMillionTokensUsd != 0
            || OutputPerMillionTokensUsd != 0 || CacheWritePerMillionTokensUsd is > 0
            || LongContextInputPerMillionTokensUsd is > 0 || LongContextCachedInputPerMillionTokensUsd is > 0
            || LongContextCacheWritePerMillionTokensUsd is > 0 || LongContextOutputPerMillionTokensUsd is > 0)) {
            throw new JsonException("An explicitly free shared tariff must contain only zero rates.");
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
