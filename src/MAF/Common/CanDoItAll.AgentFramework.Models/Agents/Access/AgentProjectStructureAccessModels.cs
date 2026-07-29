using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.AgentFramework.Models;

public sealed class AgentProjectStructureAccessSettings
{
    public bool CanRead { get; set; }

    public bool CanWrite { get; set; }

    public bool CanWriteNonTaskStructure { get; set; }

    public bool CanWriteTasks { get; set; }

    public bool CanCreateProjects { get; set; }

    public bool CanCreateSubprojects { get; set; }

    public bool AllowAllProjects { get; set; }

    public List<Guid> AllowedProjectIds { get; set; } = [];
}

public static class AgentProjectStructureAccessMetadata
{
    private const string RootPropertyName = "projectStructure";
    private const string CanReadPropertyName = "canRead";
    private const string CanWritePropertyName = "canWrite";
    private const string CanWriteNonTaskStructurePropertyName = "canWriteNonTaskStructure";
    private const string CanWriteTasksPropertyName = "canWriteTasks";
    private const string CanCreateProjectsPropertyName = "canCreateProjects";
    private const string CanCreateSubprojectsPropertyName = "canCreateSubprojects";
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

            var canWrite = TryReadBoolean(projectStructure, CanWritePropertyName);
            var canWriteNonTaskStructure = TryReadBoolean(projectStructure, CanWriteNonTaskStructurePropertyName);
            var legacyCanCreateProjects = canWrite || canWriteNonTaskStructure;
            var settings = new AgentProjectStructureAccessSettings
            {
                CanRead = TryReadBoolean(projectStructure, CanReadPropertyName),
                CanWrite = canWrite,
                CanWriteNonTaskStructure = canWriteNonTaskStructure,
                CanWriteTasks = TryReadBoolean(projectStructure, CanWriteTasksPropertyName),
                CanCreateProjects = ReadBooleanOrLegacyDefault(
                    projectStructure,
                    CanCreateProjectsPropertyName,
                    legacyCanCreateProjects),
                CanCreateSubprojects = ReadBooleanOrLegacyDefault(
                    projectStructure,
                    CanCreateSubprojectsPropertyName,
                    legacyCanCreateProjects),
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
            !normalized.CanWriteNonTaskStructure &&
            !normalized.CanWriteTasks &&
            !normalized.CanCreateProjects &&
            !normalized.CanCreateSubprojects &&
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
            [CanWriteNonTaskStructurePropertyName] = normalized.CanWriteNonTaskStructure,
            [CanWriteTasksPropertyName] = normalized.CanWriteTasks,
            [CanCreateProjectsPropertyName] = normalized.CanCreateProjects,
            [CanCreateSubprojectsPropertyName] = normalized.CanCreateSubprojects,
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

        List<Guid> allowedProjectIds = settings.AllowAllProjects
            ? []
            : settings.AllowedProjectIds
                .Where(projectId => projectId != Guid.Empty)
                .Distinct()
                .OrderBy(projectId => projectId)
                .ToList();

        return new AgentProjectStructureAccessSettings
        {
            CanRead = settings.CanRead ||
                settings.CanWrite ||
                settings.CanWriteNonTaskStructure ||
                settings.CanWriteTasks ||
                settings.CanCreateProjects ||
                settings.CanCreateSubprojects ||
                settings.AllowAllProjects ||
                allowedProjectIds.Count > 0,
            CanWrite = settings.CanWrite,
            CanWriteNonTaskStructure = settings.CanWriteNonTaskStructure,
            CanWriteTasks = settings.CanWriteTasks,
            CanCreateProjects = settings.CanCreateProjects,
            CanCreateSubprojects = settings.CanCreateSubprojects,
            AllowAllProjects = settings.AllowAllProjects,
            AllowedProjectIds = allowedProjectIds
        };
    }

    private static bool TryReadBoolean(JsonObject node, string propertyName)
    {
        return node[propertyName] is JsonValue value &&
               value.TryGetValue<bool>(out var parsedValue) &&
               parsedValue;
    }

    private static bool ReadBooleanOrLegacyDefault(
        JsonObject node,
        string propertyName,
        bool legacyDefault)
    {
        return node.ContainsKey(propertyName)
            ? TryReadBoolean(node, propertyName)
            : legacyDefault;
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
