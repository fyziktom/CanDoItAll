using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

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
}

public sealed class CrmOpportunityConversionEditorModel
{
    public Guid OpportunityId { get; set; }

    public DateTimeOffset? ExpectedUpdatedAtUtc { get; set; }

    public bool LinkExistingProject { get; set; }

    public Guid? ExistingProjectId { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public string ProjectDescription { get; set; } = string.Empty;

    public string ProjectObjective { get; set; } = string.Empty;

    public string CurrentPhase { get; set; } = "Sales handoff";

    public string LastChangedBy { get; set; } = "crm-hr-ui";
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
}

public sealed class CrmAccountConnectionEditorModel
{
    public Guid? Id { get; set; }
    public Guid RelatedPartyId { get; set; }
    public CrmAccountConnectionRole Role { get; set; } = CrmAccountConnectionRole.Stakeholder;
    public bool IsPrimary { get; set; }
    public string Notes { get; set; } = string.Empty;
    public List<Guid> ProjectIds { get; set; } = [];
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

internal enum CrmRouteSelectionResolutionFailure
{
    None,
    OpportunityNotFound,
    InteractionNotFound,
    InteractionAccountAmbiguous,
    ConflictingAccount
}

internal readonly record struct CrmRouteSelectionResolution(
    Guid? AccountPartyId,
    CrmRouteSelectionResolutionFailure Failure)
{
    public bool IsResolved => Failure == CrmRouteSelectionResolutionFailure.None;

    public static CrmRouteSelectionResolution Resolved(Guid? accountPartyId)
        => new(accountPartyId, CrmRouteSelectionResolutionFailure.None);

    public static CrmRouteSelectionResolution Unresolved(
        CrmRouteSelectionResolutionFailure failure)
    {
        if (failure == CrmRouteSelectionResolutionFailure.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failure),
                failure,
                "An unresolved CRM route selection requires a failure reason.");
        }

        return new CrmRouteSelectionResolution(null, failure);
    }
}

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
    string Notes);

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

internal sealed record StaffingCapacityCounts(
    int BenchCount,
    int OverallocatedCount);

