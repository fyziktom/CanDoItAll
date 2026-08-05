using CanDoItAll.Infrastructure.Persistence;
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

public enum ProjectResourceRateUnit
{
    Hour,
    ManDay
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

public sealed record ProjectPartyAffiliationContext(
    Guid? AffiliationId,
    string AffiliationLabel,
    string OrganizationName,
    string RoleTitle,
    string OtherAffiliationsSummary)
{
    public string PrimaryDisplayText => string.Join(
        " · ",
        new[] { OrganizationName, RoleTitle }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
}

public sealed record ProjectPartyOption(
    Guid PartyId,
    string DisplayName,
    string PartyTypeLabel,
    ProjectPartyType PartyType,
    string PrimaryEmail,
    string PrimaryPhone,
    bool IsSensitive,
    ProjectPartyAffiliationContext? Affiliation = null);

public sealed record ProjectPartyCostRate(
    Guid PartyId,
    decimal Rate,
    ProjectResourceRateUnit Unit,
    string CurrencyCode);

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
    string Source,
    string Notes,
    ProjectPartyAffiliationContext? Affiliation = null,
    Guid? PartyAffiliationId = null);

public sealed record ProjectPartyAssignmentConcurrencySnapshot(
    Guid AssignmentId,
    Guid PartyId,
    ProjectPartyType PartyType,
    bool IsPrimary,
    Guid? PartyAffiliationId = null)
{
    public static ProjectPartyAssignmentConcurrencySnapshot From(
        ProjectPartyAssignmentDetail assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        return new(
            assignment.Id,
            assignment.PartyId,
            assignment.PartyType,
            assignment.IsPrimary,
            assignment.PartyAffiliationId ??
            assignment.Affiliation?.AffiliationId);
    }
}

public readonly record struct ProjectWorkItemDirectAssignmentRevision
{
    public ProjectWorkItemDirectAssignmentRevision(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "A direct-assignment revision cannot be negative.");
        }

        Value = value;
    }

    public long Value { get; }
}

public readonly record struct ProjectPartyAssignmentMoveOperationId
{
    public ProjectPartyAssignmentMoveOperationId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "A project-party assignment move operation identifier is required.",
                nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }
}

public sealed record ProjectWorkItemDirectAssignmentState(
    ProjectPartyType PartyType,
    Guid PartyId,
    bool IsPrimary,
    string DisplayName);

public enum ProjectWorkItemDirectAssignmentMutationStatus
{
    Applied,
    WorkItemNotFound,
    RevisionConflict
}

public sealed record ProjectWorkItemDirectAssignmentMutationResult(
    ProjectWorkItemDirectAssignmentMutationStatus Status,
    ProjectWorkItemDirectAssignmentRevision? Revision);

public interface IProjectWorkItemAssignmentMutationBridge
{
    Task<ProjectWorkItemDirectAssignmentMutationResult> StageMutationAsync(
        AppDbContext dbContext,
        Guid projectId,
        ProjectNodeReference taskNode,
        IReadOnlyCollection<ProjectWorkItemDirectAssignmentState>
            finalAssignments,
        ProjectWorkItemDirectAssignmentRevision?
            expectedCurrentRevision = null,
        CancellationToken cancellationToken = default);
}

public sealed record ProjectWorkItemAssigneeBinding(
    Guid ProjectId,
    string NodeKey,
    Guid PartyId,
    ProjectPartyType PartyType);

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

    public Guid? PartyAffiliationId { get; set; }

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

public sealed record ProjectNodeDetails(
    Guid ProjectId,
    string NodeKey,
    ProjectObjectType ObjectType,
    string ObjectSubtype,
    string Title,
    string Subtitle,
    string Status,
    string ProgressMode,
    int ProgressPercent,
    DateTimeOffset? StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    string ParentNodeKey);

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

public static class ProjectPartyIntegrationErrorCodes
{
    public const string ConditionalReplacementUnavailable =
        "projects.party-assignment.conditional-replacement-unavailable";
    public const string StaleAssignmentSnapshot =
        "crmhr.project-assignment.stale-snapshot";
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

    Task<IReadOnlyList<ProjectPartyAssignmentDetail>> ListAssignmentsDetailedAsync(
        Guid projectId,
        IReadOnlyCollection<ProjectPartyAssignmentRole> roles,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectWorkItemAssigneeBinding>> ListWorkItemAssigneeBindingsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    async Task<IReadOnlyList<ProjectWorkItemAssigneeBinding>> ListWorkItemAssigneeBindingsAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectIds);
        var distinctProjectIds = projectIds.Distinct().ToArray();
        if (distinctProjectIds.Any(static projectId => projectId == Guid.Empty))
        {
            throw new ArgumentException(
                "Project identifiers cannot contain an empty value.",
                nameof(projectIds));
        }

