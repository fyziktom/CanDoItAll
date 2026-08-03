using System.Globalization;
using System.Text.Json;

namespace CanDoItAll.AgentFramework.Models;

public static class AgentProviderModelParameterPolicy
{
    private const int DefaultMaxOutputTokens = 8192;

    public const string ModelParametersConfigurationPropertyName = AgentThinkingEffortPolicy.ModelParametersConfigurationPropertyName;
    public const string ReasoningEffortConfigurationPropertyName = AgentThinkingEffortPolicy.ReasoningEffortConfigurationPropertyName;
    public const string MaxOutputTokensConfigurationPropertyName = "maxOutputTokens";
    public const string OllamaNumPredictConfigurationPropertyName = "numPredict";
    public const string OllamaNumPredictSnakeConfigurationPropertyName = "num_predict";
    public const int DefaultOllamaMaxOutputTokens = DefaultMaxOutputTokens;

    private const int MinMaxOutputTokens = 1;
    private const int OpenAiMaxOutputTokens = 128_000;

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

    public static AgentReasoningEffortLevel? ResolveReasoningEffort(
        ProviderKind providerKind,
        ProviderTransportKind providerTransport,
        string model,
        string providerConfigurationJson,
        string agentConfigurationJson)
    {
        return AgentThinkingEffortPolicy.ResolveDefinedEffectiveEffort(
            providerKind,
            providerTransport,
            model,
            providerConfigurationJson,
            agentConfigurationJson);
    }

    public static AgentReasoningEffortLevel? ResolveConfiguredReasoningEffort(
        ProviderKind providerKind,
        string model,
        string providerConfigurationJson,
        string agentConfigurationJson)
    {
        if (!IsOpenAiLikeProvider(providerKind))
        {
            return null;
        }

        return AgentThinkingEffortPolicy.ReadConfiguredEffort(
                   agentConfigurationJson,
                   "agent",
                   includeLegacyOllamaThink: false) ??
               AgentThinkingEffortPolicy.ReadConfiguredEffort(
                   providerConfigurationJson,
                   "provider",
                   includeLegacyOllamaThink: false);
    }

    public static bool CanApplyReasoningEffort(
        ProviderKind providerKind,
        ProviderTransportKind providerTransport,
        string model)
    {
        return IsOpenAiLikeProvider(providerKind) &&
               AgentThinkingEffortPolicy.ResolveDefinedCapability(providerKind, providerTransport, model).Status ==
               AgentThinkingEffortSupportStatus.Supported;
    }

    public static int? ResolveMaxOutputTokens(
        ProviderKind providerKind,
        string providerConfigurationJson,
        string agentConfigurationJson)
    {
        return ResolveMaxOutputTokens(
            providerKind,
            model: string.Empty,
            providerConfigurationJson,
            agentConfigurationJson);
    }

    public static int? ResolveMaxOutputTokens(
        ProviderKind providerKind,
        string model,
        string providerConfigurationJson,
        string agentConfigurationJson)
    {
        var maximum = ResolveMaxOutputTokenLimit(providerKind, model);
        return TryReadMaxOutputTokens(agentConfigurationJson, "agent", providerKind, maximum) ??
               TryReadMaxOutputTokens(providerConfigurationJson, "provider", providerKind, maximum);
    }

    public static int ResolveOllamaMaxOutputTokensOrDefault(
        string providerConfigurationJson,
        string agentConfigurationJson)
    {
        return ResolveMaxOutputTokens(
                   ProviderKind.Ollama,
                   model: string.Empty,
                   providerConfigurationJson,
                   agentConfigurationJson) ??
               DefaultOllamaMaxOutputTokens;
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

        return AgentThinkingEffortPolicy.IsDefinedOpenAiReasoningModel(model);
    }

    public static string FormatReasoningEffort(AgentReasoningEffortLevel effort)
    {
        return AgentThinkingEffortPolicy.FormatEffort(effort);
    }