public sealed class AiCapabilityEditorModel
{
    public string Name { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string ToolAccess { get; set; } = string.Empty;
    public string Limitations { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
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

internal sealed record CrmPartyContactValue(Guid PartyId, PartyContactType ContactType, string Value, bool IsPrimary);

internal sealed class CrmOpportunityExtendedDataModel
{
    public string CompetitorName { get; set; } = string.Empty;

    public string PartnerContributionSummary { get; set; } = string.Empty;
}

public sealed partial class PartyDirectoryService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    IActivityStream activityStream,
    ISearchIndexService searchIndexService)
{
    private const string PartyNotFoundErrorCode = "crmhr.party.not-found";
    private const string MultiplePrimaryContactsErrorCode = "crmhr.party.multiple-primary-contacts";

    public async Task<IReadOnlyList<PartySummaryModel>> ListPartiesAsync(CancellationToken cancellationToken = default)
    {
        return (await ListDirectoryAsync(cancellationToken))
            .Select(item => new PartySummaryModel(
                item.Id,
                item.DisplayName,
                item.PartyType,
                item.LifecycleStatus,
                item.IsSensitive,
                item.ExternalCode,
                item.UpdatedAtUtc))
            .ToList();
    }

    public async Task<IReadOnlyList<PartyDirectoryListItemModel>> ListDirectoryAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var parties = await dbContext.Set<Party>()
            .OrderBy(party => party.DisplayName)
            .Select(party => new
            {
                party.Id,
                party.DisplayName,
                party.PartyType,
                party.LifecycleStatus,
                party.IsSensitive,
                party.ExternalCode,
                party.Summary,
                party.TagsJson,
                party.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var partyIds = parties.Select(item => item.Id).ToList();
        var roles = await dbContext.Set<PartyRoleAssignment>()
            .Where(item => partyIds.Contains(item.PartyId))
            .OrderBy(item => item.RoleKind)
            .Select(item => new { item.PartyId, item.RoleKind })
            .ToListAsync(cancellationToken);
        var contactPoints = await dbContext.Set<PartyContactPoint>()
            .Where(item => partyIds.Contains(item.PartyId))
            .OrderBy(item => item.PartyId)
            .ThenBy(item => item.ContactType)
            .ThenByDescending(item => item.IsPrimary)
            .ThenBy(item => item.Id)
            .Select(item => new PartyDirectoryContactValue(
                item.Id,
                item.PartyId,
                item.ContactType,
                item.Value,
                item.IsPrimary))
            .ToListAsync(cancellationToken);

        var rolesByPartyId = roles
            .GroupBy(item => item.PartyId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PartyRoleKind>)group.Select(item => item.RoleKind).Distinct().ToList());
        var contactsByPartyId = contactPoints
            .GroupBy(item => item.PartyId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return parties
            .Select(party =>
            {
                var tags = DeserializeTags(party.TagsJson, party.Id);
                var partyContacts = contactsByPartyId.GetValueOrDefault(party.Id) ?? [];
                return new PartyDirectoryListItemModel(
                    party.Id,
                    party.DisplayName,
                    party.PartyType,
                    party.LifecycleStatus,
                    party.IsSensitive,
                    party.ExternalCode,
                    party.Summary,
                    tags,
                    rolesByPartyId.GetValueOrDefault(party.Id) ?? [],
                    ResolvePrimaryContact(partyContacts, PartyContactType.Email),
                    ResolvePrimaryContact(partyContacts, PartyContactType.Phone),
                    party.UpdatedAtUtc);
            })
            .ToList();
    }

    public async Task<PartyEditorModel?> GetPartyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var party = await dbContext.Set<Party>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (party is null)
        {
            return null;
        }

        var roles = await dbContext.Set<PartyRoleAssignment>().Where(item => item.PartyId == id).OrderBy(item => item.RoleKind).ToListAsync(cancellationToken);
        var contactPoints = await dbContext.Set<PartyContactPoint>()
            .Where(item => item.PartyId == id)
            .OrderBy(item => item.ContactType)
            .ThenByDescending(item => item.IsPrimary)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var addresses = await dbContext.Set<PartyAddress>().Where(item => item.PartyId == id).OrderByDescending(item => item.IsPrimary).ToListAsync(cancellationToken);
        var confidentialNotes = (await dbContext.Set<PartyConfidentialNote>()
            .Where(item => item.PartyId == id)
            .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenByDescending(item => item.CreatedAtUtc)
            .ToList();
        var tags = DeserializeTags(party.TagsJson, party.Id);

        return new PartyEditorModel
        {
            Id = party.Id,
            PartyType = party.PartyType,
            LifecycleStatus = party.LifecycleStatus,
            DisplayName = party.DisplayName,
            LegalName = party.LegalName,
            PreferredName = party.PreferredName,
            ExternalCode = party.ExternalCode,
            Summary = party.Summary,
            Notes = party.Notes,
            Tags = tags,
            Region = party.Region,
            CountryCode = party.CountryCode,
            TimeZone = party.TimeZone,
            IsSensitive = party.IsSensitive,
            ExtendedDataJson = party.ExtendedDataJson,
            LastChangedBy = party.LastChangedBy,
            UpdatedAtUtc = party.UpdatedAtUtc,
            Roles = roles.Select(role => new PartyRoleAssignmentEditorModel
            {
                Id = role.Id,
                RoleKind = role.RoleKind,
                Title = role.Title,
                IsPrimary = role.IsPrimary,
                ValidFromUtc = role.ValidFromUtc,
                ValidToUtc = role.ValidToUtc,
                Notes = role.Notes
            }).ToList(),
            ContactPoints = contactPoints.Select(contactPoint => new PartyContactPointEditorModel
            {
                Id = contactPoint.Id,
                ContactType = contactPoint.ContactType,
                Label = contactPoint.Label,
                Value = contactPoint.Value,
                NormalizedValue = contactPoint.NormalizedValue,
                IsPrimary = contactPoint.IsPrimary,
                IsPublic = contactPoint.IsPublic,
                Tags = DeserializeTags(contactPoint.TagsJson, party.Id),
                Notes = contactPoint.Notes
            }).ToList(),
            Addresses = addresses.Select(address => new PartyAddressEditorModel
            {
                Id = address.Id,
                AddressType = address.AddressType,
                Line1 = address.Line1,
                Line2 = address.Line2,
                City = address.City,
                Region = address.Region,
                PostalCode = address.PostalCode,
                CountryCode = address.CountryCode,
                IsPrimary = address.IsPrimary,
                Notes = address.Notes
            }).ToList(),
            ConfidentialNotes = confidentialNotes.Select(note => new PartyConfidentialNoteEditorModel
            {
                Id = note.Id,
                Category = note.Category,
                NoteText = note.NoteText,
                CreatedBy = note.CreatedBy,
                CreatedAtUtc = note.CreatedAtUtc,
                UpdatedAtUtc = note.UpdatedAtUtc
            }).ToList()
        };
    }

    public async Task<Result<Guid>> SavePartyAsync(PartyEditorModel model, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var saveResult = await SavePartyCoreAsync(dbContext, model, cancellationToken);
        if (saveResult.IsFailure || saveResult.Value is null)
        {
            return Result<Guid>.Failure(saveResult.Errors);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await PublishPartySavedAsync(saveResult.Value, cancellationToken);
        return Result<Guid>.Success(saveResult.Value.PartyId);
    }

    public async Task<Result<int>> ImportPartiesAtomicallyAsync(
        IReadOnlyList<PartyEditorModel> models,
        string actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(models);
        if (models.Count == 0)
        {
            return Result<int>.Failure(Error.Validation(
                "At least one party is required for import.",
                "crmhr.party.import-no-ready-rows"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var operations = new List<PartySaveOperation>(models.Count);
        foreach (var model in models)
        {
            model.LastChangedBy = string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim();
            var saveResult = await SavePartyCoreAsync(dbContext, model, cancellationToken);
            if (saveResult.IsFailure || saveResult.Value is null)
            {
                return Result<int>.Failure(saveResult.Errors);
            }

            operations.Add(saveResult.Value);
        }

        dbContext.Set<CrmHrAuditEntry>().Add(new CrmHrAuditEntry
        {
            EntityType = nameof(Party),
            EntityId = Guid.Empty,
            Action = "CsvImport",
            Summary = $"Imported {operations.Count} party row(s) from CSV.",
            DetailJson = JsonSerializer.Serialize(new { ImportedCount = operations.Count }),
            Actor = string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim(),
            CreatedAtUtc = clock.GetUtcNow()
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        foreach (var operation in operations)
        {
            await PublishPartySavedAsync(operation, cancellationToken);
        }

        return Result<int>.Success(operations.Count);
    }

    private async Task<Result<PartySaveOperation>> SavePartyCoreAsync(
        AppDbContext dbContext,
        PartyEditorModel model,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (string.IsNullOrWhiteSpace(model.DisplayName))
        {
            return Result<PartySaveOperation>.Failure([Error.Validation("Display name is required.", "crmhr.party.display-name-required")]);
        }

        var duplicatePrimaryContactTypes = model.ContactPoints
            .Where(item => item.IsPrimary)
            .GroupBy(item => item.ContactType)
            .Where(group => group.Skip(1).Any())
            .Select(group => group.Key)
            .OrderBy(contactType => contactType)
            .ToArray();
        if (duplicatePrimaryContactTypes.Length > 0)
        {
            return Result<PartySaveOperation>.Failure([Error.Validation(
                $"Only one primary contact is allowed for each contact type. Conflicting types: {string.Join(", ", duplicatePrimaryContactTypes)}.",
                MultiplePrimaryContactsErrorCode)]);
        }

        var confidentialNotes = model.ConfidentialNotes
            .Where(item => !string.IsNullOrWhiteSpace(item.NoteText))
            .ToList();
        if (!model.IsSensitive && confidentialNotes.Count > 0)
        {
            return Result<PartySaveOperation>.Failure([Error.Validation(
                "Mark the party as sensitive before saving confidential notes.",
                "crmhr.party.confidential-notes-require-sensitive")]);
        }

        var now = clock.GetUtcNow();
        Party party;
        var isNew = !model.Id.HasValue;
        if (model.Id is Guid existingPartyId)
        {
            var existingParty = await dbContext.Set<Party>()
                .SingleOrDefaultAsync(item => item.Id == existingPartyId, cancellationToken);
            if (existingParty is null)
            {
                return Result<PartySaveOperation>.Failure([Error.Failure(
                    "The selected party was not found.",
                    PartyNotFoundErrorCode)]);
            }

            party = existingParty;
        }
        else
        {
            party = new Party
            {
                CreatedAtUtc = now
            };
            dbContext.Set<Party>().Add(party);
        }

        PartyLifecycleStatus? previousLifecycleStatus = isNew
            ? null
            : party.LifecycleStatus;

        var normalizedExtendedDataResult = TryNormalizeJson(model.ExtendedDataJson, "{}");
        if (normalizedExtendedDataResult is null)
        {
            return Result<PartySaveOperation>.Failure([Error.Validation("Extended data must be valid JSON.", "crmhr.party.extended-data-invalid")]);
        }

        party.PartyType = model.PartyType;
        party.LifecycleStatus = model.LifecycleStatus;
        party.DisplayName = model.DisplayName.Trim();
        party.LegalName = model.LegalName.Trim();
        party.PreferredName = model.PreferredName.Trim();
        party.ExternalCode = model.ExternalCode.Trim();
        party.Summary = model.Summary.Trim();
        party.Notes = model.Notes.Trim();
        party.TagsJson = JsonSerializer.Serialize(model.Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        party.Region = model.Region.Trim();
        party.CountryCode = model.CountryCode.Trim();
        party.TimeZone = model.TimeZone.Trim();
        party.IsSensitive = model.IsSensitive;
        party.ExtendedDataJson = normalizedExtendedDataResult;
        party.LastChangedBy = string.IsNullOrWhiteSpace(model.LastChangedBy) ? "system" : model.LastChangedBy.Trim();
        party.UpdatedAtUtc = now;

        await ReplaceChildrenAsync(dbContext, party.Id, model, party.LastChangedBy, now, cancellationToken);
        var auditAction = isNew
            ? "PartyCreated"
            : previousLifecycleStatus == PartyLifecycleStatus.Archived && party.LifecycleStatus != PartyLifecycleStatus.Archived
                ? "PartyReactivated"
                : previousLifecycleStatus != PartyLifecycleStatus.Archived && party.LifecycleStatus == PartyLifecycleStatus.Archived
                    ? "PartyArchived"
                    : "PartyUpdated";
        var auditSummary = auditAction switch
        {
            "PartyCreated" => $"Created party '{party.DisplayName}'.",
            "PartyArchived" => $"Archived party '{party.DisplayName}'.",
            "PartyReactivated" => $"Reactivated party '{party.DisplayName}'.",
            _ => $"Updated party '{party.DisplayName}'."
        };
        CrmHrAuditWriter.AddEntry(
            dbContext,
            nameof(Party),
            party.Id,
            auditAction,
            auditSummary,
            new
            {
                party.PartyType,
                party.LifecycleStatus,
                party.ExternalCode,
                party.Region,
                party.CountryCode,
                party.TimeZone,
                RoleCount = model.Roles.Count,
                ContactCount = model.ContactPoints.Count,
                AddressCount = model.Addresses.Count,
                ConfidentialNoteCount = confidentialNotes.Count
            },
            party.LastChangedBy,
            party.IsSensitive,
            now);
        return Result<PartySaveOperation>.Success(new PartySaveOperation(
            party.Id,
            auditAction,
            auditSummary,
            party.PartyType,
            party.LifecycleStatus,
            party.LastChangedBy));
    }

    private async Task PublishPartySavedAsync(PartySaveOperation operation, CancellationToken cancellationToken)
    {
        await UpsertPartySearchDocumentAsync(operation.PartyId, cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                operation.AuditAction,
                operation.AuditSummary,
                $"{operation.PartyType} / {operation.LifecycleStatus}",
                ArtifactKind: nameof(Party),
                ArtifactId: operation.PartyId,
                Route: $"/crm-hr/directory?partyId={operation.PartyId}",
                Actor: operation.Actor),
            cancellationToken);
    }

    private sealed record PartySaveOperation(
        Guid PartyId,
        string AuditAction,
        string AuditSummary,
        PartyType PartyType,
        PartyLifecycleStatus LifecycleStatus,
        string Actor);

    private static async Task ReplaceChildrenAsync(
        AppDbContext dbContext,
        Guid partyId,
        PartyEditorModel model,
        string actor,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existingRoles = await dbContext.Set<PartyRoleAssignment>().Where(item => item.PartyId == partyId).ToListAsync(cancellationToken);
        var existingContactPoints = await dbContext.Set<PartyContactPoint>().Where(item => item.PartyId == partyId).ToListAsync(cancellationToken);
        var existingAddresses = await dbContext.Set<PartyAddress>().Where(item => item.PartyId == partyId).ToListAsync(cancellationToken);
        var existingConfidentialNotes = await dbContext.Set<PartyConfidentialNote>().Where(item => item.PartyId == partyId).ToListAsync(cancellationToken);

        dbContext.Set<PartyRoleAssignment>().RemoveRange(existingRoles);
        dbContext.Set<PartyContactPoint>().RemoveRange(existingContactPoints);
        dbContext.Set<PartyAddress>().RemoveRange(existingAddresses);

        dbContext.Set<PartyRoleAssignment>().AddRange(model.Roles.Select(role => new PartyRoleAssignment
        {
            Id = role.Id ?? Guid.NewGuid(),
            PartyId = partyId,
            RoleKind = role.RoleKind,
            Title = role.Title.Trim(),
            IsPrimary = role.IsPrimary,
            ValidFromUtc = role.ValidFromUtc,
            ValidToUtc = role.ValidToUtc,
            Notes = role.Notes.Trim()
        }));

        dbContext.Set<PartyContactPoint>().AddRange(model.ContactPoints.Select(contactPoint => new PartyContactPoint
        {
            Id = contactPoint.Id ?? Guid.NewGuid(),
            PartyId = partyId,
            ContactType = contactPoint.ContactType,
            Label = contactPoint.Label.Trim(),
            Value = contactPoint.Value.Trim(),
            NormalizedValue = contactPoint.NormalizedValue.Trim(),
            IsPrimary = contactPoint.IsPrimary,
            IsPublic = contactPoint.IsPublic,
            TagsJson = JsonSerializer.Serialize(contactPoint.Tags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()),
            Notes = contactPoint.Notes.Trim()
        }));

        dbContext.Set<PartyAddress>().AddRange(model.Addresses.Select(address => new PartyAddress
        {
            Id = address.Id ?? Guid.NewGuid(),
            PartyId = partyId,
            AddressType = address.AddressType.Trim(),
            Line1 = address.Line1.Trim(),
            Line2 = address.Line2.Trim(),
            City = address.City.Trim(),
            Region = address.Region.Trim(),
            PostalCode = address.PostalCode.Trim(),
                CountryCode = address.CountryCode.Trim(),
                IsPrimary = address.IsPrimary,
                Notes = address.Notes.Trim()
            }));

        var retainedNoteIds = model.ConfidentialNotes
            .Where(item => item.Id.HasValue)
            .Select(item => item.Id!.Value)
            .ToHashSet();
        dbContext.Set<PartyConfidentialNote>().RemoveRange(existingConfidentialNotes.Where(item => !retainedNoteIds.Contains(item.Id)));

        foreach (var item in model.ConfidentialNotes.Where(item => !string.IsNullOrWhiteSpace(item.NoteText)))
        {
            var note = item.Id.HasValue
                ? existingConfidentialNotes.SingleOrDefault(existing => existing.Id == item.Id.Value)
                : null;

            if (note is null)
            {
                note = new PartyConfidentialNote
                {
                    Id = item.Id ?? Guid.NewGuid(),
                    PartyId = partyId,
                    CreatedBy = string.IsNullOrWhiteSpace(item.CreatedBy) ? actor : item.CreatedBy.Trim(),
                    CreatedAtUtc = item.CreatedAtUtc == default ? now : item.CreatedAtUtc
                };
                dbContext.Set<PartyConfidentialNote>().Add(note);
            }

            note.Category = NormalizeConfidentialCategory(item.Category);
            note.NoteText = item.NoteText.Trim();
            note.UpdatedAtUtc = now;
        }
    }

    private static string NormalizeConfidentialCategory(string? category)
    {
        var normalized = string.IsNullOrWhiteSpace(category) ? PartyConfidentialNoteCategories.Other : category.Trim();
        return PartyConfidentialNoteCategories.All.Contains(normalized, StringComparer.Ordinal)
            ? normalized
            : PartyConfidentialNoteCategories.Other;
    }

    private static List<string> DeserializeTags(string json, Guid partyId)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"Party '{partyId}' contains invalid tags JSON.");
        }
    }

    private static string? TryNormalizeJson(string json, string fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return fallback;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ResolvePrimaryContact(
        IReadOnlyList<PartyDirectoryContactValue> contactPoints,
        PartyContactType contactType)
    {
        return contactPoints
            .Where(item => item.ContactType == contactType)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.Id)
            .Select(item => item.Value)
            .FirstOrDefault()
            ?? string.Empty;
    }

    private sealed record PartyDirectoryContactValue(
        Guid Id,
        Guid PartyId,
        PartyContactType ContactType,
        string Value,
        bool IsPrimary);
}

public sealed partial class CrmService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    IActivityStream activityStream,
    ISearchIndexService searchIndexService,
    ProjectsService projectsService,
    IProjectRecordQueryService projectRecordQueryService,
    IProjectPartyIntegrationBridge projectPartyIntegrationBridge)
{
    private const string CrmAccountEntityType = "CrmAccount";
    private const string CrmAccountSearchSourceType = "crm-account";
    private const string CrmOpportunityEntityType = "Opportunity";
    private const string CrmOpportunitySearchSourceType = "crm-opportunity";

    internal async Task<CrmRouteSelectionResolution> ResolveRouteSelectionAsync(
        Guid? requestedAccountId,
        Guid? requestedOpportunityId,
        Guid? requestedInteractionId,
        CancellationToken cancellationToken = default)
    {
        if (!requestedOpportunityId.HasValue && !requestedInteractionId.HasValue)
        {
            return CrmRouteSelectionResolution.Resolved(requestedAccountId);
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        Guid? opportunityAccountId = null;
        if (requestedOpportunityId.HasValue)
        {
            opportunityAccountId = await dbContext.Set<Opportunity>()
                .Where(item => item.Id == requestedOpportunityId.Value)
                .Select(item => (Guid?)item.AccountPartyId)
                .SingleOrDefaultAsync(cancellationToken);
            if (!opportunityAccountId.HasValue)
            {
                return CrmRouteSelectionResolution.Unresolved(
                    CrmRouteSelectionResolutionFailure.OpportunityNotFound);
            }
        }

        Guid? interactionAccountId = null;
        if (requestedInteractionId.HasValue)
        {
            var interactionAccountIds = await (
                    from interaction in dbContext.Set<InteractionRecord>()
                    where interaction.Id == requestedInteractionId.Value
                    join link in dbContext.Set<InteractionPartyLink>()
                            .Where(item => item.Role == InteractionPartyRole.Account)
                        on interaction.Id equals link.InteractionId
                    select link.PartyId)
                .Distinct()
                .Take(2)
                .ToListAsync(cancellationToken);
            if (interactionAccountIds.Count == 0)
            {
                return CrmRouteSelectionResolution.Unresolved(
                    CrmRouteSelectionResolutionFailure.InteractionNotFound);
            }

            if (interactionAccountIds.Count != 1)
            {
                return CrmRouteSelectionResolution.Unresolved(
                    CrmRouteSelectionResolutionFailure.InteractionAccountAmbiguous);
            }

            interactionAccountId = interactionAccountIds[0];
        }

        var resolvedAccountIds = new[]
            {
                requestedAccountId,
                opportunityAccountId,
                interactionAccountId
            }
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Distinct()
            .Take(2)
            .ToList();
        return resolvedAccountIds.Count <= 1
            ? CrmRouteSelectionResolution.Resolved(resolvedAccountIds.SingleOrDefault())
            : CrmRouteSelectionResolution.Unresolved(
                CrmRouteSelectionResolutionFailure.ConflictingAccount);
    }

    public async Task<IReadOnlyList<OpportunitySummaryModel>> ListOpportunitiesAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var opportunities = await dbContext.Set<Opportunity>()
            .Select(item => new
            {
                item.Id,
                item.Title,
                item.Stage,
                item.AccountPartyId,
                item.OwnerPartyId,
                item.OpportunitySource,
                item.Amount,
                item.ProbabilityPercent,
                item.ExpectedCloseDateUtc,
                item.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);
        var partyIds = opportunities
            .Select(item => item.AccountPartyId)
            .Concat(opportunities.Select(item => item.OwnerPartyId))
            .Distinct()
            .ToList();
        var partiesById = partyIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await dbContext.Set<Party>()
                .Where(item => partyIds.Contains(item.Id))
                .Select(item => new
                {
                    item.Id,
                    item.DisplayName
                })
                .ToListAsync(cancellationToken))
                .ToDictionary(item => item.Id, item => item.DisplayName);

        return opportunities
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => new OpportunitySummaryModel(
                item.Id,
                item.Title,
                item.Stage,
                item.AccountPartyId,
                item.OwnerPartyId,
                partiesById.GetValueOrDefault(item.AccountPartyId) ?? "Unknown account",
                partiesById.GetValueOrDefault(item.OwnerPartyId) ?? "Unknown owner",
                item.OpportunitySource,
                item.Amount,
                item.ProbabilityPercent,
                ToDateOnly(item.ExpectedCloseDateUtc),
                item.UpdatedAtUtc))
            .ToList();
    }

    public async Task<CrmOpportunityDetailModel?> GetOpportunityAsync(
        Guid opportunityId,
        CancellationToken cancellationToken = default)
    {
        if (opportunityId == Guid.Empty)
        {
            return null;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return (await LoadOpportunityDetailsAsync(
                dbContext,
                [opportunityId],
                cancellationToken))
            .SingleOrDefault();
    }

    public async Task<int> CountAccountsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<Party>()
            .AsNoTracking()
            .CountAsync(
                party =>
                    party.PartyType == PartyType.Organization &&
                    party.LifecycleStatus != PartyLifecycleStatus.Archived,
                cancellationToken);
    }

    public async Task<CrmAccountWorkspaceModel?> GetAccountWorkspaceAsync(Guid accountPartyId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var account = await dbContext.Set<Party>()
            .Select(item => new
            {
                item.Id,
                item.DisplayName,
                item.Summary,
                item.LifecycleStatus,
                item.TagsJson,
                item.PartyType
            })
            .SingleOrDefaultAsync(item => item.Id == accountPartyId, cancellationToken);

        if (account is null || account.PartyType != PartyType.Organization)
        {
            return null;
        }

        var roleValues = await dbContext.Set<PartyRoleAssignment>()
            .Where(item => item.PartyId == accountPartyId)
            .OrderBy(item => item.RoleKind)
            .Select(item => item.RoleKind)
            .Distinct()
            .ToListAsync(cancellationToken);
        var contactValues = await dbContext.Set<PartyContactPoint>()
            .Where(item => item.PartyId == accountPartyId)
            .Select(item => new CrmPartyContactValue(item.PartyId, item.ContactType, item.Value, item.IsPrimary))
            .ToListAsync(cancellationToken);
        var profile = await dbContext.Set<CrmAccountProfile>()
            .SingleOrDefaultAsync(item => item.AccountPartyId == accountPartyId, cancellationToken);
        var connections = await dbContext.Set<CrmAccountConnection>()
            .AsNoTracking()
            .Where(item => item.AccountPartyId == accountPartyId)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.Role)
            .ToListAsync(cancellationToken);
        var relatedPartyIds = connections.Select(item => item.RelatedPartyId).Distinct().ToList();
        var relatedParties = relatedPartyIds.Count == 0
            ? new Dictionary<Guid, PartyOptionModel>()
            : (await dbContext.Set<Party>()
                .AsNoTracking()
                .Where(item => relatedPartyIds.Contains(item.Id))
                .Select(item => new PartyOptionModel(item.Id, item.DisplayName, item.PartyType))
                .ToListAsync(cancellationToken))
                .ToDictionary(item => item.Id);
        var connectedParties = relatedParties.Values
            .OrderBy(item => item.DisplayName)
            .ToList();
        var connectionIds = connections.Select(item => item.Id).ToList();
        var connectionProjectLinks = connectionIds.Count == 0
            ? []
            : await dbContext.Set<CrmAccountConnectionProjectLink>()
                .AsNoTracking()
                .Where(item => connectionIds.Contains(item.AccountConnectionId))
                .ToListAsync(cancellationToken);
        var projectIds = connectionProjectLinks
            .Select(item => item.ProjectId)
            .Distinct()
            .ToList();
        var projects = projectIds.Count == 0
            ? new Dictionary<Guid, CrmAccountConnectionProjectItemModel>()
            : (await dbContext.Set<Project>()
                .AsNoTracking()
                .Where(item => projectIds.Contains(item.Id))
                .Select(item => new CrmAccountConnectionProjectItemModel(
                    item.Id,
                    item.Name,
                    item.Status))
                .ToListAsync(cancellationToken))
                .ToDictionary(item => item.Id);
        var projectIdsByConnectionId = connectionProjectLinks
            .GroupBy(item => item.AccountConnectionId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.ProjectId).Distinct().ToList());

        var opportunityCount = await dbContext.Set<Opportunity>()
            .AsNoTracking()
            .CountAsync(item => item.AccountPartyId == accountPartyId, cancellationToken);

        var connectedRecords = connections
            .Select(item =>
            {
                var relatedParty = relatedParties.GetValueOrDefault(item.RelatedPartyId)
                    ?? new PartyOptionModel(item.RelatedPartyId, "Unknown party", PartyType.Person);
                var relatedProjects = projectIdsByConnectionId
                    .GetValueOrDefault(item.Id, [])
                    .Select(projectId => projects.GetValueOrDefault(projectId)
                        ?? new CrmAccountConnectionProjectItemModel(
                            projectId,
                            "Unavailable project",
                            ProjectStatus.Archived))
                    .OrderBy(project => project.Name)
                    .ToList();
                return new CrmAccountConnectedRecordItemModel(
                    item.Id,
                    item.RelatedPartyId,
                    relatedParty.DisplayName,
                    relatedParty.PartyType,
                    item.Role,
                    item.IsPrimary,
                    item.Notes,
                    relatedProjects);
            })
            .ToList();
        return new CrmAccountWorkspaceModel(
            account.Id,
            account.DisplayName,
            account.Summary,
            account.LifecycleStatus,
            roleValues,
            TryDeserializeTags(account.TagsJson),
            ResolvePrimaryContact(contactValues, PartyContactType.Email),
            ResolvePrimaryContact(contactValues, PartyContactType.Phone),
            new CrmAccountProfileEditorModel
            {
                Id = profile?.Id,
                AccountPartyId = account.Id,
                RelationshipStage = ResolveRelationshipStage(account.LifecycleStatus, roleValues, profile),
                CommercialNotes = profile?.CommercialNotes ?? string.Empty,
                ConstraintNotes = profile?.ConstraintNotes ?? string.Empty,
                TimingRiskNotes = profile?.TimingRiskNotes ?? string.Empty,
                LastChangedBy = string.IsNullOrWhiteSpace(profile?.LastChangedBy) ? "crm-hr-ui" : profile.LastChangedBy
            },
            connectedRecords,
            connectedParties,
            opportunityCount);
    }

    public async Task<CrmInteractionDetailModel?> GetAccountInteractionAsync(
        Guid accountPartyId,
        Guid interactionId,
        CancellationToken cancellationToken = default)
    {
        if (accountPartyId == Guid.Empty || interactionId == Guid.Empty)
        {
            return null;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await (
                from interaction in dbContext.Set<InteractionRecord>().AsNoTracking()
                join link in dbContext.Set<InteractionPartyLink>().AsNoTracking()
                    on interaction.Id equals link.InteractionId
                where interaction.Id == interactionId &&
                      link.PartyId == accountPartyId &&
                      link.Role == InteractionPartyRole.Account
                select new CrmInteractionDetailModel(
                    interaction.Id,
                    interaction.InteractionType,
                    interaction.Subject,
                    interaction.RelatedOpportunityId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<CrmActivityHistoryPage> SearchAccountActivityAsync(
        CrmActivityHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var interactions = dbContext.Set<InteractionRecord>()
            .AsNoTracking()
            .Where(interaction => dbContext.Set<InteractionPartyLink>().Any(link =>
                link.PartyId == query.PartyId &&
                link.Role == InteractionPartyRole.Account &&
                link.InteractionId == interaction.Id));
        var auditEntries = dbContext.Set<CrmHrAuditEntry>()
            .AsNoTracking()
            .Where(item =>
                item.EntityType == CrmAccountEntityType &&
                item.EntityId == query.PartyId);

        return await CrmActivityHistoryQueryComposer.SearchAsync(
            dbContext,
            interactions,
            auditEntries,
            query,
            includeParticipantNames: true,
            clock.GetUtcNow(),
            cancellationToken);
    }

    public async Task<Result<Guid>> SaveAccountProfileAsync(CrmAccountProfileEditorModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model.AccountPartyId == Guid.Empty)
        {
            return Result<Guid>.Failure(Error.Validation("Choose an account before saving CRM details.", "crmhr.crm.account-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var party = await dbContext.Set<Party>()
            .SingleOrDefaultAsync(item => item.Id == model.AccountPartyId && item.PartyType == PartyType.Organization, cancellationToken);
        if (party is null)
        {
            return Result<Guid>.Failure(Error.Failure("The selected account no longer exists.", "crmhr.crm.account-missing"));
        }

        var now = clock.GetUtcNow();
        var profile = await dbContext.Set<CrmAccountProfile>()
            .SingleOrDefaultAsync(item => item.AccountPartyId == model.AccountPartyId, cancellationToken);
        if (profile is null)
        {
            profile = new CrmAccountProfile
            {
                AccountPartyId = model.AccountPartyId,
                CreatedAtUtc = now
            };
            dbContext.Set<CrmAccountProfile>().Add(profile);
        }

        profile.RelationshipStage = model.RelationshipStage;
        profile.CommercialNotes = model.CommercialNotes.Trim();
        profile.ConstraintNotes = model.ConstraintNotes.Trim();
        profile.TimingRiskNotes = model.TimingRiskNotes.Trim();
        profile.LastChangedBy = string.IsNullOrWhiteSpace(model.LastChangedBy) ? "crm-hr-ui" : model.LastChangedBy.Trim();
        profile.UpdatedAtUtc = now;

        party.LastChangedBy = profile.LastChangedBy;
        party.UpdatedAtUtc = now;
        party.LifecycleStatus = model.RelationshipStage switch
        {
            CrmAccountRelationshipStage.Prospect => PartyLifecycleStatus.Prospect,
            CrmAccountRelationshipStage.ActiveCustomer => PartyLifecycleStatus.Active,
            CrmAccountRelationshipStage.DormantCustomer => PartyLifecycleStatus.Inactive,
            CrmAccountRelationshipStage.LostCustomer => PartyLifecycleStatus.Inactive,
            _ => party.LifecycleStatus
        };

        var existingRoles = await dbContext.Set<PartyRoleAssignment>()
            .Where(item => item.PartyId == party.Id)
            .ToListAsync(cancellationToken);
        if (model.RelationshipStage == CrmAccountRelationshipStage.ActiveCustomer &&
            existingRoles.All(item => item.RoleKind != PartyRoleKind.Customer))
        {
            dbContext.Set<PartyRoleAssignment>().Add(new PartyRoleAssignment
            {
                PartyId = party.Id,
                RoleKind = PartyRoleKind.Customer,
                Title = "Customer",
                IsPrimary = existingRoles.Count == 0,
                Notes = "Promoted from CRM relationship stage.",
                ValidFromUtc = now
            });
        }

        AddAuditEntry(
            dbContext,
            party.Id,
            "AccountProfileUpdated",
            $"Updated CRM account profile for '{party.DisplayName}'.",
            new
            {
                model.RelationshipStage,
                profile.CommercialNotes,
                profile.ConstraintNotes,
                profile.TimingRiskNotes
            },
            profile.LastChangedBy,
            party.IsSensitive);

        await dbContext.SaveChangesAsync(cancellationToken);
        await UpsertAccountSearchDocumentAsync(party.Id, cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                "AccountProfileUpdated",
                $"Updated account profile for {party.DisplayName}",
                $"Relationship stage: {model.RelationshipStage}",
                ArtifactKind: CrmAccountEntityType,
                ArtifactId: party.Id,
                Route: $"/crm-hr/crm?accountId={party.Id}",
                Actor: profile.LastChangedBy),
            cancellationToken);

        return Result<Guid>.Success(party.Id);
    }

    public async Task<Result> SaveConnectedRecordsAsync(
        Guid accountPartyId,
        IReadOnlyList<CrmAccountConnectionEditorModel> connectedRecords,
        string actor,
        CancellationToken cancellationToken = default)
    {
        if (accountPartyId == Guid.Empty)
        {
            return Result.Failure(Error.Validation(
                "Choose an account before saving connected records.",
                "crmhr.crm.account-required"));
        }

        ArgumentNullException.ThrowIfNull(connectedRecords);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var party = await dbContext.Set<Party>()
            .SingleOrDefaultAsync(item => item.Id == accountPartyId && item.PartyType == PartyType.Organization, cancellationToken);
        if (party is null)
        {
            return Result.Failure(Error.Failure("The selected account no longer exists.", "crmhr.crm.account-missing"));
        }

        var normalizedConnections = connectedRecords
            .Where(item => item.RelatedPartyId != Guid.Empty)
            .Select(item => new CrmAccountConnectionEditorModel
            {
                Id = item.Id,
                RelatedPartyId = item.RelatedPartyId,
                Role = item.Role,
                IsPrimary = item.IsPrimary,
                Notes = item.Notes.Trim(),
                ProjectIds = item.ProjectIds
                    .Where(projectId => projectId != Guid.Empty)
                    .Distinct()
                    .ToList()
            })
            .ToList();
        if (normalizedConnections.Any(item => item.RelatedPartyId == accountPartyId))
        {
            return Result.Failure(Error.Validation(
                "An account cannot be connected to itself.",
                "crmhr.crm.connection-self-reference"));
        }

        var duplicateConnection = normalizedConnections
            .GroupBy(item => new { item.RelatedPartyId, item.Role })
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateConnection is not null)
        {
            return Result.Failure(Error.Validation(
                "The same directory record and relationship role can only be connected once.",
                "crmhr.crm.connection-duplicate"));
        }

        var relatedPartyIds = normalizedConnections
            .Select(item => item.RelatedPartyId)
            .Distinct()
            .ToList();
        var existingRelatedPartyIds = relatedPartyIds.Count == 0
            ? []
            : await dbContext.Set<Party>()
                .AsNoTracking()
                .Where(item => relatedPartyIds.Contains(item.Id))
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);
        if (existingRelatedPartyIds.Count != relatedPartyIds.Count)
        {
            return Result.Failure(Error.Validation(
                "One or more connected directory records no longer exist.",
                "crmhr.crm.connection-party-missing"));
        }

        var requestedProjectIds = normalizedConnections
            .SelectMany(item => item.ProjectIds)
            .Distinct()
            .ToList();
        var existingProjectIds = requestedProjectIds.Count == 0
            ? []
            : await dbContext.Set<Project>()
                .AsNoTracking()
                .Where(item => requestedProjectIds.Contains(item.Id))
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);
        if (existingProjectIds.Count != requestedProjectIds.Count)
        {
            return Result.Failure(Error.Validation(
                "One or more related projects no longer exist.",
                "crmhr.crm.connection-project-missing"));
        }

        var existingConnections = await dbContext.Set<CrmAccountConnection>()
            .Where(item => item.AccountPartyId == accountPartyId)
            .ToListAsync(cancellationToken);
        var existingConnectionIds = existingConnections.Select(item => item.Id).ToList();
        var existingProjectLinks = existingConnectionIds.Count == 0
            ? []
            : await dbContext.Set<CrmAccountConnectionProjectLink>()
                .Where(item => existingConnectionIds.Contains(item.AccountConnectionId))
                .ToListAsync(cancellationToken);

        var now = clock.GetUtcNow();
        var normalizedActor = string.IsNullOrWhiteSpace(actor) ? "crm-hr-ui" : actor.Trim();
        var persistedEditorIds = normalizedConnections
            .Where(item => item.Id.HasValue)
            .Select(item => item.Id!.Value)
            .ToList();
        if (persistedEditorIds.Count != persistedEditorIds.Distinct().Count())
        {
            return Result.Failure(Error.Validation(
                "The same saved connection cannot appear more than once.",
                "crmhr.crm.connection-id-duplicate"));
        }

        var existingConnectionById = existingConnections.ToDictionary(item => item.Id);
        if (persistedEditorIds.Any(connectionId => !existingConnectionById.ContainsKey(connectionId)))
        {
            return Result.Failure(Error.Validation(
                "A connected record changed or belongs to another account. Reload and try again.",
                "crmhr.crm.connection-stale"));
        }

        var requestedIds = persistedEditorIds.ToHashSet();
        var removedConnections = existingConnections
            .Where(item => !requestedIds.Contains(item.Id))
            .ToList();
        if (removedConnections.Count > 0)
        {
            dbContext.Set<CrmAccountConnection>().RemoveRange(removedConnections);
        }

        foreach (var editor in normalizedConnections)
        {
            var connection = editor.Id is Guid connectionId &&
                             existingConnectionById.TryGetValue(connectionId, out var existingConnection)
                ? existingConnection
                : new CrmAccountConnection
                {
                    Id = editor.Id ?? Guid.NewGuid(),
                    AccountPartyId = accountPartyId,
                    CreatedAtUtc = now
                };
            if (connection.AccountPartyId != accountPartyId)
            {
                return Result.Failure(Error.Validation(
                    "A connected record does not belong to the selected account.",
                    "crmhr.crm.connection-account-mismatch"));
            }

            connection.RelatedPartyId = editor.RelatedPartyId;
            connection.Role = editor.Role;
            connection.IsPrimary = editor.IsPrimary;
            connection.Notes = editor.Notes;
            connection.UpdatedAtUtc = now;
            if (dbContext.Entry(connection).State == EntityState.Detached)
            {
                dbContext.Set<CrmAccountConnection>().Add(connection);
            }

            var currentProjectLinks = existingProjectLinks
                .Where(item => item.AccountConnectionId == connection.Id)
                .ToList();
            var desiredProjectIds = editor.ProjectIds.ToHashSet();
            var removedProjectLinks = currentProjectLinks
                .Where(item => !desiredProjectIds.Contains(item.ProjectId))
                .ToList();
            if (removedProjectLinks.Count > 0)
            {
                dbContext.Set<CrmAccountConnectionProjectLink>().RemoveRange(removedProjectLinks);
            }

            var currentProjectIds = currentProjectLinks
                .Select(item => item.ProjectId)
                .ToHashSet();
            dbContext.Set<CrmAccountConnectionProjectLink>().AddRange(
                desiredProjectIds
                    .Where(projectId => !currentProjectIds.Contains(projectId))
                    .Select(projectId => new CrmAccountConnectionProjectLink
                    {
                        AccountConnectionId = connection.Id,
                        ProjectId = projectId,
                        CreatedAtUtc = now
                    }));
        }

        AddAuditEntry(
            dbContext,
            accountPartyId,
            "AccountConnectionsUpdated",
            $"Updated CRM connected records for '{party.DisplayName}'.",
            normalizedConnections.Select(item => new
            {
                item.RelatedPartyId,
                item.Role,
                item.IsPrimary,
                item.Notes,
                item.ProjectIds
            }),
            normalizedActor,
            party.IsSensitive);

        await dbContext.SaveChangesAsync(cancellationToken);
        await UpsertAccountSearchDocumentAsync(accountPartyId, cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                "AccountConnectionsUpdated",
                $"Updated connected records for {party.DisplayName}",
                $"{normalizedConnections.Count} account connection(s) saved.",
                ArtifactKind: CrmAccountEntityType,
                ArtifactId: accountPartyId,
                Route: $"/crm-hr/crm?accountId={accountPartyId}",
                Actor: normalizedActor),
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result<Guid>> AddInteractionAsync(
        Guid accountPartyId,
        CrmInteractionEditorModel model,
        string actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (accountPartyId == Guid.Empty)
        {
            return Result<Guid>.Failure(Error.Validation("Choose an account before logging an interaction.", "crmhr.crm.account-required"));
        }

        if (string.IsNullOrWhiteSpace(model.Subject))
        {
            return Result<Guid>.Failure(Error.Validation("Interaction subject is required.", "crmhr.crm.interaction-subject-required"));
        }

        if (!string.IsNullOrWhiteSpace(model.NextActionText) && !model.NextActionOwnerPartyId.HasValue)
        {
            return Result<Guid>.Failure(Error.Validation("Choose a next-action owner when a next action is provided.", "crmhr.crm.next-action-owner-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var party = await dbContext.Set<Party>()
            .SingleOrDefaultAsync(item => item.Id == accountPartyId && item.PartyType == PartyType.Organization, cancellationToken);
        if (party is null)
        {
            return Result<Guid>.Failure(Error.Failure("The selected account no longer exists.", "crmhr.crm.account-missing"));
        }

        var now = clock.GetUtcNow();
        var normalizedActor = string.IsNullOrWhiteSpace(actor) ? "crm-hr-ui" : actor.Trim();
        var connectionRoles = await dbContext.Set<CrmAccountConnection>()
            .Where(item => item.AccountPartyId == accountPartyId && model.ParticipantPartyIds.Contains(item.RelatedPartyId))
            .ToListAsync(cancellationToken);
        var connectionRoleByPartyId = connectionRoles
            .GroupBy(item => item.RelatedPartyId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Role).First());

        var interaction = new InteractionRecord
        {
            InteractionType = model.InteractionType,
            Subject = model.Subject.Trim(),
            OccurredAtUtc = ToUtcDate(model.OccurredOn),
            Summary = model.Summary.Trim(),
            Notes = model.Notes.Trim(),
            NextActionText = model.NextActionText.Trim(),
            NextActionOwnerPartyId = model.NextActionOwnerPartyId,
            NextActionDueUtc = model.NextActionDueOn.HasValue ? ToUtcDate(model.NextActionDueOn.Value) : null,
            RelatedOpportunityId = model.RelatedOpportunityId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.Set<InteractionRecord>().Add(interaction);
        dbContext.Set<InteractionPartyLink>().Add(new InteractionPartyLink
        {
            InteractionId = interaction.Id,
            PartyId = accountPartyId,
            Role = InteractionPartyRole.Account
        });

        foreach (var participantPartyId in model.ParticipantPartyIds.Distinct().Where(item => item != Guid.Empty && item != accountPartyId))
        {
            dbContext.Set<InteractionPartyLink>().Add(new InteractionPartyLink
            {
                InteractionId = interaction.Id,
                PartyId = participantPartyId,
                Role = ResolveInteractionRole(connectionRoleByPartyId.GetValueOrDefault(participantPartyId))
            });
        }

        AddAuditEntry(
            dbContext,
            accountPartyId,
            "InteractionLogged",
            $"Logged {model.InteractionType} interaction '{interaction.Subject}' for '{party.DisplayName}'.",
            new
            {
                interaction.Subject,
                interaction.InteractionType,
                model.ParticipantPartyIds,
                interaction.NextActionText,
                interaction.NextActionDueUtc,
                interaction.NextActionOwnerPartyId
            },
            normalizedActor,
            party.IsSensitive);

        await dbContext.SaveChangesAsync(cancellationToken);
        await UpsertAccountSearchDocumentAsync(accountPartyId, cancellationToken);
        await UpsertInteractionSearchDocumentAsync(interaction.Id, cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                "InteractionLogged",
                $"Logged {interaction.InteractionType} for {party.DisplayName}",
                interaction.Subject,
                ArtifactKind: nameof(InteractionRecord),
                ArtifactId: interaction.Id,
                Route: $"/crm-hr/crm?accountId={accountPartyId}",
                Actor: normalizedActor),
            cancellationToken);

        return Result<Guid>.Success(interaction.Id);
    }

    public async Task<Result<Guid>> SaveOpportunityAsync(
        CrmOpportunityEditorModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model.AccountPartyId == Guid.Empty)
        {
            return Result<Guid>.Failure(Error.Validation("Choose an account before saving an opportunity.", "crmhr.crm.opportunity-account-required"));
        }

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            return Result<Guid>.Failure(Error.Validation("Opportunity title is required.", "crmhr.crm.opportunity-title-required"));
        }

        if (model.OwnerPartyId == Guid.Empty)
        {
            return Result<Guid>.Failure(Error.Validation("Choose an owner before saving an opportunity.", "crmhr.crm.opportunity-owner-required"));
        }

        if (model.ProbabilityPercent is < 0 or > 100)
        {
            return Result<Guid>.Failure(Error.Validation("Probability must stay between 0 and 100.", "crmhr.crm.opportunity-probability-range"));
        }

        if (model.Stage == OpportunityStage.Lost && string.IsNullOrWhiteSpace(model.LostReason))
        {
            return Result<Guid>.Failure(Error.Validation("Lost opportunities require a loss reason.", "crmhr.crm.opportunity-lost-reason-required"));
        }

        var normalizedCurrencyCode = NormalizeOpportunityCurrencyCode(model.CurrencyCode);
        if (normalizedCurrencyCode is null)
        {
            return Result<Guid>.Failure(Error.Validation(
                "Currency must be a three-letter ASCII code.",
                "crmhr.crm.opportunity-currency-invalid"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var account = await dbContext.Set<Party>()
            .SingleOrDefaultAsync(
                item => item.Id == model.AccountPartyId && item.PartyType == PartyType.Organization,
                cancellationToken);
        if (account is null)
        {
            return Result<Guid>.Failure(Error.Failure("The selected account no longer exists.", "crmhr.crm.opportunity-account-missing"));
        }

        var entity = model.Id.HasValue
            ? await dbContext.Set<Opportunity>().SingleOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;
        if (model.Id.HasValue && entity is null)
        {
            return Result<Guid>.Failure(Error.Failure(
                "The opportunity no longer exists.",
                "crmhr.crm.opportunity-missing"));
        }

        if (entity is not null && !model.ExpectedUpdatedAtUtc.HasValue)
        {
            return Result<Guid>.Failure(Error.Validation(
                "Reload the opportunity before saving it.",
                "crmhr.crm.opportunity-expected-updated-at-required"));
        }

        if (entity is not null && entity.UpdatedAtUtc != model.ExpectedUpdatedAtUtc)
        {
            return Result<Guid>.Failure(Error.Failure(
                "The opportunity changed after it was loaded. Reload it before saving.",
                "crmhr.crm.opportunity-concurrency-conflict"));
        }

        if (entity is not null && entity.AccountPartyId != model.AccountPartyId)
        {
            return Result<Guid>.Failure(Error.Validation("Move the account selection before editing a different opportunity.", "crmhr.crm.opportunity-account-mismatch"));
        }

        var normalizedLinks = model.Parties
            .Where(item => item.PartyId != Guid.Empty && item.PartyId != model.AccountPartyId)
            .GroupBy(item => new { item.PartyId, item.Role })
            .Select(group => group.First())
            .ToList();
        var referencedPartyIds = normalizedLinks
            .Select(item => item.PartyId)
            .Append(model.OwnerPartyId)
            .Concat(model.DeliveryUnitPartyId.HasValue ? [model.DeliveryUnitPartyId.Value] : [])
            .Distinct()
            .ToList();
        var knownParties = referencedPartyIds.Count == 0
            ? []
            : await dbContext.Set<Party>()
                .Where(item => referencedPartyIds.Contains(item.Id))
                .Select(item => new PartyOptionModel(item.Id, item.DisplayName, item.PartyType))
                .ToListAsync(cancellationToken);
        if (knownParties.Count != referencedPartyIds.Count)
        {
            return Result<Guid>.Failure(Error.Failure("One or more selected opportunity parties no longer exist.", "crmhr.crm.opportunity-party-missing"));
        }

        var knownPartiesById = knownParties.ToDictionary(item => item.Id);
        var owner = knownPartiesById[model.OwnerPartyId];
        if (owner.PartyType is not (PartyType.Person or PartyType.OrganizationUnit or PartyType.AiAgent))
        {
            return Result<Guid>.Failure(Error.Validation(
                "Opportunity owners must be a person, organization unit, or AI agent.",
                "crmhr.crm.opportunity-owner-type-invalid"));
        }

        if (model.DeliveryUnitPartyId is Guid deliveryUnitPartyId &&
            knownPartiesById[deliveryUnitPartyId].PartyType != PartyType.OrganizationUnit)
        {
            return Result<Guid>.Failure(Error.Validation(
                "Opportunity delivery units must be organization units.",
                "crmhr.crm.opportunity-delivery-unit-type-invalid"));
        }

        if (model.LinkedProjectId.HasValue)
        {
            var linkedProject = await projectRecordQueryService.GetAsync(
                model.LinkedProjectId.Value,
                cancellationToken);
            if (linkedProject is null)
            {
                return Result<Guid>.Failure(Error.Validation("The linked project was not found.", "crmhr.crm.opportunity-linked-project-missing"));
            }
        }

        var normalizedActor = NormalizeActor(model.LastChangedBy);
        var now = clock.GetUtcNow();
        var isNew = !model.Id.HasValue;
        var previousStage = entity?.Stage;

        if (entity is null)
        {
            entity = new Opportunity
            {
                AccountPartyId = model.AccountPartyId,
                CreatedAtUtc = now
            };
            dbContext.Set<Opportunity>().Add(entity);
        }

        var accountProfile = await dbContext.Set<CrmAccountProfile>()
            .SingleOrDefaultAsync(item => item.AccountPartyId == model.AccountPartyId, cancellationToken);
        var accountRoles = await dbContext.Set<PartyRoleAssignment>()
            .Where(item => item.PartyId == model.AccountPartyId)
            .Select(item => item.RoleKind)
            .ToListAsync(cancellationToken);

        entity.Title = model.Title.Trim();
        entity.Stage = model.Stage;
        entity.RelationshipStage = string.IsNullOrWhiteSpace(model.RelationshipStage)
            ? ResolveRelationshipStage(account.LifecycleStatus, accountRoles, accountProfile).ToString()
            : model.RelationshipStage.Trim();
        entity.OpportunitySource = model.OpportunitySource;
        entity.OwnerPartyId = model.OwnerPartyId;
        entity.DeliveryUnitPartyId = model.DeliveryUnitPartyId;
        entity.LinkedProjectId = model.LinkedProjectId;
        entity.CurrencyCode = normalizedCurrencyCode;
        entity.Amount = model.Amount;
        entity.ProbabilityPercent = model.ProbabilityPercent;
        entity.ExpectedCloseDateUtc = model.ExpectedCloseOn.HasValue ? ToUtcDate(model.ExpectedCloseOn.Value) : null;
        entity.LostReason = model.Stage == OpportunityStage.Lost ? model.LostReason.Trim() : string.Empty;
        entity.Summary = model.Summary.Trim();
        entity.Notes = model.Notes.Trim();
        entity.ExtendedDataJson = JsonSerializer.Serialize(new CrmOpportunityExtendedDataModel
        {
            CompetitorName = model.CompetitorName.Trim(),
            PartnerContributionSummary = model.PartnerContributionSummary.Trim()
        });
        entity.UpdatedAtUtc = !isNew && entity.UpdatedAtUtc >= now
            ? entity.UpdatedAtUtc.AddTicks(1)
            : now;

        var existingLinks = await dbContext.Set<OpportunityPartyLink>()
            .Where(item => item.OpportunityId == entity.Id)
            .ToListAsync(cancellationToken);
        dbContext.Set<OpportunityPartyLink>().RemoveRange(existingLinks);
        dbContext.Set<OpportunityPartyLink>().AddRange(normalizedLinks.Select(item => new OpportunityPartyLink
        {
            Id = item.Id ?? Guid.NewGuid(),
            OpportunityId = entity.Id,
            PartyId = item.PartyId,
            Role = item.Role
        }));

        if (isNew || previousStage != model.Stage)
        {
            dbContext.Set<OpportunityStageHistory>().Add(new OpportunityStageHistory
            {
                OpportunityId = entity.Id,
                Stage = model.Stage,
                ChangedAtUtc = now,
                ChangedBy = normalizedActor,
                Notes = model.StageNotes.Trim(),
                RecognizedAmount = model.Stage == OpportunityStage.Won ? model.Amount : null,
                RecognizedCurrencyCode = model.Stage == OpportunityStage.Won
                    ? normalizedCurrencyCode
                    : string.Empty
            });
        }

        account.LastChangedBy = normalizedActor;
        account.UpdatedAtUtc = now;

        var auditAction = isNew
            ? "OpportunityCreated"
            : previousStage != model.Stage
                ? "OpportunityStageChanged"
                : "OpportunityUpdated";
        var auditSummary = isNew
            ? $"Created opportunity '{entity.Title}' for '{account.DisplayName}'."
            : previousStage != model.Stage
                ? $"Moved opportunity '{entity.Title}' to {entity.Stage}."
                : $"Updated opportunity '{entity.Title}'.";

        AddAuditEntry(
            dbContext,
            account.Id,
            auditAction,
            auditSummary,
            new
            {
                entity.Id,
                entity.Title,
                entity.Stage,
                entity.OpportunitySource,
                entity.Amount,
                entity.ProbabilityPercent,
                entity.ExpectedCloseDateUtc,
                entity.LinkedProjectId,
                PartyCount = normalizedLinks.Count
            },
            normalizedActor,
            account.IsSensitive);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<Guid>.Failure(Error.Failure(
                "The opportunity changed while it was being saved. Reload it before retrying.",
                "crmhr.crm.opportunity-concurrency-conflict"));
        }

        await UpsertAccountSearchDocumentAsync(account.Id, cancellationToken);
        await UpsertOpportunitySearchDocumentAsync(entity.Id, cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                auditAction,
                isNew ? $"Created opportunity {entity.Title}" : $"Updated opportunity {entity.Title}",
                $"{account.DisplayName} / {entity.Stage} / {entity.OpportunitySource}",
                ArtifactKind: CrmOpportunityEntityType,
                ArtifactId: entity.Id,
                Route: BuildOpportunityRoute(account.Id, entity.Id),
                Actor: normalizedActor),
            cancellationToken);

        return Result<Guid>.Success(entity.Id);
    }

    public async Task<Result<CrmOpportunityConversionResult>> ConvertOpportunityToProjectAsync(
        CrmOpportunityConversionEditorModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model.OpportunityId == Guid.Empty)
        {
            return Result<CrmOpportunityConversionResult>.Failure(Error.Validation(
                "Choose an opportunity before converting it to a project.",
                "crmhr.crm.opportunity-conversion-opportunity-required"));
        }

        if (model.LinkExistingProject && !model.ExistingProjectId.HasValue)
        {
            return Result<CrmOpportunityConversionResult>.Failure(Error.Validation(
                "Choose an existing project before linking it.",
                "crmhr.crm.opportunity-conversion-project-required"));
        }

        if (!model.LinkExistingProject && string.IsNullOrWhiteSpace(model.ProjectName))
        {
            return Result<CrmOpportunityConversionResult>.Failure(Error.Validation(
                "Project name is required when creating a new project.",
                "crmhr.crm.opportunity-conversion-project-name-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var opportunity = await dbContext.Set<Opportunity>()
            .SingleOrDefaultAsync(item => item.Id == model.OpportunityId, cancellationToken);
        if (opportunity is null)
        {
            return Result<CrmOpportunityConversionResult>.Failure(Error.Failure(
                "The selected opportunity no longer exists.",
                "crmhr.crm.opportunity-conversion-opportunity-missing"));
        }

        if (!model.ExpectedUpdatedAtUtc.HasValue)
        {
            return Result<CrmOpportunityConversionResult>.Failure(Error.Validation(
                "Reload the opportunity before converting it.",
                "crmhr.crm.opportunity-conversion-expected-updated-at-required"));
        }

        if (opportunity.UpdatedAtUtc != model.ExpectedUpdatedAtUtc.Value)
        {
            return Result<CrmOpportunityConversionResult>.Failure(Error.Failure(
                "The opportunity changed after it was loaded. Reload it before converting.",
                "crmhr.crm.opportunity-conversion-concurrency-conflict"));
        }

        dbContext.Entry(opportunity)
            .Property(item => item.UpdatedAtUtc)
            .OriginalValue = model.ExpectedUpdatedAtUtc.Value;

        if (opportunity.Stage != OpportunityStage.Won)
        {
            return Result<CrmOpportunityConversionResult>.Failure(Error.Validation(
                "Only won opportunities can be converted into projects.",
                "crmhr.crm.opportunity-conversion-stage-invalid"));
        }

        var account = await dbContext.Set<Party>()
            .SingleAsync(item => item.Id == opportunity.AccountPartyId, cancellationToken);
        var opportunityDetail = (await LoadOpportunityDetailsAsync(dbContext, [opportunity.Id], cancellationToken))
            .FirstOrDefault();
        if (opportunityDetail is null)
        {
            return Result<CrmOpportunityConversionResult>.Failure(Error.Failure(
                "The selected opportunity could not be loaded.",
                "crmhr.crm.opportunity-conversion-load-failed"));
        }

        var normalizedActor = NormalizeActor(model.LastChangedBy);
        var createdNewProject = false;
        Guid projectId;
        string projectName;

        if (model.LinkExistingProject)
        {
            var project = await projectRecordQueryService.GetAsync(
                model.ExistingProjectId!.Value,
                cancellationToken);
            if (project is null)
            {
                return Result<CrmOpportunityConversionResult>.Failure(Error.Validation(
                    "The selected project no longer exists.",
                    "crmhr.crm.opportunity-conversion-project-missing"));
            }

            projectId = project.Id;
            projectName = project.Name;
        }
        else
        {
            var projectResult = await projectsService.SaveAsync(
                new ProjectEditorModel
                {
                    Name = model.ProjectName.Trim(),
                    Description = string.IsNullOrWhiteSpace(model.ProjectDescription)
                        ? opportunityDetail.Summary
                        : model.ProjectDescription.Trim(),
                    Objective = string.IsNullOrWhiteSpace(model.ProjectObjective)
                        ? opportunityDetail.Title
                        : model.ProjectObjective.Trim(),
                    Status = ProjectStatus.Active,
                    CurrentPhase = string.IsNullOrWhiteSpace(model.CurrentPhase) ? "Sales handoff" : model.CurrentPhase.Trim()
                },
                cancellationToken);
            if (!projectResult.IsSuccess)
            {
                return Result<CrmOpportunityConversionResult>.Failure(projectResult.Errors.ToArray());
            }

            createdNewProject = true;
            projectId = projectResult.Value;
            projectName = model.ProjectName.Trim();
        }

        var opportunityAssignments = BuildOpportunityProjectAssignments(
            opportunityDetail,
            projectId);
        var targetRoles = opportunityAssignments
            .Select(assignment => assignment.Role)
            .Distinct()
            .ToList();
        var existingProjectAssignments = await projectPartyIntegrationBridge
            .ListAssignmentsDetailedAsync(projectId, targetRoles, cancellationToken);
        var originalRootAssignments = existingProjectAssignments
            .Where(assignment => string.IsNullOrWhiteSpace(assignment.NodeKey))
            .Select(ToProjectAssignmentRequest)
            .ToList();
        var desiredProjectAssignments = opportunityAssignments
            .Concat(originalRootAssignments)
            .GroupBy(assignment => new
            {
                assignment.PartyId,
                assignment.Role
            })
            .Select(group => group.First())
            .ToList();
        var assignmentResult = await projectPartyIntegrationBridge
            .ReplaceProjectAssignmentsAsync(
                projectId,
                desiredProjectAssignments,
                targetRoles,
                cancellationToken);
        if (!assignmentResult.IsSuccess)
        {
            if (createdNewProject)
            {
                await CompensateOpportunityConversionAsync(
                    createdNewProject,
                    projectId,
                    originalRootAssignments,
                    targetRoles,
                    cancellationToken);
            }

            return Result<CrmOpportunityConversionResult>.Failure(
                assignmentResult.Errors.ToArray());
        }

        var now = clock.GetUtcNow();
        opportunity.LinkedProjectId = projectId;
        opportunity.UpdatedAtUtc = opportunity.UpdatedAtUtc >= now
            ? opportunity.UpdatedAtUtc.AddTicks(1)
            : now;
        account.LastChangedBy = normalizedActor;
        account.UpdatedAtUtc = now;

        AddAuditEntry(
            dbContext,
            account.Id,
            "OpportunityConvertedToProject",
            $"Converted opportunity '{opportunityDetail.Title}' to project '{projectName}'.",
            new
            {
                opportunity.Id,
                ProjectId = projectId,
                CreatedNewProject = createdNewProject
            },
            normalizedActor,
            account.IsSensitive);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            await CompensateOpportunityConversionAsync(
                createdNewProject,
                projectId,
                originalRootAssignments,
                targetRoles,
                cancellationToken);
            if (SerializableMutationScope.IsConflict(exception))
            {
                return Result<CrmOpportunityConversionResult>.Failure(Error.Failure(
                    "The opportunity changed while it was being converted. Reload it before retrying.",
                    "crmhr.crm.opportunity-conversion-concurrency-conflict"));
            }

            throw;
        }

        await UpsertAccountSearchDocumentAsync(account.Id, cancellationToken);
        await UpsertOpportunitySearchDocumentAsync(opportunity.Id, cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                "OpportunityConvertedToProject",
                $"Converted opportunity {opportunityDetail.Title} to project",
                projectName,
                ArtifactKind: CrmOpportunityEntityType,
                ArtifactId: opportunity.Id,
                Route: BuildOpportunityRoute(account.Id, opportunity.Id),
                Actor: normalizedActor),
            cancellationToken);

        return Result<CrmOpportunityConversionResult>.Success(new CrmOpportunityConversionResult(
            opportunity.Id,
            projectId,
            createdNewProject));
    }

    private async Task CompensateOpportunityConversionAsync(
        bool createdNewProject,
        Guid projectId,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> originalRootAssignments,
        IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
        CancellationToken cancellationToken)
    {
        var restoreResult = await projectPartyIntegrationBridge
            .ReplaceProjectAssignmentsAsync(
                projectId,
                originalRootAssignments,
                targetRoles,
                cancellationToken);
        if (!restoreResult.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Failed to restore project '{projectId}' after opportunity conversion failed: " +
                string.Join(" ", restoreResult.Errors.Select(error => error.Message)));
        }

        if (createdNewProject)
        {
            await projectsService.DeleteAsync(projectId, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<CrmOpportunityDetailModel>> LoadOpportunityDetailsAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<Guid> opportunityIds,
        CancellationToken cancellationToken)
    {
        if (opportunityIds.Count == 0)
        {
            return [];
        }

        var opportunities = await dbContext.Set<Opportunity>()
            .Where(item => opportunityIds.Contains(item.Id))
            .Select(item => new
            {
                item.Id,
                item.AccountPartyId,
                item.Title,
                item.Stage,
                item.RelationshipStage,
                item.OpportunitySource,
                item.OwnerPartyId,
                item.DeliveryUnitPartyId,
                item.CurrencyCode,
                item.Amount,
                item.ProbabilityPercent,
                item.ExpectedCloseDateUtc,
                item.LostReason,
                item.Summary,
                item.Notes,
                item.ExtendedDataJson,
                item.LinkedProjectId,
                item.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);
        var partyLinks = await dbContext.Set<OpportunityPartyLink>()
            .Where(item => opportunityIds.Contains(item.OpportunityId))
            .ToListAsync(cancellationToken);
        var stageHistory = await dbContext.Set<OpportunityStageHistory>()
            .Where(item => opportunityIds.Contains(item.OpportunityId))
            .ToListAsync(cancellationToken);
        var partyIds = opportunities
            .Select(item => item.AccountPartyId)
            .Concat(opportunities.Select(item => item.OwnerPartyId))
            .Concat(opportunities.Where(item => item.DeliveryUnitPartyId.HasValue).Select(item => item.DeliveryUnitPartyId!.Value))
            .Concat(partyLinks.Select(item => item.PartyId))
            .Distinct()
            .ToList();
        var partiesById = partyIds.Count == 0
            ? new Dictionary<Guid, PartyOptionModel>()
            : (await dbContext.Set<Party>()
                .Where(item => partyIds.Contains(item.Id))
                .Select(item => new PartyOptionModel(item.Id, item.DisplayName, item.PartyType))
                .ToListAsync(cancellationToken))
                .ToDictionary(item => item.Id);
        var linkedProjectIds = opportunities
            .Where(item => item.LinkedProjectId.HasValue)
            .Select(item => item.LinkedProjectId!.Value)
            .Distinct()
            .ToList();
        var projectNamesById = linkedProjectIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await projectRecordQueryService.GetManyAsync(linkedProjectIds, cancellationToken))
                .ToDictionary(item => item.Id, item => item.Name);
        var partyLinksByOpportunityId = partyLinks
            .GroupBy(item => item.OpportunityId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var stageHistoryByOpportunityId = stageHistory
            .GroupBy(item => item.OpportunityId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.ChangedAtUtc).ToList());

        return opportunities
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item =>
            {
                var extendedData = DeserializeOpportunityExtendedData(item.ExtendedDataJson);
                var detailLinks = (partyLinksByOpportunityId.GetValueOrDefault(item.Id) ?? [])
                    .Select(link =>
                    {
                        var linkedParty = RequireOpportunityParty(
                            partiesById,
                            link.PartyId,
                            item.Id,
                            $"party link '{link.Id}'");
                        return new CrmOpportunityPartyLinkItemModel(
                            link.Id,
                            link.PartyId,
                            linkedParty.DisplayName,
                            linkedParty.PartyType,
                            link.Role);
                    })
                    .OrderBy(link => link.Role)
                    .ThenBy(link => link.DisplayName)
                    .ToList();
                var detailHistory = (stageHistoryByOpportunityId.GetValueOrDefault(item.Id) ?? [])
                    .Select(history => new OpportunityStageHistoryItemModel(
                        history.Id,
                        history.Stage,
                        history.ChangedAtUtc,
                        history.ChangedBy,
                        history.Notes))
                    .ToList();
                var accountParty = RequireOpportunityParty(
                    partiesById,
                    item.AccountPartyId,
                    item.Id,
                    "account");
                var ownerParty = RequireOpportunityParty(
                    partiesById,
                    item.OwnerPartyId,
                    item.Id,
                    "owner");
                var deliveryUnit = item.DeliveryUnitPartyId.HasValue
                    ? RequireOpportunityParty(
                        partiesById,
                        item.DeliveryUnitPartyId.Value,
                        item.Id,
                        "delivery unit")
                    : null;
                var linkedProjectName = string.Empty;
                if (item.LinkedProjectId is Guid linkedProjectId &&
                    !projectNamesById.TryGetValue(linkedProjectId, out linkedProjectName))
                {
                    throw new InvalidOperationException(
                        $"Opportunity '{item.Id}' references missing project '{linkedProjectId}'.");
                }

                return new CrmOpportunityDetailModel(
                    item.Id,
                    item.AccountPartyId,
                    accountParty.DisplayName,
                    item.Title,
                    item.Stage,
                    item.RelationshipStage,
                    item.OpportunitySource,
                    item.OwnerPartyId,
                    ownerParty.DisplayName,
                    item.DeliveryUnitPartyId,
                    deliveryUnit?.DisplayName ?? string.Empty,
                    item.CurrencyCode.Trim().ToUpperInvariant(),
                    item.Amount,
                    item.ProbabilityPercent,
                    ToDateOnly(item.ExpectedCloseDateUtc),
                    item.LostReason,
                    extendedData.CompetitorName,
                    extendedData.PartnerContributionSummary,
                    item.Summary,
                    item.Notes,
                    item.LinkedProjectId,
                    linkedProjectName,
                    detailLinks,
                    detailHistory,
                    item.UpdatedAtUtc);
            })
            .ToList();
    }

    private async Task UpsertOpportunitySearchDocumentAsync(Guid opportunityId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var opportunity = (await LoadOpportunityDetailsAsync(dbContext, [opportunityId], cancellationToken))
            .FirstOrDefault();
        if (opportunity is null)
        {
            await searchIndexService.DeleteAsync(CrmOpportunitySearchSourceType, opportunityId.ToString("N"), cancellationToken);
            return;
        }

        var accountIsSensitive = await dbContext.Set<Party>()
            .Where(item => item.Id == opportunity.AccountPartyId)
            .Select(item => item.IsSensitive)
            .FirstOrDefaultAsync(cancellationToken);
        if (accountIsSensitive)
        {
            await searchIndexService.DeleteAsync(CrmOpportunitySearchSourceType, opportunityId.ToString("N"), cancellationToken);
            return;
        }

        var summaryParts = new List<string>
        {
            opportunity.Stage.ToString(),
            opportunity.OpportunitySource.ToString()
        };

        if (opportunity.Amount.HasValue)
        {
            summaryParts.Add($"{opportunity.CurrencyCode} {opportunity.Amount.Value:0.##}");
        }

        if (opportunity.ProbabilityPercent > 0)
        {
            summaryParts.Add($"{opportunity.ProbabilityPercent}%");
        }

        var bodyParts = new List<string>
        {
            opportunity.AccountDisplayName,
            opportunity.OwnerDisplayName,
            opportunity.DeliveryUnitDisplayName,
            opportunity.Summary,
            opportunity.Notes,
            opportunity.LostReason,
            opportunity.CompetitorName,
            opportunity.PartnerContributionSummary,
            opportunity.LinkedProjectName,
            string.Join(", ", opportunity.Parties.Select(item => $"{item.Role}:{item.DisplayName}")),
            string.Join(Environment.NewLine, opportunity.StageHistory.Select(item => $"{item.Stage} {item.ChangedAtUtc:yyyy-MM-dd} {item.Notes}"))
        };

        await searchIndexService.UpsertAsync(
            new SearchDocumentInput(
                CrmOpportunitySearchSourceType,
                opportunity.Id.ToString("N"),
                "CRM / HR opportunity",
                opportunity.Title,
                string.Join(" / ", summaryParts),
                string.Join(Environment.NewLine, bodyParts.Where(item => !string.IsNullOrWhiteSpace(item))),
                BuildOpportunityRoute(opportunity.AccountPartyId, opportunity.Id)),
            cancellationToken);
    }

    private async Task UpsertAccountSearchDocumentAsync(Guid accountPartyId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var party = await dbContext.Set<Party>().SingleOrDefaultAsync(item => item.Id == accountPartyId, cancellationToken);
        if (party is null)
        {
            await searchIndexService.DeleteAsync(CrmAccountSearchSourceType, accountPartyId.ToString("N"), cancellationToken);
            return;
        }

        if (party.IsSensitive)
        {
            await searchIndexService.DeleteAsync(CrmAccountSearchSourceType, accountPartyId.ToString("N"), cancellationToken);
            return;
        }

        var roles = await dbContext.Set<PartyRoleAssignment>()
            .Where(item => item.PartyId == accountPartyId)
            .OrderBy(item => item.RoleKind)
            .Select(item => item.RoleKind)
            .ToListAsync(cancellationToken);
        var profile = await dbContext.Set<CrmAccountProfile>()
            .SingleOrDefaultAsync(item => item.AccountPartyId == accountPartyId, cancellationToken);
        var connections = await dbContext.Set<CrmAccountConnection>()
            .Where(item => item.AccountPartyId == accountPartyId)
            .ToListAsync(cancellationToken);
        var connectedPartyIds = connections.Select(item => item.RelatedPartyId).Distinct().ToList();
        var connectedPartyNames = connectedPartyIds.Count == 0
            ? []
            : await dbContext.Set<Party>()
                .Where(item => connectedPartyIds.Contains(item.Id))
                .OrderBy(item => item.DisplayName)
                .Select(item => item.DisplayName)
                .ToListAsync(cancellationToken);
        var interactionIds = await dbContext.Set<InteractionPartyLink>()
            .Where(item => item.PartyId == accountPartyId && item.Role == InteractionPartyRole.Account)
            .Select(item => item.InteractionId)
            .ToListAsync(cancellationToken);
        var recentInteractionSubjects = interactionIds.Count == 0
            ? []
            : await dbContext.Set<InteractionRecord>()
                .AsNoTracking()
                .Where(item => interactionIds.Contains(item.Id))
                .OrderByDescending(item => item.OccurredAtUtc)
                .Take(5)
                .Select(item => item.Subject)
                .ToListAsync(cancellationToken);

        var relationshipStage = ResolveRelationshipStage(party.LifecycleStatus, roles, profile);
        var summary = $"{relationshipStage} / {string.Join(", ", roles.Take(3))}".Trim(' ', '/');
        var bodyParts = new List<string>
        {
            party.DisplayName,
            party.Summary,
            profile?.CommercialNotes ?? string.Empty,
            profile?.ConstraintNotes ?? string.Empty,
            profile?.TimingRiskNotes ?? string.Empty,
            string.Join(", ", connectedPartyNames),
            string.Join(Environment.NewLine, recentInteractionSubjects)
        };

        await searchIndexService.UpsertAsync(
            new SearchDocumentInput(
                CrmAccountSearchSourceType,
                accountPartyId.ToString("N"),
                "CRM / HR account",
                party.DisplayName,
                string.IsNullOrWhiteSpace(summary) ? "CRM account" : summary,
                string.Join(Environment.NewLine, bodyParts.Where(item => !string.IsNullOrWhiteSpace(item))),
                $"/crm-hr/crm?accountId={accountPartyId}"),
            cancellationToken);
    }

    private static void AddAuditEntry(
        AppDbContext dbContext,
        Guid entityId,
        string action,
        string summary,
        object detail,
        string actor,
        bool isSensitive)
    {
        dbContext.Set<CrmHrAuditEntry>().Add(new CrmHrAuditEntry
        {
            EntityType = CrmAccountEntityType,
            EntityId = entityId,
            Action = action,
            Summary = summary,
            DetailJson = JsonSerializer.Serialize(detail),
            Actor = actor,
            IsSensitive = isSensitive,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
    }

    private static DateTimeOffset ToUtcDate(DateOnly value)
    {
        return new DateTimeOffset(value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }

    private static string? NormalizeOpportunityCurrencyCode(string? currencyCode)
    {
        var normalized = currencyCode?.Trim().ToUpperInvariant() ?? string.Empty;
        return normalized is [var first, var second, var third]
            && char.IsAsciiLetter(first)
            && char.IsAsciiLetter(second)
            && char.IsAsciiLetter(third)
            ? normalized
            : null;
    }

    private static PartyOptionModel RequireOpportunityParty(
        IReadOnlyDictionary<Guid, PartyOptionModel> partiesById,
        Guid partyId,
        Guid opportunityId,
        string referenceKind)
    {
        if (partiesById.TryGetValue(partyId, out var party))
        {
            return party;
        }

        throw new InvalidOperationException(
            $"Opportunity '{opportunityId}' references missing {referenceKind} party '{partyId}'.");
    }

    private static CrmAccountRelationshipStage ResolveRelationshipStage(
        PartyLifecycleStatus lifecycleStatus,
        IReadOnlyList<PartyRoleKind> roles,
        CrmAccountProfile? profile)
    {
        if (profile is not null)
        {
            return profile.RelationshipStage;
        }

        if (lifecycleStatus == PartyLifecycleStatus.Prospect)
        {
            return CrmAccountRelationshipStage.Prospect;
        }

        if (roles.Contains(PartyRoleKind.Customer))
        {
            return CrmAccountRelationshipStage.ActiveCustomer;
        }

        return lifecycleStatus switch
        {
            PartyLifecycleStatus.Inactive or PartyLifecycleStatus.Former => CrmAccountRelationshipStage.DormantCustomer,
            _ => CrmAccountRelationshipStage.Prospect
        };
    }

    private static InteractionPartyRole ResolveInteractionRole(CrmAccountConnectionRole connectionRole)
    {
        return connectionRole switch
        {
            CrmAccountConnectionRole.AccountManager
                or CrmAccountConnectionRole.DeliveryLead
                or CrmAccountConnectionRole.Sponsor
                or CrmAccountConnectionRole.Stakeholder => InteractionPartyRole.Stakeholder,
            _ => InteractionPartyRole.Contact
        };
    }

    private static IReadOnlyList<string> TryDeserializeTags(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static CrmOpportunityExtendedDataModel DeserializeOpportunityExtendedData(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CrmOpportunityExtendedDataModel();
        }

        try
        {
            return JsonSerializer.Deserialize<CrmOpportunityExtendedDataModel>(json) ?? new CrmOpportunityExtendedDataModel();
        }
        catch (JsonException)
        {
            return new CrmOpportunityExtendedDataModel();
        }
    }

    private static string ResolvePrimaryContact(
        IReadOnlyList<CrmPartyContactValue> contactPoints,
        PartyContactType contactType)
    {
        return contactPoints
            .Where(item => item.ContactType == contactType)
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => item.Value)
            .FirstOrDefault()
            ?? string.Empty;
    }

    private static string NormalizeActor(string actor)
    {
        return string.IsNullOrWhiteSpace(actor) ? "crm-hr-ui" : actor.Trim();
    }

    private static string BuildOpportunityRoute(Guid accountPartyId, Guid opportunityId)
    {
        return $"/crm-hr/crm?accountId={accountPartyId}&opportunityId={opportunityId}";
    }

    private static bool IsClosedOpportunityStage(OpportunityStage stage)
    {
        return stage is OpportunityStage.Won or OpportunityStage.Lost;
    }

    private static DateOnly? ToDateOnly(DateTimeOffset? value)
    {
        return value.HasValue
            ? DateOnly.FromDateTime(value.Value.UtcDateTime)
            : null;
    }

    private static IReadOnlyList<ProjectPartyAssignmentUpsertRequest> BuildOpportunityProjectAssignments(
        CrmOpportunityDetailModel opportunity,
        Guid projectId)
    {
        var assignments = new List<ProjectPartyAssignmentUpsertRequest>
        {
            new()
            {
                ProjectId = projectId,
                PartyId = opportunity.AccountPartyId,
                Role = ProjectPartyAssignmentRole.Customer,
                IsPrimary = true,
                Source = "crm-opportunity",
                Notes = $"Opportunity: {opportunity.Title}"
            },
            new()
            {
                ProjectId = projectId,
                PartyId = opportunity.OwnerPartyId,
                Role = ProjectPartyAssignmentRole.Manager,
                IsPrimary = true,
                Source = "crm-opportunity",
                Notes = $"Opportunity owner: {opportunity.Title}"
            }
        };

        if (opportunity.DeliveryUnitPartyId.HasValue)
        {
            assignments.Add(new ProjectPartyAssignmentUpsertRequest
            {
                ProjectId = projectId,
                PartyId = opportunity.DeliveryUnitPartyId.Value,
                Role = ProjectPartyAssignmentRole.DeliveryUnit,
                IsPrimary = true,
                Source = "crm-opportunity",
                Notes = $"Opportunity delivery unit: {opportunity.Title}"
            });
        }

        var mappedLinks = opportunity.Parties
            .Select(link => new
            {
                link.PartyId,
                Role = MapOpportunityRole(link.Role, link.PartyId == opportunity.AccountPartyId),
                Notes = $"{link.Role}: {opportunity.Title}"
            })
            .Where(item => item.Role.HasValue)
            .GroupBy(item => new { item.PartyId, Role = item.Role!.Value })
            .Select(group => group.First())
            .ToList();
        var primaryRoles = new HashSet<ProjectPartyAssignmentRole>();
        foreach (var mappedLink in mappedLinks)
        {
            assignments.Add(new ProjectPartyAssignmentUpsertRequest
            {
                ProjectId = projectId,
                PartyId = mappedLink.PartyId,
                Role = mappedLink.Role!.Value,
                IsPrimary = primaryRoles.Add(mappedLink.Role.Value),
                Source = "crm-opportunity",
                Notes = mappedLink.Notes
            });
        }

        return assignments
            .GroupBy(item => new { item.ProjectId, item.PartyId, item.Role, item.NodeKey })
            .Select(group => group.First())
            .ToList();
    }

    private static ProjectPartyAssignmentUpsertRequest ToProjectAssignmentRequest(
        ProjectPartyAssignmentDetail assignment)
    {
        return new ProjectPartyAssignmentUpsertRequest
        {
            AssignmentId = assignment.Id,
            ProjectId = assignment.ProjectId,
            PartyId = assignment.PartyId,
            PartyAffiliationId = assignment.PartyAffiliationId ??
                assignment.Affiliation?.AffiliationId,
            Role = assignment.Role,
            NodeKey = assignment.NodeKey,
            IsPrimary = assignment.IsPrimary,
            AllocationPercent = assignment.AllocationPercent,
            StartsOn = ToDateOnly(assignment.StartsAtUtc),
            EndsOn = ToDateOnly(assignment.EndsAtUtc),
            Source = assignment.Source,
            Notes = assignment.Notes
        };
    }

    private static ProjectPartyAssignmentRole? MapOpportunityRole(OpportunityPartyRole role, bool isAccountParty)
    {
        return role switch
        {
            OpportunityPartyRole.Customer when !isAccountParty => ProjectPartyAssignmentRole.CustomerContact,
            OpportunityPartyRole.Partner => ProjectPartyAssignmentRole.Partner,
            OpportunityPartyRole.TechnicalContact => ProjectPartyAssignmentRole.TechnicalContact,
            OpportunityPartyRole.BillingContact => ProjectPartyAssignmentRole.BillingContact,
            OpportunityPartyRole.DeliveryLead => ProjectPartyAssignmentRole.TeamMember,
            OpportunityPartyRole.Sponsor or OpportunityPartyRole.Stakeholder => ProjectPartyAssignmentRole.Stakeholder,
            _ => null
        };
    }
}

internal static class CrmActivityHistoryQueryComposer
{
    private const int InteractionSourceOrder = 0;
    private const int AuditSourceOrder = 1;

    public static async Task<CrmActivityHistoryPage> SearchAsync(
        AppDbContext dbContext,
        IQueryable<InteractionRecord> interactions,
        IQueryable<CrmHrAuditEntry> auditEntries,
        CrmActivityHistoryQuery query,
        bool includeParticipantNames,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var normalized = Normalize(query);
        var actionCounts = await interactions
            .GroupBy(_ => 1)
            .Select(group => new CrmActivityActionCounts(
                group.Count(item => item.NextActionText != string.Empty),
                group.Count(item =>
                    item.NextActionText != string.Empty &&
                    item.NextActionDueUtc.HasValue &&
                    item.NextActionDueUtc.Value < nowUtc)))
            .SingleOrDefaultAsync(cancellationToken)
            ?? new CrmActivityActionCounts(0, 0);

        var rows = interactions
            .Select(item => new
            {
                item.Id,
                SourceOrder = InteractionSourceOrder,
                OccurredAtUtc = item.OccurredAtUtc,
                InteractionType = (InteractionType?)item.InteractionType,
                item.Subject,
                item.Summary,
                item.Notes,
                item.NextActionText,
                item.NextActionDueUtc,
                Action = string.Empty,
                Actor = string.Empty,
                IsSensitive = false
            })
            .Concat(auditEntries.Select(item => new
            {
                item.Id,
                SourceOrder = AuditSourceOrder,
                OccurredAtUtc = item.CreatedAtUtc,
                InteractionType = (InteractionType?)null,
                Subject = item.Summary,
                Summary = string.Empty,
                Notes = string.Empty,
                NextActionText = string.Empty,
                NextActionDueUtc = (DateTimeOffset?)null,
                item.Action,
                item.Actor,
                item.IsSensitive
            }));
        var totalCount = await rows.CountAsync(cancellationToken);
        var pageRows = await rows
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenBy(item => item.SourceOrder)
            .ThenBy(item => item.Id)
            .Skip(normalized.PageIndex * normalized.PageSize)
            .Take(normalized.PageSize)
            .ToListAsync(cancellationToken);
        var interactionIds = pageRows
            .Where(item => item.SourceOrder == InteractionSourceOrder)
            .Select(item => item.Id)
            .ToList();
        var participantNames = includeParticipantNames && interactionIds.Count > 0
            ? await LoadParticipantNamesAsync(dbContext, interactionIds, cancellationToken)
            : new Dictionary<Guid, string>();
        var items = pageRows
            .Select(item => MapItem(
                new CrmActivityHistoryQueryRow(
                    item.Id,
                    item.SourceOrder,
                    item.OccurredAtUtc,
                    item.InteractionType,
                    item.Subject,
                    item.Summary,
                    item.Notes,
                    item.NextActionText,
                    item.NextActionDueUtc,
                    item.Action,
                    item.Actor,
                    item.IsSensitive),
                participantNames.GetValueOrDefault(item.Id),
                nowUtc))
            .ToList();

        return new CrmActivityHistoryPage(
            items,
            normalized.PageIndex,
            normalized.PageSize,
            totalCount,
            actionCounts.ActionCount,
            actionCounts.OverdueActionCount);
    }

    private static CrmActivityHistoryQuery Normalize(CrmActivityHistoryQuery query)
    {
        if (query.PartyId == Guid.Empty)
        {
            throw new ArgumentException("A party identifier is required.", nameof(query));
        }

        if (query.PageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageIndex,
                "Activity history page index cannot be negative.");
        }

        if (query.PageSize is < 1 or > CrmActivityHistoryQueryLimits.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageSize,
                $"Activity history page size must be between 1 and {CrmActivityHistoryQueryLimits.MaximumPageSize}.");
        }

        if (query.PageIndex > int.MaxValue / query.PageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageIndex,
                "Activity history page offset is too large.");
        }

        return query;
    }

    private static async Task<Dictionary<Guid, string>> LoadParticipantNamesAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<Guid> interactionIds,
        CancellationToken cancellationToken)
    {
        var rows = await (
                from link in dbContext.Set<InteractionPartyLink>().AsNoTracking()
                join party in dbContext.Set<Party>().AsNoTracking()
                    on link.PartyId equals party.Id
                where interactionIds.Contains(link.InteractionId) &&
                      link.Role != InteractionPartyRole.Account
                select new CrmActivityParticipantName(
                    link.InteractionId,
                    party.DisplayName))
            .ToListAsync(cancellationToken);
        return rows
            .GroupBy(item => item.InteractionId)
            .ToDictionary(
                group => group.Key,
                group => string.Join(
                    ", ",
                    group.Select(item => item.DisplayName)
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)));
    }

    private static CrmAccountActivityTimelineItemModel MapItem(
        CrmActivityHistoryQueryRow item,
        string? participantNames,
        DateTimeOffset nowUtc)
    {
        if (item.SourceOrder == AuditSourceOrder)
        {
            return new CrmAccountActivityTimelineItemModel(
                item.Id,
                "Audit",
                item.Subject,
                item.Action,
                item.Actor,
                item.OccurredAtUtc,
                item.IsSensitive ? "warning" : "neutral",
                false);
        }

        var interactionType = item.InteractionType
            ?? throw new InvalidOperationException($"Interaction activity '{item.Id}' has no interaction type.");
        var metaParts = new List<string>
        {
            interactionType.ToString()
        };
        if (!string.IsNullOrWhiteSpace(participantNames))
        {
            metaParts.Add(participantNames);
        }

        if (!string.IsNullOrWhiteSpace(item.NextActionText) &&
            item.NextActionDueUtc is DateTimeOffset dueAtUtc)
        {
            metaParts.Add($"Next action due {dueAtUtc:yyyy-MM-dd}");
        }

        return new CrmAccountActivityTimelineItemModel(
            item.Id,
            "Interaction",
            item.Subject,
            string.IsNullOrWhiteSpace(item.Summary) ? item.Notes : item.Summary,
            string.Join(" / ", metaParts),
            item.OccurredAtUtc,
            ResolveInteractionTone(interactionType),
            !string.IsNullOrWhiteSpace(item.NextActionText) &&
            item.NextActionDueUtc is DateTimeOffset nextActionDueAtUtc &&
            nextActionDueAtUtc < nowUtc);
    }

    private static string ResolveInteractionTone(InteractionType interactionType)
    {
        return interactionType switch
        {
            InteractionType.Meeting => "info",
            InteractionType.Call => "success",
            InteractionType.Email => "neutral",
            InteractionType.Message => "warning",
            _ => "neutral"
        };
    }

    private sealed record CrmActivityActionCounts(
        int ActionCount,
        int OverdueActionCount);

    private sealed record CrmActivityHistoryQueryRow(
        Guid Id,
        int SourceOrder,
        DateTimeOffset OccurredAtUtc,
        InteractionType? InteractionType,
        string Subject,
        string Summary,
        string Notes,
        string NextActionText,
        DateTimeOffset? NextActionDueUtc,
        string Action,
        string Actor,
        bool IsSensitive);

    private sealed record CrmActivityParticipantName(
        Guid InteractionId,
        string DisplayName);
}

public sealed partial class HrService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    IActivityStream activityStream,
    ISearchIndexService searchIndexService)
{
    public async Task<IReadOnlyList<WorkforceProfileSummaryModel>> ListWorkforceProfilesAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<WorkforceProfile>()
            .OrderBy(item => item.JobTitle)
            .Select(item => new WorkforceProfileSummaryModel(item.Id, item.PartyId, item.WorkforceKind, item.JobTitle, item.Status))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkforceListItemModel>> ListWorkforceDirectoryAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var parties = await dbContext.Set<Party>()
            .OrderBy(item => item.DisplayName)
            .Select(item => new
            {
                item.Id,
                item.DisplayName,
                item.PartyType,
                item.IsSensitive,
                item.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);
        if (parties.Count == 0)
        {
            return [];
        }

        var partyIds = parties.Select(item => item.Id).ToList();
        var roles = await dbContext.Set<PartyRoleAssignment>()
            .Where(item => partyIds.Contains(item.PartyId))
            .Select(item => new
            {
                item.PartyId,
                item.RoleKind
            })
            .ToListAsync(cancellationToken);
        var profiles = await dbContext.Set<WorkforceProfile>()
            .Where(item => partyIds.Contains(item.PartyId))
            .Select(item => new
            {
                item.Id,
                item.PartyId,
                item.WorkforceKind,
                item.JobTitle,
                item.Discipline,
                item.Status,
                item.Seniority,
                item.Location,
                item.EndDateUtc,
                item.CapacityHoursPerWeek,
                item.HomeUnitPartyId,
                item.ManagerPartyId
            })
            .ToListAsync(cancellationToken);
        var partySkillsByPartyId = await GetPartySkillMapAsync(dbContext, partyIds, cancellationToken);
        var projectAllocationsByPartyId = await GetProjectAllocationMapAsync(dbContext, partyIds, cancellationToken);
        var capacityBlocksByPartyId = await GetCapacityBlockMapAsync(dbContext, partyIds, cancellationToken);

        var rolesByPartyId = roles
            .GroupBy(item => item.PartyId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PartyRoleKind>)group.Select(item => item.RoleKind).Distinct().ToList());
        var namesByPartyId = parties.ToDictionary(item => item.Id, item => item.DisplayName);
        var profilesByPartyId = profiles.ToDictionary(item => item.PartyId, item => item);

        return parties
            .Where(item =>
            {
                if (profilesByPartyId.ContainsKey(item.Id))
                {
                    return true;
                }

                if (item.PartyType == PartyType.Person || item.PartyType == PartyType.OrganizationUnit)
                {
                    return true;
                }

                var partyRoles = rolesByPartyId.GetValueOrDefault(item.Id) ?? [];
                return partyRoles.Contains(PartyRoleKind.DeliveryUnit);
            })
            .Select(item =>
            {
                var profile = profilesByPartyId.GetValueOrDefault(item.Id);
                var capacitySummary = BuildCapacitySummary(
                    profile?.CapacityHoursPerWeek ?? 40m,
                    projectAllocationsByPartyId.GetValueOrDefault(item.Id) ?? [],
                    capacityBlocksByPartyId.GetValueOrDefault(item.Id) ?? []);
                var skillSummary = string.Join(
                    ", ",
                    (partySkillsByPartyId.GetValueOrDefault(item.Id) ?? [])
                        .Select(skill => $"{skill.SkillName} ({skill.Proficiency})"));

                return new WorkforceListItemModel(
                    item.Id,
                    item.DisplayName,
                    item.PartyType,
                    item.IsSensitive,
                    profile?.WorkforceKind,
                    profile?.Status ?? "No profile",
                    profile?.JobTitle ?? string.Empty,
                    profile?.Discipline ?? string.Empty,
                    profile?.HomeUnitPartyId is Guid homeUnitPartyId ? namesByPartyId.GetValueOrDefault(homeUnitPartyId) ?? string.Empty : string.Empty,
                    profile?.ManagerPartyId is Guid managerPartyId ? namesByPartyId.GetValueOrDefault(managerPartyId) ?? string.Empty : string.Empty,
                    rolesByPartyId.GetValueOrDefault(item.Id) ?? [],
                    profile is not null,
                    item.UpdatedAtUtc,
                    profile?.Seniority ?? string.Empty,
                    profile?.Location ?? string.Empty,
                    skillSummary,
                    profile is null ? null : capacitySummary.AvailabilityState,
                    profile is null ? 0m : capacitySummary.AvailablePercent,
                    ToDateOnly(profile?.EndDateUtc),
                    profile is null ? null : capacitySummary.NextAvailabilityOn);
            })
            .OrderBy(item => item.DisplayName)
            .ToList();
    }

    public async Task<WorkforceWorkspaceModel?> GetWorkforceWorkspaceAsync(Guid partyId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var profileWorkspace = await GetWorkforceProfileWorkspaceCoreAsync(dbContext, partyId, cancellationToken);
        if (profileWorkspace is null)
        {
            return null;
        }

        var capacityWorkspace = await GetWorkforceCapacityWorkspaceCoreAsync(
            dbContext,
            partyId,
            profileWorkspace.Profile.CapacityHoursPerWeek,
            cancellationToken);
        return CombineWorkforceWorkspaces(profileWorkspace, capacityWorkspace);
    }

    public async Task<WorkforceProfileWorkspaceModel?> GetWorkforceProfileWorkspaceAsync(
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await GetWorkforceProfileWorkspaceCoreAsync(dbContext, partyId, cancellationToken);
    }

    public async Task<WorkforceCapacityWorkspaceModel?> GetWorkforceCapacityWorkspaceAsync(
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var isSupportedParty = await dbContext.Set<Party>()
            .AnyAsync(item => item.Id == partyId && item.PartyType != PartyType.AiAgent, cancellationToken);
        if (!isSupportedParty)
        {
            return null;
        }

        var capacityHoursPerWeek = await dbContext.Set<WorkforceProfile>()
            .Where(item => item.PartyId == partyId)
            .Select(item => (decimal?)item.CapacityHoursPerWeek)
            .SingleOrDefaultAsync(cancellationToken) ?? 40m;
        return await GetWorkforceCapacityWorkspaceCoreAsync(
            dbContext,
            partyId,
            capacityHoursPerWeek,
            cancellationToken);
    }

    private async Task<WorkforceProfileWorkspaceModel?> GetWorkforceProfileWorkspaceCoreAsync(
        AppDbContext dbContext,
        Guid partyId,
        CancellationToken cancellationToken)
    {
        var party = await dbContext.Set<Party>()
            .Select(item => new
            {
                item.Id,
                item.DisplayName,
                item.Summary,
                item.PartyType,
                item.LifecycleStatus,
                item.IsSensitive,
                item.LastChangedBy,
                item.UpdatedAtUtc
            })
            .SingleOrDefaultAsync(item => item.Id == partyId, cancellationToken);
        if (party is null || party.PartyType == PartyType.AiAgent)
        {
            return null;
        }

        var roles = await dbContext.Set<PartyRoleAssignment>()
            .Where(item => item.PartyId == partyId)
            .OrderBy(item => item.RoleKind)
            .Select(item => item.RoleKind)
            .ToListAsync(cancellationToken);
        var contactPoints = await dbContext.Set<PartyContactPoint>()
            .Where(item => item.PartyId == partyId && item.IsPublic)
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => new CrmPartyContactValue(item.PartyId, item.ContactType, item.Value, item.IsPrimary))
            .ToListAsync(cancellationToken);
        var profile = await dbContext.Set<WorkforceProfile>()
            .SingleOrDefaultAsync(item => item.PartyId == partyId, cancellationToken);
        var relatedPartyIds = new[] { profile?.HomeUnitPartyId, profile?.ManagerPartyId }
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Distinct()
            .ToList();
        var namesByPartyId = relatedPartyIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Set<Party>()
                .Where(item => relatedPartyIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);
        var skillCatalog = await GetSkillCatalogItemsAsync(dbContext, cancellationToken);
        var partySkills = (await GetPartySkillMapAsync(dbContext, [partyId], cancellationToken)).GetValueOrDefault(partyId) ?? [];

        return new WorkforceProfileWorkspaceModel(
            party.Id,
            party.DisplayName,
            party.Summary,
            party.PartyType,
            party.LifecycleStatus,
            party.IsSensitive,
            string.IsNullOrWhiteSpace(party.LastChangedBy) ? "crm-hr-ui" : party.LastChangedBy,
            party.UpdatedAtUtc,
            roles,
            party.IsSensitive ? string.Empty : ResolvePrimaryContact(contactPoints, PartyContactType.Email),
            party.IsSensitive ? string.Empty : ResolvePrimaryContact(contactPoints, PartyContactType.Phone),
            profile?.HomeUnitPartyId is Guid homeUnitPartyId ? namesByPartyId.GetValueOrDefault(homeUnitPartyId) ?? string.Empty : string.Empty,
            profile?.ManagerPartyId is Guid managerPartyId ? namesByPartyId.GetValueOrDefault(managerPartyId) ?? string.Empty : string.Empty,
            new WorkforceProfileEditorModel
            {
                Id = profile?.Id,
                PartyId = party.Id,
                WorkforceKind = ResolveDefaultWorkforceKind(party.PartyType, roles, profile),
                EmployeeCode = profile?.EmployeeCode ?? string.Empty,
                JobTitle = profile?.JobTitle ?? string.Empty,
                Discipline = profile?.Discipline ?? string.Empty,
                Seniority = profile?.Seniority ?? string.Empty,
                HomeUnitPartyId = profile?.HomeUnitPartyId,
                ManagerPartyId = profile?.ManagerPartyId,
                StartDate = profile?.StartDateUtc is DateTimeOffset startDateUtc ? DateOnly.FromDateTime(startDateUtc.UtcDateTime) : null,
                EndDate = profile?.EndDateUtc is DateTimeOffset endDateUtc ? DateOnly.FromDateTime(endDateUtc.UtcDateTime) : null,
                Location = profile?.Location ?? string.Empty,
                TimeZone = profile?.TimeZone ?? string.Empty,
                InternalCostRate = profile?.InternalCostRate,
                ExternalBillingRate = profile?.ExternalBillingRate,
                RateUnit = profile?.RateUnit ?? ProjectResourceRateUnit.Hour,
                RateCurrencyCode = string.IsNullOrWhiteSpace(profile?.RateCurrencyCode) ? "USD" : profile.RateCurrencyCode,
                CapacityHoursPerWeek = profile?.CapacityHoursPerWeek ?? 40m,
                Status = string.IsNullOrWhiteSpace(profile?.Status) ? ResolveDefaultStatus(party.LifecycleStatus) : profile.Status,
                Notes = profile?.Notes ?? string.Empty,
                LastChangedBy = string.IsNullOrWhiteSpace(party.LastChangedBy) ? "crm-hr-ui" : party.LastChangedBy
            },
            skillCatalog,
            partySkills);
    }

