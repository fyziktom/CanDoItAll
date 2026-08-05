namespace CanDoItAll.AgentFramework.Models;

public static class OpenAiModelIds
{
    public const string Gpt54Mini = "gpt-5.4-mini";
    public const string GptImage1Mini = "gpt-image-1-mini";
    public const string GptImage2 = "gpt-image-2";
    public const string Gpt56 = "gpt-5.6";
    public const string Gpt56Luna = "gpt-5.6-luna";
    public const string Gpt56Terra = "gpt-5.6-terra";
    public const string Gpt56Sol = "gpt-5.6-sol";

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
        return OpenAiThinkingEffortModelRegistry.Find(normalizedModel)?.Model ?? normalizedModel;
    }
}

public static class OpenAiModelPricingPolicy
{
    public const int Gpt56LongContextThresholdTokens = 272_000;
}