    private static int? TryReadMaxOutputTokens(
        string configurationJson,
        string configurationOwner,
        ProviderKind providerKind,
        int maximum)
    {
        if (string.IsNullOrWhiteSpace(configurationJson) ||
            !ConfigurationMayContainMaxOutputTokens(configurationJson, providerKind))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (TryGetPropertyIgnoreCase(root, ModelParametersConfigurationPropertyName, out var modelParametersElement))
            {
                if (modelParametersElement.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidOperationException(
                        $"The {configurationOwner} model-parameter configuration property '{ModelParametersConfigurationPropertyName}' must be a JSON object.");
                }

                var nestedValue = TryReadMaxOutputTokensProperty(
                    modelParametersElement,
                    configurationOwner,
                    providerKind,
                    maximum);
                if (nestedValue is not null)
                {
                    return nestedValue;
                }
            }

            return TryReadMaxOutputTokensProperty(root, configurationOwner, providerKind, maximum);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"The {configurationOwner} model-parameter configuration contains output-token settings but is not valid JSON.",
                exception);
        }
    }

    private static bool ConfigurationMayContainMaxOutputTokens(
        string configurationJson,
        ProviderKind providerKind)
    {
        return configurationJson.Contains(MaxOutputTokensConfigurationPropertyName, StringComparison.OrdinalIgnoreCase) ||
               (providerKind == ProviderKind.Ollama &&
                (configurationJson.Contains(OllamaNumPredictConfigurationPropertyName, StringComparison.OrdinalIgnoreCase) ||
                 configurationJson.Contains(OllamaNumPredictSnakeConfigurationPropertyName, StringComparison.OrdinalIgnoreCase)));
    }

    private static int? TryReadMaxOutputTokensProperty(
        JsonElement element,
        string configurationOwner,
        ProviderKind providerKind,
        int maximum)
    {
        if (TryReadIntegerProperty(
                element,
                MaxOutputTokensConfigurationPropertyName,
                configurationOwner,
                maximum,
                out var maxOutputTokens))
        {
            return maxOutputTokens;
        }

        if (providerKind != ProviderKind.Ollama)
        {
            return null;
        }

        if (TryReadIntegerProperty(
                element,
                OllamaNumPredictConfigurationPropertyName,
                configurationOwner,
                maximum,
                out var numPredict))
        {
            return numPredict;
        }

        return TryReadIntegerProperty(
            element,
            OllamaNumPredictSnakeConfigurationPropertyName,
            configurationOwner,
            maximum,
            out numPredict)
            ? numPredict
            : null;
    }

    private static bool TryReadIntegerProperty(
        JsonElement element,
        string propertyName,
        string configurationOwner,
        int maximum,
        out int value)
    {
        value = default;
        if (!TryGetPropertyIgnoreCase(element, propertyName, out var valueElement))
        {
            return false;
        }

        value = valueElement.ValueKind switch
        {
            JsonValueKind.Number when valueElement.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(
                valueElement.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var number) => number,
            _ => throw new InvalidOperationException(
                $"The {configurationOwner} model-parameter configuration property '{propertyName}' must be an integer.")
        };
        if (value is < MinMaxOutputTokens || value > maximum)
        {
            throw new InvalidOperationException(
                $"The {configurationOwner} model-parameter configuration property '{propertyName}' must be between {MinMaxOutputTokens} and {maximum}.");
        }

        return true;
    }

    private static int ResolveMaxOutputTokenLimit(
        ProviderKind providerKind,
        string model)
    {
        return IsOpenAiLikeProvider(providerKind) && IsOpenAiGpt5Model(model)
            ? OpenAiMaxOutputTokens
            : DefaultMaxOutputTokens;
    }

    private static bool IsOpenAiGpt5Model(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return false;
        }

        var normalizedModel = model.Trim().ToLowerInvariant();
        return string.Equals(normalizedModel, "gpt-5", StringComparison.Ordinal) ||
               normalizedModel.StartsWith("gpt-5-", StringComparison.Ordinal) ||
               normalizedModel.StartsWith("gpt-5.", StringComparison.Ordinal);
    }

    private static bool TryGetPropertyIgnoreCase(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }

}
