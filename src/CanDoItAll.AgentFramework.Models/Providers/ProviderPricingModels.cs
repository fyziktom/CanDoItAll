using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.AgentFramework.Models;

public sealed record ProviderModelTokenPrice(
    string Model,
    decimal InputPerMillionTokensUsd,
    decimal CachedInputPerMillionTokensUsd,
    decimal OutputPerMillionTokensUsd);

public sealed class ProviderModelTokenPriceEditorModel
{
    public string Model { get; set; } = string.Empty;

    public decimal InputPerMillionTokensUsd { get; set; }

    public decimal CachedInputPerMillionTokensUsd { get; set; }

    public decimal OutputPerMillionTokensUsd { get; set; }
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
    decimal TotalUsd);

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

    private static readonly IReadOnlyList<ProviderModelTokenPrice> OpenAiModelPrices =
    [
        new("gpt-5.5", 5.00m, 0.50m, 30.00m),
        new("gpt-5.4", 2.50m, 0.25m, 15.00m),
        OpenAiMiniPrice,
        new("gpt-5.4-nano", 0.20m, 0.02m, 1.25m),
        new("gpt-5.3-codex", 1.75m, 0.175m, 14.00m),
        new("chat-latest", 5.00m, 0.50m, 30.00m),
        new("gpt-5-mini", 0.75m, 0.075m, 4.50m)
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

        if (normalizedPrices.Count == 0)
        {
            normalizedPrices.AddRange(CreateDefaultPrices(kind, defaultModel));
        }

        var normalizedDefaultModel = NormalizeModelName(defaultModel);
        EnsureModelPrice(normalizedPrices, kind, normalizedDefaultModel);

        return normalizedPrices
            .OrderBy(price => string.Equals(price.Model, normalizedDefaultModel, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(price => price.Model, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<ProviderModelTokenPrice> FromEditorModels(
        IEnumerable<ProviderModelTokenPriceEditorModel>? editorModels)
    {
        return (editorModels ?? [])
            .Select(model => new ProviderModelTokenPrice(
                model.Model,
                model.InputPerMillionTokensUsd,
                model.CachedInputPerMillionTokensUsd,
                model.OutputPerMillionTokensUsd))
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
                OutputPerMillionTokensUsd = price.OutputPerMillionTokensUsd
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
                price.OutputPerMillionTokensUsd < 0m)
            {
                validationMessage = $"Model price row '{normalizedModel}' cannot contain negative prices.";
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
            ProviderKind.OpenAi or ProviderKind.AzureOpenAi => OpenAiMiniPrice with { Model = model },
            _ => CreatePrivateDefaultPrice(model)
        };
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

public static class ProviderPricingCalculator
{
    private const decimal TokensPerMillion = 1_000_000m;

    public static bool TryCalculate(
        AgentRunMetric metric,
        ProviderProfile provider,
        out ProviderRunCostResult cost)
    {
        ArgumentNullException.ThrowIfNull(metric);
        ArgumentNullException.ThrowIfNull(provider);

        return TryCalculate(
            provider.Name,
            metric.Model,
            metric.InputTokens,
            metric.CachedInputTokens,
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
        out ProviderRunCostResult cost)
    {
        cost = default!;
        if (!ProviderPricingDefaults.TryFindPrice(modelPrices, model, out var price))
        {
            return false;
        }

        var normalizedCachedInputTokens = Math.Clamp(cachedInputTokens, 0, Math.Max(0, inputTokens));
        var uncachedInputTokens = Math.Max(0, inputTokens - normalizedCachedInputTokens);
        var inputCost = uncachedInputTokens / TokensPerMillion * price.InputPerMillionTokensUsd;
        var cachedInputCost = normalizedCachedInputTokens / TokensPerMillion * price.CachedInputPerMillionTokensUsd;
        var outputCost = Math.Max(0, outputTokens) / TokensPerMillion * price.OutputPerMillionTokensUsd;

        cost = new ProviderRunCostResult(
            providerName,
            price.Model,
            Math.Max(0, inputTokens),
            normalizedCachedInputTokens,
            Math.Max(0, outputTokens),
            inputCost,
            cachedInputCost,
            outputCost,
            inputCost + cachedInputCost + outputCost);
        return true;
    }

    public static bool TryResolveMetricCost(
        AgentRunMetric metric,
        IEnumerable<ProviderProfile> providers,
        out decimal costUsd)
    {
        ArgumentNullException.ThrowIfNull(metric);
        ArgumentNullException.ThrowIfNull(providers);

        if (metric.CostUsd > 0m)
        {
            costUsd = metric.CostUsd;
            return true;
        }

        var provider = providers.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, metric.ProviderName, StringComparison.OrdinalIgnoreCase));
        if (provider is not null && TryCalculate(metric, provider, out var calculatedCost))
        {
            costUsd = calculatedCost.TotalUsd;
            return true;
        }

        costUsd = 0m;
        return false;
    }

    public static bool TryResolveObservationCost(
        ProviderUsageObservation observation,
        IEnumerable<ProviderProfile> providers,
        out decimal costUsd)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(providers);

        if (!IsKnownUsageStatus(observation.UsageStatus))
        {
            costUsd = 0m;
            return false;
        }

        if (observation.ProviderCostUsd is > 0m)
        {
            costUsd = observation.ProviderCostUsd.Value;
            return true;
        }

        if (observation.CalculatedCostUsd is > 0m)
        {
            costUsd = observation.CalculatedCostUsd.Value;
            return true;
        }

        var provider = providers.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, observation.ProviderName, StringComparison.OrdinalIgnoreCase));
        if (provider is not null &&
            TryCalculate(
                provider.Name,
                observation.Model,
                observation.InputTokens,
                observation.CachedInputTokens,
                observation.OutputTokens,
                provider.ModelPrices,
                out var calculatedCost))
        {
            costUsd = calculatedCost.TotalUsd;
            return true;
        }

        costUsd = 0m;
        return false;
    }

    public static ProviderUsageSummary SummarizeUsage(
        IEnumerable<ProviderUsageObservation> observations,
        IEnumerable<ProviderProfile> providers)
    {
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
            TotalTokens: knownItems.Sum(item => item.TotalTokens),
            KnownCostUsd: decimal.Round(knownCost, 6, MidpointRounding.AwayFromZero));
    }

    public static decimal SumKnownCosts(IEnumerable<AgentRunMetric> metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        return metrics.Sum(metric => metric.CostUsd);
    }

    public static bool IsKnownUsageStatus(ProviderUsageObservationStatus status)
    {
        return status is ProviderUsageObservationStatus.Observed
            or ProviderUsageObservationStatus.ObservedFromMetric;
    }
}
