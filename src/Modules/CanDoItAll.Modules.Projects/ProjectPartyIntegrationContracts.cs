using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects;

public enum ProjectPartyPortfolioCategory
{
    Customer,
    DeliveryUnit,
    Owner,
    Stakeholder,
    Partner,
    AiAgent
}

public enum ProjectPartyAssignmentRole
{
    Customer,
    CustomerContact,
    DeliveryUnit,
    TeamMember,
    Manager,
    Partner,
    Vendor,
    Stakeholder,
    MeetingParticipant,
    WorkItemAssignee,
    Reviewer,
    AiAgent,
    BillingContact,
    TechnicalContact
}

public enum ProjectPartyQuickCreateKind
{
    Person,
    Organization,
    OrganizationUnit,
    AiAgent
}

public enum ProjectPartyType
{
    Person,
    Organization,
    OrganizationUnit,
    AiAgent
}

public sealed record ProjectPortfolioPartyItem(
    ProjectPartyPortfolioCategory Category,
    string Label,
    string DisplayName,
    bool IsPrimary);

public sealed record ProjectPortfolioPartyContext(
    string PrimaryCustomerName,
    string PrimaryDeliveryUnitName,
    string PrimaryOwnerName,
    IReadOnlyList<ProjectPortfolioPartyItem> Items,
    string SearchText);

public sealed record ProjectPartyOption(
    Guid PartyId,
    string DisplayName,
    string PartyTypeLabel,
    string PrimaryEmail,
    string PrimaryPhone,
    bool IsSensitive);

public sealed record ProjectPartyAssignmentDetail(
    Guid Id,
    Guid ProjectId,
    Guid PartyId,
    ProjectPartyAssignmentRole Role,
    string PartyDisplayName,
    string PartyTypeLabel,
    ProjectPartyType PartyType,
    string NodeKey,
    bool IsPrimary,
    decimal? AllocationPercent,
    DateTimeOffset? StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    string Notes);

public sealed class ProjectPartyQuickCreateRequest
{
    public Guid ProjectId { get; set; }

    public ProjectPartyQuickCreateKind PartyKind { get; set; } = ProjectPartyQuickCreateKind.Person;

    public string DisplayName { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;
}

public sealed record ProjectPartyQuickCreateResult(
    Guid PartyId,
    string DisplayName,
    string PartyTypeLabel);

public sealed class ProjectPartyAssignmentUpsertRequest
{
    public Guid? AssignmentId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid PartyId { get; set; }

    public ProjectPartyAssignmentRole Role { get; set; }

    public string NodeKey { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    public decimal? AllocationPercent { get; set; }

    public DateOnly? StartsOn { get; set; }

    public DateOnly? EndsOn { get; set; }

    public string Source { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;
}

public readonly record struct ProjectNodeReference
{
    public ProjectNodeReference(string nodeKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeKey);
        NodeKey = nodeKey.Trim();
    }

    public string NodeKey { get; }

    public override string ToString()
    {
        return NodeKey;
    }
}

public sealed record ProjectNodeScopeResolution(
    bool ExistsInProject,
    bool ExistsInOtherProject,
    bool IsCanonicalNode,
    ProjectObjectType? ObjectType,
    string ObjectSubtype);

public sealed record ProjectNodeAssignmentSemantics(
    IReadOnlyList<ProjectPartyAssignmentRole> AllowedRoles,
    IReadOnlyList<ProjectPartyAssignmentRole> ReplacementRoles,
    ProjectPartyAssignmentRole? PreferredRole)
{
    public static ProjectNodeAssignmentSemantics None { get; } = new(
        Array.Empty<ProjectPartyAssignmentRole>(),
        Array.Empty<ProjectPartyAssignmentRole>(),
        null);
}

public interface IProjectNodeScopeBridge
{
    Task<ProjectNodeScopeResolution> ResolveAsync(
        Guid projectId,
        ProjectNodeReference nodeReference,
        CancellationToken cancellationToken = default);
}

public interface IProjectNodeAssignmentPolicyBridge
{
    bool SupportsCanonicalNodeScope(ProjectPartyAssignmentRole role);

    bool RequiresCanonicalNodeScope(ProjectPartyAssignmentRole role);

