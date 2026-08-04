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

public readonly record struct AgentProjectStructureAccessRevocationResult(
    bool Changed,
    string ConfigurationJson);

public sealed class AgentProjectStructureAccessMetadataException : Exception
{
    public AgentProjectStructureAccessMetadataException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
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

    public static AgentProjectStructureAccessRevocationResult RevokeProject(
        string? configurationJson,
        Guid projectId)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project id is required.", nameof(projectId));
        }

        var originalConfigurationJson = configurationJson ?? string.Empty;
        var root = ParseObjectForMutation(configurationJson);
        if (!root.TryGetPropertyValue(RootPropertyName, out var projectStructureNode))
        {
            return new AgentProjectStructureAccessRevocationResult(
                Changed: false,
                ConfigurationJson: originalConfigurationJson);
        }

        if (projectStructureNode is not JsonObject projectStructure)
        {
            throw CreateMalformedMetadataException(
                $"'{RootPropertyName}' must contain a JSON object.");
        }

        ValidateBooleanForMutation(projectStructure, CanReadPropertyName);
        ValidateBooleanForMutation(projectStructure, CanWritePropertyName);
        ValidateBooleanForMutation(projectStructure, CanWriteNonTaskStructurePropertyName);
        ValidateBooleanForMutation(projectStructure, CanWriteTasksPropertyName);
        ValidateBooleanForMutation(projectStructure, CanCreateProjectsPropertyName);
        ValidateBooleanForMutation(projectStructure, CanCreateSubprojectsPropertyName);
        var allowAllProjects = ReadBooleanForMutation(
            projectStructure,
            AllowAllProjectsPropertyName);
        var allowedProjectIds = ReadProjectIdsForMutation(projectStructure);
        if (allowAllProjects || !allowedProjectIds.Any(item => item.ProjectId == projectId))
        {
            return new AgentProjectStructureAccessRevocationResult(
                Changed: false,
                ConfigurationJson: originalConfigurationJson);
        }

        projectStructure[AllowedProjectIdsPropertyName] = new JsonArray(
            allowedProjectIds
                .Where(item => item.ProjectId != projectId)
                .Select(item => JsonValue.Create(item.RawValue))
                .ToArray());

        return new AgentProjectStructureAccessRevocationResult(
            Changed: true,
            ConfigurationJson: root.ToJsonString());
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

    private static JsonObject ParseObjectForMutation(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(configurationJson) as JsonObject
                ?? throw CreateMalformedMetadataException(
                    "Agent configuration must contain a JSON object.");
        }
        catch (AgentProjectStructureAccessMetadataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            throw CreateMalformedMetadataException(
                "Agent configuration is not valid JSON metadata.",
                exception);
        }
    }

    private static bool ReadBooleanForMutation(JsonObject node, string propertyName)
    {
        if (!node.TryGetPropertyValue(propertyName, out var propertyNode))
        {
            return false;
        }

        if (propertyNode is JsonValue value &&
            value.TryGetValue<bool>(out var parsedValue))
        {
            return parsedValue;
        }

        throw CreateMalformedMetadataException(
            $"'{RootPropertyName}.{propertyName}' must contain a JSON boolean.");
    }

    private static void ValidateBooleanForMutation(JsonObject node, string propertyName)
    {
        _ = ReadBooleanForMutation(node, propertyName);
    }

    private static IReadOnlyList<ProjectIdMetadataValue> ReadProjectIdsForMutation(
        JsonObject projectStructure)
    {
        if (!projectStructure.TryGetPropertyValue(
                AllowedProjectIdsPropertyName,
                out var allowedProjectIdsNode))
        {
            return [];
        }

        if (allowedProjectIdsNode is not JsonArray allowedProjectIds)
        {
            throw CreateMalformedMetadataException(
                $"'{RootPropertyName}.{AllowedProjectIdsPropertyName}' must contain a JSON array.");
        }

        var parsedProjectIds = new List<ProjectIdMetadataValue>(allowedProjectIds.Count);
        foreach (var item in allowedProjectIds)
        {
            if (item is not JsonValue value ||
                !value.TryGetValue<string>(out var rawProjectId) ||
                !Guid.TryParse(rawProjectId, out var projectId) ||
                projectId == Guid.Empty)
            {
                throw CreateMalformedMetadataException(
                    $"'{RootPropertyName}.{AllowedProjectIdsPropertyName}' contains an invalid project id.");
            }

            parsedProjectIds.Add(new ProjectIdMetadataValue(projectId, rawProjectId));
        }

        return parsedProjectIds;
    }

    private static AgentProjectStructureAccessMetadataException CreateMalformedMetadataException(
        string message,
        Exception? innerException = null)
        => new(
            $"Project-structure access metadata is malformed. {message}",
            innerException);

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

    private readonly record struct ProjectIdMetadataValue(Guid ProjectId, string RawValue);
}
