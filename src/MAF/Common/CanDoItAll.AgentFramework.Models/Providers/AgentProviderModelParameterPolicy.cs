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

    private const int MinMaxOutputTokens = 1;
    private const int MaxMaxOutputTokens = 8192;

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

        return TryReadReasoningEffort(agentConfigurationJson, "agent") ??
               TryReadReasoningEffort(providerConfigurationJson, "provider");
    }

    public static bool CanApplyReasoningEffort(
        ProviderKind providerKind,
        ProviderTransportKind providerTransport,
        string model)
    {
        return IsOpenAiLikeProvider(providerKind) &&
               providerTransport == ProviderTransportKind.Responses &&
               IsOpenAiDefaultTemperatureModel(model);
    }

    public static int? ResolveMaxOutputTokens(
        ProviderKind providerKind,
        string providerConfigurationJson,
        string agentConfigurationJson)
    {
        return TryReadMaxOutputTokens(agentConfigurationJson, "agent", providerKind) ??
               TryReadMaxOutputTokens(providerConfigurationJson, "provider", providerKind);
    }

    public static bool? ResolveOllamaThink(
        string providerConfigurationJson,
        string agentConfigurationJson)
    {
        return TryReadOllamaThink(agentConfigurationJson, "agent") ??
               TryReadOllamaThink(providerConfigurationJson, "provider");
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
        ProviderKind providerKind)
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

                var nestedValue = TryReadMaxOutputTokensProperty(modelParametersElement, configurationOwner, providerKind);
                if (nestedValue is not null)
                {
                    return nestedValue;
                }
            }

            return TryReadMaxOutputTokensProperty(root, configurationOwner, providerKind);
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
        ProviderKind providerKind)
    {
        if (TryReadIntegerProperty(
                element,
                MaxOutputTokensConfigurationPropertyName,
                configurationOwner,
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
                out var numPredict))
        {
            return numPredict;
        }

        return TryReadIntegerProperty(
            element,
            OllamaNumPredictSnakeConfigurationPropertyName,
            configurationOwner,
            out numPredict)
            ? numPredict
            : null;
    }

    private static bool TryReadIntegerProperty(
        JsonElement element,
        string propertyName,
        string configurationOwner,
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
        if (value is < MinMaxOutputTokens or > MaxMaxOutputTokens)
        {
            throw new InvalidOperationException(
                $"The {configurationOwner} model-parameter configuration property '{propertyName}' must be between {MinMaxOutputTokens} and {MaxMaxOutputTokens}.");
        }

        return true;
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
            _ => throw new InvalidOperationException(
                $"Unsupported reasoning effort '{value}'. Supported values are none, low, medium, high, and extraHigh.")
        };
    }
}
