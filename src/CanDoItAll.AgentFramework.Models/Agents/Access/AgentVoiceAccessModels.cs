using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.AgentFramework.Models;

public sealed class AgentVoiceAccessSettings
{
    public bool CanUseVoiceMode { get; set; }

    public string PreferredVoiceId { get; set; } = string.Empty;
}

public static class AgentVoiceAccessMetadata
{
    private const string RootPropertyName = "voiceAccess";
    private const string CanUseVoiceModePropertyName = "canUseVoiceMode";
    private const string PreferredVoiceIdPropertyName = "preferredVoiceId";

    public static AgentVoiceAccessSettings Read(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return new AgentVoiceAccessSettings();
        }

        try
        {
            var root = JsonNode.Parse(configurationJson)?.AsObject();
            var voiceAccess = root?[RootPropertyName]?.AsObject();
            if (voiceAccess is null)
            {
                return new AgentVoiceAccessSettings();
            }

            return Normalize(new AgentVoiceAccessSettings
            {
                CanUseVoiceMode = TryReadBoolean(voiceAccess, CanUseVoiceModePropertyName),
                PreferredVoiceId = TryReadString(voiceAccess, PreferredVoiceIdPropertyName)
            });
        }
        catch (JsonException)
        {
            return new AgentVoiceAccessSettings();
        }
    }

    public static string Write(
        string? configurationJson,
        AgentVoiceAccessSettings? settings)
    {
        var normalized = Normalize(settings ?? new AgentVoiceAccessSettings());
        var root = ParseObject(configurationJson);

        if (!normalized.CanUseVoiceMode &&
            string.IsNullOrWhiteSpace(normalized.PreferredVoiceId))
        {
            root.Remove(RootPropertyName);
            return root.ToJsonString();
        }

        root[RootPropertyName] = new JsonObject
        {
            [CanUseVoiceModePropertyName] = normalized.CanUseVoiceMode,
            [PreferredVoiceIdPropertyName] = normalized.PreferredVoiceId
        };

        return root.ToJsonString();
    }

    public static AgentVoiceAccessSettings Normalize(AgentVoiceAccessSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new AgentVoiceAccessSettings
        {
            CanUseVoiceMode = settings.CanUseVoiceMode,
            PreferredVoiceId = settings.CanUseVoiceMode
                ? NormalizeText(settings.PreferredVoiceId)
                : string.Empty
        };
    }

    private static bool TryReadBoolean(JsonObject node, string propertyName)
    {
        return node[propertyName] is JsonValue value &&
               value.TryGetValue<bool>(out var parsedValue) &&
               parsedValue;
    }

    private static string TryReadString(JsonObject node, string propertyName)
    {
        return node[propertyName] is JsonValue value &&
               value.TryGetValue<string>(out var parsedValue)
            ? NormalizeText(parsedValue)
            : string.Empty;
    }

    private static JsonObject ParseObject(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(configurationJson)?.AsObject() ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }
}
