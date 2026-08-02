using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.AgentFramework.Models;

public static class AgentThinkingEffortConfiguration
{
    public const string ModelParametersPropertyName = "modelParameters";
    public const string ReasoningEffortPropertyName = "reasoningEffort";
    public const string LegacyOllamaThinkPropertyName = "think";

    public static AgentReasoningEffortLevel? Read(
        string? configurationJson,
        string configurationOwner)
    {
        return Read(
            configurationJson,
            configurationOwner,
            includeLegacyOllamaThink: true);
    }

    internal static AgentReasoningEffortLevel? Read(
        string? configurationJson,
        string configurationOwner,
        bool includeLegacyOllamaThink)
    {
        if (string.IsNullOrWhiteSpace(configurationJson) ||
            !MayContainValue(configurationJson, includeLegacyOllamaThink))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    $"The {configurationOwner} model-parameter configuration must be a JSON object.");
            }

            var hasModelParameters = TryGetProperty(root, ModelParametersPropertyName, out var modelParameters);
            if (hasModelParameters && modelParameters.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    $"The {configurationOwner} model-parameter configuration property '{ModelParametersPropertyName}' must be a JSON object.");
            }

            if (hasModelParameters && TryReadEffort(modelParameters, configurationOwner, out var nestedEffort))
            {
                return nestedEffort;
            }

            if (TryReadEffort(root, configurationOwner, out var rootEffort))
            {
                return rootEffort;
            }

            if (!includeLegacyOllamaThink)
            {
                return null;
            }

            if (hasModelParameters && TryReadLegacyThink(modelParameters, configurationOwner, out var nestedThink))
            {
                return nestedThink;
            }

            return TryReadLegacyThink(root, configurationOwner, out var rootThink)
                ? rootThink
                : null;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"The {configurationOwner} model-parameter configuration contains thinking-effort settings but is not valid JSON.",
                exception);
        }
    }

    public static string WriteAgentOverride(
        string? configurationJson,
        AgentReasoningEffortLevel? effort)
    {
        return Write(configurationJson, effort, "agent");
    }

    public static string WriteProviderDefault(
        string? configurationJson,
        AgentReasoningEffortLevel? effort)
    {
        return Write(configurationJson, effort, "provider");
    }

    private static string Write(
        string? configurationJson,
        AgentReasoningEffortLevel? effort,
        string configurationOwner)
    {
        var root = ParseObject(configurationJson, configurationOwner);
        var modelParameters = ExtractModelParameters(root, configurationOwner);

        RemoveProperty(root, ReasoningEffortPropertyName);
        RemoveProperty(root, LegacyOllamaThinkPropertyName);
        if (modelParameters is not null)
        {
            RemoveProperty(modelParameters, ReasoningEffortPropertyName);
            RemoveProperty(modelParameters, LegacyOllamaThinkPropertyName);
        }

        if (effort is not null)
        {
            modelParameters ??= [];
            modelParameters[ReasoningEffortPropertyName] = Format(effort.Value);
            root[ModelParametersPropertyName] = modelParameters;
        }
        else if (modelParameters is { Count: 0 })
        {
            root.Remove(ModelParametersPropertyName);
        }

        return root.ToJsonString();
    }

    public static string Format(AgentReasoningEffortLevel effort)
    {
        return effort switch
        {
            AgentReasoningEffortLevel.None => "none",
            AgentReasoningEffortLevel.Minimal => "minimal",
            AgentReasoningEffortLevel.Low => "low",
            AgentReasoningEffortLevel.Medium => "medium",
            AgentReasoningEffortLevel.High => "high",
            AgentReasoningEffortLevel.ExtraHigh => "xhigh",
            AgentReasoningEffortLevel.Max => "max",
            _ => throw new ArgumentOutOfRangeException(nameof(effort), effort, "Unsupported thinking effort.")
        };
    }

    private static bool TryReadEffort(
        JsonElement element,
        string configurationOwner,
        out AgentReasoningEffortLevel? effort)
    {
        effort = null;
        if (!TryGetProperty(element, ReasoningEffortPropertyName, out var effortElement))
        {
            return false;
        }

        if (effortElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (effortElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(effortElement.GetString()))
        {
            throw new InvalidOperationException(
                $"The {configurationOwner} model-parameter configuration property '{ReasoningEffortPropertyName}' must be a supported string value.");
        }

        effort = Parse(effortElement.GetString()!);
        return true;
    }

    private static bool TryReadLegacyThink(
        JsonElement element,
        string configurationOwner,
        out AgentReasoningEffortLevel? effort)
    {
        effort = null;
        if (!TryGetProperty(element, LegacyOllamaThinkPropertyName, out var thinkElement))
        {
            return false;
        }

        if (thinkElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        var think = thinkElement.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(thinkElement.GetString(), out var value) => value,
            _ => throw new InvalidOperationException(
                $"The {configurationOwner} model-parameter configuration property '{LegacyOllamaThinkPropertyName}' must be a boolean.")
        };
        effort = think ? AgentReasoningEffortLevel.Medium : AgentReasoningEffortLevel.None;
        return true;
    }

    private static AgentReasoningEffortLevel Parse(string value)
    {
        return value.Trim().Replace('_', '-').ToLowerInvariant() switch
        {
            "none" => AgentReasoningEffortLevel.None,
            "minimal" => AgentReasoningEffortLevel.Minimal,
            "low" => AgentReasoningEffortLevel.Low,
            "medium" => AgentReasoningEffortLevel.Medium,
            "high" => AgentReasoningEffortLevel.High,
            "extra-high" or "extrahigh" or "x-high" or "xhigh" => AgentReasoningEffortLevel.ExtraHigh,
            "max" => AgentReasoningEffortLevel.Max,
            _ => throw new InvalidOperationException(
                $"Unsupported thinking effort '{value}'. Supported values are none, minimal, low, medium, high, extraHigh, and max.")
        };
    }

    private static JsonObject ParseObject(string? configurationJson, string configurationOwner)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return [];
        }

        try
        {
            return JsonNode.Parse(configurationJson) as JsonObject
                ?? throw new InvalidOperationException(
                    $"The {configurationOwner} model-parameter configuration must be a JSON object.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"The {configurationOwner} model-parameter configuration is not valid JSON.",
                exception);
        }
    }

    private static JsonObject? ExtractModelParameters(
        JsonObject root,
        string configurationOwner)
    {
        var propertyName = FindPropertyName(root, ModelParametersPropertyName);
        if (propertyName is null)
        {
            return null;
        }

        if (root[propertyName] is not JsonObject modelParameters)
        {
            throw new InvalidOperationException(
                $"The {configurationOwner} model-parameter configuration property '{ModelParametersPropertyName}' must be a JSON object.");
        }

        root.Remove(propertyName);
        root[ModelParametersPropertyName] = modelParameters;
        return modelParameters;
    }

    private static string? FindPropertyName(JsonObject node, string propertyName)
    {
        return node
            .Select(item => item.Key)
            .FirstOrDefault(key => string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase));
    }

    private static void RemoveProperty(JsonObject node, string propertyName)
    {
        foreach (var key in node
                     .Select(item => item.Key)
                     .Where(key => string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            node.Remove(key);
        }
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
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

    private static bool MayContainValue(
        string configurationJson,
        bool includeLegacyOllamaThink)
    {
        return configurationJson.Contains(ReasoningEffortPropertyName, StringComparison.OrdinalIgnoreCase) ||
               includeLegacyOllamaThink &&
               configurationJson.Contains($"\"{LegacyOllamaThinkPropertyName}\"", StringComparison.OrdinalIgnoreCase);
    }
}
