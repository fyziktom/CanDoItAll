using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.AgentFramework.Models;

public sealed class AgentProjectStructureAccessSettings
{
    public bool CanRead { get; set; }

    public bool CanWrite { get; set; }

    public bool CanWriteTasks { get; set; }

    public bool AllowAllProjects { get; set; }

    public List<Guid> AllowedProjectIds { get; set; } = [];
}

public static class AgentProjectStructureAccessMetadata
{
    private const string RootPropertyName = "projectStructure";
    private const string CanReadPropertyName = "canRead";
    private const string CanWritePropertyName = "canWrite";
    private const string CanWriteTasksPropertyName = "canWriteTasks";
    private const string AllowAllProjectsPropertyName = "allowAllProjects";
    private const string AllowedProjectIdsPropertyName = "allowedProjectIds";

    public static AgentProjectStructureAccessSettings Read(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return new AgentProjectStructureAccessSettings();
        }

        try
        {
            var root = JsonNode.Parse(configurationJson)?.AsObject();
            var projectStructure = root?[RootPropertyName]?.AsObject();
            if (projectStructure is null)
            {
                return new AgentProjectStructureAccessSettings();
            }

            var settings = new AgentProjectStructureAccessSettings
            {
                CanRead = TryReadBoolean(projectStructure, CanReadPropertyName),
                CanWrite = TryReadBoolean(projectStructure, CanWritePropertyName),
                CanWriteTasks = TryReadBoolean(projectStructure, CanWriteTasksPropertyName),
                AllowAllProjects = TryReadBoolean(projectStructure, AllowAllProjectsPropertyName)
            };

            if (projectStructure[AllowedProjectIdsPropertyName] is JsonArray allowedProjectIds)
            {
                settings.AllowedProjectIds = ReadProjectIds(allowedProjectIds);
            }

            return Normalize(settings);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return new AgentProjectStructureAccessSettings();
        }
    }

    public static string Write(
        string? configurationJson,
        AgentProjectStructureAccessSettings? settings)
    {
        var normalized = Normalize(settings ?? new AgentProjectStructureAccessSettings());
        var root = ParseObject(configurationJson);

        if (!normalized.CanRead &&
            !normalized.CanWrite &&
            !normalized.CanWriteTasks &&
            !normalized.AllowAllProjects &&
            normalized.AllowedProjectIds.Count == 0)
        {
            root.Remove(RootPropertyName);
            return root.ToJsonString();
        }

        root[RootPropertyName] = new JsonObject
        {
            [CanReadPropertyName] = normalized.CanRead,
            [CanWritePropertyName] = normalized.CanWrite,
            [CanWriteTasksPropertyName] = normalized.CanWriteTasks,
            [AllowAllProjectsPropertyName] = normalized.AllowAllProjects,
            [AllowedProjectIdsPropertyName] = new JsonArray(
                normalized.AllowedProjectIds
                    .Select(projectId => JsonValue.Create(projectId.ToString("D")))
                    .ToArray())
        };

        return root.ToJsonString();
    }

    public static AgentProjectStructureAccessSettings Normalize(AgentProjectStructureAccessSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new AgentProjectStructureAccessSettings
        {
            CanRead = settings.CanRead || settings.CanWrite || settings.CanWriteTasks,
            CanWrite = settings.CanWrite,
            CanWriteTasks = settings.CanWriteTasks,
            AllowAllProjects = settings.AllowAllProjects,
            AllowedProjectIds = settings.AllowedProjectIds
                .Where(projectId => projectId != Guid.Empty)
                .Distinct()
                .OrderBy(projectId => projectId)
                .ToList()
        };
    }

    private static bool TryReadBoolean(JsonObject node, string propertyName)
    {
        return node[propertyName] is JsonValue value &&
               value.TryGetValue<bool>(out var parsedValue) &&
               parsedValue;
    }

    private static List<Guid> ReadProjectIds(JsonArray allowedProjectIds)
    {
        var projectIds = new HashSet<Guid>();
        foreach (var item in allowedProjectIds)
        {
            if (item is JsonValue value &&
                value.TryGetValue<string>(out var rawProjectId) &&
                Guid.TryParse(rawProjectId, out var projectId) &&
                projectId != Guid.Empty)
            {
                projectIds.Add(projectId);
            }
        }

        return projectIds.ToList();
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
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return new JsonObject();
        }
    }
}