    private async Task<WorkforceCapacityWorkspaceModel> GetWorkforceCapacityWorkspaceCoreAsync(
        AppDbContext dbContext,
        Guid partyId,
        decimal capacityHoursPerWeek,
        CancellationToken cancellationToken)
    {
        var capacityBlocks = (await GetCapacityBlockMapAsync(dbContext, [partyId], cancellationToken)).GetValueOrDefault(partyId) ?? [];
        var projectAllocations = (await GetProjectAllocationMapAsync(dbContext, [partyId], cancellationToken)).GetValueOrDefault(partyId) ?? [];

        return new WorkforceCapacityWorkspaceModel(
            partyId,
            capacityBlocks,
            projectAllocations,
            BuildCapacitySummary(capacityHoursPerWeek, projectAllocations, capacityBlocks));
    }

    private static WorkforceWorkspaceModel CombineWorkforceWorkspaces(
        WorkforceProfileWorkspaceModel profileWorkspace,
        WorkforceCapacityWorkspaceModel capacityWorkspace)
    {
        return new WorkforceWorkspaceModel(
            profileWorkspace.PartyId,
            profileWorkspace.DisplayName,
            profileWorkspace.Summary,
            profileWorkspace.PartyType,
            profileWorkspace.LifecycleStatus,
            profileWorkspace.IsSensitive,
            profileWorkspace.LastChangedBy,
            profileWorkspace.UpdatedAtUtc,
            profileWorkspace.Roles,
            profileWorkspace.PrimaryEmail,
            profileWorkspace.PrimaryPhone,
            profileWorkspace.HomeUnitName,
            profileWorkspace.ManagerName,
            profileWorkspace.Profile,
            profileWorkspace.SkillCatalog,
            profileWorkspace.Skills,
            capacityWorkspace.CapacityBlocks,
            capacityWorkspace.ProjectAllocations,
            capacityWorkspace.CapacitySummary);
    }

