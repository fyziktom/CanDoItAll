using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Web.Api;

internal static class CrmHrApiContractDefaults
{
    public const string Actor = "crm-hr-api";

    public static string NormalizeContactValue(
        PartyContactType contactType,
        string value)
    {
        var trimmedValue = value.Trim();
        return contactType == PartyContactType.Phone
            ? new string(trimmedValue
                .Where(character => char.IsDigit(character) || character == '+')
                .ToArray())
            : trimmedValue.ToLowerInvariant();
    }
}

internal sealed class CrmHrPartyPageApiQuery
{
    public string? Search { get; init; }

    public string[]? Tags { get; init; }

    public PartyRecordScope? Scope { get; init; }

    public int? PageIndex { get; init; }

    public int? PageSize { get; init; }

    public bool? IncludeArchived { get; init; }

    public PartyRecordQuery ToQuery(PartyRecordPopulation population)
        => new(
            Search ?? string.Empty,
            Tags ?? [],
            Scope ?? PartyRecordScope.All,
            PageIndex ?? 0,
            PageSize ?? PartyRecordQueryLimits.DefaultPageSize,
            ExcludedPartyId: null,
            IncludeArchived ?? false,
            population);
}

internal sealed class CrmHrWorkforcePageApiQuery
{
    private const PartyRecordScope WorkforceScope =
        PartyRecordScope.People |
        PartyRecordScope.Organizations |
        PartyRecordScope.OrganizationUnits;

    public string? Search { get; init; }

    public string[]? Tags { get; init; }

    public int? PageIndex { get; init; }

    public int? PageSize { get; init; }

    public bool? IncludeArchived { get; init; }

    public PartyRecordQuery ToQuery()
        => new(
            Search ?? string.Empty,
            Tags ?? [],
            WorkforceScope,
            PageIndex ?? 0,
            PageSize ?? PartyRecordQueryLimits.DefaultPageSize,
            ExcludedPartyId: null,
            IncludeArchived ?? false,
            PartyRecordPopulation.Workforce);
}

internal sealed class RecruitmentApplicationPageApiQuery
{
    public string? Search { get; init; }

    public RecruitmentApplicationScope? Scope { get; init; }

    public int? PageIndex { get; init; }

    public int? PageSize { get; init; }

    public RecruitmentApplicationQuery ToQuery()
        => new(
            Search ?? string.Empty,
            Scope ?? RecruitmentApplicationScope.All,
            PageIndex ?? 0,
            PageSize ?? RecruitmentApplicationQueryLimits.DefaultPageSize);
}

internal sealed class PartyCreateApiRequest
{
    public PartyType PartyType { get; init; } = PartyType.Person;

    public PartyLifecycleStatus LifecycleStatus { get; init; } = PartyLifecycleStatus.Draft;

    public string DisplayName { get; init; } = string.Empty;

    public string LegalName { get; init; } = string.Empty;

    public string PreferredName { get; init; } = string.Empty;

    public string ExternalCode { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> Tags { get; init; } = [];

    public string Region { get; init; } = string.Empty;

    public string CountryCode { get; init; } = string.Empty;

    public string TimeZone { get; init; } = string.Empty;

    public bool IsSensitive { get; init; }

    public IReadOnlyList<PartyRoleCreateApiRequest> Roles { get; init; } = [];

    public IReadOnlyList<PartyPublicContactCreateApiRequest> PublicContacts { get; init; } = [];

    public IReadOnlyList<PartyAddressCreateApiRequest> Addresses { get; init; } = [];

    public PartyEditorModel ToEditorModel()
        => new()
        {
            PartyType = PartyType,
            LifecycleStatus = LifecycleStatus,
            DisplayName = DisplayName ?? string.Empty,
            LegalName = LegalName ?? string.Empty,
            PreferredName = PreferredName ?? string.Empty,
            ExternalCode = ExternalCode ?? string.Empty,
            Summary = Summary ?? string.Empty,
            Tags = (Tags ?? []).ToList(),
            Region = Region ?? string.Empty,
            CountryCode = CountryCode ?? string.Empty,
            TimeZone = TimeZone ?? string.Empty,
            IsSensitive = IsSensitive,
            ExtendedDataJson = "{}",
            LastChangedBy = CrmHrApiContractDefaults.Actor,
            Roles = (Roles ?? [])
                .Select(role => role.ToEditorModel())
                .ToList(),
            ContactPoints = (PublicContacts ?? [])
                .Select(contact => contact.ToEditorModel())
                .ToList(),
            Addresses = (Addresses ?? [])
                .Select(address => address.ToEditorModel())
                .ToList(),
            ConfidentialNotes = []
        };
}

internal sealed class PartyRoleCreateApiRequest
{
    public PartyRoleKind RoleKind { get; init; }

