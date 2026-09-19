using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.Modules.CrmHr;

public sealed class PartyRoleAssignmentEditorModel
{
    public Guid? Id { get; set; }
    public PartyRoleKind RoleKind { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public DateTimeOffset? ValidFromUtc { get; set; }
    public DateTimeOffset? ValidToUtc { get; set; }
    public string Notes { get; set; } = string.Empty;

    // An independent submission; the model holds no mutable descendants.
    public PartyRoleAssignmentEditorModel Snapshot() => (PartyRoleAssignmentEditorModel)MemberwiseClone();
}

public sealed class PartyContactPointEditorModel
{
    public Guid? Id { get; set; }
    public PartyContactType ContactType { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string NormalizedValue { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public bool IsPublic { get; set; }
    public List<string> Tags { get; set; } = [];
    public string Notes { get; set; } = string.Empty;

    // An independent submission: its own lists and nested inputs, so later edits of the draft cannot reach it.
    public PartyContactPointEditorModel Snapshot()
    {
        var snapshot = (PartyContactPointEditorModel)MemberwiseClone();
        snapshot.Tags = Tags.ToList();
        return snapshot;
    }
}

public sealed class PartyAddressEditorModel
{
    public Guid? Id { get; set; }
    public string AddressType { get; set; } = string.Empty;
    public string Line1 { get; set; } = string.Empty;
    public string Line2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public string Notes { get; set; } = string.Empty;

    // An independent submission; the model holds no mutable descendants.
    public PartyAddressEditorModel Snapshot() => (PartyAddressEditorModel)MemberwiseClone();
}

public static class PartyConfidentialNoteCategories
{
    public const string HumanResources = "HR";
    public const string Compensation = "Compensation";
    public const string Compliance = "Compliance";
    public const string Health = "Health";
    public const string Access = "Access";
    public const string Other = "Other";

    public static readonly IReadOnlyList<string> All =
    [
        HumanResources,
        Compensation,
        Compliance,
        Health,
        Access,
        Other
    ];
}

public sealed class PartyConfidentialNoteEditorModel
{
    public Guid? Id { get; set; }
    public string Category { get; set; } = PartyConfidentialNoteCategories.HumanResources;
    public string NoteText { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    // An independent submission; the model holds no mutable descendants.
    public PartyConfidentialNoteEditorModel Snapshot() => (PartyConfidentialNoteEditorModel)MemberwiseClone();
}

public sealed class PartyEditorModel
{
    public Guid? Id { get; set; }
    public PartyType PartyType { get; set; }
    public PartyLifecycleStatus LifecycleStatus { get; set; } = PartyLifecycleStatus.Draft;
    public string DisplayName { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string PreferredName { get; set; } = string.Empty;
    public string ExternalCode { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public string Region { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public bool IsSensitive { get; set; }
    public string ExtendedDataJson { get; set; } = "{}";
    public string LastChangedBy { get; set; } = string.Empty;
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public List<PartyRoleAssignmentEditorModel> Roles { get; set; } = [];
    public List<PartyContactPointEditorModel> ContactPoints { get; set; } = [];
    public List<PartyAddressEditorModel> Addresses { get; set; } = [];
    public List<PartyConfidentialNoteEditorModel> ConfidentialNotes { get; set; } = [];

    // An independent submission: its own lists and nested inputs, so later edits of the draft cannot reach it.
    public PartyEditorModel Snapshot()
    {
        var snapshot = (PartyEditorModel)MemberwiseClone();
        snapshot.Tags = Tags.ToList();
        snapshot.Roles = Roles.Select(item => item.Snapshot()).ToList();
        snapshot.ContactPoints = ContactPoints.Select(item => item.Snapshot()).ToList();
        snapshot.Addresses = Addresses.Select(item => item.Snapshot()).ToList();
        snapshot.ConfidentialNotes = ConfidentialNotes.Select(item => item.Snapshot()).ToList();
        return snapshot;
    }
}

public sealed record PartySummaryModel(
    Guid Id,
    string DisplayName,
    PartyType PartyType,
    PartyLifecycleStatus LifecycleStatus,
    bool IsSensitive,
    string ExternalCode,
    DateTimeOffset UpdatedAtUtc);

public sealed record PartyDirectoryListItemModel(
    Guid Id,
    string DisplayName,
    PartyType PartyType,
    PartyLifecycleStatus LifecycleStatus,
    bool IsSensitive,
    string ExternalCode,
    string Summary,
    IReadOnlyList<string> Tags,
    IReadOnlyList<PartyRoleKind> Roles,
    string PrimaryEmail,
    string PrimaryPhone,
    DateTimeOffset UpdatedAtUtc);

public sealed record OpportunitySummaryModel(
    Guid Id,
    string Title,
    OpportunityStage Stage,
    Guid AccountPartyId,
    Guid OwnerPartyId,
    string AccountDisplayName = "",
    string OwnerDisplayName = "",
    OpportunitySource OpportunitySource = OpportunitySource.Direct,
    decimal? Amount = null,
    int ProbabilityPercent = 0,
    DateOnly? ExpectedCloseOn = null,
    DateTimeOffset UpdatedAtUtc = default);

public sealed record PartyOptionModel(Guid Id, string DisplayName, PartyType PartyType);

public sealed record CrmOpportunityPartyLinkItemModel(
    Guid Id,
    Guid PartyId,
    string DisplayName,
    PartyType PartyType,
    OpportunityPartyRole Role);

public sealed record OpportunityStageHistoryItemModel(
    Guid Id,
    OpportunityStage Stage,
    DateTimeOffset ChangedAtUtc,
    string ChangedBy,
    string Notes);

public sealed record CrmOpportunityDetailModel(
    Guid Id,
    Guid AccountPartyId,
    string AccountDisplayName,
    string Title,
    OpportunityStage Stage,
    string RelationshipStage,
    OpportunitySource OpportunitySource,
    Guid OwnerPartyId,
    string OwnerDisplayName,
    Guid? DeliveryUnitPartyId,
    string DeliveryUnitDisplayName,
    string CurrencyCode,
    decimal? Amount,
    int ProbabilityPercent,
    DateOnly? ExpectedCloseOn,
    string LostReason,
    string CompetitorName,
    string PartnerContributionSummary,
    string Summary,
    string Notes,
    Guid? LinkedProjectId,
    string LinkedProjectName,
    IReadOnlyList<CrmOpportunityPartyLinkItemModel> Parties,
    IReadOnlyList<OpportunityStageHistoryItemModel> StageHistory,
    DateTimeOffset UpdatedAtUtc);

public sealed class CrmOpportunityPartyLinkEditorModel
{
    public Guid? Id { get; set; }

    public Guid PartyId { get; set; }

    public OpportunityPartyRole Role { get; set; } = OpportunityPartyRole.Partner;

    // An independent submission; the model holds no mutable descendants.
    public CrmOpportunityPartyLinkEditorModel Snapshot() => (CrmOpportunityPartyLinkEditorModel)MemberwiseClone();
}

public sealed class CrmOpportunityEditorModel
{
    public Guid? Id { get; set; }

    public DateTimeOffset? ExpectedUpdatedAtUtc { get; set; }

    public Guid AccountPartyId { get; set; }

    public string Title { get; set; } = string.Empty;

    public OpportunityStage Stage { get; set; } = OpportunityStage.Identified;

    public string RelationshipStage { get; set; } = string.Empty;

    public OpportunitySource OpportunitySource { get; set; } = OpportunitySource.Direct;

    public Guid OwnerPartyId { get; set; }

    public Guid? DeliveryUnitPartyId { get; set; }

    public string CurrencyCode { get; set; } = "USD";

    public decimal? Amount { get; set; }

    public int ProbabilityPercent { get; set; } = 20;

    public DateOnly? ExpectedCloseOn { get; set; }

    public string LostReason { get; set; } = string.Empty;

    public string CompetitorName { get; set; } = string.Empty;

    public string PartnerContributionSummary { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public string StageNotes { get; set; } = string.Empty;

    public Guid? LinkedProjectId { get; set; }

    public List<CrmOpportunityPartyLinkEditorModel> Parties { get; set; } = [];

    public string LastChangedBy { get; set; } = "crm-hr-ui";

    // An independent submission: its own lists and nested inputs, so later edits of the draft cannot reach it.
    public CrmOpportunityEditorModel Snapshot()
    {
        var snapshot = (CrmOpportunityEditorModel)MemberwiseClone();
        snapshot.Parties = Parties.Select(item => item.Snapshot()).ToList();
        return snapshot;
    }
}

public sealed class CrmOpportunityConversionEditorModel
{
    public Guid OpportunityId { get; set; }

    public DateTimeOffset? ExpectedUpdatedAtUtc { get; set; }

    public bool LinkExistingProject { get; set; }

    public Guid? ExistingProjectId { get; set; }

    public ProjectWriteAdmission? ExpectedProjectAdmission { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public string ProjectDescription { get; set; } = string.Empty;

    public string ProjectObjective { get; set; } = string.Empty;

    public string CurrentPhase { get; set; } = "Sales handoff";

    public string LastChangedBy { get; set; } = "crm-hr-ui";

    public CrmOpportunityConversionEditorModel Snapshot() => (CrmOpportunityConversionEditorModel)MemberwiseClone();
}

public sealed record CrmOpportunityConversionResult(
    Guid OpportunityId,
    Guid ProjectId,
    bool CreatedNewProject);

public sealed record CrmAccountConnectionProjectItemModel(
    Guid Id,
    string Name,
    ProjectStatus Status);

public sealed record CrmAccountConnectedRecordItemModel(
    Guid Id,
    Guid RelatedPartyId,
    string DisplayName,
    PartyType PartyType,
    CrmAccountConnectionRole Role,
    bool IsPrimary,
    string Notes,
    IReadOnlyList<CrmAccountConnectionProjectItemModel> Projects);

public sealed record CrmAccountActivityTimelineItemModel(
    Guid Id,
    string Kind,
    string Title,
    string Description,
    string Meta,
    DateTimeOffset OccurredAtUtc,
    string Tone,
    bool IsOverdue);

public static class CrmActivityHistoryQueryLimits
{
    public const int DefaultPageSize = 10;
    public const int MaximumPageSize = 50;
}

public sealed record CrmActivityHistoryQuery(
    Guid PartyId,
    int PageIndex = 0,
    int PageSize = CrmActivityHistoryQueryLimits.DefaultPageSize);

public sealed record CrmActivityHistoryPage(
    IReadOnlyList<CrmAccountActivityTimelineItemModel> Items,
    int PageIndex,
    int PageSize,
    int TotalCount,
    int ActionCount,
    int OverdueActionCount)
{
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static CrmActivityHistoryPage Empty(
        int pageSize = CrmActivityHistoryQueryLimits.DefaultPageSize)
        => new([], 0, pageSize, 0, 0, 0);
}

public sealed record CrmInteractionDetailModel(
    Guid Id,
    InteractionType InteractionType,
    string Subject,
    Guid? RelatedOpportunityId);

public sealed class CrmAccountProfileEditorModel
{
    public Guid? Id { get; set; }
    public Guid AccountPartyId { get; set; }
    public CrmAccountRelationshipStage RelationshipStage { get; set; } = CrmAccountRelationshipStage.Prospect;
    public string CommercialNotes { get; set; } = string.Empty;
    public string ConstraintNotes { get; set; } = string.Empty;
    public string TimingRiskNotes { get; set; } = string.Empty;
    public string LastChangedBy { get; set; } = "crm-hr-ui";

    // An independent submission; the model holds no mutable descendants.
    public CrmAccountProfileEditorModel Snapshot() => (CrmAccountProfileEditorModel)MemberwiseClone();
}

public sealed class CrmAccountConnectionEditorModel
{
    public Guid? Id { get; set; }
    public Guid RelatedPartyId { get; set; }
    public CrmAccountConnectionRole Role { get; set; } = CrmAccountConnectionRole.Stakeholder;
    public bool IsPrimary { get; set; }
    public string Notes { get; set; } = string.Empty;
    public List<Guid> ProjectIds { get; set; } = [];

    // An independent submission: its own lists and nested inputs, so later edits of the draft cannot reach it.
    public CrmAccountConnectionEditorModel Snapshot()
    {
        var snapshot = (CrmAccountConnectionEditorModel)MemberwiseClone();
        snapshot.ProjectIds = ProjectIds.ToList();
        return snapshot;
    }
}

public sealed class CrmInteractionEditorModel
{
    public InteractionType InteractionType { get; set; } = InteractionType.Meeting;
    public string Subject { get; set; } = string.Empty;
    public DateOnly OccurredOn { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public string Summary { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string NextActionText { get; set; } = string.Empty;
    public Guid? NextActionOwnerPartyId { get; set; }
    public DateOnly? NextActionDueOn { get; set; }
    public Guid? RelatedOpportunityId { get; set; }
    public List<Guid> ParticipantPartyIds { get; set; } = [];

    // An independent submission: its own lists and nested inputs, so later edits of the draft cannot reach it.
    public CrmInteractionEditorModel Snapshot()
    {
        var snapshot = (CrmInteractionEditorModel)MemberwiseClone();
        snapshot.ParticipantPartyIds = ParticipantPartyIds.ToList();
        return snapshot;
    }
}

public sealed record CrmAccountWorkspaceModel(
    Guid AccountPartyId,
    string DisplayName,
    string Summary,
    PartyLifecycleStatus LifecycleStatus,
    IReadOnlyList<PartyRoleKind> Roles,
    IReadOnlyList<string> Tags,
    string PrimaryEmail,
    string PrimaryPhone,
    CrmAccountProfileEditorModel Profile,
    IReadOnlyList<CrmAccountConnectedRecordItemModel> ConnectedRecords,
    IReadOnlyList<PartyOptionModel> ConnectedParties,
    int OpportunityCount);

public sealed record WorkforceProfileSummaryModel(Guid Id, Guid PartyId, WorkforceKind WorkforceKind, string JobTitle, string Status);

/// <summary>
/// Availability of a party for new work on the current UTC date, as a JSON integer: 0 Bench (at most 10 percent
/// allocated and less than 25 percent blocked), 1 NearAvailable (a current allocation or a capacity block ends within
/// 30 days), 2 Allocated (any other committed state), 3 Overallocated (allocated plus blocked above 100 percent).
/// Overallocated is checked first, then Bench, then NearAvailable.
/// </summary>
public enum WorkforceAvailabilityState
{
    Bench,
    NearAvailable,
    Allocated,
    Overallocated
}

public sealed record WorkforceListItemModel(
    Guid PartyId,
    string DisplayName,
    PartyType PartyType,
    bool IsSensitive,
    WorkforceKind? WorkforceKind,
    string Status,
    string JobTitle,
    string Discipline,
    string HomeUnitName,
    string ManagerName,
    IReadOnlyList<PartyRoleKind> Roles,
    bool HasProfile,
    DateTimeOffset UpdatedAtUtc,
    string Seniority = "",
    string Location = "",
    string SkillSummary = "",
    WorkforceAvailabilityState? AvailabilityState = null,
    decimal AvailablePercent = 0m,
    DateOnly? ContractEndDate = null,
    DateOnly? NextAvailabilityOn = null);

/// <summary>
/// The workforce profile of a party as returned in the workforce workspace. The profile save request
/// (<c>POST /api/crm-hr/workforce/profiles</c>) has the same fields except <c>id</c> and <c>lastChangedBy</c>. When no
/// profile was saved yet, the fields hold defaults and <c>id</c> is null.
/// </summary>
public sealed class WorkforceProfileEditorModel
{
    /// <summary>
    /// Identifier of the workforce profile; null when the party has no saved profile. It is not the party identifier,
    /// and the profile save finds the profile by <c>partyId</c> instead.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Identifier of the party the profile belongs to.
    /// </summary>
    public Guid PartyId { get; set; }

    /// <summary>
    /// Work classification, as a JSON integer: 0 Employee, 1 Contractor, 2 Freelancer, 3 DeliveryUnit. Without a saved
    /// profile: DeliveryUnit for organizations and organization units, Contractor or Freelancer when the party has that
    /// role, otherwise Employee.
    /// </summary>
    public WorkforceKind WorkforceKind { get; set; } = WorkforceKind.Employee;

    /// <summary>
    /// Personnel number or similar code from an HR system; empty when not set.
    /// </summary>
    public string EmployeeCode { get; set; } = string.Empty;

    /// <summary>
    /// Job title; empty when not set.
    /// </summary>
    public string JobTitle { get; set; } = string.Empty;

    /// <summary>
    /// Professional discipline as free text; empty when not set.
    /// </summary>
    public string Discipline { get; set; } = string.Empty;

    /// <summary>
    /// Seniority level as free text; empty when not set.
    /// </summary>
    public string Seniority { get; set; } = string.Empty;

    /// <summary>
    /// Identifier of the organization or organization unit the party belongs to; null when none. Its name is
    /// <c>homeUnitName</c> of the workspace.
    /// </summary>
    public Guid? HomeUnitPartyId { get; set; }

    /// <summary>
    /// Identifier of the person who manages the party; null when none. Its name is <c>managerName</c> of the workspace.
    /// </summary>
    public Guid? ManagerPartyId { get; set; }

    /// <summary>
    /// Date the work relationship starts (<c>yyyy-MM-dd</c>); null when not set.
    /// </summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>
    /// Date the work relationship ends (<c>yyyy-MM-dd</c>); null when open-ended.
    /// </summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// Work location as free text; empty when not set.
    /// </summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Time zone as free text; empty when not set.
    /// </summary>
    public string TimeZone { get; set; } = string.Empty;

    /// <summary>
    /// Internal cost of one <c>rateUnit</c> of the party's work in <c>rateCurrencyCode</c>; null when unknown. A rate,
    /// not a total.
    /// </summary>
    public decimal? InternalCostRate { get; set; }

    /// <summary>
    /// Price billed to customers for one <c>rateUnit</c> of the party's work in <c>rateCurrencyCode</c>; null when
    /// unknown.
    /// </summary>
    public decimal? ExternalBillingRate { get; set; }

    /// <summary>
    /// Time unit of both rates, as a JSON integer: 0 Hour, 1 ManDay.
    /// </summary>
    public ProjectResourceRateUnit RateUnit { get; set; } = ProjectResourceRateUnit.Hour;

    /// <summary>
    /// Three-letter currency code of both rates, for example <c>EUR</c>; <c>USD</c> when not set.
    /// </summary>
    public string RateCurrencyCode { get; set; } = "USD";

    /// <summary>
    /// Weekly working capacity in hours; 40 when not set.
    /// </summary>
    public decimal CapacityHoursPerWeek { get; set; } = 40m;

    /// <summary>
    /// Employment status as free text. Without a saved profile: <c>Active</c> or <c>Inactive</c> when the party has
    /// that lifecycle status, otherwise <c>Planned</c>.
    /// </summary>
    public string Status { get; set; } = "Planned";

    /// <summary>
    /// Free-text notes; empty when not set.
    /// </summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Actor that last changed the party record, which is not necessarily a change of the profile, for example
    /// <c>crm-hr-api</c>. The profile save request does not carry it.
    /// </summary>
    public string LastChangedBy { get; set; } = "crm-hr-ui";

    // An independent submission; the model holds no mutable descendants.
    public WorkforceProfileEditorModel Snapshot() => (WorkforceProfileEditorModel)MemberwiseClone();
}

public sealed record WorkforceProfileWorkspaceModel(
    Guid PartyId,
    string DisplayName,
    string Summary,
    PartyType PartyType,
    PartyLifecycleStatus LifecycleStatus,
    bool IsSensitive,
    string LastChangedBy,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<PartyRoleKind> Roles,
    string PrimaryEmail,
    string PrimaryPhone,
    string HomeUnitName,
    string ManagerName,
    WorkforceProfileEditorModel Profile,
    IReadOnlyList<SkillCatalogItemModel> SkillCatalog,
    IReadOnlyList<PartySkillItemModel> Skills);

/// <summary>
/// The workforce view of one party returned by <c>GET /api/crm-hr/workforce/{partyId}</c>: party summary, workforce
/// profile, skill catalog and party skills, capacity blocks, project allocations and a capacity summary for the current
/// UTC date. It contains personal and HR data.
/// </summary>
/// <param name="PartyId">Identifier of the party.</param>
/// <param name="DisplayName">Name shown for the party.</param>
/// <param name="Summary">Short description of the party; returned also for sensitive parties.</param>
/// <param name="PartyType">
/// Kind of party, as a JSON integer: 0 Person, 1 Organization, 2 OrganizationUnit (AI agent parties have no workforce
/// workspace).
/// </param>
/// <param name="LifecycleStatus">
/// Lifecycle status of the party, as a JSON integer: 0 Draft, 1 Active, 2 Inactive, 3 Archived, 4 Former, 5 Candidate,
/// 6 Prospect.
/// </param>
/// <param name="IsSensitive">
/// True when the party is marked for restricted handling; <c>primaryEmail</c> and <c>primaryPhone</c> are then empty.
/// </param>
/// <param name="LastChangedBy">Actor that last changed the party record, for example <c>crm-hr-api</c>.</param>
/// <param name="UpdatedAtUtc">
/// When the party record last changed, in UTC; saving the workforce profile updates it.
/// </param>
/// <param name="Roles">
/// Business roles of the party, one entry per role assignment ordered by role, each as a JSON integer: 0 Customer,
/// 1 CustomerContact, 2 Partner, 3 Vendor, 4 Employee, 5 Contractor, 6 Freelancer, 7 DeliveryUnit, 8 Candidate,
/// 9 AiSteward, 10 AccountManager, 11 Recruiter, 12 Stakeholder.
/// </param>
/// <param name="PrimaryEmail">
/// The party's primary public email address, or another public one when none is primary; empty when there is none or
/// the party is sensitive.
/// </param>
/// <param name="PrimaryPhone">
/// The party's primary public phone number, or another public one when none is primary; empty when there is none or
/// the party is sensitive.
/// </param>
/// <param name="HomeUnitName">Display name of the profile's home unit; empty when none.</param>
/// <param name="ManagerName">Display name of the profile's manager; empty when none.</param>
/// <param name="Profile">The workforce profile, or default values with a null <c>id</c> when none was saved.</param>
/// <param name="SkillCatalog">The whole skill catalog, active skills first, then by category and name.</param>
/// <param name="Skills">The party's skills, ordered by skill category and name.</param>
/// <param name="CapacityBlocks">
/// The party's capacity blocks, past, current and future, ordered by start date and kind.
/// </param>
/// <param name="ProjectAllocations">
/// The party's project allocations, active ones first, then by start date and project name. Past allocations and
/// allocations of earlier project lifetimes are included.
/// </param>
/// <param name="CapacitySummary">Capacity figures for the current UTC date.</param>
public sealed record WorkforceWorkspaceModel(
    Guid PartyId,
    string DisplayName,
    string Summary,
    PartyType PartyType,
    PartyLifecycleStatus LifecycleStatus,
    bool IsSensitive,
    string LastChangedBy,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<PartyRoleKind> Roles,
    string PrimaryEmail,
    string PrimaryPhone,
    string HomeUnitName,
    string ManagerName,
    WorkforceProfileEditorModel Profile,
    IReadOnlyList<SkillCatalogItemModel> SkillCatalog,
    IReadOnlyList<PartySkillItemModel> Skills,
    IReadOnlyList<CapacityBlockItemModel> CapacityBlocks,
    IReadOnlyList<ProjectAllocationItemModel> ProjectAllocations,
    WorkforceCapacitySummaryModel CapacitySummary);

/// <summary>
/// A skill definition of the workforce skill catalog. Party skills refer to it by its identifier; it is not an agent
/// skill or capability.
/// </summary>
/// <param name="Id">Identifier of the skill definition; use it as <c>skillId</c> of a party skill.</param>
/// <param name="Name">Skill name, unique in the catalog.</param>
/// <param name="Category">Category that groups skills; empty when not set.</param>
/// <param name="Description">Description of the skill; empty when not set.</param>
/// <param name="IsActive">
/// False when the skill is deactivated; inactive skills stay in the catalog after the active ones.
/// </param>
public sealed record SkillCatalogItemModel(
    Guid Id,
    string Name,
    string Category,
    string Description,
    bool IsActive);

public sealed class SkillDefinitionEditorModel
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // An independent submission; the model holds no mutable descendants.
    public SkillDefinitionEditorModel Snapshot() => (SkillDefinitionEditorModel)MemberwiseClone();
}

/// <summary>
/// A party's recorded proficiency in one catalog skill, as returned in the workforce workspace.
/// </summary>
/// <param name="Id">Identifier of the party skill record; send it as <c>id</c> to update the record.</param>
/// <param name="SkillId">Identifier of the skill definition.</param>
/// <param name="SkillName">Name of the skill definition.</param>
/// <param name="SkillCategory">Category of the skill definition; empty when not set.</param>
/// <param name="Proficiency">
/// Proficiency level, lowest first, as a JSON integer: 0 Basic, 1 Working, 2 Strong, 3 Expert.
/// </param>
/// <param name="YearsExperience">Years of experience with the skill; 0 or more.</param>
/// <param name="CertificationStatus">Certification state as free text; empty when not set.</param>
/// <param name="LastValidatedOn">Date the proficiency was last checked (<c>yyyy-MM-dd</c>); null when never.</param>
/// <param name="Notes">Free-text notes; empty when not set.</param>
public sealed record PartySkillItemModel(
    Guid Id,
    Guid SkillId,
    string SkillName,
    string SkillCategory,
    SkillProficiencyLevel Proficiency,
    int YearsExperience,
    string CertificationStatus,
    DateOnly? LastValidatedOn,
    string Notes);

public sealed class PartySkillEditorModel
{
    public Guid? Id { get; set; }
    public Guid PartyId { get; set; }
    public Guid SkillId { get; set; }
    public SkillProficiencyLevel Proficiency { get; set; } = SkillProficiencyLevel.Basic;
    public int YearsExperience { get; set; }
    public string CertificationStatus { get; set; } = string.Empty;
    public DateOnly? LastValidatedOn { get; set; }
    public string Notes { get; set; } = string.Empty;

    // An independent submission; the model holds no mutable descendants.
    public PartySkillEditorModel Snapshot() => (PartySkillEditorModel)MemberwiseClone();
}

/// <summary>
/// A capacity block of a party as returned in the workforce workspace: a date range in which a percentage of the
/// party's capacity is not available. It is not a task or a project assignment.
/// </summary>
/// <param name="Id">Identifier of the capacity block; send it as <c>id</c> to update the block.</param>
/// <param name="BlockKind">
/// Reason for the block, as a JSON integer: 0 Leave, 1 Unavailable, 2 Reserve, 3 Tentative.
/// </param>
/// <param name="StartDate">First day of the block (<c>yyyy-MM-dd</c>).</param>
/// <param name="EndDate">Last day of the block, inclusive (<c>yyyy-MM-dd</c>).</param>
/// <param name="Percentage">
/// Share of the party's capacity that the block takes, in percent (above 0, at most 100).
/// </param>
/// <param name="RelatedProjectId">Identifier of the project the block relates to; null when none.</param>
/// <param name="RelatedProjectName">
/// Name of the related project; empty when none or when the project no longer exists.
/// </param>
/// <param name="Notes">Free-text notes; empty when not set.</param>
/// <param name="IsActive">
/// True when the current UTC date is within the block; active blocks count in <c>activeBlockedPercent</c>.
/// </param>
/// <param name="IsFuture">True when the block starts after the current UTC date.</param>
public sealed record CapacityBlockItemModel(
    Guid Id,
    CapacityBlockKind BlockKind,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal Percentage,
    Guid? RelatedProjectId,
    string RelatedProjectName,
    string Notes,
    bool IsActive,
    bool IsFuture);

public sealed class CapacityBlockEditorModel
{
    public Guid? Id { get; set; }
    public Guid PartyId { get; set; }
    public CapacityBlockKind BlockKind { get; set; } = CapacityBlockKind.Leave;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal Percentage { get; set; } = 100m;
    public Guid? RelatedProjectId { get; set; }
    public string Notes { get; set; } = string.Empty;

    // An independent submission; the model holds no mutable descendants.
    public CapacityBlockEditorModel Snapshot() => (CapacityBlockEditorModel)MemberwiseClone();
}

/// <summary>
/// A project allocation of a party as returned in the workforce workspace: a project participation or a task work
/// assignment that carries an allocation percentage. Only allocations bound to the current lifetime of their project
/// count in the capacity summary.
/// </summary>
/// <param name="AssignmentId">Identifier of the underlying project participation or task work assignment.</param>
/// <param name="ProjectId">Identifier of the project.</param>
/// <param name="ProjectName">
/// Name of the project; empty when the project no longer exists or was recreated with the same identifier after the
/// allocation was made.
/// </param>
/// <param name="PartyId">Identifier of the allocated party.</param>
/// <param name="PartyDisplayName">Display name of the allocated party.</param>
/// <param name="Role">
/// Role of the party in the project, as a JSON integer: 0 Customer, 1 CustomerContact, 2 DeliveryUnit, 3 TeamMember,
/// 4 Manager, 5 Partner, 6 Vendor, 7 Stakeholder, 8 MeetingParticipant, 9 WorkItemAssignee (a task work assignment),
/// 10 Reviewer, 11 AiAgent, 12 BillingContact, 13 TechnicalContact.
/// </param>
/// <param name="AllocationPercent">Share of the party's capacity allocated, in percent.</param>
/// <param name="StartsOn">First day of the allocation as a UTC date (<c>yyyy-MM-dd</c>); null when open.</param>
/// <param name="EndsOn">Last day of the allocation as a UTC date (<c>yyyy-MM-dd</c>); null when open-ended.</param>
/// <param name="Notes">Free-text notes of the participation or assignment; empty when not set.</param>
/// <param name="IsActive">
/// True when the current UTC date is within the allocation's dates; missing dates are open.
/// </param>
/// <param name="IsFuture">True when the allocation starts after the current UTC date.</param>
public sealed record ProjectAllocationItemModel(
    Guid AssignmentId,
    Guid ProjectId,
    string ProjectName,
    Guid PartyId,
    string PartyDisplayName,
    ProjectPartyAssignmentRole Role,
    decimal AllocationPercent,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    string Notes,
    bool IsActive,
    bool IsFuture);

/// <summary>
/// Capacity figures of a party for the current UTC date, calculated from its active capacity blocks and from its active
/// allocations bound to current project lifetimes.
/// </summary>
/// <param name="CapacityHoursPerWeek">
/// Weekly capacity in hours from the workforce profile; 40 when there is no profile or it has 0 or less.
/// </param>
/// <param name="ActiveAllocationPercent">
/// Sum of the allocation percentages of the party's active allocations in current project lifetimes; can exceed 100.
/// </param>
/// <param name="ActiveBlockedPercent">Sum of the percentages of the party's active capacity blocks.</param>
/// <param name="AvailablePercent">100 minus the allocated and blocked percentages, never below 0.</param>
/// <param name="AvailabilityState">
/// Availability classification, as a JSON integer: 0 Bench, 1 NearAvailable, 2 Allocated, 3 Overallocated.
/// </param>
/// <param name="AvailabilityMessage">
/// English sentence describing the availability, for display; its wording can change.
/// </param>
/// <param name="NextAvailabilityOn">
/// Earliest end date, today or later, of a current-lifetime allocation or of a capacity block; null when there is none.
/// </param>
/// <param name="IsOverallocated">True when <c>availabilityState</c> is Overallocated.</param>
/// <param name="IsBench">True when <c>availabilityState</c> is Bench.</param>
public sealed record WorkforceCapacitySummaryModel(
    decimal CapacityHoursPerWeek,
    decimal ActiveAllocationPercent,
    decimal ActiveBlockedPercent,
    decimal AvailablePercent,
    WorkforceAvailabilityState AvailabilityState,
    string AvailabilityMessage,
    DateOnly? NextAvailabilityOn,
    bool IsOverallocated,
    bool IsBench);

public sealed record WorkforceCapacityWorkspaceModel(
    Guid PartyId,
    IReadOnlyList<CapacityBlockItemModel> CapacityBlocks,
    IReadOnlyList<ProjectAllocationItemModel> ProjectAllocations,
    WorkforceCapacitySummaryModel CapacitySummary);

public sealed class StaffingRequestEditorModel
{
    public Guid? Id { get; set; }
    public Guid? ProjectId { get; set; }
    public ProjectWriteAdmission? ExpectedProjectAdmission { get; set; }

    public ProjectAssignmentReference? ExpectedSourceProjectReference { get; set; }
    public Guid? RequestedByPartyId { get; set; }
    public Guid? DeliveryUnitPartyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string NeededRole { get; set; } = string.Empty;
    public List<Guid> SkillIds { get; set; } = [];
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal AllocationPercent { get; set; } = 100m;
    public StaffingRequestStatus Status { get; set; } = StaffingRequestStatus.Draft;
    public string Notes { get; set; } = string.Empty;

    public StaffingRequestEditorModel Snapshot() {
        var snapshot = (StaffingRequestEditorModel)MemberwiseClone();
        snapshot.SkillIds = SkillIds.ToList();
        return snapshot;
    }
}

public sealed record StaffingRequestItemModel(
    Guid Id,
    Guid? ProjectId,
    string ProjectName,
    Guid? RequestedByPartyId,
    string RequestedByName,
    Guid? DeliveryUnitPartyId,
    string DeliveryUnitName,
    string Title,
    string NeededRole,
    IReadOnlyList<SkillCatalogItemModel> NeededSkills,
    DateOnly? StartDate,
    DateOnly? EndDate,
    decimal AllocationPercent,
    StaffingRequestStatus Status,
    string Notes,
    Guid? ProjectLifetimeId = null);

public sealed record StaffingCandidateItemModel(
    Guid PartyId,
    string DisplayName,
    PartyType PartyType,
    string JobTitle,
    string Discipline,
    string Seniority,
    string Location,
    string SkillSummary,
    WorkforceAvailabilityState AvailabilityState,
    decimal AvailablePercent,
    DateOnly? NextAvailabilityOn,
    WorkforceRecordClassification Classification =
        WorkforceRecordClassification.ExternalContact,
    string PrimaryAffiliationText = "",
    string OtherAffiliationsSummary = "");

public static class StaffingQueryLimits
{
    public const int DefaultPageSize = 6;
    public const int MaximumPageSize = 50;
    public const int MaximumSearchLength = 200;
}

public sealed record StaffingRequestQuery(
    Guid ProjectId,
    string SearchText = "",
    StaffingRequestStatus? Status = null,
    int PageIndex = 0,
    int PageSize = StaffingQueryLimits.DefaultPageSize);

public sealed record StaffingRequestPage(
    IReadOnlyList<StaffingRequestItemModel> Items,
    int PageIndex,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static StaffingRequestPage Empty(int pageSize = StaffingQueryLimits.DefaultPageSize)
        => new([], 0, pageSize, 0);
}

public sealed record StaffingCandidateQuery(
    Guid? SkillId = null,
    string SearchText = "",
    WorkforceAvailabilityState? AvailabilityState = null,
    int PageIndex = 0,
    int PageSize = StaffingQueryLimits.DefaultPageSize);

public sealed record StaffingCandidatePage(
    IReadOnlyList<StaffingCandidateItemModel> Items,
    int PageIndex,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static StaffingCandidatePage Empty(int pageSize = StaffingQueryLimits.DefaultPageSize)
        => new([], 0, pageSize, 0);
}

public sealed record StaffingDashboardModel(
    int OpenRequestCount,
    decimal OpenDemandPercent,
    int BenchCount,
    int OverallocatedCount);

public sealed class AiCapabilityEditorModel
{
    public string Name { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string ToolAccess { get; set; } = string.Empty;
    public string Limitations { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    // An independent submission; the model holds no mutable descendants.
    public AiCapabilityEditorModel Snapshot() => (AiCapabilityEditorModel)MemberwiseClone();
}

public sealed class AiAgentProfileEditorModel
{
    public Guid? Id { get; set; }
    public Guid PartyId { get; set; }
    public Guid? ProviderProfileId { get; set; }
    public string DefaultModel { get; set; } = string.Empty;
    public AiExecutionMode ExecutionMode { get; set; } = AiExecutionMode.Remote;
    public Guid? OwnerPartyId { get; set; }
    public AiValidationStatus ValidationStatus { get; set; } = AiValidationStatus.Draft;
    public DateOnly? LastReviewedOn { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string ExtendedDataJson { get; set; } = "{}";
    public string LastChangedBy { get; set; } = "crm-hr-ui";
    public List<AiCapabilityEditorModel> Capabilities { get; set; } = [];

    // An independent submission: its own lists and nested inputs, so later edits of the draft cannot reach it.
    public AiAgentProfileEditorModel Snapshot()
    {
        var snapshot = (AiAgentProfileEditorModel)MemberwiseClone();
        snapshot.Capabilities = Capabilities.Select(item => item.Snapshot()).ToList();
        return snapshot;
    }
}

public sealed record AiProviderOptionModel(
    Guid Id,
    string Name,
    string ProviderLabel,
    string DefaultModel,
    bool IsEnabled);

public sealed record AiAgentListItemModel(
    Guid PartyId,
    string DisplayName,
    string Summary,
    PartyLifecycleStatus LifecycleStatus,
    Guid? TechnicalAgentId,
    AiResourceBindingStatus BindingStatus,
    string BindingSummary,
    AiExecutionMode? ExecutionMode,
    AiValidationStatus? ValidationStatus,
    string ProviderName,
    string DefaultModel,
    string OwnerName,
    int CapabilityCount,
    bool HasProfile,
    string AgentsRoute,
    DateTimeOffset UpdatedAtUtc);

public sealed record AiAgentWorkspaceModel(
    Guid PartyId,
    string DisplayName,
    string Summary,
    PartyLifecycleStatus LifecycleStatus,
    string PrimaryEmail,
    string PrimaryPhone,
    Guid? TechnicalAgentId,
    AiResourceBindingStatus BindingStatus,
    string BindingSummary,
    string AgentsRoute,
    string ProviderName,
    string OwnerName,
    int CapabilityCount,
    AiAgentProfileEditorModel Profile);

public sealed record AiAgentStaffingFactListItemModel(
    Guid PartyId,
    Guid? TechnicalAgentId,
    string DisplayName,
    string RoleTitle,
    string Summary,
    string Instructions,
    AiResourceBindingStatus BindingStatus,
    string BindingSummary,
    AiExecutionMode? ExecutionMode,
    string ProviderName,
    string DefaultModel,
    string TemplateKey,
    IReadOnlyList<string> Tags,
    IReadOnlyList<AiCapabilityEditorModel> Capabilities,
    string AgentsRoute);

public sealed record AiAgentProfileSummaryModel(Guid Id, Guid PartyId, Guid? ProviderProfileId, AiExecutionMode ExecutionMode, AiValidationStatus ValidationStatus);

public sealed record ProjectPartyAssignmentSummaryModel(Guid Id, Guid ProjectId, Guid PartyId, ProjectPartyAssignmentKind AssignmentKind, string NodeKey, bool IsPrimary);