    public async Task<Result<Guid>> SaveWorkforceProfileAsync(WorkforceProfileEditorModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model.PartyId == Guid.Empty)
        {
            return Result<Guid>.Failure(Error.Validation("Choose a party before saving the workforce profile.", "crmhr.workforce.party-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var party = await dbContext.Set<Party>()
            .SingleOrDefaultAsync(item => item.Id == model.PartyId, cancellationToken);
        if (party is null)
        {
            return Result<Guid>.Failure(Error.Failure("The selected party could not be found.", "crmhr.workforce.party-not-found"));
        }

        if (model.ManagerPartyId == model.PartyId)
        {
            return Result<Guid>.Failure(Error.Validation("A party cannot manage itself.", "crmhr.workforce.self-manager"));
        }

        if (model.HomeUnitPartyId == model.PartyId)
        {
            return Result<Guid>.Failure(Error.Validation("A party cannot reference itself as its home unit.", "crmhr.workforce.self-home-unit"));
        }

        if (model.InternalCostRate is < 0m || model.ExternalBillingRate is < 0m)
        {
            return Result<Guid>.Failure(Error.Validation(
                "Workforce rates cannot be negative.",
                "crmhr.workforce.rate-negative"));
        }

        if (!Enum.IsDefined(model.RateUnit))
        {
            return Result<Guid>.Failure(Error.Validation(
                "Choose a supported workforce rate unit.",
                "crmhr.workforce.rate-unit-invalid"));
        }

        var rateCurrencyCode = string.IsNullOrWhiteSpace(model.RateCurrencyCode)
            ? "USD"
            : model.RateCurrencyCode.Trim().ToUpperInvariant();
        if (rateCurrencyCode.Length != 3 || rateCurrencyCode.Any(character => character is < 'A' or > 'Z'))
        {
            return Result<Guid>.Failure(Error.Validation(
                "Rate currency must be a three-letter currency code.",
                "crmhr.workforce.rate-currency-invalid"));
        }

        if (party.PartyType == PartyType.Person && model.WorkforceKind == WorkforceKind.DeliveryUnit)
        {
            return Result<Guid>.Failure(Error.Validation("People cannot be saved as delivery units.", "crmhr.workforce.person-delivery-unit"));
        }

        if ((party.PartyType == PartyType.Organization || party.PartyType == PartyType.OrganizationUnit) && model.WorkforceKind != WorkforceKind.DeliveryUnit)
        {
            return Result<Guid>.Failure(Error.Validation("Organizations and organization units can only use the delivery-unit workforce kind.", "crmhr.workforce.organization-kind"));
        }

        if (model.ManagerPartyId is Guid managerPartyId)
        {
            var manager = await dbContext.Set<Party>()
                .Select(item => new
                {
                    item.Id,
                    item.PartyType
                })
                .SingleOrDefaultAsync(item => item.Id == managerPartyId, cancellationToken);
            if (manager is null || manager.PartyType != PartyType.Person)
            {
                return Result<Guid>.Failure(Error.Validation("Manager must reference an existing person.", "crmhr.workforce.manager-invalid"));
            }
        }

        if (model.HomeUnitPartyId is Guid homeUnitPartyId)
        {
            var homeUnit = await dbContext.Set<Party>()
                .Select(item => new
                {
                    item.Id,
                    item.PartyType
                })
                .SingleOrDefaultAsync(item => item.Id == homeUnitPartyId, cancellationToken);
            if (homeUnit is null || (homeUnit.PartyType != PartyType.Organization && homeUnit.PartyType != PartyType.OrganizationUnit))
            {
                return Result<Guid>.Failure(Error.Validation("Home unit must reference an existing organization or organization unit.", "crmhr.workforce.home-unit-invalid"));
            }
        }

        var profile = await dbContext.Set<WorkforceProfile>()
            .SingleOrDefaultAsync(item => item.PartyId == model.PartyId, cancellationToken);
        if (profile is null)
        {
            profile = new WorkforceProfile
            {
                PartyId = model.PartyId
            };
            dbContext.Set<WorkforceProfile>().Add(profile);
        }

        profile.WorkforceKind = model.WorkforceKind;
        profile.EmployeeCode = model.EmployeeCode.Trim();
        profile.JobTitle = model.JobTitle.Trim();
        profile.Discipline = model.Discipline.Trim();
        profile.Seniority = model.Seniority.Trim();
        profile.HomeUnitPartyId = model.HomeUnitPartyId;
        profile.ManagerPartyId = model.ManagerPartyId;
        profile.StartDateUtc = ToUtcDate(model.StartDate);
        profile.EndDateUtc = ToUtcDate(model.EndDate);
        profile.Location = model.Location.Trim();
        profile.TimeZone = model.TimeZone.Trim();
        profile.InternalCostRate = model.InternalCostRate;
        profile.ExternalBillingRate = model.ExternalBillingRate;
        profile.RateUnit = model.RateUnit;
        profile.RateCurrencyCode = rateCurrencyCode;
        profile.CapacityHoursPerWeek = model.CapacityHoursPerWeek <= 0m ? 40m : model.CapacityHoursPerWeek;
        profile.Status = model.Status.Trim();
        profile.Notes = model.Notes.Trim();

        var workforceRole = ResolveWorkforceRole(model.WorkforceKind);
        var currentRoles = await dbContext.Set<PartyRoleAssignment>()
            .Where(item => item.PartyId == model.PartyId)
            .ToListAsync(cancellationToken);
        if (!currentRoles.Any(item => item.RoleKind == workforceRole))
        {
            dbContext.Set<PartyRoleAssignment>().Add(new PartyRoleAssignment
            {
                PartyId = model.PartyId,
                RoleKind = workforceRole,
                Title = workforceRole.ToString(),
                IsPrimary = currentRoles.Count == 0
            });
        }

        party.LastChangedBy = string.IsNullOrWhiteSpace(model.LastChangedBy) ? "crm-hr-ui" : model.LastChangedBy.Trim();
        party.UpdatedAtUtc = clock.GetUtcNow();
        CrmHrAuditWriter.AddEntry(
            dbContext,
            nameof(WorkforceProfile),
            party.Id,
            "WorkforceProfileSaved",
            $"Saved workforce profile for '{party.DisplayName}'.",
            new
            {
                profile.WorkforceKind,
                profile.JobTitle,
                profile.Discipline,
                profile.Seniority,
                profile.Status,
                profile.HomeUnitPartyId,
                profile.ManagerPartyId
            },
            party.LastChangedBy,
            party.IsSensitive,
            party.UpdatedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
        await UpsertWorkforceSearchDocumentAsync(party.Id, cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                "WorkforceProfileSaved",
                $"Saved workforce profile for {party.DisplayName}",
                $"{profile.WorkforceKind} / {profile.JobTitle} / {profile.Status}",
                ArtifactKind: nameof(WorkforceProfile),
                ArtifactId: party.Id,
                Route: $"/crm-hr/workforce?partyId={party.Id}",
                Actor: party.LastChangedBy),
            cancellationToken);
        return Result<Guid>.Success(profile.Id);
    }

    public async Task<IReadOnlyList<SkillCatalogItemModel>> ListSkillCatalogAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await GetSkillCatalogItemsAsync(dbContext, cancellationToken);
    }

    public async Task<Result<Guid>> SaveSkillDefinitionAsync(SkillDefinitionEditorModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return Result<Guid>.Failure(Error.Validation("Skill name is required.", "crmhr.skills.name-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var normalizedName = model.Name.Trim();
        var entity = model.Id.HasValue
            ? await dbContext.Set<SkillDefinition>().SingleOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;

        entity ??= await dbContext.Set<SkillDefinition>()
            .SingleOrDefaultAsync(item => item.Name == normalizedName, cancellationToken);

        if (entity is null)
        {
            entity = new SkillDefinition();
            dbContext.Set<SkillDefinition>().Add(entity);
        }

        entity.Name = normalizedName;
        entity.Category = model.Category.Trim();
        entity.Description = model.Description.Trim();
        entity.IsActive = model.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(entity.Id);
    }

    public async Task<Result<Guid>> SavePartySkillAsync(PartySkillEditorModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model.PartyId == Guid.Empty)
        {
            return Result<Guid>.Failure(Error.Validation("Choose a party before saving a skill.", "crmhr.skills.party-required"));
        }

        if (model.SkillId == Guid.Empty)
        {
            return Result<Guid>.Failure(Error.Validation("Choose a skill before saving.", "crmhr.skills.skill-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var partyExists = await dbContext.Set<Party>().AnyAsync(item => item.Id == model.PartyId, cancellationToken);
        if (!partyExists)
        {
            return Result<Guid>.Failure(Error.Validation("The selected party was not found.", "crmhr.skills.party-not-found"));
        }

        var skillExists = await dbContext.Set<SkillDefinition>().AnyAsync(item => item.Id == model.SkillId, cancellationToken);
        if (!skillExists)
        {
            return Result<Guid>.Failure(Error.Validation("The selected skill was not found.", "crmhr.skills.skill-not-found"));
        }

        var entity = model.Id.HasValue
            ? await dbContext.Set<PartySkill>().SingleOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;
        entity ??= await dbContext.Set<PartySkill>()
            .SingleOrDefaultAsync(item => item.PartyId == model.PartyId && item.SkillId == model.SkillId, cancellationToken);

        if (entity is null)
        {
            entity = new PartySkill
            {
                PartyId = model.PartyId,
                SkillId = model.SkillId
            };
            dbContext.Set<PartySkill>().Add(entity);
        }

        entity.PartyId = model.PartyId;
        entity.SkillId = model.SkillId;
        entity.Proficiency = model.Proficiency;
        entity.YearsExperience = Math.Max(0, model.YearsExperience);
        entity.CertificationStatus = model.CertificationStatus.Trim();
        entity.LastValidatedAtUtc = ToUtcDate(model.LastValidatedOn);
        entity.Notes = model.Notes.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(entity.Id);
    }

    public async Task DeletePartySkillAsync(Guid partySkillId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<PartySkill>().SingleOrDefaultAsync(item => item.Id == partySkillId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        dbContext.Set<PartySkill>().Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Result<Guid>> SaveCapacityBlockAsync(CapacityBlockEditorModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model.PartyId == Guid.Empty)
        {
            return Result<Guid>.Failure(Error.Validation("Choose a party before saving a capacity block.", "crmhr.capacity.party-required"));
        }

        if (!model.StartDate.HasValue || !model.EndDate.HasValue)
        {
            return Result<Guid>.Failure(Error.Validation("Capacity block start and end dates are required.", "crmhr.capacity.date-required"));
        }

        if (model.EndDate.Value < model.StartDate.Value)
        {
            return Result<Guid>.Failure(Error.Validation("Capacity block end date must be on or after the start date.", "crmhr.capacity.date-range-invalid"));
        }

        if (model.Percentage is <= 0m or > 100m)
        {
            return Result<Guid>.Failure(Error.Validation("Capacity block percentage must stay between 0 and 100.", "crmhr.capacity.percentage-range"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var partyExists = await dbContext.Set<Party>().AnyAsync(item => item.Id == model.PartyId, cancellationToken);
        if (!partyExists)
        {
            return Result<Guid>.Failure(Error.Validation("The selected party was not found.", "crmhr.capacity.party-not-found"));
        }

        if (model.RelatedProjectId.HasValue)
        {
            var projectExists = await dbContext.Set<Project>().AnyAsync(item => item.Id == model.RelatedProjectId.Value, cancellationToken);
            if (!projectExists)
            {
                return Result<Guid>.Failure(Error.Validation("The related project was not found.", "crmhr.capacity.project-not-found"));
            }
        }

        var entity = model.Id.HasValue
            ? await dbContext.Set<CapacityBlock>().SingleOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;
        if (entity is null)
        {
            entity = new CapacityBlock
            {
                PartyId = model.PartyId
            };
            dbContext.Set<CapacityBlock>().Add(entity);
        }

        entity.PartyId = model.PartyId;
        entity.BlockKind = model.BlockKind;
        entity.StartDateUtc = ToUtcDate(model.StartDate)!.Value;
        entity.EndDateUtc = ToUtcDate(model.EndDate)!.Value;
        entity.Percentage = model.Percentage;
        entity.RelatedProjectId = model.RelatedProjectId;
        entity.Notes = model.Notes.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(entity.Id);
    }

    public async Task DeleteCapacityBlockAsync(Guid capacityBlockId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<CapacityBlock>().SingleOrDefaultAsync(item => item.Id == capacityBlockId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        dbContext.Set<CapacityBlock>().Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<StaffingRequestPage> SearchStaffingRequestsAsync(
        StaffingRequestQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var normalized = NormalizeStaffingRequestQuery(query);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<StaffingRequest> candidates = dbContext.Set<StaffingRequest>()
            .AsNoTracking()
            .Where(item => item.ProjectId == normalized.ProjectId);
        if (normalized.Status.HasValue)
        {
            candidates = candidates.Where(item => item.Status == normalized.Status.Value);
        }

        if (!string.IsNullOrEmpty(normalized.SearchText))
        {
            var search = normalized.SearchText.ToUpperInvariant();
            candidates = candidates.Where(item =>
                item.Title.ToUpper().Contains(search) ||
                item.NeededRole.ToUpper().Contains(search) ||
                item.Notes.ToUpper().Contains(search) ||
                dbContext.Set<Project>().Any(project =>
                    project.Id == item.ProjectId &&
                    project.Name.ToUpper().Contains(search)) ||
                dbContext.Set<Party>().Any(party =>
                    (party.Id == item.RequestedByPartyId ||
                     party.Id == item.DeliveryUnitPartyId) &&
                    party.DisplayName.ToUpper().Contains(search)) ||
                dbContext.Set<SkillDefinition>().Any(skill =>
                    (skill.Name.ToUpper().Contains(search) ||
                     skill.Category.ToUpper().Contains(search)) &&
                    item.NeededSkillsJson.Contains(skill.Id.ToString())));
        }

        var totalCount = await candidates.CountAsync(cancellationToken);
        var requests = await candidates
            .OrderBy(item => item.Status)
            .ThenBy(item => item.StartDateUtc ?? DateTimeOffset.MinValue)
            .ThenBy(item => item.Title)
            .ThenBy(item => item.Id)
            .Skip(normalized.PageIndex * normalized.PageSize)
            .Take(normalized.PageSize)
            .Select(item => new
            {
                item.Id,
                item.ProjectId,
                item.RequestedByPartyId,
                item.DeliveryUnitPartyId,
                item.Title,
                item.NeededRole,
                item.NeededSkillsJson,
                item.StartDateUtc,
                item.EndDateUtc,
                item.AllocationPercent,
                item.Status,
                item.Notes
            })
            .ToListAsync(cancellationToken);
        if (requests.Count == 0)
        {
            return new StaffingRequestPage(
                [],
                normalized.PageIndex,
                normalized.PageSize,
                totalCount);
        }

        var projectName = await dbContext.Set<Project>()
            .AsNoTracking()
            .Where(item => item.Id == normalized.ProjectId)
            .Select(item => item.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? string.Empty;
        var partyIds = requests
            .SelectMany(item => new[] { item.RequestedByPartyId, item.DeliveryUnitPartyId })
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Distinct()
            .ToList();
        var partyNames = partyIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Set<Party>()
                .AsNoTracking()
                .Where(item => partyIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);
        var skillIds = requests
            .SelectMany(item => ParseSkillIds(item.NeededSkillsJson))
            .Distinct()
            .ToList();
        var skillsById = skillIds.Count == 0
            ? new Dictionary<Guid, SkillCatalogItemModel>()
            : await dbContext.Set<SkillDefinition>()
                .AsNoTracking()
                .Where(item => skillIds.Contains(item.Id))
                .Select(item => new SkillCatalogItemModel(
                    item.Id,
                    item.Name,
                    item.Category,
                    item.Description,
                    item.IsActive))
                .ToDictionaryAsync(item => item.Id, cancellationToken);

        var items = requests
            .Select(item => new StaffingRequestItemModel(
                item.Id,
                item.ProjectId,
                projectName,
                item.RequestedByPartyId,
                item.RequestedByPartyId.HasValue
                    ? partyNames.GetValueOrDefault(item.RequestedByPartyId.Value) ?? string.Empty
                    : string.Empty,
                item.DeliveryUnitPartyId,
                item.DeliveryUnitPartyId.HasValue
                    ? partyNames.GetValueOrDefault(item.DeliveryUnitPartyId.Value) ?? string.Empty
                    : string.Empty,
                item.Title,
                item.NeededRole,
                ParseSkillIds(item.NeededSkillsJson)
                    .Select(skillId => skillsById.GetValueOrDefault(skillId))
                    .Where(skill => skill is not null)
                    .Cast<SkillCatalogItemModel>()
                    .ToList(),
                ToDateOnly(item.StartDateUtc),
                ToDateOnly(item.EndDateUtc),
                item.AllocationPercent,
                item.Status,
                item.Notes))
            .ToList();
        return new StaffingRequestPage(
            items,
            normalized.PageIndex,
            normalized.PageSize,
            totalCount);
    }

    public async Task<Result<Guid>> SaveStaffingRequestAsync(StaffingRequestEditorModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            return Result<Guid>.Failure(Error.Validation("Staffing request title is required.", "crmhr.staffing-request.title-required"));
        }

        if (string.IsNullOrWhiteSpace(model.NeededRole))
        {
            return Result<Guid>.Failure(Error.Validation("Needed role is required.", "crmhr.staffing-request.role-required"));
        }

        if (model.AllocationPercent is <= 0m or > 100m)
        {
            return Result<Guid>.Failure(Error.Validation("Allocation must stay between 0 and 100.", "crmhr.staffing-request.allocation-range"));
        }

        if (model.StartDate.HasValue && model.EndDate.HasValue && model.EndDate.Value < model.StartDate.Value)
        {
            return Result<Guid>.Failure(Error.Validation("Staffing request end date must be on or after the start date.", "crmhr.staffing-request.date-range-invalid"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (model.ProjectId.HasValue)
        {
            var projectExists = await dbContext.Set<Project>().AnyAsync(item => item.Id == model.ProjectId.Value, cancellationToken);
            if (!projectExists)
            {
                return Result<Guid>.Failure(Error.Validation("The selected project was not found.", "crmhr.staffing-request.project-not-found"));
            }
        }

        if (model.RequestedByPartyId.HasValue)
        {
            var requesterExists = await dbContext.Set<Party>().AnyAsync(item => item.Id == model.RequestedByPartyId.Value, cancellationToken);
            if (!requesterExists)
            {
                return Result<Guid>.Failure(Error.Validation("The requester was not found.", "crmhr.staffing-request.requester-not-found"));
            }
        }

        if (model.DeliveryUnitPartyId.HasValue)
        {
            var deliveryUnitExists = await dbContext.Set<Party>().AnyAsync(item => item.Id == model.DeliveryUnitPartyId.Value, cancellationToken);
            if (!deliveryUnitExists)
            {
                return Result<Guid>.Failure(Error.Validation("The selected delivery unit was not found.", "crmhr.staffing-request.delivery-unit-not-found"));
            }
        }

        var normalizedSkillIds = model.SkillIds.Where(item => item != Guid.Empty).Distinct().ToList();
        if (normalizedSkillIds.Count > 0)
        {
            var knownSkillIds = await dbContext.Set<SkillDefinition>()
                .Where(item => normalizedSkillIds.Contains(item.Id))
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);
            if (knownSkillIds.Count != normalizedSkillIds.Count)
            {
                return Result<Guid>.Failure(Error.Validation("One or more selected skills no longer exist.", "crmhr.staffing-request.skill-not-found"));
            }
        }

        var entity = model.Id.HasValue
            ? await dbContext.Set<StaffingRequest>().SingleOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;
        if (entity is null)
        {
            entity = new StaffingRequest();
            dbContext.Set<StaffingRequest>().Add(entity);
        }

        entity.ProjectId = model.ProjectId;
        entity.RequestedByPartyId = model.RequestedByPartyId;
        entity.DeliveryUnitPartyId = model.DeliveryUnitPartyId;
        entity.Title = model.Title.Trim();
        entity.NeededRole = model.NeededRole.Trim();
        entity.NeededSkillsJson = JsonSerializer.Serialize(normalizedSkillIds);
        entity.StartDateUtc = ToUtcDate(model.StartDate);
        entity.EndDateUtc = ToUtcDate(model.EndDate);
        entity.AllocationPercent = model.AllocationPercent;
        entity.Status = model.Status;
        entity.Notes = model.Notes.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(entity.Id);
    }

    public async Task DeleteStaffingRequestAsync(Guid staffingRequestId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<StaffingRequest>().SingleOrDefaultAsync(item => item.Id == staffingRequestId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        dbContext.Set<StaffingRequest>().Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<StaffingCandidatePage> SearchStaffingCandidatesAsync(
        StaffingCandidateQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var normalized = NormalizeStaffingCandidateQuery(query);
        var todayUtc = new DateTimeOffset(clock.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);
        var tomorrowUtc = todayUtc.AddDays(1);
        var nearAvailabilityUtc = todayUtc.AddDays(31);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var candidateProfiles =
            from profile in dbContext.Set<WorkforceProfile>().AsNoTracking()
            join party in dbContext.Set<Party>().AsNoTracking()
                on profile.PartyId equals party.Id
            select new
            {
                Profile = profile,
                Party = party
            };
        if (normalized.SkillId.HasValue)
        {
            candidateProfiles = candidateProfiles.Where(candidate =>
                dbContext.Set<PartySkill>().Any(skill =>
                    skill.PartyId == candidate.Party.Id &&
                    skill.SkillId == normalized.SkillId.Value));
        }

        if (!string.IsNullOrEmpty(normalized.SearchText))
        {
            var search = normalized.SearchText.ToUpperInvariant();
            var hasProficiency = Enum.TryParse<SkillProficiencyLevel>(
                normalized.SearchText,
                ignoreCase: true,
                out var proficiency);
            candidateProfiles = candidateProfiles.Where(candidate =>
                candidate.Party.DisplayName.ToUpper().Contains(search) ||
                candidate.Profile.JobTitle.ToUpper().Contains(search) ||
                candidate.Profile.Discipline.ToUpper().Contains(search) ||
                candidate.Profile.Seniority.ToUpper().Contains(search) ||
                candidate.Profile.Location.ToUpper().Contains(search) ||
                dbContext.Set<PartySkill>().Any(partySkill =>
                    partySkill.PartyId == candidate.Party.Id &&
                    ((hasProficiency && partySkill.Proficiency == proficiency) ||
                     dbContext.Set<SkillDefinition>().Any(skill =>
                         skill.Id == partySkill.SkillId &&
                         (skill.Name.ToUpper().Contains(search) ||
                          skill.Category.ToUpper().Contains(search))))));
        }

        var capacityCandidates = candidateProfiles.Select(candidate => new
        {
            candidate.Party.Id,
            candidate.Party.DisplayName,
            candidate.Party.PartyType,
            candidate.Profile.WorkforceKind,
            candidate.Profile.JobTitle,
            candidate.Profile.Discipline,
            candidate.Profile.Seniority,
            candidate.Profile.Location,
            ActiveAllocationPercent = dbContext.Set<ProjectPartyAssignment>()
                .Where(allocation =>
                    allocation.PartyId == candidate.Party.Id &&
                    allocation.AllocationPercent.HasValue &&
                    (!allocation.StartsAtUtc.HasValue || allocation.StartsAtUtc.Value < tomorrowUtc) &&
                    (!allocation.EndsAtUtc.HasValue || allocation.EndsAtUtc.Value >= todayUtc))
                .Sum(allocation => allocation.AllocationPercent) ?? 0m,
            ActiveBlockedPercent = dbContext.Set<CapacityBlock>()
                .Where(block =>
                    block.PartyId == candidate.Party.Id &&
                    block.StartDateUtc < tomorrowUtc &&
                    block.EndDateUtc >= todayUtc)
                .Sum(block => (decimal?)block.Percentage) ?? 0m,
            NextAllocationAtUtc = dbContext.Set<ProjectPartyAssignment>()
                .Where(allocation =>
                    allocation.PartyId == candidate.Party.Id &&
                    allocation.EndsAtUtc.HasValue &&
                    allocation.EndsAtUtc.Value >= todayUtc)
                .Min(allocation => allocation.EndsAtUtc),
            NextCapacityBlockAtUtc = dbContext.Set<CapacityBlock>()
                .Where(block =>
                    block.PartyId == candidate.Party.Id &&
                    block.EndDateUtc >= todayUtc)
                .Min(block => (DateTimeOffset?)block.EndDateUtc)
        });
        if (normalized.AvailabilityState.HasValue)
        {
            capacityCandidates = normalized.AvailabilityState.Value switch
            {
                WorkforceAvailabilityState.Bench => capacityCandidates.Where(candidate =>
                    candidate.ActiveAllocationPercent <= 10m &&
                    candidate.ActiveBlockedPercent < 25m),
                WorkforceAvailabilityState.Overallocated => capacityCandidates.Where(candidate =>
                    candidate.ActiveAllocationPercent + candidate.ActiveBlockedPercent > 100m),
                WorkforceAvailabilityState.NearAvailable => capacityCandidates.Where(candidate =>
                    candidate.ActiveAllocationPercent + candidate.ActiveBlockedPercent <= 100m &&
                    (candidate.ActiveAllocationPercent > 10m ||
                     candidate.ActiveBlockedPercent >= 25m) &&
                    ((candidate.NextAllocationAtUtc.HasValue &&
                      candidate.NextAllocationAtUtc.Value < nearAvailabilityUtc) ||
                     (candidate.NextCapacityBlockAtUtc.HasValue &&
                      candidate.NextCapacityBlockAtUtc.Value < nearAvailabilityUtc))),
                WorkforceAvailabilityState.Allocated => capacityCandidates.Where(candidate =>
                    candidate.ActiveAllocationPercent + candidate.ActiveBlockedPercent <= 100m &&
                    (candidate.ActiveAllocationPercent > 10m ||
                     candidate.ActiveBlockedPercent >= 25m) &&
                    (!candidate.NextAllocationAtUtc.HasValue ||
                     candidate.NextAllocationAtUtc.Value >= nearAvailabilityUtc) &&
                    (!candidate.NextCapacityBlockAtUtc.HasValue ||
                     candidate.NextCapacityBlockAtUtc.Value >= nearAvailabilityUtc)),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(query),
                    normalized.AvailabilityState,
                    "The staffing availability state is not supported.")
            };
        }

        var totalCount = await capacityCandidates.CountAsync(cancellationToken);
        var rows = await capacityCandidates
            .OrderByDescending(candidate =>
                candidate.ActiveAllocationPercent <= 10m &&
                candidate.ActiveBlockedPercent < 25m)
            .ThenByDescending(candidate =>
                candidate.ActiveAllocationPercent + candidate.ActiveBlockedPercent >= 100m
                    ? 0m
                    : 100m - candidate.ActiveAllocationPercent - candidate.ActiveBlockedPercent)
            .ThenBy(candidate => candidate.DisplayName)
            .ThenBy(candidate => candidate.Id)
            .Skip(normalized.PageIndex * normalized.PageSize)
            .Take(normalized.PageSize)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return new StaffingCandidatePage(
                [],
                normalized.PageIndex,
                normalized.PageSize,
                totalCount);
        }

        var partyIds = rows.Select(item => item.Id).ToList();
        var currentAffiliationRows = await (
                from affiliation in dbContext
                    .Set<PartyOrganizationAffiliation>()
                    .AsNoTracking()
                where partyIds.Contains(affiliation.PersonPartyId) &&
                      (!affiliation.ValidFromUtc.HasValue ||
                       affiliation.ValidFromUtc.Value <= todayUtc) &&
                      (!affiliation.ValidToUtc.HasValue ||
                       affiliation.ValidToUtc.Value >= todayUtc)
                join organization in dbContext.Set<Party>().AsNoTracking()
                    on affiliation.OrganizationPartyId equals organization.Id
                select new
                {
                    affiliation.Id,
                    affiliation.PersonPartyId,
                    affiliation.AffiliationKind,
                    affiliation.IsPrimary,
                    affiliation.JobTitle,
                    affiliation.ValidFromUtc,
                    affiliation.UpdatedAtUtc,
                    OrganizationName = organization.DisplayName
                })
            .ToListAsync(cancellationToken);
        var currentAffiliationsByPartyId = currentAffiliationRows
            .GroupBy(item => item.PersonPartyId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.IsPrimary)
                    .ThenByDescending(item => item.ValidFromUtc)
                    .ThenByDescending(item => item.UpdatedAtUtc)
                    .ThenBy(item => item.Id)
                    .ToArray());
        var partySkillsByPartyId = await GetPartySkillMapAsync(
            dbContext,
            partyIds,
            cancellationToken);
        var items = rows.Select(item =>
        {
            var nextAvailabilityAtUtc = MinNullable(
                item.NextAllocationAtUtc,
                item.NextCapacityBlockAtUtc);
            var nextAvailabilityOn = ToDateOnly(nextAvailabilityAtUtc);
            var availabilityState = ResolveAvailabilityState(
                item.ActiveAllocationPercent,
                item.ActiveBlockedPercent,
                nextAvailabilityOn);
            var skillSummary = string.Join(
                ", ",
                (partySkillsByPartyId.GetValueOrDefault(item.Id) ?? [])
                    .Select(skill => $"{skill.SkillName} ({skill.Proficiency})"));
            var currentAffiliations =
                currentAffiliationsByPartyId.GetValueOrDefault(item.Id) ?? [];
            var selectedAffiliation = currentAffiliations.FirstOrDefault();
            var classification = WorkforceRecordClassificationPolicy.Resolve(
                selectedAffiliation?.AffiliationKind,
                item.WorkforceKind,
                item.PartyType,
                hasDeliveryUnitRole: false);
            var primaryAffiliationText = selectedAffiliation is null
                ? item.JobTitle
                : FormatOrganizationAffiliation(
                    selectedAffiliation.OrganizationName,
                    selectedAffiliation.JobTitle);
            var otherAffiliationsSummary = string.Join(
                "; ",
                currentAffiliations
                    .Skip(1)
                    .Select(affiliation => FormatOrganizationAffiliation(
                        affiliation.OrganizationName,
                        affiliation.JobTitle))
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            return new StaffingCandidateItemModel(
                item.Id,
                item.DisplayName,
                item.PartyType,
                item.JobTitle,
                item.Discipline,
                item.Seniority,
                item.Location,
                skillSummary,
                availabilityState,
                Math.Max(0m, 100m - item.ActiveAllocationPercent - item.ActiveBlockedPercent),
                nextAvailabilityOn,
                classification,
                primaryAffiliationText,
                otherAffiliationsSummary);
        }).ToList();
        return new StaffingCandidatePage(
            items,
            normalized.PageIndex,
            normalized.PageSize,
            totalCount);
    }

    private static string FormatOrganizationAffiliation(
        string organizationName,
        string jobTitle)
    {
        var organization = organizationName?.Trim() ?? string.Empty;
        var title = jobTitle?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(organization))
        {
            return title;
        }

        return string.IsNullOrEmpty(title)
            ? organization
            : $"{organization} / {title}";
    }

    public async Task<StaffingDashboardModel> GetStaffingDashboardAsync(CancellationToken cancellationToken = default)
    {
        var todayUtc = new DateTimeOffset(clock.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);
        var tomorrowUtc = todayUtc.AddDays(1);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var openRequests = dbContext.Set<StaffingRequest>()
            .AsNoTracking()
            .Where(item =>
                item.Status == StaffingRequestStatus.Draft ||
                item.Status == StaffingRequestStatus.Open ||
                item.Status == StaffingRequestStatus.Proposed ||
                item.Status == StaffingRequestStatus.Confirmed);
        var openRequestCount = await openRequests.CountAsync(cancellationToken);
        var openDemandPercent = await openRequests
            .SumAsync(item => (decimal?)item.AllocationPercent, cancellationToken) ?? 0m;

        var workforceCapacity = dbContext.Set<WorkforceProfile>()
            .AsNoTracking()
            .Select(profile => new
            {
                ActiveAllocationPercent = dbContext.Set<ProjectPartyAssignment>()
                    .Where(allocation =>
                        allocation.PartyId == profile.PartyId &&
                        allocation.AllocationPercent.HasValue &&
                        (!allocation.StartsAtUtc.HasValue || allocation.StartsAtUtc.Value < tomorrowUtc) &&
                        (!allocation.EndsAtUtc.HasValue || allocation.EndsAtUtc.Value >= todayUtc))
                    .Sum(allocation => allocation.AllocationPercent) ?? 0m,
                ActiveBlockedPercent = dbContext.Set<CapacityBlock>()
                    .Where(block =>
                        block.PartyId == profile.PartyId &&
                        block.StartDateUtc < tomorrowUtc &&
                        block.EndDateUtc >= todayUtc)
                    .Sum(block => (decimal?)block.Percentage) ?? 0m
            });
        var capacityCounts = await workforceCapacity
            .GroupBy(_ => 1)
            .Select(group => new StaffingCapacityCounts(
                group.Count(item =>
                    item.ActiveAllocationPercent <= 10m &&
                    item.ActiveBlockedPercent < 25m),
                group.Count(item =>
                    item.ActiveAllocationPercent + item.ActiveBlockedPercent > 100m)))
            .SingleOrDefaultAsync(cancellationToken);
        return new StaffingDashboardModel(
            openRequestCount,
            openDemandPercent,
            capacityCounts?.BenchCount ?? 0,
            capacityCounts?.OverallocatedCount ?? 0);
    }

    private static StaffingRequestQuery NormalizeStaffingRequestQuery(StaffingRequestQuery query)
    {
        if (query.ProjectId == Guid.Empty)
        {
            throw new ArgumentException("A project identifier is required.", nameof(query));
        }

        ValidateStaffingPage(query.PageIndex, query.PageSize, nameof(query));
        if (query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.Status,
                "The staffing request status is not supported.");
        }

        return query with
        {
            SearchText = NormalizeStaffingSearch(query.SearchText, nameof(query))
        };
    }

    private static StaffingCandidateQuery NormalizeStaffingCandidateQuery(StaffingCandidateQuery query)
    {
        ValidateStaffingPage(query.PageIndex, query.PageSize, nameof(query));
        if (query.SkillId == Guid.Empty)
        {
            throw new ArgumentException(
                "A staffing skill identifier cannot be empty.",
                nameof(query));
        }

        if (query.AvailabilityState.HasValue &&
            !Enum.IsDefined(query.AvailabilityState.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.AvailabilityState,
                "The staffing availability state is not supported.");
        }

        return query with
        {
            SearchText = NormalizeStaffingSearch(query.SearchText, nameof(query))
        };
    }

    private static void ValidateStaffingPage(
        int pageIndex,
        int pageSize,
        string parameterName)
    {
        if (pageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                pageIndex,
                "Staffing page index cannot be negative.");
        }

        if (pageSize is < 1 or > StaffingQueryLimits.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                pageSize,
                $"Staffing page size must be between 1 and {StaffingQueryLimits.MaximumPageSize}.");
        }

        if (pageIndex > int.MaxValue / pageSize)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                pageIndex,
                "Staffing page offset is too large.");
        }
    }

    private static string NormalizeStaffingSearch(
        string? searchText,
        string parameterName)
    {
        var normalized = searchText?.Trim() ?? string.Empty;
        if (normalized.Length > StaffingQueryLimits.MaximumSearchLength)
        {
            throw new ArgumentException(
                $"Staffing search cannot exceed {StaffingQueryLimits.MaximumSearchLength} characters.",
                parameterName);
        }

        return normalized;
    }

    private static DateTimeOffset? MinNullable(
        DateTimeOffset? first,
        DateTimeOffset? second)
    {
        if (!first.HasValue)
        {
            return second;
        }

        if (!second.HasValue)
        {
            return first;
        }

        return first.Value <= second.Value ? first : second;
    }

    private static WorkforceCapacitySummaryModel BuildCapacitySummary(
        decimal capacityHoursPerWeek,
        IReadOnlyList<ProjectAllocationItemModel> allocations,
        IReadOnlyList<CapacityBlockItemModel> capacityBlocks)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activeAllocationPercent = allocations
            .Where(item => item.IsActive)
            .Sum(item => item.AllocationPercent);
        var activeBlockedPercent = capacityBlocks
            .Where(item => item.IsActive)
            .Sum(item => item.Percentage);
        var rawAvailablePercent = 100m - activeAllocationPercent - activeBlockedPercent;
        var availablePercent = Math.Max(0m, rawAvailablePercent);
        var nextAvailabilityOn = allocations
            .Where(item => item.EndsOn.HasValue && item.EndsOn.Value >= today)
            .Select(item => item.EndsOn!.Value)
            .Concat(capacityBlocks
                .Where(item => item.EndDate >= today)
                .Select(item => item.EndDate))
            .OrderBy(item => item)
            .FirstOrDefault();
        var availabilityState = ResolveAvailabilityState(activeAllocationPercent, activeBlockedPercent, nextAvailabilityOn);

        return new WorkforceCapacitySummaryModel(
            capacityHoursPerWeek <= 0m ? 40m : capacityHoursPerWeek,
            activeAllocationPercent,
            activeBlockedPercent,
            availablePercent,
            availabilityState,
            BuildAvailabilityMessage(availabilityState, activeAllocationPercent, activeBlockedPercent, nextAvailabilityOn),
            nextAvailabilityOn == default ? null : nextAvailabilityOn,
            availabilityState == WorkforceAvailabilityState.Overallocated,
            availabilityState == WorkforceAvailabilityState.Bench);
    }

    private static WorkforceAvailabilityState ResolveAvailabilityState(
        decimal activeAllocationPercent,
        decimal activeBlockedPercent,
        DateOnly? nextAvailabilityOn)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (activeAllocationPercent + activeBlockedPercent > 100m)
        {
            return WorkforceAvailabilityState.Overallocated;
        }

        if (activeAllocationPercent <= 10m && activeBlockedPercent < 25m)
        {
            return WorkforceAvailabilityState.Bench;
        }

        if (nextAvailabilityOn.HasValue && nextAvailabilityOn.Value <= today.AddDays(30))
        {
            return WorkforceAvailabilityState.NearAvailable;
        }

        return WorkforceAvailabilityState.Allocated;
    }

    private static string BuildAvailabilityMessage(
        WorkforceAvailabilityState availabilityState,
        decimal activeAllocationPercent,
        decimal activeBlockedPercent,
        DateOnly? nextAvailabilityOn)
    {
        return availabilityState switch
        {
            WorkforceAvailabilityState.Overallocated => $"Overallocated at {(activeAllocationPercent + activeBlockedPercent):0.##}% commitment.",
            WorkforceAvailabilityState.Bench => "Bench or lightly committed.",
            WorkforceAvailabilityState.NearAvailable when nextAvailabilityOn.HasValue => $"Near availability on {nextAvailabilityOn.Value:yyyy-MM-dd}.",
            _ => $"Allocated at {activeAllocationPercent:0.##}% with {activeBlockedPercent:0.##}% blocked."
        };
    }

    private static IReadOnlyList<Guid> ParseSkillIds(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task<IReadOnlyList<SkillCatalogItemModel>> GetSkillCatalogItemsAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        return await dbContext.Set<SkillDefinition>()
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.Category)
            .ThenBy(item => item.Name)
            .Select(item => new SkillCatalogItemModel(item.Id, item.Name, item.Category, item.Description, item.IsActive))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<PartySkillItemModel>>> GetPartySkillMapAsync(
        AppDbContext dbContext,
        IReadOnlyList<Guid> partyIds,
        CancellationToken cancellationToken)
    {
        if (partyIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<PartySkillItemModel>>();
        }

        var items = await dbContext.Set<PartySkill>()
            .Where(item => partyIds.Contains(item.PartyId))
            .Join(
                dbContext.Set<SkillDefinition>(),
                partySkill => partySkill.SkillId,
                skill => skill.Id,
                (partySkill, skill) => new
                {
                    partySkill.PartyId,
                    partySkill.Id,
                    SkillId = skill.Id,
                    SkillName = skill.Name,
                    SkillCategory = skill.Category,
                    partySkill.Proficiency,
                    partySkill.YearsExperience,
                    partySkill.CertificationStatus,
                    partySkill.LastValidatedAtUtc,
                    partySkill.Notes
                })
            .OrderBy(item => item.SkillCategory)
            .ThenBy(item => item.SkillName)
            .ToListAsync(cancellationToken);

        return items
            .GroupBy(item => item.PartyId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PartySkillItemModel>)group.Select(item =>
                    new PartySkillItemModel(
                        item.Id,
                        item.SkillId,
                        item.SkillName,
                        item.SkillCategory,
                        item.Proficiency,
                        item.YearsExperience,
                        item.CertificationStatus,
                        ToDateOnly(item.LastValidatedAtUtc),
                        item.Notes)).ToList());
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<CapacityBlockItemModel>>> GetCapacityBlockMapAsync(
        AppDbContext dbContext,
        IReadOnlyList<Guid> partyIds,
        CancellationToken cancellationToken)
    {
        if (partyIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<CapacityBlockItemModel>>();
        }

        var blocks = await dbContext.Set<CapacityBlock>()
            .Where(item => partyIds.Contains(item.PartyId))
            .ToListAsync(cancellationToken);
        if (blocks.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<CapacityBlockItemModel>>();
        }

        blocks = blocks
            .OrderBy(item => item.StartDateUtc)
            .ThenBy(item => item.EndDateUtc)
            .ToList();

        var projectIds = blocks.Where(item => item.RelatedProjectId.HasValue).Select(item => item.RelatedProjectId!.Value).Distinct().ToList();
        var projectNames = projectIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Set<Project>()
                .Where(item => projectIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return blocks
            .GroupBy(item => item.PartyId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<CapacityBlockItemModel>)group.Select(item =>
                {
                    var startDate = ToDateOnly(item.StartDateUtc) ?? today;
                    var endDate = ToDateOnly(item.EndDateUtc) ?? startDate;
                    return new CapacityBlockItemModel(
                        item.Id,
                        item.BlockKind,
                        startDate,
                        endDate,
                        item.Percentage,
                        item.RelatedProjectId,
                        item.RelatedProjectId.HasValue ? projectNames.GetValueOrDefault(item.RelatedProjectId.Value) ?? string.Empty : string.Empty,
                        item.Notes,
                        startDate <= today && endDate >= today,
                        startDate > today);
                })
                .OrderBy(item => item.StartDate)
                .ThenBy(item => item.BlockKind)
                .ToList());
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ProjectAllocationItemModel>>> GetProjectAllocationMapAsync(
        AppDbContext dbContext,
        IReadOnlyList<Guid> partyIds,
        CancellationToken cancellationToken)
    {
        if (partyIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<ProjectAllocationItemModel>>();
        }

        var assignments = await dbContext.Set<ProjectPartyAssignment>()
            .Where(item => partyIds.Contains(item.PartyId) && item.AllocationPercent.HasValue)
            .Select(item => new
            {
                item.Id,
                item.ProjectId,
                item.PartyId,
                item.AssignmentKind,
                item.AllocationPercent,
                item.StartsAtUtc,
                item.EndsAtUtc,
                item.Notes
            })
            .ToListAsync(cancellationToken);
        if (assignments.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<ProjectAllocationItemModel>>();
        }

        assignments = assignments
            .OrderBy(item => item.StartsAtUtc ?? DateTimeOffset.MinValue)
            .ThenBy(item => item.EndsAtUtc ?? DateTimeOffset.MaxValue)
            .ToList();

        var projectIds = assignments.Select(item => item.ProjectId).Distinct().ToList();
        var partyNameIds = assignments.Select(item => item.PartyId).Distinct().ToList();
        var projectNames = await dbContext.Set<Project>()
            .Where(item => projectIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var partyNames = await dbContext.Set<Party>()
            .Where(item => partyNameIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return assignments
            .GroupBy(item => item.PartyId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProjectAllocationItemModel>)group.Select(item =>
                {
                    var startsOn = ToDateOnly(item.StartsAtUtc);
                    var endsOn = ToDateOnly(item.EndsAtUtc);
                    var isActive = (!startsOn.HasValue || startsOn.Value <= today) && (!endsOn.HasValue || endsOn.Value >= today);
                    var isFuture = startsOn.HasValue && startsOn.Value > today;
                    return new ProjectAllocationItemModel(
                        item.Id,
                        item.ProjectId,
                        projectNames.GetValueOrDefault(item.ProjectId) ?? string.Empty,
                        item.PartyId,
                        partyNames.GetValueOrDefault(item.PartyId) ?? string.Empty,
                        MapProjectAssignmentRole(item.AssignmentKind),
                        item.AllocationPercent ?? 0m,
                        startsOn,
                        endsOn,
                        item.Notes,
                        isActive,
                        isFuture);
                })
                .OrderByDescending(item => item.IsActive)
                .ThenBy(item => item.StartsOn)
                .ThenBy(item => item.ProjectName)
                .ToList());
    }

    private static WorkforceKind ResolveDefaultWorkforceKind(PartyType partyType, IReadOnlyList<PartyRoleKind> roles, WorkforceProfile? profile)
    {
        if (profile is not null)
        {
            return profile.WorkforceKind;
        }

        if (partyType == PartyType.Organization || partyType == PartyType.OrganizationUnit)
        {
            return WorkforceKind.DeliveryUnit;
        }

        if (roles.Contains(PartyRoleKind.Contractor))
        {
            return WorkforceKind.Contractor;
        }

        if (roles.Contains(PartyRoleKind.Freelancer))
        {
            return WorkforceKind.Freelancer;
        }

        return WorkforceKind.Employee;
    }

    private static string ResolveDefaultStatus(PartyLifecycleStatus lifecycleStatus)
    {
        return lifecycleStatus switch
        {
            PartyLifecycleStatus.Active => "Active",
            PartyLifecycleStatus.Inactive => "Inactive",
            _ => "Planned"
        };
    }

    private static PartyRoleKind ResolveWorkforceRole(WorkforceKind workforceKind)
    {
        return workforceKind switch
        {
            WorkforceKind.Contractor => PartyRoleKind.Contractor,
            WorkforceKind.Freelancer => PartyRoleKind.Freelancer,
            WorkforceKind.DeliveryUnit => PartyRoleKind.DeliveryUnit,
            _ => PartyRoleKind.Employee
        };
    }

    private static DateTimeOffset? ToUtcDate(DateOnly? value)
    {
        return value.HasValue
            ? new DateTimeOffset(value.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : null;
    }

    private static DateOnly? ToDateOnly(DateTimeOffset? value)
    {
        return value.HasValue
            ? DateOnly.FromDateTime(value.Value.UtcDateTime)
            : null;
    }

    private static ProjectPartyAssignmentRole MapProjectAssignmentRole(ProjectPartyAssignmentKind role)
    {
        return role switch
        {
            ProjectPartyAssignmentKind.Customer => ProjectPartyAssignmentRole.Customer,
            ProjectPartyAssignmentKind.CustomerContact => ProjectPartyAssignmentRole.CustomerContact,
            ProjectPartyAssignmentKind.DeliveryUnit => ProjectPartyAssignmentRole.DeliveryUnit,
            ProjectPartyAssignmentKind.TeamMember => ProjectPartyAssignmentRole.TeamMember,
            ProjectPartyAssignmentKind.Manager => ProjectPartyAssignmentRole.Manager,
            ProjectPartyAssignmentKind.Partner => ProjectPartyAssignmentRole.Partner,
            ProjectPartyAssignmentKind.Vendor => ProjectPartyAssignmentRole.Vendor,
            ProjectPartyAssignmentKind.Stakeholder => ProjectPartyAssignmentRole.Stakeholder,
            ProjectPartyAssignmentKind.MeetingParticipant => ProjectPartyAssignmentRole.MeetingParticipant,
            ProjectPartyAssignmentKind.WorkItemAssignee => ProjectPartyAssignmentRole.WorkItemAssignee,
            ProjectPartyAssignmentKind.Reviewer => ProjectPartyAssignmentRole.Reviewer,
            ProjectPartyAssignmentKind.AiAgent => ProjectPartyAssignmentRole.AiAgent,
            ProjectPartyAssignmentKind.BillingContact => ProjectPartyAssignmentRole.BillingContact,
            ProjectPartyAssignmentKind.TechnicalContact => ProjectPartyAssignmentRole.TechnicalContact,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "The persisted project assignment role is not supported.")
        };
    }

    private static string ResolvePrimaryContact(IEnumerable<CrmPartyContactValue> contacts, PartyContactType contactType)
    {
        return contacts
            .Where(item => item.ContactType == contactType)
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => item.Value)
            .FirstOrDefault()
            ?? string.Empty;
    }
}

public sealed partial class AiAgentService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    IActivityStream activityStream,
    ISearchIndexService searchIndexService,
    PartyDirectoryService partyDirectoryService,
    IAiTechnicalAgentBridge technicalAgentBridge)
{
    private const int LegacyDirectorySnapshotLimit = AiAgentDirectoryQueryLimits.MaximumPageSize;

    public Task SynchronizeDirectoryProjectionAsync(CancellationToken cancellationToken = default)
    {
        return technicalAgentBridge.SynchronizeDirectoryProjectionAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AiAgentListItemModel>> ListAgentDirectoryAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await ListAgentDirectoryFromProjectionAsync(dbContext, cancellationToken);
    }

    public Task<IReadOnlyList<AiAgentListItemModel>> ListAgentDirectorySnapshotAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        return ListAgentDirectoryFromProjectionAsync(dbContext, cancellationToken);
    }

    public async Task<IReadOnlyList<AiAgentStaffingFactListItemModel>> ListAgentStaffingFactsAsync(
        IReadOnlyList<Guid>? partyIds = null,
        CancellationToken cancellationToken = default)
    {
        return await ListAgentStaffingFactsSnapshotAsync(partyIds, cancellationToken);
    }

    public async Task<IReadOnlyList<AiAgentStaffingFactListItemModel>> ListAgentStaffingFactsSnapshotAsync(
        IReadOnlyList<Guid>? partyIds = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedPartyIds = partyIds?
            .Where(item => item != Guid.Empty)
            .Distinct()
            .ToList()
            ?? [];
        if (resolvedPartyIds.Count == 0)
        {
            throw new ArgumentException(
                "AI agent staffing facts require at least one party identifier.",
                nameof(partyIds));
        }

        var facts = await technicalAgentBridge.GetStaffingFactsAsync(resolvedPartyIds, cancellationToken);

        return facts.Values
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(item => new AiAgentStaffingFactListItemModel(
                item.PartyId,
                item.TechnicalAgentId,
                item.DisplayName,
                item.RoleTitle,
                item.Summary,
                item.Instructions,
                item.BindingStatus,
                item.BindingSummary,
                item.ExecutionMode,
                item.ProviderName,
                item.DefaultModel,
                item.TemplateKey,
                item.Tags,
                item.Capabilities,
                item.AgentsRoute))
            .ToList();
    }

    public async Task<IReadOnlyList<AiAgentStaffingFactListItemModel>> ListAgentStaffingFactsProjectionAsync(
        AppDbContext dbContext,
        IReadOnlyList<Guid>? partyIds = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var resolvedPartyIds = partyIds?
            .Where(item => item != Guid.Empty)
            .Distinct()
            .ToList()
            ?? [];
        if (resolvedPartyIds.Count == 0)
        {
            throw new ArgumentException(
                "AI agent staffing facts require at least one party identifier.",
                nameof(partyIds));
        }

        var partiesQuery = dbContext.Set<Party>()
            .AsNoTracking()
            .Where(item => item.PartyType == PartyType.AiAgent)
            .Where(item => resolvedPartyIds.Contains(item.Id));

        var parties = await partiesQuery
            .OrderBy(item => item.DisplayName)
            .Select(item => new
            {
                item.Id,
                item.DisplayName,
                item.Summary,
                item.TagsJson
            })
            .ToListAsync(cancellationToken);
        if (parties.Count == 0)
        {
            return [];
        }

        var partyIdsToLoad = parties.Select(item => item.Id).ToList();
        var technicalFacts = await technicalAgentBridge.GetStaffingFactsAsync(partyIdsToLoad, cancellationToken);

        return parties
            .Select(party =>
            {
                technicalFacts.TryGetValue(party.Id, out var technicalFact);
                if (technicalFact is null)
                {
                    throw new InvalidOperationException(
                        $"AI agent party '{party.Id:D}' did not receive a technical projection fact.");
                }

                return new AiAgentStaffingFactListItemModel(
                    party.Id,
                    technicalFact.TechnicalAgentId,
                    party.DisplayName,
                    technicalFact.RoleTitle,
                    party.Summary,
                    technicalFact.Instructions,
                    technicalFact.BindingStatus,
                    technicalFact.BindingSummary,
                    technicalFact.ExecutionMode,
                    technicalFact.ProviderName,
                    technicalFact.DefaultModel,
                    technicalFact.TemplateKey,
                    ResolveAiProjectionTags(party.TagsJson, party.Id, technicalFact.Tags),
                    technicalFact.Capabilities,
                    technicalFact.AgentsRoute);
            })
            .ToList();
    }

    private async Task<IReadOnlyList<AiAgentListItemModel>> ListAgentDirectoryFromProjectionAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var parties = await dbContext.Set<Party>()
            .Where(item => item.PartyType == PartyType.AiAgent)
            .OrderBy(item => item.DisplayName)
            .Take(LegacyDirectorySnapshotLimit + 1)
            .Select(item => new
            {
                item.Id,
                item.DisplayName,
                item.Summary,
                item.LifecycleStatus,
                item.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);
        if (parties.Count > LegacyDirectorySnapshotLimit)
        {
            throw new InvalidOperationException(
                $"The legacy AI agent directory snapshot is limited to {LegacyDirectorySnapshotLimit} records. Use {nameof(IAiAgentDirectoryQueryService)} for paged access.");
        }
        if (parties.Count == 0)
        {
            return [];
        }

        var partyIds = parties.Select(item => item.Id).ToList();
        var profiles = await dbContext.Set<AiAgentProfile>()
            .Where(item => partyIds.Contains(item.PartyId))
            .ToListAsync(cancellationToken);
        var technicalSummaries = await technicalAgentBridge.GetDirectorySummariesAsync(partyIds, cancellationToken);
        var ownerIds = profiles
            .Where(item => item.OwnerPartyId.HasValue)
            .Select(item => item.OwnerPartyId!.Value)
            .Distinct()
            .ToList();

        var ownerNames = ownerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Set<Party>()
                .Where(item => ownerIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);
        var profileByPartyId = profiles.ToDictionary(item => item.PartyId);

        return parties
            .Where(item => technicalSummaries.GetValueOrDefault(item.Id)?.HasTechnicalProfile == true)
            .Select(item =>
            {
                profileByPartyId.TryGetValue(item.Id, out var profile);
                technicalSummaries.TryGetValue(item.Id, out var technicalSummary);
                var hasTechnicalProfile = technicalSummary?.HasTechnicalProfile == true;
                var hasProfile = hasTechnicalProfile || profile is not null;
                var validationStatus = profile?.ValidationStatus ?? (hasTechnicalProfile ? AiValidationStatus.Draft : null);
                var executionMode = technicalSummary?.ExecutionMode;
                return new AiAgentListItemModel(
                    item.Id,
                    item.DisplayName,
                    item.Summary,
                    item.LifecycleStatus,
                    technicalSummary?.TechnicalAgentId,
                    technicalSummary?.BindingStatus ?? AiResourceBindingStatus.Unbound,
                    technicalSummary?.BindingSummary ?? "No technical binding.",
                    executionMode,
                    validationStatus,
                    technicalSummary?.ProviderName ?? string.Empty,
                    technicalSummary?.DefaultModel ?? string.Empty,
                    profile?.OwnerPartyId is Guid ownerPartyId ? ownerNames.GetValueOrDefault(ownerPartyId) ?? string.Empty : string.Empty,
                    technicalSummary?.CapabilityCount ?? 0,
                    hasProfile,
                    technicalSummary?.AgentsRoute ?? "/agents?tab=agents",
                    item.UpdatedAtUtc);
            })
            .ToList();
    }

    public async Task<Result<Guid>> CreateAgentAsync(
        string displayName,
        string externalCode = "",
        string summary = "",
        string lastChangedBy = "crm-hr-ui",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Result<Guid>.Failure(Error.Validation("AI agent name is required.", "crmhr.ai-agent.display-name-required"));
        }

        var createResult = await partyDirectoryService.SavePartyAsync(
            new PartyEditorModel
            {
                PartyType = PartyType.AiAgent,
                LifecycleStatus = PartyLifecycleStatus.Active,
                DisplayName = displayName.Trim(),
                ExternalCode = externalCode.Trim(),
                Summary = summary.Trim(),
                LastChangedBy = string.IsNullOrWhiteSpace(lastChangedBy) ? "crm-hr-ui" : lastChangedBy.Trim()
            },
            cancellationToken);
        if (createResult.IsFailure)
        {
            return createResult;
        }

        var partyId = createResult.Value;
        var profileSaveResult = await SaveAgentProfileAsync(
            new AiAgentProfileEditorModel
            {
                PartyId = partyId,
                Notes = summary.Trim(),
                LastChangedBy = string.IsNullOrWhiteSpace(lastChangedBy) ? "crm-hr-ui" : lastChangedBy.Trim()
            },
            cancellationToken);
        if (profileSaveResult.IsSuccess)
        {
            return Result<Guid>.Success(partyId);
        }

        await RollBackFailedAgentCreationAsync(partyId, cancellationToken);
        return Result<Guid>.Failure(profileSaveResult.Errors);
    }

    public async Task<AiAgentWorkspaceModel?> GetAgentWorkspaceAsync(Guid partyId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var party = await dbContext.Set<Party>()
            .Select(item => new
            {
                item.Id,
                item.DisplayName,
                item.Summary,
                item.LifecycleStatus,
                item.PartyType
            })
            .SingleOrDefaultAsync(item => item.Id == partyId, cancellationToken);
        if (party is null || party.PartyType != PartyType.AiAgent)
        {
            return null;
        }

        var contactPoints = await dbContext.Set<PartyContactPoint>()
            .Where(item => item.PartyId == partyId)
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => new CrmPartyContactValue(item.PartyId, item.ContactType, item.Value, item.IsPrimary))
            .ToListAsync(cancellationToken);
        var profile = await dbContext.Set<AiAgentProfile>()
            .SingleOrDefaultAsync(item => item.PartyId == partyId, cancellationToken);
        var technicalSummaries = await technicalAgentBridge.GetDirectorySummariesAsync(
            [partyId],
            cancellationToken);
        if (!technicalSummaries.TryGetValue(partyId, out var technicalSummary) ||
            !technicalSummary.HasTechnicalProfile)
        {
            return null;
        }

        var ownerName = profile?.OwnerPartyId is Guid ownerPartyId
            ? await dbContext.Set<Party>()
                .AsNoTracking()
                .Where(item => item.Id == ownerPartyId)
                .Select(item => item.DisplayName)
                .SingleOrDefaultAsync(cancellationToken)
                ?? string.Empty
            : string.Empty;

        return new AiAgentWorkspaceModel(
            party.Id,
            party.DisplayName,
            party.Summary,
            party.LifecycleStatus,
            ResolvePrimaryContactValue(contactPoints, PartyContactType.Email),
            ResolvePrimaryContactValue(contactPoints, PartyContactType.Phone),
            technicalSummary.TechnicalAgentId,
            technicalSummary.BindingStatus,
            technicalSummary.BindingSummary,
            technicalSummary.AgentsRoute,
            technicalSummary.ProviderName,
            ownerName,
            technicalSummary.CapabilityCount,
            new AiAgentProfileEditorModel
            {
                Id = profile?.Id,
                PartyId = party.Id,
                DefaultModel = technicalSummary.DefaultModel,
                ExecutionMode = technicalSummary.ExecutionMode ?? AiExecutionMode.Remote,
                OwnerPartyId = profile?.OwnerPartyId,
                ValidationStatus = profile?.ValidationStatus ?? AiValidationStatus.Draft,
                LastReviewedOn = profile?.LastReviewedAtUtc is DateTimeOffset reviewedAtUtc ? DateOnly.FromDateTime(reviewedAtUtc.UtcDateTime) : null,
                Notes = profile?.Notes ?? string.Empty,
                ExtendedDataJson = profile?.ExtendedDataJson ?? "{}",
                LastChangedBy = "crm-hr-ui"
            });
    }

    public async Task<Result<Guid>> SaveAgentProfileAsync(AiAgentProfileEditorModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model.PartyId == Guid.Empty)
        {
            return Result<Guid>.Failure(Error.Validation("Choose an AI agent before saving the profile.", "crmhr.ai-agent.party-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var party = await dbContext.Set<Party>()
            .SingleOrDefaultAsync(item => item.Id == model.PartyId, cancellationToken);
        if (party is null)
        {
            return Result<Guid>.Failure(Error.Failure("The selected AI agent party could not be found.", "crmhr.ai-agent.party-not-found"));
        }

        if (party.PartyType != PartyType.AiAgent)
        {
            return Result<Guid>.Failure(Error.Validation("Only AI agent parties can carry AI agent operational profiles.", "crmhr.ai-agent.party-type-invalid"));
        }

        if (model.OwnerPartyId == model.PartyId)
        {
            return Result<Guid>.Failure(Error.Validation("An AI agent cannot own itself.", "crmhr.ai-agent.self-owner"));
        }

        Party? owner = null;
        if (model.OwnerPartyId is Guid ownerPartyId)
        {
            owner = await dbContext.Set<Party>()
                .SingleOrDefaultAsync(item => item.Id == ownerPartyId, cancellationToken);
            if (owner is null || owner.PartyType != PartyType.Person)
            {
                return Result<Guid>.Failure(Error.Validation("Owner must reference an existing person.", "crmhr.ai-agent.owner-invalid"));
            }
        }

        var normalizedExtendedData = NormalizeJson(model.ExtendedDataJson, "{}");
        if (normalizedExtendedData is null)
        {
            return Result<Guid>.Failure(Error.Validation("Extended data must be valid JSON.", "crmhr.ai-agent.extended-data-invalid"));
        }

        if (owner is not null)
        {
            var ownerRoles = await dbContext.Set<PartyRoleAssignment>()
                .Where(item => item.PartyId == owner.Id)
                .ToListAsync(cancellationToken);
            if (!ownerRoles.Any(item => item.RoleKind == PartyRoleKind.AiSteward))
            {
                dbContext.Set<PartyRoleAssignment>().Add(new PartyRoleAssignment
                {
                    PartyId = owner.Id,
                    RoleKind = PartyRoleKind.AiSteward,
                    Title = "AI steward",
                    IsPrimary = ownerRoles.Count == 0
                });
            }
        }

        var technicalSaveResult = await technicalAgentBridge.SaveAsync(model, cancellationToken);
        if (technicalSaveResult.IsFailure)
        {
            return Result<Guid>.Failure(technicalSaveResult.Errors);
        }
        var technicalAgentSave = technicalSaveResult.Value!;

        var profile = await dbContext.Set<AiAgentProfile>()
            .SingleOrDefaultAsync(item => item.PartyId == model.PartyId, cancellationToken);
        if (profile is null)
        {
            profile = new AiAgentProfile
            {
                PartyId = model.PartyId
            };
            dbContext.Set<AiAgentProfile>().Add(profile);
        }

        profile.OwnerPartyId = model.OwnerPartyId;
        profile.ValidationStatus = model.ValidationStatus;
        profile.LastReviewedAtUtc = ToUtcDate(model.LastReviewedOn);
        profile.Notes = model.Notes.Trim();
        profile.ExtendedDataJson = normalizedExtendedData;

        party.LastChangedBy = string.IsNullOrWhiteSpace(model.LastChangedBy) ? "crm-hr-ui" : model.LastChangedBy.Trim();
        party.UpdatedAtUtc = clock.GetUtcNow();
        CrmHrAuditWriter.AddEntry(
            dbContext,
            nameof(AiAgentProfile),
            party.Id,
            "AiAgentProfileSaved",
            $"Saved AI agent profile for '{party.DisplayName}'.",
            new
            {
                model.ExecutionMode,
                profile.ValidationStatus,
                model.ProviderProfileId,
                profile.OwnerPartyId,
                TechnicalAgentId = technicalAgentSave.TechnicalAgentId,
                technicalAgentSave.BindingStatus
            },
            party.LastChangedBy,
            party.IsSensitive,
            party.UpdatedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
        await UpsertAiAgentSearchDocumentAsync(party.Id, cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                "AiAgentProfileSaved",
                $"Saved AI agent profile for {party.DisplayName}",
                $"{model.ExecutionMode} / {profile.ValidationStatus}",
                ArtifactKind: nameof(AiAgentProfile),
                ArtifactId: party.Id,
                Route: $"/crm-hr/agents?partyId={party.Id}",
                Actor: party.LastChangedBy),
            cancellationToken);
        return Result<Guid>.Success(profile.Id);
    }

    private async Task RollBackFailedAgentCreationAsync(Guid partyId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var party = await dbContext.Set<Party>()
            .SingleOrDefaultAsync(item => item.Id == partyId, cancellationToken);
        var profile = await dbContext.Set<AiAgentProfile>()
            .SingleOrDefaultAsync(item => item.PartyId == partyId, cancellationToken);
        var binding = await dbContext.Set<AiResourceBinding>()
            .SingleOrDefaultAsync(item => item.PartyId == partyId, cancellationToken);
        var auditEntries = await dbContext.Set<CrmHrAuditEntry>()
            .Where(item =>
                item.EntityId == partyId &&
                (item.EntityType == nameof(Party) || item.EntityType == nameof(AiAgentProfile)))
            .ToListAsync(cancellationToken);

        if (profile is not null)
        {
            dbContext.Set<AiAgentProfile>().Remove(profile);
        }

        if (binding is not null)
        {
            dbContext.Set<AiResourceBinding>().Remove(binding);
        }

        if (party is not null)
        {
            dbContext.Set<Party>().Remove(party);
        }

        if (auditEntries.Count > 0)
        {
            dbContext.Set<CrmHrAuditEntry>().RemoveRange(auditEntries);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await searchIndexService.DeleteAsync(CrmHrSearchSourceTypes.Party, partyId.ToString("N"), cancellationToken);
        await searchIndexService.DeleteAsync(CrmHrSearchSourceTypes.AiAgent, partyId.ToString("N"), cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                "AiAgentCreationRolledBack",
                $"Rolled back AI agent creation for {partyId:D}",
                "Technical registration failed",
                ArtifactKind: nameof(Party),
                ArtifactId: partyId,
                Route: "/crm-hr/agents",
                Actor: "crm-hr-ui"),
            cancellationToken);
    }

    public async Task<IReadOnlyList<AiAgentProfileSummaryModel>> ListAgentProfilesAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<AiAgentProfile>()
            .OrderBy(item => item.ValidationStatus)
            .ThenBy(item => item.DefaultModel)
            .Select(item => new AiAgentProfileSummaryModel(item.Id, item.PartyId, item.ProviderProfileId, item.ExecutionMode, item.ValidationStatus))
            .ToListAsync(cancellationToken);
    }

    private static List<AiCapabilityEditorModel> DeserializeCapabilities(string json, Guid profileId)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<AiCapabilityEditorModel>>(json)?
                .Select(CloneCapability)
                .ToList()
                ?? [];
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"AI agent profile '{profileId}' contains invalid capability JSON.");
        }
    }

