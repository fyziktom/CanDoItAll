using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects;

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
    Guid? PartyAffiliationId = null,
    Guid? ProjectLifetimeId = null);

public sealed class ProjectPartyAssignmentUpsertRequest
{
    public Guid? AssignmentId { get; set; }

    public Guid ProjectId { get; set; }

    public ProjectWriteAdmission? ExpectedProjectAdmission { get; set; }

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

    public ProjectPartyAssignmentUpsertRequest Snapshot() => (ProjectPartyAssignmentUpsertRequest)MemberwiseClone();
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
