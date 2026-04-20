using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.AgentFramework.Models;

public sealed class AgentProcessAccessSettings
{
    public bool CanRead { get; set; }

    public bool CanWrite { get; set; }

    public bool AllowAllDefinitions { get; set; }

    public List<Guid> AllowedDefinitionIds { get; set; } = [];
}

public static class AgentProcessAccessMetadata
{
    private const string RootPropertyName = "processes";
    private const string CanReadPropertyName = "canRead";
    private const string CanWritePropertyName = "canWrite";
    private const string AllowAllDefinitionsPropertyName = "allowAllDefinitions";
    private const string AllowedDefinitionIdsPropertyName = "allowedDefinitionIds";

    public static AgentProcessAccessSettings Read(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return new AgentProcessAccessSettings();
        }

        try
        {
            var root = JsonNode.Parse(configurationJson)?.AsObject();
            var processes = root?[RootPropertyName]?.AsObject();
            if (processes is null)
            {
                return new AgentProcessAccessSettings();
            }

            var settings = new AgentProcessAccessSettings
            {
                CanRead = TryReadBoolean(processes, CanReadPropertyName),
                CanWrite = TryReadBoolean(processes, CanWritePropertyName),
                AllowAllDefinitions = TryReadBoolean(processes, AllowAllDefinitionsPropertyName)
            };

            if (processes[AllowedDefinitionIdsPropertyName] is JsonArray allowedDefinitionIds)
            {
                settings.AllowedDefinitionIds = allowedDefinitionIds
                    .Select(item => item?.GetValue<string>())
                    .Where(item => Guid.TryParse(item, out _))
                    .Select(item => Guid.Parse(item!))
                    .Distinct()
                    .ToList();
            }

            return Normalize(settings);
        }
        catch (JsonException)
        {
            return new AgentProcessAccessSettings();
        }
    }

    public static string Write(
        string? configurationJson,
        AgentProcessAccessSettings? settings)
    {
        var normalized = Normalize(settings ?? new AgentProcessAccessSettings());
        var root = ParseObject(configurationJson);

        if (!normalized.CanRead &&
            !normalized.CanWrite &&
            !normalized.AllowAllDefinitions &&
            normalized.AllowedDefinitionIds.Count == 0)
        {
            root.Remove(RootPropertyName);
            return root.ToJsonString();
        }

        root[RootPropertyName] = new JsonObject
        {
            [CanReadPropertyName] = normalized.CanRead,
            [CanWritePropertyName] = normalized.CanWrite,
            [AllowAllDefinitionsPropertyName] = normalized.AllowAllDefinitions,
            [AllowedDefinitionIdsPropertyName] = new JsonArray(
                normalized.AllowedDefinitionIds
                    .Select(definitionId => JsonValue.Create(definitionId.ToString("D")))
                    .ToArray())
        };

        return root.ToJsonString();
    }

    public static AgentProcessAccessSettings Normalize(AgentProcessAccessSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new AgentProcessAccessSettings
        {
            CanRead = settings.CanRead || settings.CanWrite,
            CanWrite = settings.CanWrite,
            AllowAllDefinitions = settings.AllowAllDefinitions,
            AllowedDefinitionIds = settings.AllowedDefinitionIds
                .Where(definitionId => definitionId != Guid.Empty)
                .Distinct()
                .OrderBy(definitionId => definitionId)
                .ToList()
        };
    }

    private static bool TryReadBoolean(JsonObject node, string propertyName)
    {
        return node[propertyName] is JsonValue value &&
               value.TryGetValue<bool>(out var parsedValue) &&
               parsedValue;
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
}
