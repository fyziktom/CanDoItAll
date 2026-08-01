using System.Globalization;
using System.Text.Json;

namespace CanDoItAll.AgentFramework.Models;

public static class AgentProviderModelParameterPolicy
{
    public const string ModelParametersConfigurationPropertyName = "modelParameters";
    public const string ReasoningEffortConfigurationPropertyName = "reasoningEffort";
    public const string MaxOutputTokensConfigurationPropertyName = "maxOutputTokens";
    public const string OllamaNumPredictConfigurationPropertyName = "numPredict";
    public const string OllamaNumPredictSnakeConfigurationPropertyName = "num_predict";
    public const string OllamaThinkConfigurationPropertyName = "think";
    public const int DefaultOllamaMaxOutputTokens = 2048;
    public const bool DefaultOllamaThinkEnabled = false;

    private const int MinMaxOutputTokens = 1;
    private const int DefaultMaxOutputTokens = 8192;
    private const int OpenAiMaxOutputTokens = 128_000;

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

    public static AgentReasoningEffortLevel? ResolveReasoningEffort(
        ProviderKind providerKind,
        ProviderTransportKind providerTransport,
        string model,
        string providerConfigurationJson,
        string agentConfigurationJson)
    {
        if (!CanApplyReasoningEffort(providerKind, providerTransport, model))
        {
            return null;
        }

        return ResolveConfiguredReasoningEffort(
            providerKind,
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
        if (!IsOpenAiLikeProvider(providerKind) ||
            !IsOpenAiDefaultTemperatureModel(model))
        {
            return null;
        }

        var configuredEffort = TryReadReasoningEffort(agentConfigurationJson, "agent") ??
                               TryReadReasoningEffort(providerConfigurationJson, "provider");
        if (configuredEffort == AgentReasoningEffortLevel.Max &&
            !OpenAiModelIds.Gpt56Models.Contains(model.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Reasoning effort 'max' is only supported by GPT-5.6 models. Model '{model}' does not support it.");
        }

        return configuredEffort;
    }

    public static bool CanApplyReasoningEffort(
        ProviderKind providerKind,
        ProviderTransportKind providerTransport,
        string model)
    {
        return IsOpenAiLikeProvider(providerKind) &&
               providerTransport is ProviderTransportKind.Responses or ProviderTransportKind.ChatCompletions &&
               IsOpenAiDefaultTemperatureModel(model);
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

    public static bool? ResolveOllamaThink(
        string providerConfigurationJson,
        string agentConfigurationJson)
    {
        return TryReadOllamaThink(agentConfigurationJson, "agent") ??
               TryReadOllamaThink(providerConfigurationJson, "provider");
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

    public static bool ResolveOllamaThinkOrDefault(
        string providerConfigurationJson,
        string agentConfigurationJson)
    {
        return ResolveOllamaThink(providerConfigurationJson, agentConfigurationJson) ??
               DefaultOllamaThinkEnabled;
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

    public static string FormatReasoningEffort(AgentReasoningEffortLevel effort)
    {
        return effort switch
        {
            AgentReasoningEffortLevel.None => "none",
            AgentReasoningEffortLevel.Low => "low",
            AgentReasoningEffortLevel.Medium => "medium",
            AgentReasoningEffortLevel.High => "high",
            AgentReasoningEffortLevel.ExtraHigh => "xhigh",
            AgentReasoningEffortLevel.Max => "max",
            _ => throw new ArgumentOutOfRangeException(nameof(effort), effort, "Unsupported reasoning effort.")
        };
    }

    private static AgentReasoningEffortLevel? TryReadReasoningEffort(
        string configurationJson,
        string configurationOwner)
    {
        if (string.IsNullOrWhiteSpace(configurationJson) ||
            !configurationJson.Contains(ReasoningEffortConfigurationPropertyName, StringComparison.OrdinalIgnoreCase))
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

            if (TryGetPropertyIgnoreCase(root, ModelParametersConfigurationPropertyName, out var modelParametersElement) &&
                modelParametersElement.ValueKind == JsonValueKind.Object &&
                TryReadReasoningEffortProperty(modelParametersElement, out var nestedEffort))
            {
                return nestedEffort;
            }

            return TryReadReasoningEffortProperty(root, out var rootEffort)
                ? rootEffort
                : null;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"The {configurationOwner} model-parameter configuration contains '{ReasoningEffortConfigurationPropertyName}' but is not valid JSON.",
                exception);
        }
    }

    private static bool? TryReadOllamaThink(
        string configurationJson,
        string configurationOwner)
    {
        if (string.IsNullOrWhiteSpace(configurationJson) ||
            !configurationJson.Contains(OllamaThinkConfigurationPropertyName, StringComparison.OrdinalIgnoreCase))
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

                var nestedValue = TryReadBooleanProperty(
                    modelParametersElement,
                    OllamaThinkConfigurationPropertyName,
                    configurationOwner);
                if (nestedValue is not null)
                {
                    return nestedValue;
                }
            }

            return TryReadBooleanProperty(root, OllamaThinkConfigurationPropertyName, configurationOwner);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"The {configurationOwner} model-parameter configuration contains '{OllamaThinkConfigurationPropertyName}' but is not valid JSON.",
                exception);
        }
    }

    private static bool? TryReadBooleanProperty(
        JsonElement element,
        string propertyName,
        string configurationOwner)
    {
        if (!TryGetPropertyIgnoreCase(element, propertyName, out var valueElement))
        {
            return null;
        }

        return valueElement.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(valueElement.GetString(), out var value) => value,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => throw new InvalidOperationException(
                $"The {configurationOwner} model-parameter configuration property '{propertyName}' must be a boolean.")
        };
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

    private static bool TryReadReasoningEffortProperty(
        JsonElement element,
        out AgentReasoningEffortLevel effort)
    {
        effort = default;
        if (!TryGetPropertyIgnoreCase(element, ReasoningEffortConfigurationPropertyName, out var effortElement))
        {
            return false;
        }

        var value = effortElement.ValueKind switch
        {
            JsonValueKind.String => effortElement.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => effortElement.ToString()
        };
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        effort = ParseReasoningEffort(value);
        return true;
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

    private static AgentReasoningEffortLevel ParseReasoningEffort(string value)
    {
        var normalizedValue = value.Trim().Replace('_', '-').ToLowerInvariant();
        return normalizedValue switch
        {
            "none" => AgentReasoningEffortLevel.None,
            "low" => AgentReasoningEffortLevel.Low,
            "medium" => AgentReasoningEffortLevel.Medium,
            "high" => AgentReasoningEffortLevel.High,
            "extra-high" or "extrahigh" or "x-high" or "xhigh" => AgentReasoningEffortLevel.ExtraHigh,
            "max" => AgentReasoningEffortLevel.Max,
            _ => throw new InvalidOperationException(
                $"Unsupported reasoning effort '{value}'. Supported values are none, low, medium, high, extraHigh, and max.")
        };
    }
}