    private static string SerializeCapabilities(IEnumerable<AiCapabilityEditorModel> capabilities)
    {
        return JsonSerializer.Serialize(capabilities
            .Select(CloneCapability)
            .Where(HasCapabilityContent)
            .ToList());
    }

    private static AiCapabilityEditorModel CloneCapability(AiCapabilityEditorModel capability)
    {
        return new AiCapabilityEditorModel
        {
            Name = capability.Name.Trim(),
            Scope = capability.Scope.Trim(),
            ToolAccess = capability.ToolAccess.Trim(),
            Limitations = capability.Limitations.Trim(),
            Notes = capability.Notes.Trim()
        };
    }

    private static bool HasCapabilityContent(AiCapabilityEditorModel capability)
    {
        return !string.IsNullOrWhiteSpace(capability.Name)
            || !string.IsNullOrWhiteSpace(capability.Scope)
            || !string.IsNullOrWhiteSpace(capability.ToolAccess)
            || !string.IsNullOrWhiteSpace(capability.Limitations)
            || !string.IsNullOrWhiteSpace(capability.Notes);
    }

    private static string ResolveDefaultModel(string requestedModel, string? providerDefaultModel)
    {
        if (!string.IsNullOrWhiteSpace(requestedModel))
        {
            return requestedModel.Trim();
        }

        return providerDefaultModel?.Trim() ?? string.Empty;
    }

