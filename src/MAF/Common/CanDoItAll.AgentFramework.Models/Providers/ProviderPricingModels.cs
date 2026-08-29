using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.AgentFramework.Models;

public sealed record ProviderModelTokenPrice(
    string Model,
    decimal InputPerMillionTokensUsd,
    decimal CachedInputPerMillionTokensUsd,
    decimal OutputPerMillionTokensUsd)
{
    public ProviderTariffKind TariffKind { get; init; }

    public decimal? CacheWritePerMillionTokensUsd { get; init; }

    public int? LongContextThresholdTokens { get; init; }

    public decimal? LongContextInputPerMillionTokensUsd { get; init; }

    public decimal? LongContextCachedInputPerMillionTokensUsd { get; init; }

    public decimal? LongContextCacheWritePerMillionTokensUsd { get; init; }

    public decimal? LongContextOutputPerMillionTokensUsd { get; init; }

    public bool HasConfiguredStandardPrice =>
        InputPerMillionTokensUsd > 0m ||
        CachedInputPerMillionTokensUsd > 0m ||
        OutputPerMillionTokensUsd > 0m;
}

public sealed record ProviderDiscoveredModelPrice(
    string Model,
    decimal? InputPerMillionTokensUsd,
    decimal? CachedInputPerMillionTokensUsd,
    decimal? OutputPerMillionTokensUsd)
{
    public bool HasExplicitPrices => InputPerMillionTokensUsd is >= 0m &&
                                     CachedInputPerMillionTokensUsd is >= 0m &&
                                     OutputPerMillionTokensUsd is >= 0m;
}

public sealed record ProviderModelPricingMergeResult(
    IReadOnlyList<ProviderModelTokenPrice> ModelPrices,
    int DiscoveredModelCount,
    int ExplicitPriceCount,
    int ModelNameOnlyCount);

public sealed class ProviderModelTokenPriceEditorModel
{
    public string Model { get; set; } = string.Empty;

    public decimal InputPerMillionTokensUsd { get; set; }

    public decimal CachedInputPerMillionTokensUsd { get; set; }

    public decimal OutputPerMillionTokensUsd { get; set; }

    public ProviderTariffKind TariffKind { get; set; }

    public decimal? CacheWritePerMillionTokensUsd { get; set; }

    public int? LongContextThresholdTokens { get; set; }

    public decimal? LongContextInputPerMillionTokensUsd { get; set; }

    public decimal? LongContextCachedInputPerMillionTokensUsd { get; set; }

    public decimal? LongContextCacheWritePerMillionTokensUsd { get; set; }

    public decimal? LongContextOutputPerMillionTokensUsd { get; set; }
}

public sealed record ProviderPricingMetadataSnapshot(
    bool? IsPrivateProvider,
    IReadOnlyList<ProviderModelTokenPrice> ModelPrices);

public sealed record ProviderRunCostResult(
    string ProviderName,
    string Model,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    decimal InputCostUsd,
    decimal CachedInputCostUsd,
    decimal OutputCostUsd,
    decimal TotalUsd)
{
    public int CacheWriteTokens { get; init; }

    public decimal CacheWriteCostUsd { get; init; }
}

public sealed record WorkflowUsageMetrics(
    string ProviderName,
    string Model,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    decimal CostUsd)
{
    public int KnownObservationCount { get; init; } = 1;

    public int UnknownObservationCount { get; init; }

    public bool HasUnknownUsage => UnknownObservationCount > 0;
}

public static class ProviderPricingDefaults
{
    public const decimal PrivateInputPerMillionTokensUsd = 0.10m;
    public const decimal PrivateCachedInputPerMillionTokensUsd = 0.02m;
    public const decimal PrivateOutputPerMillionTokensUsd = 0.20m;

    private static readonly ProviderModelTokenPrice OpenAiMiniPrice = new(
        "gpt-5.4-mini",
        0.75m,
        0.075m,
        4.50m);

    private static readonly ProviderModelTokenPrice OpenAiGpt56SolPrice = new(
        OpenAiModelIds.Gpt56Sol,
        4.00m,
        0.40m,
        20.00m)
    {
        CacheWritePerMillionTokensUsd = 5.00m,
        LongContextThresholdTokens = OpenAiModelPricingPolicy.Gpt56LongContextThresholdTokens,
        LongContextInputPerMillionTokensUsd = 8.00m,
        LongContextCachedInputPerMillionTokensUsd = 0.80m,
        LongContextCacheWritePerMillionTokensUsd = 10.00m,
        LongContextOutputPerMillionTokensUsd = 30.00m
    };

