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

public sealed record ProjectNodeScopeResolution(
    bool ExistsInProject,
    bool ExistsInOtherProject,
    ProjectObjectType? ObjectType,
    string ObjectSubtype);

public interface IProjectNodeScopeBridge
{
    Task<ProjectNodeScopeResolution> ResolveAsync(
        Guid projectId,
        string nodeKey,
        CancellationToken cancellationToken = default);
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

    Task DeleteAssignmentAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default);

    Task<Result<ProjectPartyQuickCreateResult>> CreatePartyAsync(
        ProjectPartyQuickCreateRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed class NoopProjectNodeScopeBridge : IProjectNodeScopeBridge
{
    public Task<ProjectNodeScopeResolution> ResolveAsync(
        Guid projectId,
        string nodeKey,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProjectNodeScopeResolution(false, false, null, string.Empty));
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

    public Task DeleteAssignmentAsync(
        Guid assignmentId,
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
