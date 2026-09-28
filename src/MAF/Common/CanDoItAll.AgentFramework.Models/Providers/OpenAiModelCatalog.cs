namespace CanDoItAll.AgentFramework.Models;

public static class OpenAiModelIds
{
    public const string Gpt54Mini = "gpt-5.4-mini";
    public const string GptImage1Mini = "gpt-image-1-mini";
    public const string GptImage2 = "gpt-image-2";
    public const string GptImage25Sunburst = "gpt-image-2.5-sunburst";
    public const string GptImage25Flare = "gpt-image-2.5-flare";
    public const string Gpt6Astra = "gpt-6-astra";
    public const string Gpt56 = "gpt-5.6";
    public const string Gpt56Luna = "gpt-5.6-luna";
    public const string Gpt56Terra = "gpt-5.6-terra";
    public const string Gpt56Sol = "gpt-5.6-sol";

    public static IReadOnlyList<string> ImageModels { get; } =
    [
        GptImage25Sunburst,
        GptImage25Flare,
        GptImage2,
        GptImage1Mini
    ];

    public static bool SupportsExtendedImageQuality(string model) {
        var normalized = NormalizeKnownModelOrSnapshot(model);
        return normalized is GptImage25Sunburst or GptImage25Flare;
    }

    public static IReadOnlyList<string> Gpt56Models { get; } =
    [
        Gpt56,
        Gpt56Luna,
        Gpt56Terra,
        Gpt56Sol
    ];

    public static string NormalizeKnownModelOrSnapshot(string? model)
    {
        var normalizedModel = model?.Trim() ?? string.Empty;
        return OpenAiThinkingEffortModelRegistry.Find(normalizedModel)?.Model
            ?? ImageModels.FirstOrDefault(candidate => OpenAiThinkingEffortModelRegistry.MatchesModelOrSnapshot(normalizedModel, candidate))
            ?? normalizedModel;
    }
}

public static class OpenAiModelPricingPolicy
{
    public const int Gpt56LongContextThresholdTokens = 272_000;
    public const int Gpt6LongContextThresholdTokens = 272_000;
}

public static class OpenAiModelSuggestions {
    private static readonly HashSet<string> MainModels = new(StringComparer.OrdinalIgnoreCase) {
        "gpt-4.1", "gpt-4.1-mini", "gpt-4.1-nano",
        "gpt-5.3-codex", "gpt-5.4", OpenAiModelIds.Gpt54Mini, "gpt-5.4-nano", "gpt-5.4-pro",
        "gpt-5.5", "gpt-5.5-pro",
        OpenAiModelIds.Gpt56, OpenAiModelIds.Gpt56Luna, OpenAiModelIds.Gpt56Terra, OpenAiModelIds.Gpt56Sol,
        OpenAiModelIds.Gpt6Astra
    };

    public static bool IsMainModel(string model) => MainModels.Contains(model);
}
