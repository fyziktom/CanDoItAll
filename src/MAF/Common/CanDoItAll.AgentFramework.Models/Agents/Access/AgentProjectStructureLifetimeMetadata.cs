using System.Text.Json.Nodes;

namespace CanDoItAll.AgentFramework.Models;

public sealed record AgentProjectStructureLifetime {
    public AgentProjectStructureLifetime(Guid databaseProfileId, Guid projectId, Guid lifetimeId) {
        if (databaseProfileId == Guid.Empty || projectId == Guid.Empty || lifetimeId == Guid.Empty) {
            throw new ArgumentException("Project access requires nonempty database profile, project and lifetime identifiers.");
        }
        DatabaseProfileId = databaseProfileId;
        ProjectId = projectId;
        LifetimeId = lifetimeId;
    }

    public Guid DatabaseProfileId { get; }
    public Guid ProjectId { get; }
    public Guid LifetimeId { get; }
}

public sealed record AgentProjectStructureRevocationTarget {
    private AgentProjectStructureRevocationTarget(Guid databaseProfileId, Guid projectId, AgentProjectStructureLifetime? lifetime) {
        if (databaseProfileId == Guid.Empty || projectId == Guid.Empty) {
            throw new ArgumentException("A database profile and project id are required.");
        }
        DatabaseProfileId = databaseProfileId;
        ProjectId = projectId;
        Lifetime = lifetime;
    }

    public Guid DatabaseProfileId { get; }
    public Guid ProjectId { get; }
    public AgentProjectStructureLifetime? Lifetime { get; }

    public static AgentProjectStructureRevocationTarget UnboundLegacy(Guid databaseProfileId, Guid projectId) => new(databaseProfileId, projectId, null);

    public static AgentProjectStructureRevocationTarget ForLifetime(AgentProjectStructureLifetime lifetime) {
        ArgumentNullException.ThrowIfNull(lifetime);
        return new(lifetime.DatabaseProfileId, lifetime.ProjectId, lifetime);
    }
}

public static partial class AgentProjectStructureAccessMetadata {
    private const string AllowedProjectLifetimesPropertyName = "allowedProjectLifetimes";
    private const string DatabaseProfileIdPropertyName = "databaseProfileId";
    private const string ProjectIdPropertyName = "projectId";
    private const string LifetimeIdPropertyName = "lifetimeId";

    public static string GrantProjectLifetime(string? configurationJson, AgentProjectStructureLifetime lifetime) {
        ArgumentNullException.ThrowIfNull(lifetime);
        var root = ParseObjectForMutation(configurationJson);
        var projectStructure = GetProjectStructureForLifetimeMutation(root);
        if (projectStructure is null) {
            projectStructure = new JsonObject();
            root[RootPropertyName] = projectStructure;
        }
        var projectIds = ReadProjectIdsForMutation(projectStructure);
        var lifetimes = ReadProjectLifetimesForMutation(projectStructure);
        if (ReadBooleanForMutation(projectStructure, AllowAllProjectsPropertyName)) {
            return configurationJson ?? string.Empty;
        }
        var hasProjectId = projectIds.Any(item => item.ProjectId == lifetime.ProjectId);
        if (hasProjectId && lifetimes.Any(item => item.Lifetime == lifetime)) {
            return configurationJson ?? string.Empty;
        }
        var updatedIds = new JsonArray(projectIds.Select(item => JsonValue.Create(item.RawValue)).ToArray());
        if (!hasProjectId) {
            updatedIds.Add(lifetime.ProjectId.ToString("D"));
        }
        var updatedLifetimes = new JsonArray(lifetimes.Select(item => item.Node.DeepClone()).ToArray());
        if (!lifetimes.Any(item => item.Lifetime == lifetime)) {
            updatedLifetimes.Add(WriteProjectLifetime(lifetime));
        }
        projectStructure[CanReadPropertyName] = true;
        projectStructure[AllowedProjectIdsPropertyName] = updatedIds;
        projectStructure[AllowedProjectLifetimesPropertyName] = updatedLifetimes;
        return root.ToJsonString();
    }