    ProjectNodeAssignmentSemantics Resolve(ProjectObjectType objectType, string objectSubtype);
}

public interface IProjectPartyIntegrationBridge
{
    Task<IReadOnlyDictionary<Guid, ProjectPortfolioPartyContext>> GetPortfolioContextsAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectPartyOption>> ListPartyOptionsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<ProjectPartyOption?> GetPartyOptionAsync(
        Guid partyId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectPartyAssignmentDetail>> ListAssignmentsDetailedAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<Result<Guid>> SaveAssignmentAsync(
        ProjectPartyAssignmentUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> ReplaceNodeAssignmentsAsync(
        Guid projectId,
        ProjectNodeReference nodeReference,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
        CancellationToken cancellationToken = default);

    Task DeleteAssignmentAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default);

    Task DeleteAssignmentsForNodesAsync(
        Guid projectId,
        IReadOnlyCollection<ProjectNodeReference> nodeReferences,
        CancellationToken cancellationToken = default);

    Task MoveAssignmentsToProjectAsync(
        Guid sourceProjectId,
        IReadOnlyCollection<ProjectNodeReference> nodeReferences,
        Guid targetProjectId,
        CancellationToken cancellationToken = default);

    Task<Result<ProjectPartyQuickCreateResult>> CreatePartyAsync(
        ProjectPartyQuickCreateRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed class NoopProjectNodeScopeBridge : IProjectNodeScopeBridge
{
    public Task<ProjectNodeScopeResolution> ResolveAsync(
        Guid projectId,
        ProjectNodeReference nodeReference,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProjectNodeScopeResolution(false, false, false, null, string.Empty));
    }
}

internal sealed class NoopProjectNodeAssignmentPolicyBridge : IProjectNodeAssignmentPolicyBridge
{
    public bool SupportsCanonicalNodeScope(ProjectPartyAssignmentRole role)
    {
        return false;
    }

    public bool RequiresCanonicalNodeScope(ProjectPartyAssignmentRole role)
    {
        return false;
    }

    public ProjectNodeAssignmentSemantics Resolve(ProjectObjectType objectType, string objectSubtype)
    {
        return ProjectNodeAssignmentSemantics.None;
    }
}

internal sealed class NoopProjectPartyIntegrationBridge : IProjectPartyIntegrationBridge
{
    public Task<IReadOnlyDictionary<Guid, ProjectPortfolioPartyContext>> GetPortfolioContextsAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyDictionary<Guid, ProjectPortfolioPartyContext>>(new Dictionary<Guid, ProjectPortfolioPartyContext>());
    }

    public Task<IReadOnlyList<ProjectPartyOption>> ListPartyOptionsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ProjectPartyOption>>([]);
    }

    public Task<ProjectPartyOption?> GetPartyOptionAsync(
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ProjectPartyOption?>(null);
    }

    public Task<IReadOnlyList<ProjectPartyAssignmentDetail>> ListAssignmentsDetailedAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ProjectPartyAssignmentDetail>>([]);
    }

    public Task<Result<Guid>> SaveAssignmentAsync(
        ProjectPartyAssignmentUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<Guid>.Failure(Error.Failure(
            "Project-party integration is not available.",
            "projects.party-integration-unavailable")));
    }

    public Task<Result> ReplaceNodeAssignmentsAsync(
        Guid projectId,
        ProjectNodeReference nodeReference,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure(Error.Failure(
            "Project-party integration is not available.",
            "projects.party-integration-unavailable")));
    }

    public Task DeleteAssignmentAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task DeleteAssignmentsForNodesAsync(
        Guid projectId,
        IReadOnlyCollection<ProjectNodeReference> nodeReferences,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task MoveAssignmentsToProjectAsync(
        Guid sourceProjectId,
        IReadOnlyCollection<ProjectNodeReference> nodeReferences,
        Guid targetProjectId,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<Result<ProjectPartyQuickCreateResult>> CreatePartyAsync(
        ProjectPartyQuickCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<ProjectPartyQuickCreateResult>.Failure(Error.Failure(
            "Project-party integration is not available.",
            "projects.party-integration-unavailable")));
    }
}