    public string Title { get; init; } = string.Empty;

    public bool IsPrimary { get; init; }

    public DateTimeOffset? ValidFromUtc { get; init; }

    public DateTimeOffset? ValidToUtc { get; init; }

    public PartyRoleAssignmentEditorModel ToEditorModel()
        => new()
        {
            RoleKind = RoleKind,
            Title = Title ?? string.Empty,
            IsPrimary = IsPrimary,
            ValidFromUtc = ValidFromUtc,
            ValidToUtc = ValidToUtc
        };
}

internal sealed class PartyPublicContactCreateApiRequest
{
    public PartyContactType ContactType { get; init; }

    public string Label { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public bool IsPrimary { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public PartyContactPointEditorModel ToEditorModel()
        => new()
        {
            ContactType = ContactType,
            Label = Label ?? string.Empty,
            Value = Value ?? string.Empty,
            NormalizedValue = CrmHrApiContractDefaults.NormalizeContactValue(
                ContactType,
                Value ?? string.Empty),
            IsPrimary = IsPrimary,
            IsPublic = true,
            Tags = (Tags ?? []).ToList()
        };
}

internal sealed class PartyAddressCreateApiRequest
{
    public string AddressType { get; init; } = string.Empty;

    public string Line1 { get; init; } = string.Empty;

    public string Line2 { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string Region { get; init; } = string.Empty;

    public string PostalCode { get; init; } = string.Empty;

    public string CountryCode { get; init; } = string.Empty;

    public bool IsPrimary { get; init; }

    public PartyAddressEditorModel ToEditorModel()
        => new()
        {
            AddressType = AddressType ?? string.Empty,
            Line1 = Line1 ?? string.Empty,
            Line2 = Line2 ?? string.Empty,
            City = City ?? string.Empty,
            Region = Region ?? string.Empty,
            PostalCode = PostalCode ?? string.Empty,
            CountryCode = CountryCode ?? string.Empty,
            IsPrimary = IsPrimary
        };
}

internal sealed class PartyRelationshipsReplaceApiRequest
{
    public IReadOnlyList<PartyRelationshipReplaceItemApiRequest> Relationships { get; init; } = [];
}

internal sealed class PartyRelationshipReplaceItemApiRequest
{
    public Guid RelatedPartyId { get; init; }

    public PartyRelationshipKind RelationshipKind { get; init; } = PartyRelationshipKind.MemberOf;

    public bool IsOutgoing { get; init; } = true;

    public bool IsPrimary { get; init; }

    public DateTimeOffset? StartDateUtc { get; init; }

    public DateTimeOffset? EndDateUtc { get; init; }

    public string Notes { get; init; } = string.Empty;

    public PartyRelationshipEditorModel ToEditorModel()
        => new()
        {
            RelatedPartyId = RelatedPartyId,
            RelationshipKind = RelationshipKind,
            IsOutgoing = IsOutgoing,
            IsPrimary = IsPrimary,
            StartDateUtc = StartDateUtc?.ToUniversalTime(),
            EndDateUtc = EndDateUtc?.ToUniversalTime(),
            Notes = Notes ?? string.Empty
        };
}

internal sealed class WorkforceProfileSaveApiRequest
{
    public Guid PartyId { get; init; }

    public WorkforceKind WorkforceKind { get; init; } = WorkforceKind.Employee;

    public string EmployeeCode { get; init; } = string.Empty;

    public string JobTitle { get; init; } = string.Empty;

    public string Discipline { get; init; } = string.Empty;

    public string Seniority { get; init; } = string.Empty;

    public Guid? HomeUnitPartyId { get; init; }

    public Guid? ManagerPartyId { get; init; }

    public DateOnly? StartDate { get; init; }

    public DateOnly? EndDate { get; init; }

    public string Location { get; init; } = string.Empty;

    public string TimeZone { get; init; } = string.Empty;

    public decimal? InternalCostRate { get; init; }

    public decimal? ExternalBillingRate { get; init; }

    public ProjectResourceRateUnit RateUnit { get; init; } = ProjectResourceRateUnit.Hour;

    public string RateCurrencyCode { get; init; } = "USD";

    public decimal CapacityHoursPerWeek { get; init; } = 40m;

    public string Status { get; init; } = "Planned";

    public string Notes { get; init; } = string.Empty;

    public WorkforceProfileEditorModel ToEditorModel()
        => new()
        {
            PartyId = PartyId,
            WorkforceKind = WorkforceKind,
            EmployeeCode = EmployeeCode ?? string.Empty,
            JobTitle = JobTitle ?? string.Empty,
            Discipline = Discipline ?? string.Empty,
            Seniority = Seniority ?? string.Empty,
            HomeUnitPartyId = HomeUnitPartyId,
            ManagerPartyId = ManagerPartyId,
            StartDate = StartDate,
            EndDate = EndDate,
            Location = Location ?? string.Empty,
            TimeZone = TimeZone ?? string.Empty,
            InternalCostRate = InternalCostRate,
            ExternalBillingRate = ExternalBillingRate,
            RateUnit = RateUnit,
            RateCurrencyCode = RateCurrencyCode ?? string.Empty,
            CapacityHoursPerWeek = CapacityHoursPerWeek,
            Status = Status ?? string.Empty,
            Notes = Notes ?? string.Empty,
            LastChangedBy = CrmHrApiContractDefaults.Actor
        };
}

internal sealed class SkillDefinitionSaveApiRequest
{
    public Guid? Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;

    public SkillDefinitionEditorModel ToEditorModel()
        => new()
        {
            Id = Id,
            Name = Name ?? string.Empty,
            Category = Category ?? string.Empty,
            Description = Description ?? string.Empty,
            IsActive = IsActive
        };
}

internal sealed class PartySkillSaveApiRequest
{
    public Guid? Id { get; init; }

    public Guid PartyId { get; init; }

    public Guid SkillId { get; init; }

    public SkillProficiencyLevel Proficiency { get; init; } = SkillProficiencyLevel.Basic;

    public int YearsExperience { get; init; }

    public string CertificationStatus { get; init; } = string.Empty;

    public DateOnly? LastValidatedOn { get; init; }

    public string Notes { get; init; } = string.Empty;

    public PartySkillEditorModel ToEditorModel()
        => new()
        {
            Id = Id,
            PartyId = PartyId,
            SkillId = SkillId,
            Proficiency = Proficiency,
            YearsExperience = YearsExperience,
            CertificationStatus = CertificationStatus ?? string.Empty,
            LastValidatedOn = LastValidatedOn,
            Notes = Notes ?? string.Empty
        };
}

internal sealed class CapacityBlockSaveApiRequest
{
    public Guid? Id { get; init; }

    public Guid PartyId { get; init; }

    public CapacityBlockKind BlockKind { get; init; } = CapacityBlockKind.Leave;

    public DateOnly? StartDate { get; init; }

    public DateOnly? EndDate { get; init; }

    public decimal Percentage { get; init; } = 100m;

    public Guid? RelatedProjectId { get; init; }

    public string Notes { get; init; } = string.Empty;

    public CapacityBlockEditorModel ToEditorModel()
        => new()
        {
            Id = Id,
            PartyId = PartyId,
            BlockKind = BlockKind,
            StartDate = StartDate,
            EndDate = EndDate,
            Percentage = Percentage,
            RelatedProjectId = RelatedProjectId,
            Notes = Notes ?? string.Empty
        };
}

internal sealed class RecruitmentApplicationSaveApiRequest
{
    public Guid? Id { get; init; }

    public Guid? PartyId { get; init; }

    public string CandidateName { get; init; } = string.Empty;

    public string CandidateEmail { get; init; } = string.Empty;

    public string CandidatePhone { get; init; } = string.Empty;

    public string CandidateSummary { get; init; } = string.Empty;

    public Guid? TargetUnitPartyId { get; init; }

    public Guid? RecruiterPartyId { get; init; }

    public Guid? HiringManagerPartyId { get; init; }

    public string DesiredRole { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public RecruitmentStage Stage { get; init; } = RecruitmentStage.Applied;

    public DateOnly? AvailableFrom { get; init; }

    public RecruitmentDecision Decision { get; init; } = RecruitmentDecision.Pending;

    public string StageNotes { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;

    public RecruitmentApplicationEditorModel ToEditorModel()
        => new()
        {
            Id = Id,
            PartyId = PartyId,
            CandidateName = CandidateName ?? string.Empty,
            CandidateEmail = CandidateEmail ?? string.Empty,
            CandidatePhone = CandidatePhone ?? string.Empty,
            CandidateSummary = CandidateSummary ?? string.Empty,
            TargetUnitPartyId = TargetUnitPartyId,
            RecruiterPartyId = RecruiterPartyId,
            HiringManagerPartyId = HiringManagerPartyId,
            DesiredRole = DesiredRole ?? string.Empty,
            Source = Source ?? string.Empty,
            Stage = Stage,
            AvailableFrom = AvailableFrom,
            Decision = Decision,
            StageNotes = StageNotes ?? string.Empty,
            Notes = Notes ?? string.Empty,
            LastChangedBy = CrmHrApiContractDefaults.Actor
        };
}

internal sealed class RecruitmentInterviewSaveApiRequest
{
    public Guid? Id { get; init; }

    public Guid ApplicationId { get; init; }

    public DateTimeOffset? ScheduledAtUtc { get; init; }

    public RecruitmentInterviewType InterviewType { get; init; } = RecruitmentInterviewType.Screening;

    public Guid? InterviewerPartyId { get; init; }

    public RecruitmentInterviewOutcome Outcome { get; init; } = RecruitmentInterviewOutcome.Pending;

    public string Feedback { get; init; } = string.Empty;

    public string Recommendation { get; init; } = string.Empty;

    public RecruitmentInterviewEditorModel ToEditorModel()
        => new()
        {
            Id = Id,
            ApplicationId = ApplicationId,
            ScheduledAtLocal = ScheduledAtUtc?.UtcDateTime,
            InterviewType = InterviewType,
            InterviewerPartyId = InterviewerPartyId,
            Outcome = Outcome,
            Feedback = Feedback ?? string.Empty,
            Recommendation = Recommendation ?? string.Empty
        };
}

internal sealed class LifecycleTaskSaveApiRequest
{
    public Guid? Id { get; init; }

    public Guid PartyId { get; init; }

    public LifecycleTaskKind TaskKind { get; init; } = LifecycleTaskKind.Onboarding;

    public string Title { get; init; } = string.Empty;

    public Guid? OwnerPartyId { get; init; }

    public DateOnly? DueDate { get; init; }

    public LifecycleTaskStatus Status { get; init; } = LifecycleTaskStatus.NotStarted;

    public Guid? RelatedProjectId { get; init; }

    public string Notes { get; init; } = string.Empty;

    public LifecycleTaskEditorModel ToEditorModel()
        => new()
        {
            Id = Id,
            PartyId = PartyId,
            TaskKind = TaskKind,
            Title = Title ?? string.Empty,
            OwnerPartyId = OwnerPartyId,
            DueDate = DueDate,
            Status = Status,
            RelatedProjectId = RelatedProjectId,
            Notes = Notes ?? string.Empty
        };
}

internal sealed class RecruitmentSupportAssignmentsSaveApiRequest
{
    public Guid PartyId { get; init; }

    public Guid? ManagerPartyId { get; init; }

    public Guid? BuddyPartyId { get; init; }

    public Guid? MentorPartyId { get; init; }

    public RecruitmentSupportAssignmentsEditorModel ToEditorModel()
        => new()
        {
            PartyId = PartyId,
            ManagerPartyId = ManagerPartyId,
            BuddyPartyId = BuddyPartyId,
            MentorPartyId = MentorPartyId,
            LastChangedBy = CrmHrApiContractDefaults.Actor
        };
}

internal sealed class RecruitmentConversionApiRequest
{
    public Guid ApplicationId { get; init; }

    public WorkforceKind WorkforceKind { get; init; } = WorkforceKind.Employee;

    public string JobTitle { get; init; } = string.Empty;

    public string Discipline { get; init; } = string.Empty;

    public string Seniority { get; init; } = string.Empty;

    public Guid? HomeUnitPartyId { get; init; }

    public Guid? ManagerPartyId { get; init; }

    public DateOnly? StartDate { get; init; }

    public string Location { get; init; } = string.Empty;

    public string TimeZone { get; init; } = string.Empty;

    public decimal CapacityHoursPerWeek { get; init; } = 40m;

    public string Status { get; init; } = "Active";

    public string Notes { get; init; } = string.Empty;

    public RecruitmentConversionEditorModel ToEditorModel()
        => new()
        {
            ApplicationId = ApplicationId,
            WorkforceKind = WorkforceKind,
            JobTitle = JobTitle ?? string.Empty,
            Discipline = Discipline ?? string.Empty,
            Seniority = Seniority ?? string.Empty,
            HomeUnitPartyId = HomeUnitPartyId,
            ManagerPartyId = ManagerPartyId,
            StartDate = StartDate,
            Location = Location ?? string.Empty,
            TimeZone = TimeZone ?? string.Empty,
            CapacityHoursPerWeek = CapacityHoursPerWeek,
            Status = Status ?? string.Empty,
            Notes = Notes ?? string.Empty,
            LastChangedBy = CrmHrApiContractDefaults.Actor
        };
}
