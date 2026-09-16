using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

public sealed record WorkflowProjectLifetime {
    [JsonConstructor]
    public WorkflowProjectLifetime(Guid databaseProfileId, Guid projectId, Guid lifetimeId) {
        if (databaseProfileId == Guid.Empty || projectId == Guid.Empty || lifetimeId == Guid.Empty) {
            throw new ArgumentException("A Workflow project target requires its original profile, project and lifetime.");
        }
        DatabaseProfileId = databaseProfileId;
        ProjectId = projectId;
        LifetimeId = lifetimeId;
    }

    public Guid DatabaseProfileId { get; }
    public Guid ProjectId { get; }
    public Guid LifetimeId { get; }
}

public sealed record WorkflowStructureProjectScope {
    public const int CurrentSchemaVersion = 1;
    public const int MaximumProjectCount = 1024;

    [JsonConstructor]
    public WorkflowStructureProjectScope(IReadOnlyList<WorkflowProjectLifetime> projects, IReadOnlyList<Guid>? admissionProjectIds = null,
        int schemaVersion = CurrentSchemaVersion, Guid? workflowStartCapabilityId = null) {
        ArgumentNullException.ThrowIfNull(projects);
        if (schemaVersion != CurrentSchemaVersion || workflowStartCapabilityId == Guid.Empty || projects.Count > MaximumProjectCount ||
                projects.Any(project => project is null) || projects.Select(project => project.ProjectId).Distinct().Count() != projects.Count) {
            throw new ArgumentException("The saved Workflow project scope has an unsupported version, duplicate target or invalid size.");
        }
        Projects = projects.OrderBy(project => project.ProjectId).ToImmutableArray();
        AdmissionProjectIds = (admissionProjectIds ?? []).Distinct().Order().ToImmutableArray();
        if (AdmissionProjectIds.Any(id => Projects.All(project => project.ProjectId != id))) {
            throw new ArgumentException("Every prepared Workflow target must retain its captured project lifetime.");
        }
        SchemaVersion = schemaVersion;
        WorkflowStartCapabilityId = workflowStartCapabilityId;
    }

    public IReadOnlyList<WorkflowProjectLifetime> Projects { get; }
    public IReadOnlyList<Guid> AdmissionProjectIds { get; }
    public int SchemaVersion { get; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? WorkflowStartCapabilityId { get; }

    public WorkflowProjectLifetime? Find(Guid projectId) => Projects.SingleOrDefault(project => project.ProjectId == projectId);

    public void Validate(WorkflowStructureAuthority authority) {
        if (authority.DatabaseProfileId == Guid.Empty || Projects.Any(project => project.DatabaseProfileId != authority.DatabaseProfileId) ||
                authority.ProjectId != Guid.Empty && (Find(authority.ProjectId) is null || !AdmissionProjectIds.Contains(authority.ProjectId)) ||
                !authority.AllProjects && authority.ProjectId == Guid.Empty && authority.ProjectIds.Any(id => Find(id) is null)) {
            throw new InvalidOperationException("The saved Workflow authority does not retain its exact original project scope.");
        }
    }
}

public static class WorkflowStructureAuthorityFingerprint {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Create(WorkflowStructureAuthority authority) {
        ArgumentNullException.ThrowIfNull(authority);
        authority.ProjectScope?.Validate(authority);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(authority, JsonOptions))));
    }
}
