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

public sealed class WorkforceProfileEditorModel
{
    public Guid? Id { get; set; }
    public Guid PartyId { get; set; }
    public WorkforceKind WorkforceKind { get; set; } = WorkforceKind.Employee;
    public string EmployeeCode { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string Discipline { get; set; } = string.Empty;
    public string Seniority { get; set; } = string.Empty;
    public Guid? HomeUnitPartyId { get; set; }
    public Guid? ManagerPartyId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public decimal? InternalCostRate { get; set; }
    public decimal? ExternalBillingRate { get; set; }
    public ProjectResourceRateUnit RateUnit { get; set; } = ProjectResourceRateUnit.Hour;
    public string RateCurrencyCode { get; set; } = "USD";
    public decimal CapacityHoursPerWeek { get; set; } = 40m;
    public string Status { get; set; } = "Planned";
    public string Notes { get; set; } = string.Empty;
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