        var assignmentTasks = distinctProjectIds
            .Select(projectId => ListWorkItemAssigneeBindingsAsync(projectId, cancellationToken))
            .ToArray();
        var assignments = await Task.WhenAll(assignmentTasks);
        return assignments.SelectMany(static items => items).ToArray();
    }

    Task<Result<Guid>> SaveAssignmentAsync(
        ProjectPartyAssignmentUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> ReplaceProjectAssignmentsAsync(
        Guid projectId,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Failure(Error.Failure(
            "Project-level assignment replacement is not available.",
            "projects.party-assignment.project-replacement-unavailable")));

    Task<Result> ReplaceNodeAssignmentsAsync(
        Guid projectId,
        ProjectNodeReference nodeReference,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
        CancellationToken cancellationToken = default);

    Task<Result> ReplaceNodeAssignmentsIfCurrentAsync(
        Guid projectId,
        ProjectNodeReference nodeReference,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
        IReadOnlyCollection<ProjectPartyAssignmentConcurrencySnapshot>
            expectedAssignments,
        ProjectWorkItemDirectAssignmentRevision?
            expectedDirectAssignmentRevision,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Failure(Error.Failure(
            "Conditional project-party assignment replacement is not available.",
            ProjectPartyIntegrationErrorCodes.ConditionalReplacementUnavailable)));

    Task DeleteAssignmentAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default);

    Task DeleteAssignmentsForNodesAsync(
        Guid projectId,
        IReadOnlyCollection<ProjectNodeReference> nodeReferences,
        CancellationToken cancellationToken = default);

    Task DeleteAssignmentsForProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task MoveAssignmentsToProjectAsync(
        ProjectPartyAssignmentMoveOperationId operationId,
        Guid sourceProjectId,
        IReadOnlyCollection<ProjectNodeReference> nodeReferences,
        Guid targetProjectId,
        CancellationToken cancellationToken = default);

    Task<Result<ProjectPartyQuickCreateResult>> CreatePartyAsync(
        ProjectPartyQuickCreateRequest request,
        CancellationToken cancellationToken = default);
}

public interface IProjectNodeDetailsBridge
{
    Task<ProjectNodeDetails?> GetAsync(
        Guid projectId,
        ProjectNodeReference nodeReference,
        CancellationToken cancellationToken = default);
}

public interface IProjectPartyCostRateBridge
{
    Task<ProjectPartyCostRate?> GetInternalCostRateAsync(
        Guid partyId,
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

    public Task<IReadOnlyList<ProjectPartyAssignmentDetail>> ListAssignmentsDetailedAsync(
        Guid projectId,
        IReadOnlyCollection<ProjectPartyAssignmentRole> roles,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ProjectPartyAssignmentDetail>>([]);
    }

    public Task<IReadOnlyList<ProjectWorkItemAssigneeBinding>> ListWorkItemAssigneeBindingsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ProjectWorkItemAssigneeBinding>>([]);
    }

    public Task<Result<Guid>> SaveAssignmentAsync(
        ProjectPartyAssignmentUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<Guid>.Failure(Error.Failure(
            "Project-party integration is not available.",
            "projects.party-integration-unavailable")));
    }

    public Task<Result> ReplaceProjectAssignmentsAsync(
        Guid projectId,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure(Error.Failure(
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

    public Task DeleteAssignmentsForProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task MoveAssignmentsToProjectAsync(
        ProjectPartyAssignmentMoveOperationId operationId,
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

internal sealed class NoopProjectWorkItemAssignmentMutationBridge :
    IProjectWorkItemAssignmentMutationBridge
{
    public Task<ProjectWorkItemDirectAssignmentMutationResult>
        StageMutationAsync(
            AppDbContext dbContext,
            Guid projectId,
            ProjectNodeReference taskNode,
            IReadOnlyCollection<ProjectWorkItemDirectAssignmentState>
                finalAssignments,
            ProjectWorkItemDirectAssignmentRevision?
                expectedCurrentRevision = null,
            CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(
            "Canonical task assignment mutation integration is unavailable.");
}

internal sealed class NoopProjectNodeDetailsBridge : IProjectNodeDetailsBridge
{
    public Task<ProjectNodeDetails?> GetAsync(
        Guid projectId,
        ProjectNodeReference nodeReference,
        CancellationToken cancellationToken = default)
    {
        return Task.FromException<ProjectNodeDetails?>(new InvalidOperationException(
            "Project node details integration is not configured."));
    }
}

internal sealed class NoopProjectPartyCostRateBridge : IProjectPartyCostRateBridge
{
    public Task<ProjectPartyCostRate?> GetInternalCostRateAsync(
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ProjectPartyCostRate?>(null);
    }
}
