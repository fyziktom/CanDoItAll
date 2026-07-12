using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

public static class AgentMemoryAccessMetadata
{
    private const string RootPropertyName = "memory";

    internal static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static AgentMemoryAccessSettings Read(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return new AgentMemoryAccessSettings();
        }

        try
        {
            var root = JsonNode.Parse(configurationJson) as JsonObject
                ?? throw new AgentMemoryConfigurationException("Agent configuration must be a JSON object.");
            if (!root.TryGetPropertyValue(RootPropertyName, out var memoryNode) || memoryNode is null)
            {
                return new AgentMemoryAccessSettings();
            }

            var dto = memoryNode.Deserialize<AgentMemoryConfigurationDto>(SerializerOptions)
                ?? throw new AgentMemoryConfigurationException("Agent memory configuration must be a JSON object.");
            return Normalize(AgentMemoryConfigurationMapper.FromDto(dto));
        }
        catch (AgentMemoryConfigurationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or NotSupportedException)
        {
            throw new AgentMemoryConfigurationException(
                "Agent memory configuration is invalid and cannot be used safely.",
                exception);
        }
    }

    public static string Write(
        string? configurationJson,
        AgentMemoryAccessSettings? settings)
    {
        var normalized = Normalize(settings ?? new AgentMemoryAccessSettings());
        var root = ParseRoot(configurationJson);
        if (AgentMemoryAccessNormalizer.IsDefault(normalized))
        {
            root.Remove(RootPropertyName);
            return root.ToJsonString(SerializerOptions);
        }

        root[RootPropertyName] = JsonSerializer.SerializeToNode(
            AgentMemoryConfigurationMapper.ToDto(normalized),
            SerializerOptions);
        return root.ToJsonString(SerializerOptions);
    }

    public static AgentMemoryAccessSettings Normalize(AgentMemoryAccessSettings settings)
    {
        return AgentMemoryAccessNormalizer.Normalize(settings);
    }

    private static JsonObject ParseRoot(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(configurationJson) as JsonObject
                ?? throw new AgentMemoryConfigurationException("Agent configuration must be a JSON object.");
        }
        catch (JsonException exception)
        {
            throw new AgentMemoryConfigurationException(
                "Agent configuration is invalid JSON and cannot be updated safely.",
                exception);
        }
    }
}