    public static AgentProjectStructureAccessRevocationResult RevokeProjectLifetime(
        string? configurationJson, AgentProjectStructureRevocationTarget target) {
        ArgumentNullException.ThrowIfNull(target);
        var original = configurationJson ?? string.Empty;
        var root = ParseObjectForMutation(configurationJson);
        var projectStructure = GetProjectStructureForLifetimeMutation(root);
        if (projectStructure is null) {
            return new(false, original);
        }
        var projectIds = ReadProjectIdsForMutation(projectStructure);
        var lifetimes = ReadProjectLifetimesForMutation(projectStructure);
        if (ReadBooleanForMutation(projectStructure, AllowAllProjectsPropertyName)) {
            return new(false, original);
        }
        var matchingProject = lifetimes.Where(item => item.Lifetime.ProjectId == target.ProjectId).ToArray();
        if (matchingProject.Length == 0) {
            return RevokeProject(configurationJson, target.ProjectId);
        }
        if (target.Lifetime is null || matchingProject.All(item => item.Lifetime != target.Lifetime)) {
            return new(false, original);
        }
        var remaining = lifetimes.Where(item => item.Lifetime != target.Lifetime).ToArray();
        projectStructure[AllowedProjectLifetimesPropertyName] = new JsonArray(remaining.Select(item => item.Node.DeepClone()).ToArray());
        if (remaining.All(item => item.Lifetime.ProjectId != target.ProjectId)) {
            projectStructure[AllowedProjectIdsPropertyName] = new JsonArray(projectIds
                .Where(item => item.ProjectId != target.ProjectId).Select(item => JsonValue.Create(item.RawValue)).ToArray());
        }
        return new(true, root.ToJsonString());
    }

    private static JsonObject? GetProjectStructureForLifetimeMutation(JsonObject root) {
        if (!root.TryGetPropertyValue(RootPropertyName, out var node)) {
            return null;
        }
        if (node is not JsonObject projectStructure) {
            throw CreateMalformedMetadataException($"'{RootPropertyName}' must contain a JSON object.");
        }
        ValidateBooleanForMutation(projectStructure, CanReadPropertyName);
        ValidateBooleanForMutation(projectStructure, CanWritePropertyName);
        ValidateBooleanForMutation(projectStructure, CanWriteNonTaskStructurePropertyName);
        ValidateBooleanForMutation(projectStructure, CanWriteTasksPropertyName);
        ValidateBooleanForMutation(projectStructure, CanCreateProjectsPropertyName);
        ValidateBooleanForMutation(projectStructure, CanCreateSubprojectsPropertyName);
        ValidateBooleanForMutation(projectStructure, AllowAllProjectsPropertyName);
        return projectStructure;
    }

    private static IReadOnlyList<(AgentProjectStructureLifetime Lifetime, JsonObject Node)> ReadProjectLifetimesForMutation(JsonObject projectStructure) {
        if (!projectStructure.TryGetPropertyValue(AllowedProjectLifetimesPropertyName, out var value)) {
            return [];
        }
        if (value is not JsonArray values) {
            throw CreateMalformedMetadataException($"'{AllowedProjectLifetimesPropertyName}' must contain a JSON array.");
        }
        var results = new List<(AgentProjectStructureLifetime, JsonObject)>();
        foreach (var item in values) {
            if (item is not JsonObject node) {
                throw CreateMalformedMetadataException("Each project lifetime must contain a JSON object.");
            }
            results.Add((new AgentProjectStructureLifetime(
                ReadLifetimeIdentifier(node, DatabaseProfileIdPropertyName),
                ReadLifetimeIdentifier(node, ProjectIdPropertyName),
                ReadLifetimeIdentifier(node, LifetimeIdPropertyName)), node));
        }
        return results;
    }

    private static Guid ReadLifetimeIdentifier(JsonObject node, string propertyName) {
        if (node[propertyName] is not JsonValue value || !value.TryGetValue<string>(out var text) ||
            !Guid.TryParse(text, out var id) || id == Guid.Empty) {
            throw CreateMalformedMetadataException($"Project lifetime '{propertyName}' must contain a nonempty GUID.");
        }
        return id;
    }

    private static JsonObject WriteProjectLifetime(AgentProjectStructureLifetime lifetime) => new() {
        [DatabaseProfileIdPropertyName] = lifetime.DatabaseProfileId.ToString("D"),
        [ProjectIdPropertyName] = lifetime.ProjectId.ToString("D"),
        [LifetimeIdPropertyName] = lifetime.LifetimeId.ToString("D")
    };
}
