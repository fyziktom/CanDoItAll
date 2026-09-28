using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Voice settings of an agent, the <c>voiceAccess</c> member of the agent editor form, stored in the agent's
/// <c>configurationJson</c> under <c>voiceAccess</c>. A save replaces that section, and an omitted or empty object
/// removes it. They affect the voice features of the chat interface only; they attach no runtime tools.
/// </summary>
public sealed class AgentVoiceAccessSettings
{
    /// <summary>Enables voice mode for this agent in the chat interface, including spoken replies.</summary>
    public bool CanUseVoiceMode { get; set; }

    /// <summary>
    /// Text-to-speech voice identifier to use for this agent instead of the global voice; trimmed, and empty when voice
    /// mode is off or no preference is set. It is not validated when saved.
    /// </summary>
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
