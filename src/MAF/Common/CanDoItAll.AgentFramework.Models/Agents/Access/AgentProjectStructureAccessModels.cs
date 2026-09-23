using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Project Structure access of an agent, the <c>projectStructureAccess</c> member of the agent editor form. It is
/// stored in the agent's <c>configurationJson</c> under <c>projectStructure</c>; a save replaces that section with
/// these values, and an omitted object or one with every flag false and no project removes it. The flags decide which
/// Project Structure runtime tools an agent run can receive; the run still needs the agent's <c>canUseTools</c>
/// permission, a project-scoped run context and the approval policy. They are not HTTP permissions.
/// </summary>
public sealed class AgentProjectStructureAccessSettings
{
    /// <summary>
    /// Allows the Project Structure read tools for the permitted projects. The server stores true whenever any other
    /// flag is true or a project is listed.
    /// </summary>
    public bool CanRead { get; set; }

    /// <summary>
    /// Allows every Project Structure write tool, including changes to task nodes, in the permitted projects. A write
    /// also needs a live lifetime binding of the project unless <c>allowAllProjects</c> is true.
    /// </summary>
    public bool CanWrite { get; set; }

    /// <summary>
    /// Allows structure writes that do not change canonical task nodes; tools that need full structure write, such as
    /// the structure import, stay unavailable unless <c>canWrite</c> is also true.
    /// </summary>
    public bool CanWriteNonTaskStructure { get; set; }

    /// <summary>
    /// Adds the canonical task tools (task create, task update and task resource attach) for the permitted projects.
    /// </summary>
    public bool CanWriteTasks { get; set; }

    /// <summary>Adds the tool that creates standalone projects.</summary>
    public bool CanCreateProjects { get; set; }

    /// <summary>
    /// Adds the tool that creates a subproject under a parent project. Linking existing projects as subprojects and
    /// moving nodes into a new subproject also need <c>canWrite</c>.
    /// </summary>
    public bool CanCreateSubprojects { get; set; }

    /// <summary>
    /// True to permit every project, including projects created later, without lifetime bindings; the server then
    /// clears <c>allowedProjectIds</c> and <c>allowedProjectLifetimes</c>. It is ignored in governed process runs.
    /// </summary>
    public bool AllowAllProjects { get; set; }

    /// <summary>
    /// Identifiers of the permitted projects when <c>allowAllProjects</c> is false. The server drops all-zero
    /// identifiers and duplicates and sorts the list. A project added here for the first time must exist (or be
    /// reserved for creation by this agent); otherwise the save is rejected. The server binds a newly added existing
    /// project to its current lifetime automatically.
    /// </summary>
    public List<Guid> AllowedProjectIds { get; set; } = [];

    /// <summary>
    /// Server-maintained bindings of permitted projects to the project lifetimes they apply to; writes need a live
    /// binding. Send back the list as read. On save the list is merged with the bindings already stored and limited
    /// to <c>allowedProjectIds</c>; a binding the agent did not have must name the project's current live lifetime, and
    /// omitting stored bindings of a still permitted project does not remove them.
    /// </summary>
    public List<AgentProjectStructureLifetime> AllowedProjectLifetimes { get; set; } = [];
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

public static partial class AgentProjectStructureAccessMetadata
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

            settings.AllowedProjectLifetimes = ReadProjectLifetimesForMutation(projectStructure).Select(item => item.Lifetime).ToList();
            return Normalize(settings);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or AgentProjectStructureAccessMetadataException)
        {
            return new AgentProjectStructureAccessSettings();
        }
    }

    public static string Write(
        string? configurationJson,
        AgentProjectStructureAccessSettings? settings)
    {
        var normalized = Normalize(settings ?? new AgentProjectStructureAccessSettings());
        normalized.AllowedProjectLifetimes = normalized.AllowedProjectLifetimes
            .Concat(Read(configurationJson).AllowedProjectLifetimes)
            .Where(lifetime => normalized.AllowedProjectIds.Contains(lifetime.ProjectId))
            .Distinct().OrderBy(lifetime => lifetime.DatabaseProfileId).ThenBy(lifetime => lifetime.ProjectId)
            .ThenBy(lifetime => lifetime.LifetimeId).ToList();
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
        if (normalized.AllowedProjectLifetimes.Count > 0) {
            root[RootPropertyName]![AllowedProjectLifetimesPropertyName] = new JsonArray(
                normalized.AllowedProjectLifetimes.Select(WriteProjectLifetime).Cast<JsonNode>().ToArray());
        }

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
        var lifetimes = ReadProjectLifetimesForMutation(projectStructure);
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

        if (projectStructure.ContainsKey(AllowedProjectLifetimesPropertyName)) {
            projectStructure[AllowedProjectLifetimesPropertyName] = new JsonArray(lifetimes
                .Where(item => item.Lifetime.ProjectId != projectId).Select(item => item.Node.DeepClone()).ToArray());
        }

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
            AllowedProjectIds = allowedProjectIds,
            AllowedProjectLifetimes = settings.AllowedProjectLifetimes
                .Where(lifetime => allowedProjectIds.Contains(lifetime.ProjectId)).Distinct()
                .OrderBy(lifetime => lifetime.DatabaseProfileId).ThenBy(lifetime => lifetime.ProjectId)
                .ThenBy(lifetime => lifetime.LifetimeId).ToList()
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
