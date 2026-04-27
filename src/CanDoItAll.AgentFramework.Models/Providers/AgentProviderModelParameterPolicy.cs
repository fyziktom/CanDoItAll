namespace CanDoItAll.AgentFramework.Models;

public static class AgentProviderModelParameterPolicy
{
    private static readonly string[] OpenAiDefaultTemperatureModelPrefixes =
    [
        "gpt-5",
        "o1",
        "o3",
        "o4"
    ];

    public static bool ShouldOmitTemperature(
        ProviderKind providerKind,
        string model,
        bool forceOmitTemperature = false)
    {
        if (forceOmitTemperature)
        {
            return true;
        }

        return IsOpenAiLikeProvider(providerKind) &&
               IsOpenAiDefaultTemperatureModel(model);
    }

    public static bool IsOpenAiLikeProvider(ProviderKind providerKind)
    {
        return providerKind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi;
    }

    public static bool IsOpenAiDefaultTemperatureModel(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return false;
        }

        var normalizedModel = model.Trim().ToLowerInvariant();
        return OpenAiDefaultTemperatureModelPrefixes.Any(prefix =>
            string.Equals(normalizedModel, prefix, StringComparison.Ordinal) ||
            normalizedModel.StartsWith(prefix + "-", StringComparison.Ordinal) ||
            normalizedModel.StartsWith(prefix + ".", StringComparison.Ordinal));
    }
}
