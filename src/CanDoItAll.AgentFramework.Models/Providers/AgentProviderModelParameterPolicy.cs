using System.Text.Json;

namespace CanDoItAll.AgentFramework.Models;

public static class AgentProviderModelParameterPolicy
{
    public const string ModelParametersConfigurationPropertyName = "modelParameters";
    public const string ReasoningEffortConfigurationPropertyName = "reasoningEffort";

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
