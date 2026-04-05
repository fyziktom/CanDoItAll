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

public sealed record CrmNextActionItemModel(
    Guid InteractionId,
    string InteractionSubject,
    string NextActionText,
    string OwnerName,
    DateTimeOffset DueAtUtc,
    bool IsOverdue);

public sealed record CrmAccountStakeholderItemModel(
    Guid Id,
    Guid RelatedPartyId,
    string DisplayName,
    PartyType PartyType,
    CrmAccountStakeholderRole Role,
    bool IsPrimary,
    string Notes);

public sealed record CrmAccountActivityTimelineItemModel(
    Guid Id,
    string Kind,
    string Title,
    string Description,
    string Meta,
    DateTimeOffset OccurredAtUtc,
    string Tone,
    bool IsOverdue);

public sealed record CrmAccountListItemModel(
    Guid AccountPartyId,
    string DisplayName,
    CrmAccountRelationshipStage RelationshipStage,
    IReadOnlyList<PartyRoleKind> Roles,
    string PrimaryEmail,
    string PrimaryPhone,
    int OpenOpportunityCount,
    int OverdueNextActionCount,
    DateTimeOffset? LastInteractionAtUtc,
    DateTimeOffset UpdatedAtUtc);

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

public sealed class CrmAccountStakeholderEditorModel
{
    public Guid? Id { get; set; }
    public Guid RelatedPartyId { get; set; }
    public CrmAccountStakeholderRole Role { get; set; } = CrmAccountStakeholderRole.Stakeholder;
    public bool IsPrimary { get; set; }
    public string Notes { get; set; } = string.Empty;
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
    IReadOnlyList<CrmAccountStakeholderItemModel> Stakeholders,
    IReadOnlyList<PartyOptionModel> AvailableParties,
    IReadOnlyList<CrmAccountActivityTimelineItemModel> ActivityTimeline,
    IReadOnlyList<CrmNextActionItemModel> OverdueNextActions,
    IReadOnlyList<CrmOpportunityDetailModel> Opportunities);

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
    public decimal CapacityHoursPerWeek { get; set; } = 40m;
    public string Status { get; set; } = "Planned";
    public string Notes { get; set; } = string.Empty;
    public string LastChangedBy { get; set; } = "crm-hr-ui";
}

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
    IReadOnlyList<PartyOptionModel> ManagerOptions,
    IReadOnlyList<PartyOptionModel> HomeUnitOptions,
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
    DateOnly? NextAvailabilityOn);

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
    ProviderKind ProviderKind,
    string DefaultModel,
    bool IsEnabled);

public sealed record AiAgentListItemModel(
    Guid PartyId,
    string DisplayName,
    string Summary,
    PartyLifecycleStatus LifecycleStatus,
    AiExecutionMode? ExecutionMode,
    AiValidationStatus? ValidationStatus,
    string ProviderName,
    string DefaultModel,
    string OwnerName,
    int CapabilityCount,
    bool HasProfile,
    DateTimeOffset UpdatedAtUtc);