    private static readonly IReadOnlyList<ProviderModelTokenPrice> OpenAiModelPrices =
    [
        OpenAiGpt56SolPrice with { Model = OpenAiModelIds.Gpt56 },
        new(OpenAiModelIds.Gpt56Luna, 0.20m, 0.02m, 1.20m)
        {
            CacheWritePerMillionTokensUsd = 0.25m,
            LongContextThresholdTokens = OpenAiModelPricingPolicy.Gpt56LongContextThresholdTokens,
            LongContextInputPerMillionTokensUsd = 0.40m,
            LongContextCachedInputPerMillionTokensUsd = 0.04m,
            LongContextCacheWritePerMillionTokensUsd = 0.50m,
            LongContextOutputPerMillionTokensUsd = 1.80m
        },
        new(OpenAiModelIds.Gpt56Terra, 2.00m, 0.20m, 12.00m)
        {
            CacheWritePerMillionTokensUsd = 2.50m,
            LongContextThresholdTokens = OpenAiModelPricingPolicy.Gpt56LongContextThresholdTokens,
            LongContextInputPerMillionTokensUsd = 4.00m,
            LongContextCachedInputPerMillionTokensUsd = 0.40m,
            LongContextCacheWritePerMillionTokensUsd = 5.00m,
            LongContextOutputPerMillionTokensUsd = 18.00m
        },
        OpenAiGpt56SolPrice,
        new("gpt-5.5", 5.00m, 0.50m, 30.00m),
        new("gpt-5.4", 2.50m, 0.25m, 15.00m),
        OpenAiMiniPrice,
        new("gpt-5.4-nano", 0.20m, 0.02m, 1.25m),
        new("gpt-5.3-codex", 1.75m, 0.175m, 14.00m),
        new("chat-latest", 5.00m, 0.50m, 30.00m),
        new("gpt-5-mini", 0.25m, 0.025m, 2.00m)
    ];

    public static bool IsPrivateProvider(ProviderKind kind)
    {
        return kind is ProviderKind.Ollama or ProviderKind.ComfyUi;
    }

    public static bool ResolveIsPrivateProvider(ProviderKind kind, bool? configuredValue)
    {
        return IsPrivateProvider(kind) || configuredValue == true;
    }

    public static IReadOnlyList<ProviderModelTokenPrice> CreateDefaultPrices(
        ProviderKind kind,
        string? defaultModel)
    {
        var normalizedDefaultModel = NormalizeModelName(defaultModel);
        var prices = kind switch
        {
            ProviderKind.OpenAi or ProviderKind.AzureOpenAi => OpenAiModelPrices.ToList(),
            ProviderKind.Ollama or ProviderKind.ComfyUi => [CreatePrivateDefaultPrice(normalizedDefaultModel)],
            _ => [CreatePrivateDefaultPrice(normalizedDefaultModel)]
        };

        return EnsureModelPrice(prices, kind, normalizedDefaultModel);
    }

    public static List<ProviderModelTokenPriceEditorModel> CreateDefaultEditorModels(
        ProviderKind kind,
        string? defaultModel)
    {
        return ToEditorModels(CreateDefaultPrices(kind, defaultModel));
    }

