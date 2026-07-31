using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using ModelCapabilityKind = CanDoItAll.AgentFramework.Models.CapabilityKind;

namespace CanDoItAll.AgentFramework.Core;

internal static class InlineSkillConfigurationNormalizer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string Normalize(ModelCapabilityKind kind, string capabilityKey, string configurationJson)
    {
        var normalizedConfiguration = configurationJson.Trim();
        if (kind != ModelCapabilityKind.Skill || string.IsNullOrWhiteSpace(normalizedConfiguration))
        {
            return normalizedConfiguration;
        }

        var root = JsonNode.Parse(normalizedConfiguration) as JsonObject
            ?? throw new ArgumentException("Skill capability configuration must be a JSON object.", nameof(configurationJson));
        if (root["inlineSkill"] is null)
        {
            return normalizedConfiguration;
        }

        if (root["inlineSkill"] is not JsonObject inlineSkill)
        {
            throw new ArgumentException("Inline skill configuration must be a JSON object.", nameof(configurationJson));
        }

        string? configuredName;
        try
        {
            configuredName = inlineSkill["name"]?.GetValue<string>();
        }
        catch (InvalidOperationException exception)
        {
            throw new ArgumentException("Inline skill name must be a string.", nameof(configurationJson), exception);
        }

        inlineSkill["name"] = SkillName.Normalize(
            string.IsNullOrWhiteSpace(configuredName) ? capabilityKey : configuredName).Value;
        return root.ToJsonString(SerializerOptions);
    }
}