    private static string ResolveAiProjectionBindingSummary(AiResourceBinding? binding)
    {
        if (binding is null)
        {
            return "No technical binding.";
        }

        if (!string.IsNullOrWhiteSpace(binding.LastError))
        {
            return $"{binding.BindingStatus}: {binding.LastError.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(binding.BindingReason))
        {
            return binding.BindingReason.Trim();
        }

        return binding.BindingStatus.ToString();
    }

    private static IReadOnlyList<string> DeserializeAiProjectionTags(string json, Guid partyId)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"AI agent party '{partyId}' contains invalid tags JSON.");
        }
    }

    private static IReadOnlyList<string> ResolveAiProjectionTags(
        string json,
        Guid partyId,
        IReadOnlyList<string> technicalTags)
    {
        return DeserializeAiProjectionTags(json, partyId)
            .Concat(technicalTags)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildAgentsRoute(Guid? technicalAgentId)
    {
        return technicalAgentId.HasValue
            ? $"/agents?tab=agents&agentId={technicalAgentId.Value:D}"
            : "/agents?tab=agents";
    }

    private static DateTimeOffset? ToUtcDate(DateOnly? value)
    {
        return value.HasValue
            ? new DateTimeOffset(value.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : null;
    }

    private static string ResolvePrimaryContactValue(IEnumerable<CrmPartyContactValue> contacts, PartyContactType contactType)
    {
        return contacts
            .Where(item => item.ContactType == contactType)
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => item.Value)
            .FirstOrDefault()
            ?? string.Empty;
    }

    private static string? NormalizeJson(string json, string fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return fallback;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed class ProjectPartyIntegrationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    PartyDirectoryService partyDirectoryService,
    ProjectPartyAssignmentNodePolicy projectPartyAssignmentNodePolicy,
    ProjectPartyAffiliationContextService
        projectPartyAffiliationContextService,
    IProjectWorkItemAssignmentMutationBridge
        workItemAssignmentMutationBridge,
    IClock clock) :
    IProjectPartyIntegrationBridge,
    IProjectPartyCostRateBridge
{
    private static readonly ProjectPartyAssignmentKind[] AllocationAssignmentKinds =
    [
        ProjectPartyAssignmentKind.TeamMember,
        ProjectPartyAssignmentKind.DeliveryUnit,
        ProjectPartyAssignmentKind.Manager,
        ProjectPartyAssignmentKind.AiAgent,
        ProjectPartyAssignmentKind.Reviewer,
        ProjectPartyAssignmentKind.WorkItemAssignee
    ];

    public async Task<IReadOnlyList<ProjectPartyAssignmentSummaryModel>> ListAssignmentsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return (await ListAssignmentsDetailedAsync(projectId, cancellationToken))
            .Select(item => new ProjectPartyAssignmentSummaryModel(
                item.Id,
                item.ProjectId,
                item.PartyId,
                MapRole(item.Role),
                item.NodeKey,
                item.IsPrimary))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, ProjectPortfolioPartyContext>> GetPortfolioContextsAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default)
    {
        if (projectIds.Count == 0)
        {
            return new Dictionary<Guid, ProjectPortfolioPartyContext>();
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var assignments = await dbContext.Set<ProjectPartyAssignment>()
            .Where(item => projectIds.Contains(item.ProjectId) && string.IsNullOrWhiteSpace(item.NodeKey))
            .Join(
                dbContext.Set<Party>(),
                assignment => assignment.PartyId,
                party => party.Id,
                (assignment, party) => new
                {
                    assignment.ProjectId,
                    assignment.AssignmentKind,
                    assignment.IsPrimary,
                    party.DisplayName
                })
            .OrderBy(item => item.ProjectId)
            .ThenByDescending(item => item.IsPrimary)
            .ThenBy(item => item.DisplayName)
            .ToListAsync(cancellationToken);

        return assignments
            .GroupBy(item => item.ProjectId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var items = group
                        .Select(item => new ProjectPortfolioPartyItem(
                            MapPortfolioCategory(item.AssignmentKind),
                            ResolvePortfolioLabel(item.AssignmentKind),
                            item.DisplayName,
                            item.IsPrimary))
                        .Distinct()
                        .ToList();

                    return new ProjectPortfolioPartyContext(
                        ResolvePrimaryDisplayName(items, ProjectPartyPortfolioCategory.Customer),
                        ResolvePrimaryDisplayName(items, ProjectPartyPortfolioCategory.DeliveryUnit),
                        ResolvePrimaryDisplayName(items, ProjectPartyPortfolioCategory.Owner),
                        items,
                        string.Join(
                            '\n',
                            items.Select(item => $"{item.Label}:{item.DisplayName}")
                                .Distinct(StringComparer.OrdinalIgnoreCase)));
                });
    }

    public async Task<IReadOnlyList<ProjectPartyOption>> ListPartyOptionsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var assignedPartyIds = await dbContext.Set<ProjectPartyAssignment>()
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId)
            .Select(item => item.PartyId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var parties = await dbContext.Set<Party>()
            .AsNoTracking()
            .Select(party => new
            {
                party.Id,
                party.DisplayName,
                party.PartyType,
                party.IsSensitive
            })
            .ToListAsync(cancellationToken);
        var partyIds = parties.Select(item => item.Id).ToList();
        var contacts = await dbContext.Set<PartyContactPoint>()
            .AsNoTracking()
            .Where(item => partyIds.Contains(item.PartyId) && item.IsPublic)
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => new CrmPartyContactValue(item.PartyId, item.ContactType, item.Value, item.IsPrimary))
            .ToListAsync(cancellationToken);
        var contactsByPartyId = contacts
            .GroupBy(item => item.PartyId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<CrmPartyContactValue>)group.ToList());
        var affiliationContexts =
            await projectPartyAffiliationContextService.LoadPartyContextsAsync(
                dbContext,
                parties.ToDictionary(
                    item => item.Id,
                    item => item.PartyType),
                cancellationToken);
        var assignedSet = assignedPartyIds.ToHashSet();

        return parties
            .OrderByDescending(item => assignedSet.Contains(item.Id))
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(item =>
            {
                var partyContacts = contactsByPartyId.GetValueOrDefault(item.Id) ?? [];
                return new ProjectPartyOption(
                    item.Id,
                    item.DisplayName,
                    ResolvePartyTypeLabel(item.PartyType),
                    MapProjectPartyType(item.PartyType),
                    item.IsSensitive
                        ? string.Empty
                        : ResolvePrimaryContactValue(partyContacts, PartyContactType.Email),
                    item.IsSensitive
                        ? string.Empty
                        : ResolvePrimaryContactValue(partyContacts, PartyContactType.Phone),
                    item.IsSensitive,
                    item.IsSensitive
                        ? null
                        : affiliationContexts.GetValueOrDefault(item.Id));
            })
            .ToList();
    }

    public async Task<ProjectPartyOption?> GetPartyOptionAsync(Guid partyId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var party = await dbContext.Set<Party>()
            .AsNoTracking()
            .Select(item => new
            {
                item.Id,
                item.DisplayName,
                item.PartyType,
                item.IsSensitive
            })
            .SingleOrDefaultAsync(item => item.Id == partyId, cancellationToken);
        if (party is null)
        {
            return null;
        }

        var contacts = await dbContext.Set<PartyContactPoint>()
            .AsNoTracking()
            .Where(item => item.PartyId == partyId && item.IsPublic)
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => new CrmPartyContactValue(item.PartyId, item.ContactType, item.Value, item.IsPrimary))
            .ToListAsync(cancellationToken);
        var affiliationContexts =
            await projectPartyAffiliationContextService.LoadPartyContextsAsync(
                dbContext,
                new Dictionary<Guid, PartyType>
                {
                    [party.Id] = party.PartyType
                },
                cancellationToken);

        return new ProjectPartyOption(
            party.Id,
            party.DisplayName,
            ResolvePartyTypeLabel(party.PartyType),
            MapProjectPartyType(party.PartyType),
            party.IsSensitive
                ? string.Empty
                : ResolvePrimaryContactValue(contacts, PartyContactType.Email),
            party.IsSensitive
                ? string.Empty
                : ResolvePrimaryContactValue(contacts, PartyContactType.Phone),
            party.IsSensitive,
            party.IsSensitive
                ? null
                : affiliationContexts.GetValueOrDefault(party.Id));
    }

    public async Task<ProjectPartyCostRate?> GetInternalCostRateAsync(
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        if (partyId == Guid.Empty)
        {
            return null;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rate = await dbContext.Set<WorkforceProfile>()
            .AsNoTracking()
            .Where(profile => profile.PartyId == partyId && profile.InternalCostRate.HasValue)
            .Select(profile => new
            {
                profile.PartyId,
                Rate = profile.InternalCostRate!.Value,
                Unit = profile.RateUnit,
                CurrencyCode = profile.RateCurrencyCode
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (rate is null)
        {
            return null;
        }

        return new ProjectPartyCostRate(
            rate.PartyId,
            rate.Rate,
            rate.Unit,
            rate.CurrencyCode);
    }

    public Task<IReadOnlyList<ProjectPartyAssignmentDetail>> ListAssignmentsDetailedAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return ListAssignmentsDetailedCoreAsync(
            projectId,
            Enum.GetValues<ProjectPartyAssignmentRole>(),
            orderResults: true,
            cancellationToken);
    }

    public async Task<ProjectPartyAssignmentCounts> GetAssignmentCountsAsync(
        Guid projectId,
        DateTimeOffset scheduleWindowStartUtc,
        DateTimeOffset scheduleWindowEndUtc,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            return ProjectPartyAssignmentCounts.Empty;
        }

        if (scheduleWindowStartUtc > scheduleWindowEndUtc)
        {
            throw new ArgumentException(
                "The assignment schedule window start cannot be after its end.",
                nameof(scheduleWindowStartUtc));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var summary = await dbContext.Set<ProjectPartyAssignment>()
            .AsNoTracking()
            .Where(assignment => assignment.ProjectId == projectId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalCount = group.Count(),
                AllocationCount = group.Count(assignment =>
                    assignment.AllocationPercent.HasValue ||
                    AllocationAssignmentKinds.Contains(assignment.AssignmentKind)),
                ScheduledCount = group.Count(assignment =>
                    (assignment.AllocationPercent.HasValue ||
                     AllocationAssignmentKinds.Contains(assignment.AssignmentKind)) &&
                    (!assignment.EndsAtUtc.HasValue ||
                     assignment.EndsAtUtc.Value >= scheduleWindowStartUtc) &&
                    (!assignment.StartsAtUtc.HasValue ||
                     assignment.StartsAtUtc.Value <= scheduleWindowEndUtc))
            })
            .SingleOrDefaultAsync(cancellationToken);

        return summary is null
            ? ProjectPartyAssignmentCounts.Empty
            : new(
                summary.TotalCount,
                summary.AllocationCount,
                summary.ScheduledCount);
    }

    public async Task<ProjectPartyAssignmentPage> SearchAssignmentsDetailedAsync(
        ProjectPartyAssignmentQuery request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = Normalize(request);
        if (query.ProjectId == Guid.Empty || query.Roles.Count == 0)
        {
            return ProjectPartyAssignmentPage.Empty(query.PageSize);
        }

        var assignmentKinds = query.Roles
            .Select(MapRole)
            .Distinct()
            .ToArray();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var candidates =
            from assignment in dbContext.Set<ProjectPartyAssignment>().AsNoTracking()
            join party in dbContext.Set<Party>().AsNoTracking()
                on assignment.PartyId equals party.Id
            where assignment.ProjectId == query.ProjectId &&
                  assignmentKinds.Contains(assignment.AssignmentKind)
            select new
            {
                assignment.Id,
                assignment.ProjectId,
                assignment.PartyId,
                assignment.PartyOrganizationAffiliationId,
                assignment.AssignmentKind,
                assignment.NodeKey,
                assignment.IsPrimary,
                assignment.AllocationPercent,
                assignment.StartsAtUtc,
                assignment.EndsAtUtc,
                assignment.Source,
                assignment.Notes,
                party.DisplayName,
                party.PartyType,
                party.IsSensitive
            };

        if (query.AllocationOnly)
        {
            candidates = candidates.Where(item =>
                item.AllocationPercent.HasValue ||
                AllocationAssignmentKinds.Contains(item.AssignmentKind));
        }

        if (query.WindowStartUtc.HasValue)
        {
            var windowStartUtc = query.WindowStartUtc.Value;
            candidates = candidates.Where(item =>
                !item.EndsAtUtc.HasValue ||
                item.EndsAtUtc.Value >= windowStartUtc);
        }

        if (query.WindowEndUtc.HasValue)
        {
            var windowEndUtc = query.WindowEndUtc.Value;
            candidates = candidates.Where(item =>
                !item.StartsAtUtc.HasValue ||
                item.StartsAtUtc.Value <= windowEndUtc);
        }

        if (!string.IsNullOrEmpty(query.SearchText))
        {
            var search = query.SearchText.ToUpperInvariant();
            candidates = candidates.Where(item =>
                item.DisplayName.ToUpper().Contains(search) ||
                item.NodeKey.ToUpper().Contains(search) ||
                item.Source.ToUpper().Contains(search) ||
                item.Notes.ToUpper().Contains(search));
        }

        var totalCount = await candidates.CountAsync(cancellationToken);
        var pageRows = await candidates
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.StartsAtUtc)
            .ThenBy(item => item.DisplayName)
            .ThenBy(item => item.Id)
            .Skip(query.PageIndex * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var affiliationContexts = await projectPartyAffiliationContextService
            .LoadAssignmentContextsAsync(
                dbContext,
                pageRows
                    .Select(item => new ProjectPartyAffiliationReference(
                        item.Id,
                        item.PartyId,
                        item.PartyType,
                        item.PartyOrganizationAffiliationId,
                        item.IsSensitive))
                    .ToArray(),
                cancellationToken);
        var items = pageRows
            .Select(item => new ProjectPartyAssignmentDetail(
                item.Id,
                item.ProjectId,
                item.PartyId,
                MapRole(item.AssignmentKind),
                item.DisplayName,
                ResolvePartyTypeLabel(item.PartyType),
                MapProjectPartyType(item.PartyType),
                item.NodeKey,
                item.IsPrimary,
                item.AllocationPercent,
                item.StartsAtUtc,
                item.EndsAtUtc,
                item.Source,
                item.Notes,
                affiliationContexts.GetValueOrDefault(item.Id),
                item.PartyOrganizationAffiliationId))
            .ToList();
        return new(
            items,
            query.PageIndex,
            query.PageSize,
            totalCount);
    }

    private static ProjectPartyAssignmentQuery Normalize(ProjectPartyAssignmentQuery request)
    {
        ArgumentNullException.ThrowIfNull(request.Roles);
        if (request.PageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.PageIndex,
                "The assignment page index cannot be negative.");
        }

        if (request.PageSize is < 1 or > ProjectPartyAssignmentQueryLimits.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.PageSize,
                $"The assignment page size must be between 1 and {ProjectPartyAssignmentQueryLimits.MaximumPageSize}.");
        }

        if (request.PageIndex > int.MaxValue / request.PageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.PageIndex,
                "The assignment page offset is too large.");
        }

        if (request.WindowStartUtc > request.WindowEndUtc)
        {
            throw new ArgumentException(
                "The assignment schedule window start cannot be after its end.",
                nameof(request));
        }

        var searchText = request.SearchText?.Trim() ?? string.Empty;
        if (searchText.Length > ProjectPartyAssignmentQueryLimits.MaximumSearchLength)
        {
            throw new ArgumentException(
                $"Assignment search cannot exceed {ProjectPartyAssignmentQueryLimits.MaximumSearchLength} characters.",
                nameof(request));
        }

        var roles = request.Roles
            .Distinct()
            .ToArray();
        if (roles.Any(role => !Enum.IsDefined(role)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.Roles,
                "The assignment query contains an unsupported role.");
        }

        return request with
        {
            Roles = roles,
            SearchText = searchText
        };
    }

    public Task<IReadOnlyList<ProjectPartyAssignmentDetail>> ListAssignmentsDetailedAsync(
        Guid projectId,
        IReadOnlyCollection<ProjectPartyAssignmentRole> roles,
        CancellationToken cancellationToken = default)
    {
        return ListAssignmentsDetailedCoreAsync(
            projectId,
            roles,
            orderResults: false,
            cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectWorkItemAssigneeBinding>> ListWorkItemAssigneeBindingsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<ProjectPartyAssignment>()
            .Where(item =>
                item.ProjectId == projectId &&
                item.AssignmentKind == ProjectPartyAssignmentKind.WorkItemAssignee)
            .Join(
                dbContext.Set<Party>(),
                assignment => assignment.PartyId,
                party => party.Id,
                (assignment, party) => new ProjectWorkItemAssigneeBinding(
                    assignment.ProjectId,
                    assignment.NodeKey,
                    assignment.PartyId,
                    MapProjectPartyType(party.PartyType)))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectWorkItemAssigneeBinding>> ListWorkItemAssigneeBindingsAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectIds);
        var distinctProjectIds = projectIds.Distinct().ToArray();
        if (distinctProjectIds.Length == 0)
        {
            return [];
        }

        if (distinctProjectIds.Any(static projectId => projectId == Guid.Empty))
        {
            throw new ArgumentException(
                "Project identifiers cannot contain an empty value.",
                nameof(projectIds));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<ProjectPartyAssignment>()
            .Where(item =>
                distinctProjectIds.Contains(item.ProjectId) &&
                item.AssignmentKind == ProjectPartyAssignmentKind.WorkItemAssignee)
            .Join(
                dbContext.Set<Party>(),
                assignment => assignment.PartyId,
                party => party.Id,
                (assignment, party) => new ProjectWorkItemAssigneeBinding(
                    assignment.ProjectId,
                    assignment.NodeKey,
                    assignment.PartyId,
                    MapProjectPartyType(party.PartyType)))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ProjectPartyAssignmentDetail>> ListAssignmentsDetailedCoreAsync(
        Guid projectId,
        IReadOnlyCollection<ProjectPartyAssignmentRole> roles,
        bool orderResults,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(roles);
        if (roles.Count == 0)
        {
            return [];
        }

        var assignmentKinds = roles.Select(MapRole).Distinct().ToArray();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = dbContext.Set<ProjectPartyAssignment>()
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId && assignmentKinds.Contains(item.AssignmentKind))
            .Join(
                dbContext.Set<Party>().AsNoTracking(),
                assignment => assignment.PartyId,
                party => party.Id,
                (assignment, party) => new
                {
                    assignment.Id,
                    assignment.ProjectId,
                    assignment.PartyId,
                    assignment.PartyOrganizationAffiliationId,
                    assignment.AssignmentKind,
                    assignment.NodeKey,
                    assignment.IsPrimary,
                    assignment.AllocationPercent,
                    assignment.StartsAtUtc,
                    assignment.EndsAtUtc,
                    assignment.Source,
                    assignment.Notes,
                    party.DisplayName,
                    party.PartyType,
                    party.IsSensitive
                });
        if (orderResults)
        {
            query = query
                .OrderBy(item => item.NodeKey)
                .ThenBy(item => item.AssignmentKind)
                .ThenByDescending(item => item.IsPrimary)
                .ThenBy(item => item.DisplayName);
        }

        var rows = await query.ToListAsync(cancellationToken);
        var affiliationContexts = await projectPartyAffiliationContextService
            .LoadAssignmentContextsAsync(
                dbContext,
                rows
                    .Select(item => new ProjectPartyAffiliationReference(
                        item.Id,
                        item.PartyId,
                        item.PartyType,
                        item.PartyOrganizationAffiliationId,
                        item.IsSensitive))
                    .ToArray(),
                cancellationToken);
        return rows
            .Select(item => new ProjectPartyAssignmentDetail(
                item.Id,
                item.ProjectId,
                item.PartyId,
                MapRole(item.AssignmentKind),
                item.DisplayName,
                ResolvePartyTypeLabel(item.PartyType),
                MapProjectPartyType(item.PartyType),
                item.NodeKey,
                item.IsPrimary,
                item.AllocationPercent,
                item.StartsAtUtc,
                item.EndsAtUtc,
                item.Source,
                item.Notes,
                affiliationContexts.GetValueOrDefault(item.Id),
                item.PartyOrganizationAffiliationId))
            .ToList();
    }

    public async Task<Result<Guid>> SaveAssignmentAsync(
        ProjectPartyAssignmentUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ProjectId == Guid.Empty)
        {
            return Result<Guid>.Failure(Error.Validation("Project is required.", "crmhr.project-assignment.project-required"));
        }

        if (request.PartyId == Guid.Empty)
        {
            return Result<Guid>.Failure(Error.Validation("Party is required.", "crmhr.project-assignment.party-required"));
        }

        var valueError = ProjectPartyAssignmentInvariantPolicy.ValidateValues(request);
        if (valueError is not null)
        {
            return Result<Guid>.Failure(valueError);
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await SerializableMutationScope.BeginAsync(
            dbContext,
            $"project:{request.ProjectId:D}",
            cancellationToken);
        var projectExists = await dbContext.Set<Project>()
            .AnyAsync(item => item.Id == request.ProjectId, cancellationToken);
        if (!projectExists)
        {
            return Result<Guid>.Failure(Error.Validation("Project was not found.", "crmhr.project-assignment.project-not-found"));
        }

        var partyType = await dbContext.Set<Party>()
            .Where(item => item.Id == request.PartyId)
            .Select(item => (PartyType?)item.PartyType)
            .SingleOrDefaultAsync(cancellationToken);
        if (!partyType.HasValue)
        {
            return Result<Guid>.Failure(Error.Validation("Party was not found.", "crmhr.project-assignment.party-not-found"));
        }

        var partyTypeError = ProjectPartyAssignmentInvariantPolicy.ValidatePartyType(request.Role, partyType.Value);
        if (partyTypeError is not null)
        {
            return Result<Guid>.Failure(partyTypeError);
        }

        var affiliationError =
            await projectPartyAffiliationContextService.ValidateAsync(
                dbContext,
                request.PartyId,
                request.PartyAffiliationId,
                ToUtcDate(request.StartsOn),
                ToUtcDate(request.EndsOn),
                cancellationToken);
        if (affiliationError is not null)
        {
            return Result<Guid>.Failure(affiliationError);
        }

        var normalizedNodeKey = request.NodeKey?.Trim() ?? string.Empty;
        var (nodeScope, nodeScopeError) = await projectPartyAssignmentNodePolicy.ResolveScopeAsync(
            request.ProjectId,
            normalizedNodeKey,
            [request.Role],
            allowUnresolvedNamedScope: true,
            cancellationToken);
        if (nodeScopeError is not null)
        {
            return Result<Guid>.Failure(nodeScopeError);
        }
        var roleError = projectPartyAssignmentNodePolicy.ValidateRole(request.Role, nodeScope);
        if (roleError is not null)
        {
            return Result<Guid>.Failure(roleError);
        }

        var assignmentKind = MapRole(request.Role);
        var entity = request.AssignmentId.HasValue
            ? await dbContext.Set<ProjectPartyAssignment>()
                .SingleOrDefaultAsync(item => item.Id == request.AssignmentId.Value, cancellationToken)
            : null;
        if (entity is null)
        {
            entity = await dbContext.Set<ProjectPartyAssignment>()
                .SingleOrDefaultAsync(item =>
                    item.ProjectId == request.ProjectId &&
                    item.PartyId == request.PartyId &&
                    item.AssignmentKind == assignmentKind &&
                    item.NodeKey == normalizedNodeKey,
                cancellationToken);
        }

        if (entity is not null && entity.ProjectId != request.ProjectId)
        {
            return Result<Guid>.Failure(Error.Validation(
                "The assignment does not belong to the requested project.",
                "crmhr.project-assignment.project-mismatch"));
        }

        var affectedTaskNodeKeys = new HashSet<string>(StringComparer.Ordinal);
        if (entity?.AssignmentKind ==
            ProjectPartyAssignmentKind.WorkItemAssignee)
        {
            affectedTaskNodeKeys.Add(entity.NodeKey);
        }

        if (assignmentKind == ProjectPartyAssignmentKind.WorkItemAssignee)
        {
            affectedTaskNodeKeys.Add(normalizedNodeKey);
        }

        if (affectedTaskNodeKeys.Count > 0)
        {
            await dbContext.Set<ProjectPartyAssignment>()
                .Where(item =>
                    item.ProjectId == request.ProjectId &&
                    affectedTaskNodeKeys.Contains(item.NodeKey) &&
                    item.AssignmentKind ==
                    ProjectPartyAssignmentKind.WorkItemAssignee)
                .LoadAsync(cancellationToken);
        }

        if (entity is null)
        {
            entity = new ProjectPartyAssignment();
            dbContext.Set<ProjectPartyAssignment>().Add(entity);
        }

        entity.ProjectId = request.ProjectId;
        entity.PartyId = request.PartyId;
        entity.PartyOrganizationAffiliationId =
            request.PartyAffiliationId;
        entity.AssignmentKind = assignmentKind;
        entity.NodeKey = normalizedNodeKey;
        entity.IsPrimary = request.IsPrimary;
        entity.AllocationPercent = request.AllocationPercent;
        entity.StartsAtUtc = ToUtcDate(request.StartsOn);
        entity.EndsAtUtc = ToUtcDate(request.EndsOn);
        entity.Source = string.IsNullOrWhiteSpace(request.Source) ? "crm-hr-ui" : request.Source.Trim();
        entity.Notes = request.Notes?.Trim() ?? string.Empty;

        if (entity.IsPrimary)
        {
            var primaryAssignments = await dbContext.Set<ProjectPartyAssignment>()
                .Where(item =>
                    item.ProjectId == request.ProjectId &&
                    item.AssignmentKind == assignmentKind &&
                    item.NodeKey == normalizedNodeKey &&
                    item.Id != entity.Id)
                .ToListAsync(cancellationToken);
            foreach (var primaryAssignment in primaryAssignments)
            {
                primaryAssignment.IsPrimary = false;
            }
        }

        foreach (var affectedTaskNodeKey in affectedTaskNodeKeys)
        {
            await StageTaskAssignmentRevisionAsync(
                dbContext,
                request.ProjectId,
                affectedTaskNodeKey,
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await mutationScope.CommitAsync(cancellationToken);
        return Result<Guid>.Success(entity.Id);
    }

    public Task<Result> ReplaceProjectAssignmentsAsync(
        Guid projectId,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
        CancellationToken cancellationToken = default)
        => ReplaceAssignmentsCoreAsync(
            projectId,
            string.Empty,
            desiredAssignments,
            targetRoles,
            expectedAssignments: null,
            expectedDirectAssignmentRevision: null,
            cancellationToken);

    public Task<Result> ReplaceNodeAssignmentsAsync(
        Guid projectId,
        ProjectNodeReference nodeReference,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
        CancellationToken cancellationToken = default)
        => ReplaceAssignmentsCoreAsync(
            projectId,
            nodeReference.NodeKey,
            desiredAssignments,
            targetRoles,
            expectedAssignments: null,
            expectedDirectAssignmentRevision: null,
            cancellationToken);

    public Task<Result> ReplaceNodeAssignmentsIfCurrentAsync(
        Guid projectId,
        ProjectNodeReference nodeReference,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
        IReadOnlyCollection<ProjectPartyAssignmentConcurrencySnapshot>
            expectedAssignments,
        ProjectWorkItemDirectAssignmentRevision?
            expectedDirectAssignmentRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedAssignments);
        return ReplaceAssignmentsCoreAsync(
            projectId,
            nodeReference.NodeKey,
            desiredAssignments,
            targetRoles,
            expectedAssignments,
            expectedDirectAssignmentRevision,
            cancellationToken);
    }

    private async Task<Result> ReplaceAssignmentsCoreAsync(
        Guid projectId,
        string normalizedNodeKey,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
        IReadOnlyCollection<ProjectPartyAssignmentConcurrencySnapshot>?
            expectedAssignments,
        ProjectWorkItemDirectAssignmentRevision?
            expectedDirectAssignmentRevision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(desiredAssignments);
        ArgumentNullException.ThrowIfNull(targetRoles);

        if (projectId == Guid.Empty)
        {
            return Result.Failure(Error.Validation("Project is required.", "crmhr.project-assignment.project-required"));
        }

        var targetRoleSet = targetRoles
            .Distinct()
            .ToHashSet();
        if (targetRoleSet.Count == 0)
        {
            return desiredAssignments.Count == 0
                ? Result.Success()
                : Result.Failure(Error.Validation(
                    "At least one assignment role must be supplied.",
                    "crmhr.project-assignment.target-roles-required"));
        }

        if (desiredAssignments.Any(item => !targetRoleSet.Contains(item.Role)))
        {
            return Result.Failure(Error.Validation(
                "Desired assignments included a role outside the replacement scope.",
                "crmhr.project-assignment.target-role-mismatch"));
        }

        if (expectedDirectAssignmentRevision.HasValue &&
            !targetRoleSet.Contains(
                ProjectPartyAssignmentRole.WorkItemAssignee))
        {
            return Result.Failure(Error.Validation(
                "A direct-assignment revision can only guard work-item assignee replacement.",
                "crmhr.project-assignment.revision-role-mismatch"));
        }

        foreach (var desiredAssignment in desiredAssignments)
        {
            var valueError = ProjectPartyAssignmentInvariantPolicy.ValidateValues(desiredAssignment);
            if (valueError is not null)
            {
                return Result.Failure(valueError);
            }
        }

        var duplicateAssignment = desiredAssignments
            .GroupBy(item => (item.PartyId, item.Role))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateAssignment is not null)
        {
            return Result.Failure(Error.Validation(
                "Desired assignments contain the same party and role more than once.",
                "crmhr.project-assignment.duplicate"));
        }

        var (nodeScope, nodeScopeError) = await projectPartyAssignmentNodePolicy.ResolveScopeAsync(
            projectId,
            normalizedNodeKey,
            targetRoleSet.ToList(),
            allowUnresolvedNamedScope: false,
            cancellationToken);
        if (nodeScopeError is not null)
        {
            return Result.Failure(nodeScopeError);
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await SerializableMutationScope.BeginAsync(
                dbContext,
                $"project:{projectId:D}",
                cancellationToken);
        var projectExists = await dbContext.Set<Project>()
            .AnyAsync(item => item.Id == projectId, cancellationToken);
        if (!projectExists)
        {
            return Result.Failure(Error.Validation("Project was not found.", "crmhr.project-assignment.project-not-found"));
        }

        var desiredPartyIds = desiredAssignments
            .Select(item => item.PartyId)
            .Distinct()
            .ToList();
        if (desiredPartyIds.Any(id => id == Guid.Empty))
        {
            return Result.Failure(Error.Validation("Party is required.", "crmhr.project-assignment.party-required"));
        }

        if (desiredPartyIds.Count > 0)
        {
            var existingParties = await dbContext.Set<Party>()
                .Where(item => desiredPartyIds.Contains(item.Id))
                .Select(item => new { item.Id, item.PartyType })
                .ToListAsync(cancellationToken);
            var existingPartyIdSet = existingParties.Select(item => item.Id).ToHashSet();
            if (desiredPartyIds.Any(id => !existingPartyIdSet.Contains(id)))
            {
                return Result.Failure(Error.Validation("Party was not found.", "crmhr.project-assignment.party-not-found"));
            }

            var partyTypesById = existingParties.ToDictionary(item => item.Id, item => item.PartyType);
            foreach (var desiredAssignment in desiredAssignments)
            {
                var partyTypeError = ProjectPartyAssignmentInvariantPolicy.ValidatePartyType(
                    desiredAssignment.Role,
                    partyTypesById[desiredAssignment.PartyId]);
                if (partyTypeError is not null)
                {
                    return Result.Failure(partyTypeError);
                }
            }
        }

        var affiliationError =
            await projectPartyAffiliationContextService.ValidateAsync(
                dbContext,
                desiredAssignments
                    .Select(item =>
                        new ProjectPartyAffiliationValidation(
                            item.PartyId,
                            item.PartyAffiliationId,
                            ToUtcDate(item.StartsOn),
                            ToUtcDate(item.EndsOn)))
                    .ToArray(),
                cancellationToken);
        if (affiliationError is not null)
        {
            return Result.Failure(affiliationError);
        }

        foreach (var desiredAssignment in desiredAssignments)
        {
            if (desiredAssignment.ProjectId != Guid.Empty && desiredAssignment.ProjectId != projectId)
            {
                return Result.Failure(Error.Validation(
                    "Desired assignments must target the same project.",
                    "crmhr.project-assignment.project-mismatch"));
            }

            var assignmentNodeKey = desiredAssignment.NodeKey?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(assignmentNodeKey) &&
                !string.Equals(assignmentNodeKey, normalizedNodeKey, StringComparison.Ordinal))
            {
                return Result.Failure(Error.Validation(
                    "Desired assignments must target the same node.",
                    "crmhr.project-assignment.node-mismatch"));
            }

            var roleError = projectPartyAssignmentNodePolicy.ValidateRole(desiredAssignment.Role, nodeScope);
            if (roleError is not null)
            {
                return Result.Failure(roleError);
            }
        }

        var targetAssignmentKinds = targetRoleSet
            .Select(MapRole)
            .Distinct()
            .ToList();
        var existingAssignments = await dbContext.Set<ProjectPartyAssignment>()
            .Where(item =>
                item.ProjectId == projectId &&
                item.NodeKey == normalizedNodeKey &&
                targetAssignmentKinds.Contains(item.AssignmentKind))
            .ToListAsync(cancellationToken);
        if (expectedAssignments is not null)
        {
            var currentPartyIds = existingAssignments
                .Select(static assignment => assignment.PartyId)
                .Distinct()
                .ToArray();
            var currentPartyTypes = await dbContext.Set<Party>()
                .Where(party => currentPartyIds.Contains(party.Id))
                .ToDictionaryAsync(
                    party => party.Id,
                    party => party.PartyType,
                    cancellationToken);
            var currentSnapshots = existingAssignments
                .Where(assignment =>
                    currentPartyTypes.ContainsKey(assignment.PartyId))
                .Select(assignment =>
                    new ProjectPartyAssignmentConcurrencySnapshot(
                        assignment.Id,
                        assignment.PartyId,
                        MapProjectPartyType(
                            currentPartyTypes[assignment.PartyId]),
                        assignment.IsPrimary,
                        assignment.PartyOrganizationAffiliationId))
                .ToHashSet();
            if (currentSnapshots.Count != existingAssignments.Count ||
                !currentSnapshots.SetEquals(expectedAssignments))
            {
                return Result.Failure(Error.Failure(
                    "Project assignments changed before the requested replacement could be applied.",
                    ProjectPartyIntegrationErrorCodes.StaleAssignmentSnapshot));
            }
        }

        if (existingAssignments.Count > 0)
        {
            dbContext.RemoveRange(existingAssignments);
        }

        var desiredAssignmentItems = desiredAssignments
            .Select((request, index) => new
            {
                Request = request,
                Index = index,
                AssignmentKind = MapRole(request.Role)
            })
            .ToList();
        var explicitPrimaryKinds = desiredAssignmentItems
            .Where(item => item.Request.IsPrimary)
            .Select(item => item.AssignmentKind)
            .ToHashSet();
        var emittedPrimaryKinds = new HashSet<ProjectPartyAssignmentKind>();

        foreach (var desiredAssignment in desiredAssignmentItems.OrderBy(item => item.Index))
        {
            var isPrimary = desiredAssignment.Request.IsPrimary;
            if (emittedPrimaryKinds.Contains(desiredAssignment.AssignmentKind))
            {
                isPrimary = false;
            }
            else if (!explicitPrimaryKinds.Contains(desiredAssignment.AssignmentKind))
            {
                isPrimary = true;
            }

            if (isPrimary)
            {
                emittedPrimaryKinds.Add(desiredAssignment.AssignmentKind);
            }

            var requestedAssignmentId =
                desiredAssignment.Request.AssignmentId;
            dbContext.Set<ProjectPartyAssignment>().Add(new ProjectPartyAssignment
            {
                Id = requestedAssignmentId.HasValue &&
                    existingAssignments.All(existing =>
                        existing.Id != requestedAssignmentId.Value)
                        ? requestedAssignmentId.Value
                        : Guid.NewGuid(),
                ProjectId = projectId,
                PartyId = desiredAssignment.Request.PartyId,
                PartyOrganizationAffiliationId =
                    desiredAssignment.Request.PartyAffiliationId,
                AssignmentKind = desiredAssignment.AssignmentKind,
                NodeKey = normalizedNodeKey,
                IsPrimary = isPrimary,
                AllocationPercent = desiredAssignment.Request.AllocationPercent,
                StartsAtUtc = ToUtcDate(desiredAssignment.Request.StartsOn),
                EndsAtUtc = ToUtcDate(desiredAssignment.Request.EndsOn),
                Source = string.IsNullOrWhiteSpace(desiredAssignment.Request.Source) ? "crm-hr-ui" : desiredAssignment.Request.Source.Trim(),
                Notes = desiredAssignment.Request.Notes?.Trim() ?? string.Empty
            });
        }

        if (targetAssignmentKinds.Contains(
                ProjectPartyAssignmentKind.WorkItemAssignee))
        {
            var mutationResult =
                await StageTaskAssignmentRevisionAsync(
                    dbContext,
                    projectId,
                    normalizedNodeKey,
                    cancellationToken,
                    expectedDirectAssignmentRevision);
            if (mutationResult.Status !=
                ProjectWorkItemDirectAssignmentMutationStatus.Applied)
            {
                return Result.Failure(Error.Failure(
                    "Project assignments changed before the requested replacement could be applied.",
                    ProjectPartyIntegrationErrorCodes
                        .StaleAssignmentSnapshot));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await mutationScope.CommitAsync(cancellationToken);

        return Result.Success();
    }

    public async Task DeleteAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var assignmentScope = await dbContext.Set<ProjectPartyAssignment>()
            .AsNoTracking()
            .Where(item => item.Id == assignmentId)
            .Select(item => new
            {
                item.ProjectId,
                item.NodeKey,
                item.AssignmentKind
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (assignmentScope is null)
        {
            return;
        }

        await using var mutationScope = await SerializableMutationScope.BeginAsync(
            dbContext,
            $"project:{assignmentScope.ProjectId:D}",
            cancellationToken);
        var entity = await dbContext.Set<ProjectPartyAssignment>()
            .SingleOrDefaultAsync(item => item.Id == assignmentId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        if (entity.ProjectId != assignmentScope.ProjectId)
        {
            throw new InvalidOperationException(
                "The assignment project changed while it was being deleted.");
        }

        if (entity.AssignmentKind ==
            ProjectPartyAssignmentKind.WorkItemAssignee)
        {
            await dbContext.Set<ProjectPartyAssignment>()
                .Where(item =>
                    item.ProjectId == entity.ProjectId &&
                    item.NodeKey == entity.NodeKey &&
                    item.AssignmentKind ==
                    ProjectPartyAssignmentKind.WorkItemAssignee)
                .LoadAsync(cancellationToken);
        }

        dbContext.Set<ProjectPartyAssignment>().Remove(entity);
        if (entity.AssignmentKind ==
            ProjectPartyAssignmentKind.WorkItemAssignee)
        {
            await StageTaskAssignmentRevisionAsync(
                dbContext,
                entity.ProjectId,
                entity.NodeKey,
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await mutationScope.CommitAsync(cancellationToken);
    }

    public async Task DeleteAssignmentsForNodesAsync(
        Guid projectId,
        IReadOnlyCollection<ProjectNodeReference> nodeReferences,
        CancellationToken cancellationToken = default)
    {
        var normalizedNodeKeys = NormalizeNodeKeys(nodeReferences);
        if (projectId == Guid.Empty || normalizedNodeKeys.Count == 0)
        {
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await SerializableMutationScope.BeginAsync(
            dbContext,
            $"project:{projectId:D}",
            cancellationToken);
        var assignments = await dbContext.Set<ProjectPartyAssignment>()
            .Where(item => item.ProjectId == projectId && normalizedNodeKeys.Contains(item.NodeKey))
            .ToListAsync(cancellationToken);
        if (assignments.Count == 0)
        {
            return;
        }

        var affectedTaskNodeKeys = assignments
            .Where(static assignment =>
                assignment.AssignmentKind ==
                ProjectPartyAssignmentKind.WorkItemAssignee)
            .Select(static assignment => assignment.NodeKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        dbContext.RemoveRange(assignments);
        foreach (var affectedTaskNodeKey in affectedTaskNodeKeys)
        {
            await StageTaskAssignmentRevisionAsync(
                dbContext,
                projectId,
                affectedTaskNodeKey,
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await mutationScope.CommitAsync(cancellationToken);
    }

    public async Task DeleteAssignmentsForProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project identifier is required.", nameof(projectId));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await SerializableMutationScope.BeginAsync(
            dbContext,
            ProjectMutationScopeKeys.ForProject(projectId),
            cancellationToken);
        var assignments = await dbContext.Set<ProjectPartyAssignment>()
            .Where(item => item.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        if (assignments.Count > 0)
        {
            dbContext.RemoveRange(assignments);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await mutationScope.CommitAsync(cancellationToken);
    }

    public async Task MoveAssignmentsToProjectAsync(
        ProjectPartyAssignmentMoveOperationId operationId,
        Guid sourceProjectId,
        IReadOnlyCollection<ProjectNodeReference> nodeReferences,
        Guid targetProjectId,
        CancellationToken cancellationToken = default)
    {
        if (operationId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "A project-party assignment move operation identifier is required.",
                nameof(operationId));
        }

        var normalizedNodeKeys = NormalizeNodeKeys(nodeReferences);
        var nodeSetFingerprint = BuildNodeSetFingerprint(normalizedNodeKeys);
        if (sourceProjectId == Guid.Empty ||
            targetProjectId == Guid.Empty ||
            sourceProjectId == targetProjectId ||
            normalizedNodeKeys.Count == 0)
        {
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await SerializableMutationScope.BeginAsync(
            dbContext,
            new[]
            {
                $"crmhr:project-assignment-move:{operationId.Value:D}",
                $"project:{sourceProjectId:D}",
                $"project:{targetProjectId:D}"
            },
            cancellationToken);
        var existingReceipt = await dbContext
            .Set<ProjectPartyAssignmentMoveReceipt>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.OperationId == operationId.Value,
                cancellationToken);
        if (existingReceipt is not null)
        {
            if (existingReceipt.SourceProjectId != sourceProjectId ||
                existingReceipt.TargetProjectId != targetProjectId ||
                !string.Equals(
                    existingReceipt.NodeSetFingerprint,
                    nodeSetFingerprint,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The project-party assignment move operation identifier was already used for a different request.");
            }

            await mutationScope.CommitAsync(cancellationToken);
            return;
        }

        var targetProjectExists = await dbContext.Set<Project>()
            .AnyAsync(item => item.Id == targetProjectId, cancellationToken);
        if (!targetProjectExists)
        {
            throw new InvalidOperationException($"Target project '{targetProjectId}' was not found for assignment transfer.");
        }

        var staleTargetAssignments = await dbContext.Set<ProjectPartyAssignment>()
            .Where(item => item.ProjectId == targetProjectId && normalizedNodeKeys.Contains(item.NodeKey))
            .ToListAsync(cancellationToken);
        if (staleTargetAssignments.Count > 0)
        {
            dbContext.RemoveRange(staleTargetAssignments);
        }

        var assignmentsToMove = await dbContext.Set<ProjectPartyAssignment>()
            .Where(item => item.ProjectId == sourceProjectId && normalizedNodeKeys.Contains(item.NodeKey))
            .ToListAsync(cancellationToken);
        foreach (var assignment in assignmentsToMove)
        {
            assignment.ProjectId = targetProjectId;
        }

        var affectedTaskNodeKeys = staleTargetAssignments
            .Concat(assignmentsToMove)
            .Where(static assignment =>
                assignment.AssignmentKind ==
                ProjectPartyAssignmentKind.WorkItemAssignee)
            .Select(static assignment => assignment.NodeKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (var affectedTaskNodeKey in affectedTaskNodeKeys)
        {
            await StageTaskAssignmentRevisionAsync(
                dbContext,
                sourceProjectId,
                affectedTaskNodeKey,
                cancellationToken);
            await StageTaskAssignmentRevisionAsync(
                dbContext,
                targetProjectId,
                affectedTaskNodeKey,
                cancellationToken);
        }

        dbContext.Set<ProjectPartyAssignmentMoveReceipt>().Add(
            new ProjectPartyAssignmentMoveReceipt
            {
                OperationId = operationId.Value,
                SourceProjectId = sourceProjectId,
                TargetProjectId = targetProjectId,
                NodeSetFingerprint = nodeSetFingerprint,
                CompletedAtUtc = clock.GetUtcNow()
            });

        await dbContext.SaveChangesAsync(cancellationToken);
        await mutationScope.CommitAsync(cancellationToken);
    }

    private static string BuildNodeSetFingerprint(
        IReadOnlyCollection<string> normalizedNodeKeys)
    {
        var canonicalPayload = JsonSerializer.Serialize(
            normalizedNodeKeys.Order(StringComparer.Ordinal));
        return Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload)));
    }

    private async Task<ProjectWorkItemDirectAssignmentMutationResult>
        StageTaskAssignmentRevisionAsync(
        AppDbContext dbContext,
        Guid projectId,
        string taskNodeId,
        CancellationToken cancellationToken,
        ProjectWorkItemDirectAssignmentRevision?
            expectedDirectAssignmentRevision = null)
    {
        if (string.IsNullOrWhiteSpace(taskNodeId))
        {
            return new ProjectWorkItemDirectAssignmentMutationResult(
                ProjectWorkItemDirectAssignmentMutationStatus
                    .WorkItemNotFound,
                Revision: null);
        }

        var finalAssignments = dbContext.ChangeTracker
            .Entries<ProjectPartyAssignment>()
            .Where(entry =>
                entry.State is not (
                    EntityState.Deleted or
                    EntityState.Detached) &&
                entry.Entity.ProjectId == projectId &&
                entry.Entity.NodeKey == taskNodeId &&
                entry.Entity.AssignmentKind ==
                ProjectPartyAssignmentKind.WorkItemAssignee)
            .Select(static entry => entry.Entity)
            .ToArray();
        var partyIds = finalAssignments
            .Select(static assignment => assignment.PartyId)
            .Distinct()
            .ToArray();
        var parties = await dbContext.Set<Party>()
            .Where(party => partyIds.Contains(party.Id))
            .ToDictionaryAsync(
                party => party.Id,
                cancellationToken);
        if (parties.Count != partyIds.Length)
        {
            throw new InvalidOperationException(
                "A direct task assignment references a party that is no longer available.");
        }

        var states = finalAssignments
            .Select(assignment =>
            {
                var party = parties[assignment.PartyId];
                var partyType = party.PartyType switch
                {
                    PartyType.Person =>
                        ProjectPartyType.Person,
                    PartyType.AiAgent =>
                        ProjectPartyType.AiAgent,
                    _ => throw new InvalidOperationException(
                        $"Party type '{party.PartyType}' cannot be assigned directly to a task.")
                };
                return new ProjectWorkItemDirectAssignmentState(
                    partyType,
                    party.Id,
                    assignment.IsPrimary,
                    party.DisplayName);
            })
            .ToArray();
        return await workItemAssignmentMutationBridge.StageMutationAsync(
            dbContext,
            projectId,
            new ProjectNodeReference(taskNodeId),
            states,
            expectedDirectAssignmentRevision,
            cancellationToken);
    }

    public async Task<Result<ProjectPartyQuickCreateResult>> CreatePartyAsync(
        ProjectPartyQuickCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return Result<ProjectPartyQuickCreateResult>.Failure(Error.Validation(
                "Display name is required.",
                "crmhr.project-party.display-name-required"));
        }

        var partyType = request.PartyKind switch
        {
            ProjectPartyQuickCreateKind.Organization => PartyType.Organization,
            ProjectPartyQuickCreateKind.OrganizationUnit => PartyType.OrganizationUnit,
            ProjectPartyQuickCreateKind.AiAgent => PartyType.AiAgent,
            _ => PartyType.Person
        };

        var editor = new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = request.DisplayName.Trim(),
            Summary = request.Summary?.Trim() ?? string.Empty,
            LastChangedBy = "project-structure",
            ContactPoints = BuildQuickCreateContacts(request)
        };

        var saveResult = await partyDirectoryService.SavePartyAsync(editor, cancellationToken);
        if (!saveResult.IsSuccess)
        {
            return Result<ProjectPartyQuickCreateResult>.Failure(saveResult.Errors.ToArray());
        }

        var option = await GetPartyOptionAsync(saveResult.Value, cancellationToken);
        if (option is null)
        {
            return Result<ProjectPartyQuickCreateResult>.Failure(Error.Failure(
                "The created party could not be loaded.",
                "crmhr.project-party.created-party-not-found"));
        }

        return Result<ProjectPartyQuickCreateResult>.Success(new ProjectPartyQuickCreateResult(
            option.PartyId,
            option.DisplayName,
            option.PartyTypeLabel));
    }

    private static List<PartyContactPointEditorModel> BuildQuickCreateContacts(ProjectPartyQuickCreateRequest request)
    {
        var contacts = new List<PartyContactPointEditorModel>();
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            contacts.Add(new PartyContactPointEditorModel
            {
                ContactType = PartyContactType.Email,
                Label = "Primary email",
                Value = request.Email.Trim(),
                IsPrimary = true,
                IsPublic = true
            });
        }

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            contacts.Add(new PartyContactPointEditorModel
            {
                ContactType = PartyContactType.Phone,
                Label = "Primary phone",
                Value = request.Phone.Trim(),
                IsPrimary = contacts.Count == 0,
                IsPublic = true
            });
        }

        return contacts;
    }

    private static string ResolvePrimaryDisplayName(
        IReadOnlyList<ProjectPortfolioPartyItem> items,
        ProjectPartyPortfolioCategory category)
    {
        return items
            .Where(item => item.Category == category)
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => item.DisplayName)
            .FirstOrDefault() ?? string.Empty;
    }

    private static ProjectPartyAssignmentRole MapRole(ProjectPartyAssignmentKind role)
    {
        return role switch
        {
            ProjectPartyAssignmentKind.Customer => ProjectPartyAssignmentRole.Customer,
            ProjectPartyAssignmentKind.CustomerContact => ProjectPartyAssignmentRole.CustomerContact,
            ProjectPartyAssignmentKind.DeliveryUnit => ProjectPartyAssignmentRole.DeliveryUnit,
            ProjectPartyAssignmentKind.TeamMember => ProjectPartyAssignmentRole.TeamMember,
            ProjectPartyAssignmentKind.Manager => ProjectPartyAssignmentRole.Manager,
            ProjectPartyAssignmentKind.Partner => ProjectPartyAssignmentRole.Partner,
            ProjectPartyAssignmentKind.Vendor => ProjectPartyAssignmentRole.Vendor,
            ProjectPartyAssignmentKind.Stakeholder => ProjectPartyAssignmentRole.Stakeholder,
            ProjectPartyAssignmentKind.MeetingParticipant => ProjectPartyAssignmentRole.MeetingParticipant,
            ProjectPartyAssignmentKind.WorkItemAssignee => ProjectPartyAssignmentRole.WorkItemAssignee,
            ProjectPartyAssignmentKind.Reviewer => ProjectPartyAssignmentRole.Reviewer,
            ProjectPartyAssignmentKind.AiAgent => ProjectPartyAssignmentRole.AiAgent,
            ProjectPartyAssignmentKind.BillingContact => ProjectPartyAssignmentRole.BillingContact,
            _ => ProjectPartyAssignmentRole.TechnicalContact
        };
    }

    private static ProjectPartyAssignmentKind MapRole(ProjectPartyAssignmentRole role)
    {
        return role switch
        {
            ProjectPartyAssignmentRole.Customer => ProjectPartyAssignmentKind.Customer,
            ProjectPartyAssignmentRole.CustomerContact => ProjectPartyAssignmentKind.CustomerContact,
            ProjectPartyAssignmentRole.DeliveryUnit => ProjectPartyAssignmentKind.DeliveryUnit,
            ProjectPartyAssignmentRole.TeamMember => ProjectPartyAssignmentKind.TeamMember,
            ProjectPartyAssignmentRole.Manager => ProjectPartyAssignmentKind.Manager,
            ProjectPartyAssignmentRole.Partner => ProjectPartyAssignmentKind.Partner,
            ProjectPartyAssignmentRole.Vendor => ProjectPartyAssignmentKind.Vendor,
            ProjectPartyAssignmentRole.Stakeholder => ProjectPartyAssignmentKind.Stakeholder,
            ProjectPartyAssignmentRole.MeetingParticipant => ProjectPartyAssignmentKind.MeetingParticipant,
            ProjectPartyAssignmentRole.WorkItemAssignee => ProjectPartyAssignmentKind.WorkItemAssignee,
            ProjectPartyAssignmentRole.Reviewer => ProjectPartyAssignmentKind.Reviewer,
            ProjectPartyAssignmentRole.AiAgent => ProjectPartyAssignmentKind.AiAgent,
            ProjectPartyAssignmentRole.BillingContact => ProjectPartyAssignmentKind.BillingContact,
            ProjectPartyAssignmentRole.TechnicalContact => ProjectPartyAssignmentKind.TechnicalContact,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "The project assignment role is not supported.")
        };
    }

    private static ProjectPartyPortfolioCategory MapPortfolioCategory(ProjectPartyAssignmentKind assignmentKind)
    {
        return assignmentKind switch
        {
            ProjectPartyAssignmentKind.Customer => ProjectPartyPortfolioCategory.Customer,
            ProjectPartyAssignmentKind.DeliveryUnit => ProjectPartyPortfolioCategory.DeliveryUnit,
            ProjectPartyAssignmentKind.Manager or ProjectPartyAssignmentKind.TeamMember or ProjectPartyAssignmentKind.Reviewer => ProjectPartyPortfolioCategory.Owner,
            ProjectPartyAssignmentKind.Partner => ProjectPartyPortfolioCategory.Partner,
            ProjectPartyAssignmentKind.AiAgent => ProjectPartyPortfolioCategory.AiAgent,
            _ => ProjectPartyPortfolioCategory.Stakeholder
        };
    }

    private static string ResolvePortfolioLabel(ProjectPartyAssignmentKind assignmentKind)
    {
        return assignmentKind switch
        {
            ProjectPartyAssignmentKind.CustomerContact => "Customer contact",
            ProjectPartyAssignmentKind.DeliveryUnit => "Delivery unit",
            ProjectPartyAssignmentKind.TeamMember => "Team member",
            ProjectPartyAssignmentKind.AiAgent => "AI agent",
            ProjectPartyAssignmentKind.BillingContact => "Billing contact",
            ProjectPartyAssignmentKind.TechnicalContact => "Technical contact",
            _ => assignmentKind.ToString()
        };
    }

    private static string ResolvePartyTypeLabel(PartyType partyType)
    {
        return partyType switch
        {
            PartyType.OrganizationUnit => "Organization unit",
            PartyType.AiAgent => "AI agent",
            _ => partyType.ToString()
        };
    }

    private static ProjectPartyType MapProjectPartyType(PartyType partyType)
    {
        return partyType switch
        {
            PartyType.Person => ProjectPartyType.Person,
            PartyType.Organization => ProjectPartyType.Organization,
            PartyType.OrganizationUnit => ProjectPartyType.OrganizationUnit,
            PartyType.AiAgent => ProjectPartyType.AiAgent,
            _ => throw new ArgumentOutOfRangeException(nameof(partyType), partyType, "The party type is not supported by project integration.")
        };
    }

    private static string ResolvePrimaryContactValue(
        IReadOnlyList<CrmPartyContactValue> contacts,
        PartyContactType contactType)
    {
        return contacts
            .Where(item => item.ContactType == contactType)
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => item.Value)
            .FirstOrDefault()
            ?? string.Empty;
    }

    private static DateTimeOffset? ToUtcDate(DateOnly? value)
    {
        return value.HasValue
            ? new DateTimeOffset(value.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            : null;
    }

    private static List<string> NormalizeNodeKeys(IReadOnlyCollection<ProjectNodeReference> nodeReferences)
    {
        return nodeReferences
            .Select(nodeReference => nodeReference.NodeKey)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