    public static IReadOnlyList<ProviderModelTokenPrice> NormalizeModelPrices(
        ProviderKind kind,
        string? defaultModel,
        IEnumerable<ProviderModelTokenPrice>? configuredPrices)
    {
        var normalizedPrices = (configuredPrices ?? [])
            .Select(NormalizePrice)
            .Where(price => !string.IsNullOrWhiteSpace(price.Model))
            .GroupBy(price => price.Model, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToList();

        var normalizedDefaultModel = NormalizeModelName(defaultModel);

        return normalizedPrices
            .OrderBy(price => string.Equals(price.Model, normalizedDefaultModel, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(price => price.Model, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<ProviderModelTokenPrice> MergeKnownDefaultPrices(
        ProviderKind kind,
        string? defaultModel,
        IEnumerable<ProviderModelTokenPrice>? configuredPrices)
    {
        var configured = (configuredPrices ?? [])
            .Select(NormalizePrice)
            .Where(price => !string.IsNullOrWhiteSpace(price.Model))
            .GroupBy(price => price.Model, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToDictionary(price => price.Model, StringComparer.OrdinalIgnoreCase);

        foreach (var defaultPrice in CreateDefaultPrices(kind, defaultModel))
        {
            configured[defaultPrice.Model] = configured.TryGetValue(defaultPrice.Model, out var configuredPrice)
                ? EnrichKnownPrice(configuredPrice, defaultPrice)
                : defaultPrice;
        }

        return NormalizeModelPrices(kind, defaultModel, configured.Values);
    }

    public static IReadOnlyList<ProviderModelTokenPrice> MergeAuthoritativeKnownDefaultPrices(
        ProviderKind kind,
        string? defaultModel,
        IEnumerable<ProviderModelTokenPrice>? configuredPrices)
    {
        var configured = (configuredPrices ?? [])
            .Select(NormalizePrice)
            .Where(price => !string.IsNullOrWhiteSpace(price.Model))
            .GroupBy(price => price.Model, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToDictionary(price => price.Model, StringComparer.OrdinalIgnoreCase);

        foreach (var defaultPrice in CreateDefaultPrices(kind, defaultModel))
        {
            if (TryFindKnownDefaultPrice(kind, defaultPrice.Model, out _))
            {
                configured[defaultPrice.Model] = defaultPrice;
            }
        }

        return NormalizeModelPrices(kind, defaultModel, configured.Values);
    }

    public static ProviderModelPricingMergeResult MergeDiscoveredModelPrices(
        ProviderKind kind,
        string? defaultModel,
        IEnumerable<ProviderModelTokenPrice>? configuredPrices,
        IEnumerable<ProviderDiscoveredModelPrice>? discoveredPrices)
    {
        var configured = NormalizeModelPrices(kind, defaultModel, configuredPrices)
            .ToDictionary(price => price.Model, StringComparer.OrdinalIgnoreCase);
        var mergedPrices = new Dictionary<string, ProviderModelTokenPrice>(StringComparer.OrdinalIgnoreCase);
        var discoveredModels = (discoveredPrices ?? [])
            .Select(NormalizeDiscoveredPrice)
            .Where(price => !string.IsNullOrWhiteSpace(price.Model))
            .GroupBy(price => price.Model, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToList();

        var explicitPriceCount = 0;
        var modelNameOnlyCount = 0;
        foreach (var discoveredPrice in discoveredModels)
        {
            if (discoveredPrice.HasExplicitPrices)
            {
                var explicitPrice = new ProviderModelTokenPrice(
                    discoveredPrice.Model,
                    discoveredPrice.InputPerMillionTokensUsd!.Value,
                    discoveredPrice.CachedInputPerMillionTokensUsd!.Value,
                    discoveredPrice.OutputPerMillionTokensUsd!.Value);
                configured.TryGetValue(discoveredPrice.Model, out var existingPrice);
                TryFindKnownDefaultPrice(kind, discoveredPrice.Model, out var knownDefaultPrice);
                mergedPrices[discoveredPrice.Model] = PreserveOptionalPriceMetadata(
                    explicitPrice,
                    existingPrice,
                    knownDefaultPrice);
                explicitPriceCount++;
                continue;
            }

            modelNameOnlyCount++;
            if (TryFindKnownDefaultPrice(kind, discoveredPrice.Model, out var defaultPrice)) {
                mergedPrices[discoveredPrice.Model] = defaultPrice;
            } else if (configured.TryGetValue(discoveredPrice.Model, out var configuredPrice)) {
                mergedPrices[discoveredPrice.Model] = configuredPrice;
            }
        }

        return new ProviderModelPricingMergeResult(
            NormalizeModelPrices(kind, defaultModel, mergedPrices.Values),
            discoveredModels.Count,
            explicitPriceCount,
            modelNameOnlyCount);
    }

    public static ProviderModelTokenPrice CreateManualPriceTemplate(
        ProviderKind kind,
        string? model)
    {
        var normalizedModel = NormalizeModelName(model);
        if (string.IsNullOrWhiteSpace(normalizedModel))
        {
            throw new ArgumentException("Model name is required for a manual price template.", nameof(model));
        }

        return CreateDefaultPrice(kind, normalizedModel);
    }

    public static IReadOnlyList<ProviderModelTokenPrice> FromEditorModels(
        IEnumerable<ProviderModelTokenPriceEditorModel>? editorModels)
    {
        return (editorModels ?? [])
            .Select(model => new ProviderModelTokenPrice(
                model.Model,
                model.InputPerMillionTokensUsd,
                model.CachedInputPerMillionTokensUsd,
                model.OutputPerMillionTokensUsd)
            {
                TariffKind = model.TariffKind,
                CacheWritePerMillionTokensUsd = model.CacheWritePerMillionTokensUsd,
                LongContextThresholdTokens = model.LongContextThresholdTokens,
                LongContextInputPerMillionTokensUsd = model.LongContextInputPerMillionTokensUsd,
                LongContextCachedInputPerMillionTokensUsd = model.LongContextCachedInputPerMillionTokensUsd,
                LongContextCacheWritePerMillionTokensUsd = model.LongContextCacheWritePerMillionTokensUsd,
                LongContextOutputPerMillionTokensUsd = model.LongContextOutputPerMillionTokensUsd
            })
            .ToList();
    }

    public static List<ProviderModelTokenPriceEditorModel> ToEditorModels(
        IEnumerable<ProviderModelTokenPrice>? prices)
    {
        return (prices ?? [])
            .Select(price => new ProviderModelTokenPriceEditorModel
            {
                Model = price.Model,
                InputPerMillionTokensUsd = price.InputPerMillionTokensUsd,
                CachedInputPerMillionTokensUsd = price.CachedInputPerMillionTokensUsd,
                OutputPerMillionTokensUsd = price.OutputPerMillionTokensUsd,
                TariffKind = price.TariffKind,
                CacheWritePerMillionTokensUsd = price.CacheWritePerMillionTokensUsd,
                LongContextThresholdTokens = price.LongContextThresholdTokens,
                LongContextInputPerMillionTokensUsd = price.LongContextInputPerMillionTokensUsd,
                LongContextCachedInputPerMillionTokensUsd = price.LongContextCachedInputPerMillionTokensUsd,
                LongContextCacheWritePerMillionTokensUsd = price.LongContextCacheWritePerMillionTokensUsd,
                LongContextOutputPerMillionTokensUsd = price.LongContextOutputPerMillionTokensUsd
            })
            .ToList();
    }

    public static bool TryFindPrice(
        IEnumerable<ProviderModelTokenPrice>? prices,
        string? model,
        out ProviderModelTokenPrice price)
    {
        price = default!;
        var normalizedModel = NormalizeModelName(model);
        if (string.IsNullOrWhiteSpace(normalizedModel))
        {
            return false;
        }

        var match = (prices ?? [])
            .FirstOrDefault(candidate => string.Equals(candidate.Model, normalizedModel, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return false;
        }

        if (!match.HasConfiguredStandardPrice && match.TariffKind != ProviderTariffKind.ExplicitFree)
        {
            return false;
        }

        price = match;
        return true;
    }

    public static bool TryValidateModelPrices(
        IEnumerable<ProviderModelTokenPrice>? prices,
        out string validationMessage)
    {
        var seenModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var price in prices ?? [])
        {
            var normalizedModel = NormalizeModelName(price.Model);
            if (string.IsNullOrWhiteSpace(normalizedModel))
            {
                validationMessage = "Each model price row must include a model name.";
                return false;
            }

            if (!seenModels.Add(normalizedModel))
            {
                validationMessage = $"Model price row '{normalizedModel}' is duplicated.";
                return false;
            }

            if (price.InputPerMillionTokensUsd < 0m ||
                price.CachedInputPerMillionTokensUsd < 0m ||
                price.OutputPerMillionTokensUsd < 0m ||
                HasNegativeOptionalPrice(price.CacheWritePerMillionTokensUsd) ||
                HasNegativeOptionalPrice(price.LongContextInputPerMillionTokensUsd) ||
                HasNegativeOptionalPrice(price.LongContextCachedInputPerMillionTokensUsd) ||
                HasNegativeOptionalPrice(price.LongContextCacheWritePerMillionTokensUsd) ||
                HasNegativeOptionalPrice(price.LongContextOutputPerMillionTokensUsd))
            {
                validationMessage = $"Model price row '{normalizedModel}' cannot contain negative prices.";
                return false;
            }

            if (!Enum.IsDefined(price.TariffKind)
                || price.TariffKind == ProviderTariffKind.ExplicitFree && !ProviderTokenCostCalculator.HasOnlyZeroRates(price)) {
                validationMessage = "An explicitly free tariff must contain only zero rates.";
                return false;
            }

            if (!HasLongContextConfiguration(price))
            {
                continue;
            }

            if (price.LongContextThresholdTokens is not > 0)
            {
                validationMessage = $"Model price row '{normalizedModel}' must include a positive long-context threshold.";
                return false;
            }

            if (price.LongContextInputPerMillionTokensUsd is null ||
                price.LongContextCachedInputPerMillionTokensUsd is null ||
                price.LongContextOutputPerMillionTokensUsd is null)
            {
                validationMessage = $"Model price row '{normalizedModel}' must include long-context input, cached input, and output prices.";
                return false;
            }
        }

        validationMessage = string.Empty;
        return true;
    }

    public static string NormalizeModelName(string? model)
    {
        return model?.Trim() ?? string.Empty;
    }

    private static ProviderModelTokenPrice NormalizePrice(ProviderModelTokenPrice price)
    {
        return price with
        {
            Model = NormalizeModelName(price.Model)
        };
    }

    private static ProviderModelTokenPrice EnrichKnownPrice(
        ProviderModelTokenPrice configuredPrice,
        ProviderModelTokenPrice defaultPrice)
    {
        var enrichedPrice = configuredPrice.HasConfiguredStandardPrice
            ? configuredPrice
            : configuredPrice with
            {
                InputPerMillionTokensUsd = defaultPrice.InputPerMillionTokensUsd,
                CachedInputPerMillionTokensUsd = defaultPrice.CachedInputPerMillionTokensUsd,
                OutputPerMillionTokensUsd = defaultPrice.OutputPerMillionTokensUsd
            };

        return PreserveOptionalPriceMetadata(enrichedPrice, configuredPrice, defaultPrice);
    }

    private static ProviderModelTokenPrice PreserveOptionalPriceMetadata(
        ProviderModelTokenPrice price,
        ProviderModelTokenPrice? preferredMetadata,
        ProviderModelTokenPrice? fallbackMetadata)
    {
        return price with
        {
            CacheWritePerMillionTokensUsd = preferredMetadata?.CacheWritePerMillionTokensUsd ?? fallbackMetadata?.CacheWritePerMillionTokensUsd,
            LongContextThresholdTokens = preferredMetadata?.LongContextThresholdTokens ?? fallbackMetadata?.LongContextThresholdTokens,
            LongContextInputPerMillionTokensUsd = preferredMetadata?.LongContextInputPerMillionTokensUsd ?? fallbackMetadata?.LongContextInputPerMillionTokensUsd,
            LongContextCachedInputPerMillionTokensUsd = preferredMetadata?.LongContextCachedInputPerMillionTokensUsd ?? fallbackMetadata?.LongContextCachedInputPerMillionTokensUsd,
            LongContextCacheWritePerMillionTokensUsd = preferredMetadata?.LongContextCacheWritePerMillionTokensUsd ?? fallbackMetadata?.LongContextCacheWritePerMillionTokensUsd,
            LongContextOutputPerMillionTokensUsd = preferredMetadata?.LongContextOutputPerMillionTokensUsd ?? fallbackMetadata?.LongContextOutputPerMillionTokensUsd
        };
    }

    private static bool TryFindKnownDefaultPrice(
        ProviderKind kind,
        string model,
        out ProviderModelTokenPrice price)
    {
        price = default!;
        if (kind is not (ProviderKind.OpenAi or ProviderKind.AzureOpenAi))
        {
            return false;
        }

        var match = OpenAiModelPrices.FirstOrDefault(candidate =>
            string.Equals(candidate.Model, model, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return false;
        }

        price = match;
        return true;
    }

    private static ProviderDiscoveredModelPrice NormalizeDiscoveredPrice(ProviderDiscoveredModelPrice price)
    {
        return price with
        {
            Model = NormalizeModelName(price.Model)
        };
    }

    private static List<ProviderModelTokenPrice> EnsureModelPrice(
        List<ProviderModelTokenPrice> prices,
        ProviderKind kind,
        string defaultModel)
    {
        if (string.IsNullOrWhiteSpace(defaultModel) ||
            prices.Any(price => string.Equals(price.Model, defaultModel, StringComparison.OrdinalIgnoreCase)))
        {
            return prices;
        }

        prices.Add(CreateDefaultPrice(kind, defaultModel));
        return prices;
    }

    private static ProviderModelTokenPrice CreateDefaultPrice(
        ProviderKind kind,
        string model)
    {
        return kind switch
        {
            ProviderKind.OpenAi or ProviderKind.AzureOpenAi => OpenAiModelPrices.FirstOrDefault(
                price => string.Equals(price.Model, model, StringComparison.OrdinalIgnoreCase)) ??
                new ProviderModelTokenPrice(model, 0m, 0m, 0m),
            _ => CreatePrivateDefaultPrice(model)
        };
    }

    private static bool HasNegativeOptionalPrice(decimal? value)
    {
        return value is < 0m;
    }

    private static bool HasLongContextConfiguration(ProviderModelTokenPrice price)
    {
        return price.LongContextThresholdTokens.HasValue ||
               price.LongContextInputPerMillionTokensUsd.HasValue ||
               price.LongContextCachedInputPerMillionTokensUsd.HasValue ||
               price.LongContextCacheWritePerMillionTokensUsd.HasValue ||
               price.LongContextOutputPerMillionTokensUsd.HasValue;
    }

    private static ProviderModelTokenPrice CreatePrivateDefaultPrice(string model)
    {
        var normalizedModel = string.IsNullOrWhiteSpace(model)
            ? "llama3.1"
            : model;

        return new ProviderModelTokenPrice(
            normalizedModel,
            PrivateInputPerMillionTokensUsd,
            PrivateCachedInputPerMillionTokensUsd,
            PrivateOutputPerMillionTokensUsd);
    }
}

public static class ProviderPricingMetadata
{
    private const string IsPrivateProviderPropertyName = "isPrivateProvider";
    private const string ModelPricesPropertyName = "modelPrices";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static ProviderPricingMetadataSnapshot Read(string? json)
    {
        var configuration = ParseObject(json);
        return new ProviderPricingMetadataSnapshot(
            ReadNullableBool(configuration[IsPrivateProviderPropertyName]),
            ReadModelPrices(configuration[ModelPricesPropertyName]));
    }

    public static string Write(
        string? json,
        bool isPrivateProvider,
        IEnumerable<ProviderModelTokenPrice> modelPrices)
    {
        var configuration = ParseObject(json);
        configuration[IsPrivateProviderPropertyName] = isPrivateProvider;
        configuration[ModelPricesPropertyName] = JsonSerializer.SerializeToNode(modelPrices, SerializerOptions);
        return configuration.ToJsonString(SerializerOptions);
    }

    private static IReadOnlyList<ProviderModelTokenPrice> ReadModelPrices(JsonNode? node)
    {
        if (node is null)
        {
            return [];
        }

        try
        {
            if (node is JsonValue value &&
                value.TryGetValue<string>(out var rawValue) &&
                !string.IsNullOrWhiteSpace(rawValue))
            {
                return JsonSerializer.Deserialize<IReadOnlyList<ProviderModelTokenPrice>>(rawValue, SerializerOptions) ?? [];
            }

            return node.Deserialize<IReadOnlyList<ProviderModelTokenPrice>>(SerializerOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool? ReadNullableBool(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<bool>(out var boolValue))
        {
            return boolValue;
        }

        return value.TryGetValue<string>(out var stringValue) &&
               bool.TryParse(stringValue, out var parsedValue)
            ? parsedValue
            : null;
    }

    private static JsonObject ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(json)?.AsObject() ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }
}

public static class ProviderPricingSnapshot
{
    public const string Version = "provider-pricing-v2";
    public const int ProfileHashLength = 64;

    public static string CreateProfileHash(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var prices = string.Join(
            ';',
            provider.ModelPrices
                .OrderBy(price => price.Model, StringComparer.OrdinalIgnoreCase)
                .Select(price => string.Join(
                    ':',
                    price.Model.Trim().ToUpperInvariant(),
                    price.TariffKind,
                    price.InputPerMillionTokensUsd.ToString(CultureInfo.InvariantCulture),
                    price.CachedInputPerMillionTokensUsd.ToString(CultureInfo.InvariantCulture),
                    price.OutputPerMillionTokensUsd.ToString(CultureInfo.InvariantCulture),
                    FormatNullable(price.CacheWritePerMillionTokensUsd),
                    FormatNullable(price.LongContextThresholdTokens),
                    FormatNullable(price.LongContextInputPerMillionTokensUsd),
                    FormatNullable(price.LongContextCachedInputPerMillionTokensUsd),
                    FormatNullable(price.LongContextCacheWritePerMillionTokensUsd),
                    FormatNullable(price.LongContextOutputPerMillionTokensUsd))));
        var canonical = string.Join(
            '|',
            provider.Id.ToString("D"),
            provider.Kind,
            provider.Transport,
            provider.IsPrivateProvider,
            prices);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string FormatNullable<T>(T? value) where T : struct, IFormattable
    {
        return value?.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