public sealed record AiAgentWorkspaceModel(
    Guid PartyId,
    string DisplayName,
    string Summary,
    PartyLifecycleStatus LifecycleStatus,
    string PrimaryEmail,
    string PrimaryPhone,
    string ProviderName,
    string OwnerName,
    AiAgentProfileEditorModel Profile,
    IReadOnlyList<PartyOptionModel> OwnerOptions,
    IReadOnlyList<AiProviderOptionModel> ProviderOptions);

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
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => new PartyDirectoryContactValue(item.PartyId, item.ContactType, item.Value, item.IsPrimary))
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
        var contactPoints = await dbContext.Set<PartyContactPoint>().Where(item => item.PartyId == id).OrderByDescending(item => item.IsPrimary).ToListAsync(cancellationToken);
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
        ArgumentNullException.ThrowIfNull(model);

        if (string.IsNullOrWhiteSpace(model.DisplayName))
        {
            return Result<Guid>.Failure([Error.Validation("Display name is required.", "crmhr.party.display-name-required")]);
        }

        var confidentialNotes = model.ConfidentialNotes
            .Where(item => !string.IsNullOrWhiteSpace(item.NoteText))
            .ToList();
        if (!model.IsSensitive && confidentialNotes.Count > 0)
        {
            return Result<Guid>.Failure([Error.Validation(
                "Mark the party as sensitive before saving confidential notes.",
                "crmhr.party.confidential-notes-require-sensitive")]);
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var party = model.Id.HasValue
            ? await dbContext.Set<Party>().SingleOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;
        var isNew = party is null;
        var previousLifecycleStatus = party?.LifecycleStatus;

        if (party is null)
        {
            party = new Party
            {
                CreatedAtUtc = now
            };
            dbContext.Set<Party>().Add(party);
        }

        var normalizedExtendedDataResult = TryNormalizeJson(model.ExtendedDataJson, "{}");
        if (normalizedExtendedDataResult is null)
        {
            return Result<Guid>.Failure([Error.Validation("Extended data must be valid JSON.", "crmhr.party.extended-data-invalid")]);
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
        await dbContext.SaveChangesAsync(cancellationToken);
        await UpsertPartySearchDocumentAsync(party.Id, cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                auditAction,
                auditSummary,
                $"{party.PartyType} / {party.LifecycleStatus}",
                ArtifactKind: nameof(Party),
                ArtifactId: party.Id,
                Route: $"/crm-hr/directory?partyId={party.Id}",
                Actor: party.LastChangedBy),
            cancellationToken);
        return Result<Guid>.Success(party.Id);
    }

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
            .Select(item => item.Value)
            .FirstOrDefault()
            ?? string.Empty;
    }

    private sealed record PartyDirectoryContactValue(
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
    IProjectPartyIntegrationBridge projectPartyIntegrationBridge)
{
    private const string CrmAccountEntityType = "CrmAccount";
    private const string CrmAccountSearchSourceType = "crm-account";
    private const string CrmOpportunityEntityType = "Opportunity";
    private const string CrmOpportunitySearchSourceType = "crm-opportunity";

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

    public async Task<IReadOnlyList<CrmAccountListItemModel>> ListAccountsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var parties = await dbContext.Set<Party>()
            .Where(item => item.PartyType == PartyType.Organization)
            .OrderBy(item => item.DisplayName)
            .Select(item => new
            {
                item.Id,
                item.DisplayName,
                item.LifecycleStatus,
                item.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        if (parties.Count == 0)
        {
            return [];
        }

        var accountIds = parties.Select(item => item.Id).ToList();
        var roles = await dbContext.Set<PartyRoleAssignment>()
            .Where(item => accountIds.Contains(item.PartyId))
            .Select(item => new { item.PartyId, item.RoleKind })
            .ToListAsync(cancellationToken);
        var contacts = await dbContext.Set<PartyContactPoint>()
            .Where(item => accountIds.Contains(item.PartyId))
            .Select(item => new CrmPartyContactValue(item.PartyId, item.ContactType, item.Value, item.IsPrimary))
            .ToListAsync(cancellationToken);
        var profiles = await dbContext.Set<CrmAccountProfile>()
            .Where(item => accountIds.Contains(item.AccountPartyId))
            .ToListAsync(cancellationToken);
        var opportunities = await dbContext.Set<Opportunity>()
            .Where(item =>
                accountIds.Contains(item.AccountPartyId) &&
                item.Stage != OpportunityStage.Won &&
                item.Stage != OpportunityStage.Lost)
            .Select(item => new { item.AccountPartyId, item.Id })
            .ToListAsync(cancellationToken);
        var accountLinks = await dbContext.Set<InteractionPartyLink>()
            .Where(item => accountIds.Contains(item.PartyId) && item.Role == InteractionPartyRole.Account)
            .Select(item => new { item.PartyId, item.InteractionId })
            .ToListAsync(cancellationToken);

        var interactionIds = accountLinks.Select(item => item.InteractionId).Distinct().ToList();
        var interactions = interactionIds.Count == 0
            ? []
            : await dbContext.Set<InteractionRecord>()
                .Where(item => interactionIds.Contains(item.Id))
                .Select(item => new
                {
                    item.Id,
                    item.OccurredAtUtc,
                    item.NextActionText,
                    item.NextActionDueUtc
                })
                .ToListAsync(cancellationToken);

        var rolesByPartyId = roles
            .GroupBy(item => item.PartyId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PartyRoleKind>)group.Select(item => item.RoleKind).Distinct().ToList());
        var contactsByPartyId = contacts
            .GroupBy(item => item.PartyId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<CrmPartyContactValue>)group.ToList());
        var profilesByAccountId = profiles.ToDictionary(item => item.AccountPartyId);
        var opportunityCountByAccountId = opportunities
            .GroupBy(item => item.AccountPartyId)
            .ToDictionary(group => group.Key, group => group.Count());
        var interactionsById = interactions.ToDictionary(item => item.Id);
        var now = clock.GetUtcNow();
        var interactionInfoByAccountId = accountLinks
            .GroupBy(item => item.PartyId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var lastInteractionAtUtc = group
                        .Select(link => interactionsById.GetValueOrDefault(link.InteractionId)?.OccurredAtUtc)
                        .Where(value => value.HasValue)
                        .Select(value => value!.Value)
                        .DefaultIfEmpty()
                        .Max();
                    var overdueNextActionCount = group
                        .Select(link => interactionsById.GetValueOrDefault(link.InteractionId))
                        .Count(item =>
                            item is not null &&
                            !string.IsNullOrWhiteSpace(item.NextActionText) &&
                            item.NextActionDueUtc is DateTimeOffset dueAtUtc &&
                            dueAtUtc < now);

                    return new
                    {
                        LastInteractionAtUtc = lastInteractionAtUtc == default ? (DateTimeOffset?)null : lastInteractionAtUtc,
                        OverdueNextActionCount = overdueNextActionCount
                    };
                });

        return parties
            .Select(item =>
            {
                var contactValues = contactsByPartyId.GetValueOrDefault(item.Id) ?? [];
                var roleValues = rolesByPartyId.GetValueOrDefault(item.Id) ?? [];
                var profile = profilesByAccountId.GetValueOrDefault(item.Id);
                var interactionInfo = interactionInfoByAccountId.GetValueOrDefault(item.Id);

                return new CrmAccountListItemModel(
                    item.Id,
                    item.DisplayName,
                    ResolveRelationshipStage(item.LifecycleStatus, roleValues, profile),
                    roleValues,
                    ResolvePrimaryContact(contactValues, PartyContactType.Email),
                    ResolvePrimaryContact(contactValues, PartyContactType.Phone),
                    opportunityCountByAccountId.GetValueOrDefault(item.Id),
                    interactionInfo?.OverdueNextActionCount ?? 0,
                    interactionInfo?.LastInteractionAtUtc,
                    item.UpdatedAtUtc);
            })
            .OrderBy(item => item.DisplayName)
            .ToList();
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
        var stakeholderLinks = await dbContext.Set<CrmAccountStakeholderLink>()
            .Where(item => item.AccountPartyId == accountPartyId)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.Role)
            .ToListAsync(cancellationToken);
        var availableParties = await dbContext.Set<Party>()
            .Where(item => item.Id != accountPartyId && item.LifecycleStatus != PartyLifecycleStatus.Archived)
            .OrderBy(item => item.DisplayName)
            .Select(item => new PartyOptionModel(item.Id, item.DisplayName, item.PartyType))
            .ToListAsync(cancellationToken);

        var relatedPartyIds = stakeholderLinks.Select(item => item.RelatedPartyId).Distinct().ToList();
        var relatedParties = relatedPartyIds.Count == 0
            ? new Dictionary<Guid, PartyOptionModel>()
            : (await dbContext.Set<Party>()
                .Where(item => relatedPartyIds.Contains(item.Id))
                .Select(item => new PartyOptionModel(item.Id, item.DisplayName, item.PartyType))
                .ToListAsync(cancellationToken))
                .ToDictionary(item => item.Id);

        var accountLinks = await dbContext.Set<InteractionPartyLink>()
            .Where(item => item.PartyId == accountPartyId && item.Role == InteractionPartyRole.Account)
            .Select(item => item.InteractionId)
            .ToListAsync(cancellationToken);
        var interactions = accountLinks.Count == 0
            ? []
            : await dbContext.Set<InteractionRecord>()
                .Where(item => accountLinks.Contains(item.Id))
                .ToListAsync(cancellationToken);
        var participantLinks = accountLinks.Count == 0
            ? []
            : await dbContext.Set<InteractionPartyLink>()
                .Where(item => accountLinks.Contains(item.InteractionId) && item.Role != InteractionPartyRole.Account)
                .ToListAsync(cancellationToken);
        var participantPartyIds = participantLinks
            .Select(item => item.PartyId)
            .Concat(interactions.Where(item => item.NextActionOwnerPartyId.HasValue).Select(item => item.NextActionOwnerPartyId!.Value))
            .Distinct()
            .ToList();
        var interactionPartyNames = participantPartyIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await dbContext.Set<Party>()
                .Where(item => participantPartyIds.Contains(item.Id))
                .Select(item => new { item.Id, item.DisplayName })
                .ToListAsync(cancellationToken))
                .ToDictionary(item => item.Id, item => item.DisplayName);
        var auditEntries = (await dbContext.Set<CrmHrAuditEntry>()
            .Where(item => item.EntityType == CrmAccountEntityType && item.EntityId == accountPartyId)
            .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(20)
            .ToList();
        var opportunityIds = await dbContext.Set<Opportunity>()
            .Where(item => item.AccountPartyId == accountPartyId)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var opportunities = await LoadOpportunityDetailsAsync(dbContext, opportunityIds, cancellationToken);

        var stakeholders = stakeholderLinks
            .Select(item =>
            {
                var relatedParty = relatedParties.GetValueOrDefault(item.RelatedPartyId)
                    ?? new PartyOptionModel(item.RelatedPartyId, "Unknown party", PartyType.Person);
                return new CrmAccountStakeholderItemModel(
                    item.Id,
                    item.RelatedPartyId,
                    relatedParty.DisplayName,
                    relatedParty.PartyType,
                    item.Role,
                    item.IsPrimary,
                    item.Notes);
            })
            .ToList();
        var overdueNextActions = interactions
            .Where(item =>
                !string.IsNullOrWhiteSpace(item.NextActionText) &&
                item.NextActionDueUtc is DateTimeOffset dueAtUtc &&
                dueAtUtc < clock.GetUtcNow())
            .OrderBy(item => item.NextActionDueUtc)
            .Select(item => new CrmNextActionItemModel(
                item.Id,
                item.Subject,
                item.NextActionText,
                ResolvePartyName(item.NextActionOwnerPartyId, interactionPartyNames, stakeholders),
                item.NextActionDueUtc!.Value,
                true))
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
            stakeholders,
            availableParties,
            BuildActivityTimeline(interactions, participantLinks, interactionPartyNames, auditEntries),
            overdueNextActions,
            opportunities
                .OrderByDescending(item => item.UpdatedAtUtc)
                .ToList());
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

    public async Task<Result> SaveStakeholdersAsync(
        Guid accountPartyId,
        IReadOnlyList<CrmAccountStakeholderEditorModel> stakeholders,
        string actor,
        CancellationToken cancellationToken = default)
    {
        if (accountPartyId == Guid.Empty)
        {
            return Result.Failure(Error.Validation("Choose an account before saving stakeholders.", "crmhr.crm.account-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var party = await dbContext.Set<Party>()
            .SingleOrDefaultAsync(item => item.Id == accountPartyId && item.PartyType == PartyType.Organization, cancellationToken);
        if (party is null)
        {
            return Result.Failure(Error.Failure("The selected account no longer exists.", "crmhr.crm.account-missing"));
        }

        var existingLinks = await dbContext.Set<CrmAccountStakeholderLink>()
            .Where(item => item.AccountPartyId == accountPartyId)
            .ToListAsync(cancellationToken);
        dbContext.Set<CrmAccountStakeholderLink>().RemoveRange(existingLinks);

        var now = clock.GetUtcNow();
        var normalizedActor = string.IsNullOrWhiteSpace(actor) ? "crm-hr-ui" : actor.Trim();
        var distinctStakeholders = stakeholders
            .Where(item => item.RelatedPartyId != Guid.Empty && item.RelatedPartyId != accountPartyId)
            .GroupBy(item => new { item.RelatedPartyId, item.Role })
            .Select(group => group.First())
            .ToList();

        dbContext.Set<CrmAccountStakeholderLink>().AddRange(distinctStakeholders.Select(item => new CrmAccountStakeholderLink
        {
            Id = item.Id ?? Guid.NewGuid(),
            AccountPartyId = accountPartyId,
            RelatedPartyId = item.RelatedPartyId,
            Role = item.Role,
            IsPrimary = item.IsPrimary,
            Notes = item.Notes.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        }));

        AddAuditEntry(
            dbContext,
            accountPartyId,
            "AccountStakeholdersUpdated",
            $"Updated CRM stakeholders for '{party.DisplayName}'.",
            distinctStakeholders.Select(item => new { item.RelatedPartyId, item.Role, item.IsPrimary, item.Notes }),
            normalizedActor,
            party.IsSensitive);

        await dbContext.SaveChangesAsync(cancellationToken);
        await UpsertAccountSearchDocumentAsync(accountPartyId, cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                "AccountStakeholdersUpdated",
                $"Updated stakeholders for {party.DisplayName}",
                $"{distinctStakeholders.Count} stakeholder link(s) saved.",
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
        var stakeholderRoles = await dbContext.Set<CrmAccountStakeholderLink>()
            .Where(item => item.AccountPartyId == accountPartyId && model.ParticipantPartyIds.Contains(item.RelatedPartyId))
            .ToListAsync(cancellationToken);
        var stakeholderRoleByPartyId = stakeholderRoles
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
                Role = ResolveInteractionRole(stakeholderRoleByPartyId.GetValueOrDefault(participantPartyId))
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
        var knownPartyIds = referencedPartyIds.Count == 0
            ? []
            : await dbContext.Set<Party>()
                .Where(item => referencedPartyIds.Contains(item.Id))
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);
        if (knownPartyIds.Count != referencedPartyIds.Count)
        {
            return Result<Guid>.Failure(Error.Failure("One or more selected opportunity parties no longer exist.", "crmhr.crm.opportunity-party-missing"));
        }

        if (model.LinkedProjectId.HasValue)
        {
            var projectExists = await dbContext.Set<Project>()
                .AnyAsync(item => item.Id == model.LinkedProjectId.Value, cancellationToken);
            if (!projectExists)
            {
                return Result<Guid>.Failure(Error.Validation("The linked project was not found.", "crmhr.crm.opportunity-linked-project-missing"));
            }
        }

        var normalizedActor = NormalizeActor(model.LastChangedBy);
        var now = clock.GetUtcNow();
        var isNew = entity is null;
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
        entity.CurrencyCode = string.IsNullOrWhiteSpace(model.CurrencyCode) ? "USD" : model.CurrencyCode.Trim().ToUpperInvariant();
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
        entity.UpdatedAtUtc = now;

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
                Notes = model.StageNotes.Trim()
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

        await dbContext.SaveChangesAsync(cancellationToken);
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
            var project = await dbContext.Set<Project>()
                .SingleOrDefaultAsync(item => item.Id == model.ExistingProjectId!.Value, cancellationToken);
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

        foreach (var assignment in BuildOpportunityProjectAssignments(opportunityDetail, projectId))
        {
            var assignmentResult = await projectPartyIntegrationBridge.SaveAssignmentAsync(assignment, cancellationToken);
            if (!assignmentResult.IsSuccess)
            {
                return Result<CrmOpportunityConversionResult>.Failure(assignmentResult.Errors.ToArray());
            }
        }

        var now = clock.GetUtcNow();
        opportunity.LinkedProjectId = projectId;
        opportunity.UpdatedAtUtc = now;
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

        await dbContext.SaveChangesAsync(cancellationToken);
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
            : (await dbContext.Set<Project>()
                .Where(item => linkedProjectIds.Contains(item.Id))
                .Select(item => new
                {
                    item.Id,
                    item.Name
                })
                .ToListAsync(cancellationToken))
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
                        var linkedParty = partiesById.GetValueOrDefault(link.PartyId);
                        return new CrmOpportunityPartyLinkItemModel(
                            link.Id,
                            link.PartyId,
                            linkedParty?.DisplayName ?? "Unknown party",
                            linkedParty?.PartyType ?? PartyType.Person,
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
                var accountParty = partiesById.GetValueOrDefault(item.AccountPartyId);
                var ownerParty = partiesById.GetValueOrDefault(item.OwnerPartyId);
                var deliveryUnit = item.DeliveryUnitPartyId.HasValue
                    ? partiesById.GetValueOrDefault(item.DeliveryUnitPartyId.Value)
                    : null;

                return new CrmOpportunityDetailModel(
                    item.Id,
                    item.AccountPartyId,
                    accountParty?.DisplayName ?? "Unknown account",
                    item.Title,
                    item.Stage,
                    item.RelationshipStage,
                    item.OpportunitySource,
                    item.OwnerPartyId,
                    ownerParty?.DisplayName ?? "Unknown owner",
                    item.DeliveryUnitPartyId,
                    deliveryUnit?.DisplayName ?? string.Empty,
                    string.IsNullOrWhiteSpace(item.CurrencyCode) ? "USD" : item.CurrencyCode,
                    item.Amount,
                    item.ProbabilityPercent,
                    ToDateOnly(item.ExpectedCloseDateUtc),
                    item.LostReason,
                    extendedData.CompetitorName,
                    extendedData.PartnerContributionSummary,
                    item.Summary,
                    item.Notes,
                    item.LinkedProjectId,
                    item.LinkedProjectId.HasValue
                        ? projectNamesById.GetValueOrDefault(item.LinkedProjectId.Value) ?? string.Empty
                        : string.Empty,
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
        var stakeholders = await dbContext.Set<CrmAccountStakeholderLink>()
            .Where(item => item.AccountPartyId == accountPartyId)
            .ToListAsync(cancellationToken);
        var stakeholderPartyIds = stakeholders.Select(item => item.RelatedPartyId).Distinct().ToList();
        var stakeholderNames = stakeholderPartyIds.Count == 0
            ? []
            : await dbContext.Set<Party>()
                .Where(item => stakeholderPartyIds.Contains(item.Id))
                .OrderBy(item => item.DisplayName)
                .Select(item => item.DisplayName)
                .ToListAsync(cancellationToken);
        var interactionIds = await dbContext.Set<InteractionPartyLink>()
            .Where(item => item.PartyId == accountPartyId && item.Role == InteractionPartyRole.Account)
            .Select(item => item.InteractionId)
            .ToListAsync(cancellationToken);
        var recentInteractionSubjects = interactionIds.Count == 0
            ? []
            : (await dbContext.Set<InteractionRecord>()
                .Where(item => interactionIds.Contains(item.Id))
                .Select(item => new
                {
                    item.Subject,
                    item.OccurredAtUtc
                })
                .ToListAsync(cancellationToken))
                .OrderByDescending(item => item.OccurredAtUtc)
                .Take(5)
                .Select(item => item.Subject)
                .ToList();

        var relationshipStage = ResolveRelationshipStage(party.LifecycleStatus, roles, profile);
        var summary = $"{relationshipStage} / {string.Join(", ", roles.Take(3))}".Trim(' ', '/');
        var bodyParts = new List<string>
        {
            party.DisplayName,
            party.Summary,
            profile?.CommercialNotes ?? string.Empty,
            profile?.ConstraintNotes ?? string.Empty,
            profile?.TimingRiskNotes ?? string.Empty,
            string.Join(", ", stakeholderNames),
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

    private static IReadOnlyList<CrmAccountActivityTimelineItemModel> BuildActivityTimeline(
        IReadOnlyList<InteractionRecord> interactions,
        IReadOnlyList<InteractionPartyLink> participantLinks,
        IReadOnlyDictionary<Guid, string> interactionPartyNames,
        IReadOnlyList<CrmHrAuditEntry> auditEntries)
    {
        var participantsByInteractionId = participantLinks
            .GroupBy(item => item.InteractionId)
            .ToDictionary(
                group => group.Key,
                group => string.Join(
                    ", ",
                    group.Select(item => interactionPartyNames.GetValueOrDefault(item.PartyId))
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Distinct()
                        .Cast<string>()));

        var interactionItems = interactions.Select(item =>
        {
            var participantText = participantsByInteractionId.GetValueOrDefault(item.Id) ?? string.Empty;
            var metaParts = new List<string>
            {
                item.InteractionType.ToString()
            };

            if (!string.IsNullOrWhiteSpace(participantText))
            {
                metaParts.Add(participantText);
            }

            if (!string.IsNullOrWhiteSpace(item.NextActionText) && item.NextActionDueUtc is DateTimeOffset dueAtUtc)
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
                ResolveInteractionTone(item.InteractionType),
                !string.IsNullOrWhiteSpace(item.NextActionText) &&
                item.NextActionDueUtc is DateTimeOffset nextActionDueAtUtc &&
                nextActionDueAtUtc < DateTimeOffset.UtcNow);
        });

        var auditItems = auditEntries.Select(item => new CrmAccountActivityTimelineItemModel(
            item.Id,
            "Audit",
            item.Summary,
            item.Action,
            item.Actor,
            item.CreatedAtUtc,
            item.IsSensitive ? "warning" : "neutral",
            false));

        return interactionItems
            .Concat(auditItems)
            .OrderByDescending(item => item.OccurredAtUtc)
            .Take(20)
            .ToList();
    }

    private static DateTimeOffset ToUtcDate(DateOnly value)
    {
        return new DateTimeOffset(value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }

    private static string ResolvePartyName(
        Guid? partyId,
        IReadOnlyDictionary<Guid, string> interactionPartyNames,
        IReadOnlyList<CrmAccountStakeholderItemModel> stakeholders)
    {
        if (!partyId.HasValue)
        {
            return "Unassigned";
        }

        var partyName = interactionPartyNames.GetValueOrDefault(partyId.Value);
        if (!string.IsNullOrWhiteSpace(partyName))
        {
            return partyName;
        }

        return stakeholders.FirstOrDefault(item => item.RelatedPartyId == partyId.Value)?.DisplayName ?? "Unknown owner";
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

    private static InteractionPartyRole ResolveInteractionRole(CrmAccountStakeholderRole stakeholderRole)
    {
        return stakeholderRole switch
        {
            CrmAccountStakeholderRole.AccountManager
                or CrmAccountStakeholderRole.DeliveryLead
                or CrmAccountStakeholderRole.Sponsor
                or CrmAccountStakeholderRole.Stakeholder => InteractionPartyRole.Stakeholder,
            _ => InteractionPartyRole.Contact
        };
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
            .Where(item => item.PartyId == partyId)
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => new CrmPartyContactValue(item.PartyId, item.ContactType, item.Value, item.IsPrimary))
            .ToListAsync(cancellationToken);
        var profile = await dbContext.Set<WorkforceProfile>()
            .SingleOrDefaultAsync(item => item.PartyId == partyId, cancellationToken);
        var relatedParties = await dbContext.Set<Party>()
            .OrderBy(item => item.DisplayName)
            .Select(item => new
            {
                item.Id,
                item.DisplayName,
                item.PartyType
            })
            .ToListAsync(cancellationToken);
        var relatedPartyIds = relatedParties.Select(item => item.Id).ToList();
        var relatedRoles = await dbContext.Set<PartyRoleAssignment>()
            .Where(item => relatedPartyIds.Contains(item.PartyId))
            .Select(item => new
            {
                item.PartyId,
                item.RoleKind
            })
            .ToListAsync(cancellationToken);
        var roleLookup = relatedRoles
            .GroupBy(item => item.PartyId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.RoleKind).ToHashSet());

        var managerOptions = relatedParties
            .Where(item => item.Id != partyId && item.PartyType == PartyType.Person)
            .Select(item => new PartyOptionModel(item.Id, item.DisplayName, item.PartyType))
            .ToList();
        var homeUnitOptions = relatedParties
            .Where(item =>
                item.Id != partyId &&
                (item.PartyType == PartyType.Organization || item.PartyType == PartyType.OrganizationUnit || (roleLookup.GetValueOrDefault(item.Id)?.Contains(PartyRoleKind.DeliveryUnit) ?? false)))
            .Select(item => new PartyOptionModel(item.Id, item.DisplayName, item.PartyType))
            .ToList();
        var namesByPartyId = relatedParties.ToDictionary(item => item.Id, item => item.DisplayName);
        var skillCatalog = await GetSkillCatalogItemsAsync(dbContext, cancellationToken);
        var partySkills = (await GetPartySkillMapAsync(dbContext, [partyId], cancellationToken)).GetValueOrDefault(partyId) ?? [];
        var capacityBlocks = (await GetCapacityBlockMapAsync(dbContext, [partyId], cancellationToken)).GetValueOrDefault(partyId) ?? [];
        var projectAllocations = (await GetProjectAllocationMapAsync(dbContext, [partyId], cancellationToken)).GetValueOrDefault(partyId) ?? [];
        var capacitySummary = BuildCapacitySummary(profile?.CapacityHoursPerWeek ?? 40m, projectAllocations, capacityBlocks);

        return new WorkforceWorkspaceModel(
            party.Id,
            party.DisplayName,
            party.Summary,
            party.PartyType,
            party.LifecycleStatus,
            party.IsSensitive,
            string.IsNullOrWhiteSpace(party.LastChangedBy) ? "crm-hr-ui" : party.LastChangedBy,
            party.UpdatedAtUtc,
            roles,
            ResolvePrimaryContact(contactPoints, PartyContactType.Email),
            ResolvePrimaryContact(contactPoints, PartyContactType.Phone),
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
                CapacityHoursPerWeek = profile?.CapacityHoursPerWeek ?? 40m,
                Status = string.IsNullOrWhiteSpace(profile?.Status) ? ResolveDefaultStatus(party.LifecycleStatus) : profile.Status,
                Notes = profile?.Notes ?? string.Empty,
                LastChangedBy = string.IsNullOrWhiteSpace(party.LastChangedBy) ? "crm-hr-ui" : party.LastChangedBy
            },
            managerOptions,
            homeUnitOptions,
            skillCatalog,
            partySkills,
            capacityBlocks,
            projectAllocations,
            capacitySummary);
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

    public async Task<IReadOnlyList<StaffingRequestItemModel>> ListStaffingRequestsAsync(Guid? projectId = null, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var requests = await dbContext.Set<StaffingRequest>()
            .Where(item => !projectId.HasValue || item.ProjectId == projectId.Value)
            .ToListAsync(cancellationToken);
        if (requests.Count == 0)
        {
            return [];
        }

        requests = requests
            .OrderBy(item => item.Status)
            .ThenBy(item => item.StartDateUtc ?? DateTimeOffset.MinValue)
            .ThenBy(item => item.Title)
            .ToList();

        var projectIds = requests.Where(item => item.ProjectId.HasValue).Select(item => item.ProjectId!.Value).Distinct().ToList();
        var partyIds = requests
            .SelectMany(item => new[] { item.RequestedByPartyId, item.DeliveryUnitPartyId })
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Distinct()
            .ToList();
        var projectNames = projectIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Set<Project>()
                .Where(item => projectIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var partyNames = partyIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Set<Party>()
                .Where(item => partyIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);

        var skillCatalog = await GetSkillCatalogItemsAsync(dbContext, cancellationToken);
        var skillsById = skillCatalog.ToDictionary(item => item.Id, item => item);

        return requests.Select(item =>
            new StaffingRequestItemModel(
                item.Id,
                item.ProjectId,
                item.ProjectId.HasValue ? projectNames.GetValueOrDefault(item.ProjectId.Value) ?? string.Empty : string.Empty,
                item.RequestedByPartyId,
                item.RequestedByPartyId.HasValue ? partyNames.GetValueOrDefault(item.RequestedByPartyId.Value) ?? string.Empty : string.Empty,
                item.DeliveryUnitPartyId,
                item.DeliveryUnitPartyId.HasValue ? partyNames.GetValueOrDefault(item.DeliveryUnitPartyId.Value) ?? string.Empty : string.Empty,
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

    public async Task<IReadOnlyList<StaffingCandidateItemModel>> SearchStaffingCandidatesAsync(
        Guid? skillId = null,
        string searchText = "",
        WorkforceAvailabilityState? availabilityState = null,
        CancellationToken cancellationToken = default)
    {
        var workforceItems = (await ListWorkforceDirectoryAsync(cancellationToken))
            .Where(item => item.HasProfile)
            .ToList();
        if (workforceItems.Count == 0)
        {
            return [];
        }

        if (skillId.HasValue)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var matchedPartyIds = await dbContext.Set<PartySkill>()
                .Where(item => item.SkillId == skillId.Value)
                .Select(item => item.PartyId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var matchedSet = matchedPartyIds.ToHashSet();
            workforceItems = workforceItems
                .Where(item => matchedSet.Contains(item.PartyId))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            workforceItems = workforceItems
                .Where(item =>
                    item.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || item.JobTitle.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || item.Discipline.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || item.Seniority.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || item.Location.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || item.SkillSummary.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (availabilityState.HasValue)
        {
            workforceItems = workforceItems
                .Where(item => item.AvailabilityState == availabilityState.Value)
                .ToList();
        }

        return workforceItems
            .Select(item => new StaffingCandidateItemModel(
                item.PartyId,
                item.DisplayName,
                item.PartyType,
                item.JobTitle,
                item.Discipline,
                item.Seniority,
                item.Location,
                item.SkillSummary,
                item.AvailabilityState ?? WorkforceAvailabilityState.Bench,
                item.AvailablePercent,
                item.NextAvailabilityOn))
            .OrderByDescending(item => item.AvailabilityState == WorkforceAvailabilityState.Bench)
            .ThenByDescending(item => item.AvailablePercent)
            .ThenBy(item => item.DisplayName)
            .ToList();
    }

    public async Task<StaffingDashboardModel> GetStaffingDashboardAsync(CancellationToken cancellationToken = default)
    {
        var requests = await ListStaffingRequestsAsync(null, cancellationToken);
        var workforceItems = await ListWorkforceDirectoryAsync(cancellationToken);
        var openRequests = requests
            .Where(item => item.Status is StaffingRequestStatus.Draft or StaffingRequestStatus.Open or StaffingRequestStatus.Proposed or StaffingRequestStatus.Confirmed)
            .ToList();

        return new StaffingDashboardModel(
            openRequests.Count,
            openRequests.Sum(item => item.AllocationPercent),
            workforceItems.Count(item => item.AvailabilityState == WorkforceAvailabilityState.Bench),
            workforceItems.Count(item => item.AvailabilityState == WorkforceAvailabilityState.Overallocated));
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
            _ => ProjectPartyAssignmentRole.TechnicalContact
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
    ISearchIndexService searchIndexService)
{
    public async Task<IReadOnlyList<AiAgentListItemModel>> ListAgentDirectoryAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var parties = await dbContext.Set<Party>()
            .Where(item => item.PartyType == PartyType.AiAgent)
            .OrderBy(item => item.DisplayName)
            .Select(item => new
            {
                item.Id,
                item.DisplayName,
                item.Summary,
                item.LifecycleStatus,
                item.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);
        if (parties.Count == 0)
        {
            return [];
        }

        var partyIds = parties.Select(item => item.Id).ToList();
        var profiles = await dbContext.Set<AiAgentProfile>()
            .Where(item => partyIds.Contains(item.PartyId))
            .ToListAsync(cancellationToken);
        var ownerIds = profiles
            .Where(item => item.OwnerPartyId.HasValue)
            .Select(item => item.OwnerPartyId!.Value)
            .Distinct()
            .ToList();
        var providerIds = profiles
            .Where(item => item.ProviderProfileId.HasValue)
            .Select(item => item.ProviderProfileId!.Value)
            .Distinct()
            .ToList();

        var ownerNames = ownerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Set<Party>()
                .Where(item => ownerIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);
        var providerNames = providerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Set<ProviderProfile>()
                .Where(item => providerIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var profileByPartyId = profiles.ToDictionary(item => item.PartyId);

        return parties
            .Select(item =>
            {
                profileByPartyId.TryGetValue(item.Id, out var profile);
                return new AiAgentListItemModel(
                    item.Id,
                    item.DisplayName,
                    item.Summary,
                    item.LifecycleStatus,
                    profile?.ExecutionMode,
                    profile?.ValidationStatus,
                    profile?.ProviderProfileId is Guid providerProfileId ? providerNames.GetValueOrDefault(providerProfileId) ?? string.Empty : string.Empty,
                    profile?.DefaultModel ?? string.Empty,
                    profile?.OwnerPartyId is Guid ownerPartyId ? ownerNames.GetValueOrDefault(ownerPartyId) ?? string.Empty : string.Empty,
                    profile is null ? 0 : DeserializeCapabilities(profile.CapabilityJson, profile.Id).Count,
                    profile is not null,
                    item.UpdatedAtUtc);
            })
            .ToList();
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
        var providerOptions = await dbContext.Set<ProviderProfile>()
            .OrderBy(item => item.Name)
            .Select(item => new AiProviderOptionModel(
                item.Id,
                item.Name,
                item.ProviderKind,
                item.DefaultModel,
                item.IsEnabled))
            .ToListAsync(cancellationToken);
        var ownerOptions = await dbContext.Set<Party>()
            .Where(item => item.Id != partyId && item.PartyType == PartyType.Person)
            .OrderBy(item => item.DisplayName)
            .Select(item => new PartyOptionModel(item.Id, item.DisplayName, item.PartyType))
            .ToListAsync(cancellationToken);

        var providerName = string.Empty;
        var ownerName = string.Empty;
        if (profile?.ProviderProfileId is Guid providerProfileId)
        {
            providerName = providerOptions
                .FirstOrDefault(item => item.Id == providerProfileId)?
                .Name
                ?? string.Empty;
        }

        if (profile?.OwnerPartyId is Guid ownerPartyId)
        {
            ownerName = ownerOptions
                .FirstOrDefault(item => item.Id == ownerPartyId)?
                .DisplayName
                ?? await dbContext.Set<Party>()
                    .Where(item => item.Id == ownerPartyId)
                    .Select(item => item.DisplayName)
                    .FirstOrDefaultAsync(cancellationToken)
                ?? string.Empty;
        }

        var resolvedDefaultModel = profile is null
            ? string.Empty
            : ResolveDefaultModel(profile.DefaultModel, providerOptions.FirstOrDefault(item => item.Id == profile.ProviderProfileId)?.DefaultModel);

        return new AiAgentWorkspaceModel(
            party.Id,
            party.DisplayName,
            party.Summary,
            party.LifecycleStatus,
            ResolvePrimaryContactValue(contactPoints, PartyContactType.Email),
            ResolvePrimaryContactValue(contactPoints, PartyContactType.Phone),
            providerName,
            ownerName,
            new AiAgentProfileEditorModel
            {
                Id = profile?.Id,
                PartyId = party.Id,
                ProviderProfileId = profile?.ProviderProfileId,
                DefaultModel = resolvedDefaultModel,
                ExecutionMode = profile?.ExecutionMode ?? AiExecutionMode.Remote,
                OwnerPartyId = profile?.OwnerPartyId,
                ValidationStatus = profile?.ValidationStatus ?? AiValidationStatus.Draft,
                LastReviewedOn = profile?.LastReviewedAtUtc is DateTimeOffset reviewedAtUtc ? DateOnly.FromDateTime(reviewedAtUtc.UtcDateTime) : null,
                Notes = profile?.Notes ?? string.Empty,
                ExtendedDataJson = profile?.ExtendedDataJson ?? "{}",
                LastChangedBy = "crm-hr-ui",
                Capabilities = profile is null ? [] : DeserializeCapabilities(profile.CapabilityJson, profile.Id)
            },
            ownerOptions,
            providerOptions);
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

        ProviderProfile? provider = null;
        if (model.ProviderProfileId is Guid providerProfileId)
        {
            provider = await dbContext.Set<ProviderProfile>()
                .SingleOrDefaultAsync(item => item.Id == providerProfileId, cancellationToken);
            if (provider is null)
            {
                return Result<Guid>.Failure(Error.Validation("Provider profile must reference an existing workspace provider.", "crmhr.ai-agent.provider-invalid"));
            }
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

        profile.ProviderProfileId = model.ProviderProfileId;
        profile.DefaultModel = ResolveDefaultModel(model.DefaultModel, provider?.DefaultModel);
        profile.ExecutionMode = model.ExecutionMode;
        profile.OwnerPartyId = model.OwnerPartyId;
        profile.CapabilityJson = SerializeCapabilities(model.Capabilities);
        profile.ValidationStatus = model.ValidationStatus;
        profile.LastReviewedAtUtc = ToUtcDate(model.LastReviewedOn);
        profile.Notes = model.Notes.Trim();
        profile.ExtendedDataJson = normalizedExtendedData;

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
                profile.ExecutionMode,
                profile.ValidationStatus,
                profile.ProviderProfileId,
                profile.OwnerPartyId
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
                $"{profile.ExecutionMode} / {profile.ValidationStatus}",
                ArtifactKind: nameof(AiAgentProfile),
                ArtifactId: party.Id,
                Route: $"/crm-hr/agents?partyId={party.Id}",
                Actor: party.LastChangedBy),
            cancellationToken);
        return Result<Guid>.Success(profile.Id);
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
    ProjectPartyAssignmentNodePolicy projectPartyAssignmentNodePolicy) : IProjectPartyIntegrationBridge
{
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
            .Where(item => item.ProjectId == projectId)
            .Select(item => item.PartyId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var parties = await dbContext.Set<Party>()
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
            .Where(item => partyIds.Contains(item.PartyId))
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => new CrmPartyContactValue(item.PartyId, item.ContactType, item.Value, item.IsPrimary))
            .ToListAsync(cancellationToken);
        var contactsByPartyId = contacts
            .GroupBy(item => item.PartyId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<CrmPartyContactValue>)group.ToList());
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
                    ResolvePrimaryContactValue(partyContacts, PartyContactType.Email),
                    ResolvePrimaryContactValue(partyContacts, PartyContactType.Phone),
                    item.IsSensitive);
            })
            .ToList();
    }

    public async Task<ProjectPartyOption?> GetPartyOptionAsync(Guid partyId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var party = await dbContext.Set<Party>()
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
            .Where(item => item.PartyId == partyId)
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => new CrmPartyContactValue(item.PartyId, item.ContactType, item.Value, item.IsPrimary))
            .ToListAsync(cancellationToken);

        return new ProjectPartyOption(
            party.Id,
            party.DisplayName,
            ResolvePartyTypeLabel(party.PartyType),
            ResolvePrimaryContactValue(contacts, PartyContactType.Email),
            ResolvePrimaryContactValue(contacts, PartyContactType.Phone),
            party.IsSensitive);
    }

    public async Task<IReadOnlyList<ProjectPartyAssignmentDetail>> ListAssignmentsDetailedAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<ProjectPartyAssignment>()
            .Where(item => item.ProjectId == projectId)
            .Join(
                dbContext.Set<Party>(),
                assignment => assignment.PartyId,
                party => party.Id,
                (assignment, party) => new
                {
                    assignment.Id,
                    assignment.ProjectId,
                    assignment.PartyId,
                    assignment.AssignmentKind,
                    assignment.NodeKey,
                    assignment.IsPrimary,
                    assignment.AllocationPercent,
                    assignment.StartsAtUtc,
                    assignment.EndsAtUtc,
                    assignment.Notes,
                    party.DisplayName,
                    party.PartyType
                })
            .OrderBy(item => item.NodeKey)
            .ThenBy(item => item.AssignmentKind)
            .ThenByDescending(item => item.IsPrimary)
            .ThenBy(item => item.DisplayName)
            .Select(item => new ProjectPartyAssignmentDetail(
                item.Id,
                item.ProjectId,
                item.PartyId,
                MapRole(item.AssignmentKind),
                item.DisplayName,
                ResolvePartyTypeLabel(item.PartyType),
                item.NodeKey,
                item.IsPrimary,
                item.AllocationPercent,
                item.StartsAtUtc,
                item.EndsAtUtc,
                item.Notes))
            .ToListAsync(cancellationToken);
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

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var projectExists = await dbContext.Set<Project>()
            .AnyAsync(item => item.Id == request.ProjectId, cancellationToken);
        if (!projectExists)
        {
            return Result<Guid>.Failure(Error.Validation("Project was not found.", "crmhr.project-assignment.project-not-found"));
        }

        var partyExists = await dbContext.Set<Party>()
            .AnyAsync(item => item.Id == request.PartyId, cancellationToken);
        if (!partyExists)
        {
            return Result<Guid>.Failure(Error.Validation("Party was not found.", "crmhr.project-assignment.party-not-found"));
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

        if (entity is null)
        {
            entity = new ProjectPartyAssignment();
            dbContext.Set<ProjectPartyAssignment>().Add(entity);
        }

        entity.ProjectId = request.ProjectId;
        entity.PartyId = request.PartyId;
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

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(entity.Id);
    }

    public async Task<Result> ReplaceNodeAssignmentsAsync(
        Guid projectId,
        ProjectNodeReference nodeReference,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(desiredAssignments);
        ArgumentNullException.ThrowIfNull(targetRoles);

        if (projectId == Guid.Empty)
        {
            return Result.Failure(Error.Validation("Project is required.", "crmhr.project-assignment.project-required"));
        }

        var normalizedNodeKey = nodeReference.NodeKey;

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
            var existingPartyIds = await dbContext.Set<Party>()
                .Where(item => desiredPartyIds.Contains(item.Id))
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);
            var existingPartyIdSet = existingPartyIds.ToHashSet();
            if (desiredPartyIds.Any(id => !existingPartyIdSet.Contains(id)))
            {
                return Result.Failure(Error.Validation("Party was not found.", "crmhr.project-assignment.party-not-found"));
            }
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

            dbContext.Set<ProjectPartyAssignment>().Add(new ProjectPartyAssignment
            {
                ProjectId = projectId,
                PartyId = desiredAssignment.Request.PartyId,
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

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task DeleteAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<ProjectPartyAssignment>()
            .SingleOrDefaultAsync(item => item.Id == assignmentId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        dbContext.Set<ProjectPartyAssignment>().Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
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
        var assignments = await dbContext.Set<ProjectPartyAssignment>()
            .Where(item => item.ProjectId == projectId && normalizedNodeKeys.Contains(item.NodeKey))
            .ToListAsync(cancellationToken);
        if (assignments.Count == 0)
        {
            return;
        }

        dbContext.RemoveRange(assignments);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MoveAssignmentsToProjectAsync(
        Guid sourceProjectId,
        IReadOnlyCollection<ProjectNodeReference> nodeReferences,
        Guid targetProjectId,
        CancellationToken cancellationToken = default)
    {
        var normalizedNodeKeys = NormalizeNodeKeys(nodeReferences);
        if (sourceProjectId == Guid.Empty ||
            targetProjectId == Guid.Empty ||
            sourceProjectId == targetProjectId ||
            normalizedNodeKeys.Count == 0)
        {
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
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
        if (assignmentsToMove.Count == 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        foreach (var assignment in assignmentsToMove)
        {
            assignment.ProjectId = targetProjectId;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
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
            _ => ProjectPartyAssignmentKind.TechnicalContact
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
