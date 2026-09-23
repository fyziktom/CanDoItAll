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

/// <summary>
/// Query-string filters for listing CRM/HR parties. Every filter is optional.
/// </summary>
internal sealed class CrmHrPartyPageApiQuery
{
    /// <summary>
    /// Text matched case-insensitively as a substring of the display name and, for parties that are not sensitive,
    /// of the external code and summary. Surrounding whitespace is ignored; at most 200 characters. Omitted or empty
    /// applies no text filter.
    /// </summary>
    public string? Search { get; init; }

    /// <summary>
    /// Tags a party must all have (whole-tag, case-insensitive match). Repeat the parameter for several tags, for
    /// example <c>?Tags=vip&amp;Tags=partner</c>. Blank values are ignored and duplicates merged; at most 20 distinct
    /// tags. Sensitive parties never match a tag filter.
    /// </summary>
    public string[]? Tags { get; init; }

    /// <summary>
    /// Party types to include, as a flags value: 1 People, 2 Organizations, 4 OrganizationUnits, 8 AiAgents; add the
    /// values to combine types (for example 3 for people and organizations). Omitted means all types (15). A value
    /// with no supported type, or with other bits, is rejected.
    /// </summary>
    public PartyRecordScope? Scope { get; init; }

    /// <summary>
    /// Zero-based page number. Omitted means 0; must not be negative.
    /// </summary>
    public int? PageIndex { get; init; }

    /// <summary>
    /// Number of parties per page, from 1 through 100. Omitted means 24.
    /// </summary>
    public int? PageSize { get; init; }

    /// <summary>
    /// True to include archived parties. Omitted or false excludes them.
    /// </summary>
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

/// <summary>
/// Query-string filters for listing the workforce population of CRM/HR parties. Every filter is optional.
/// </summary>
internal sealed class CrmHrWorkforcePageApiQuery
{
    private const PartyRecordScope WorkforceScope =
        PartyRecordScope.People |
        PartyRecordScope.Organizations |
        PartyRecordScope.OrganizationUnits;

    /// <summary>
    /// Text matched case-insensitively as a substring of the display name and, for parties that are not sensitive,
    /// of the external code and summary. Surrounding whitespace is ignored; at most 200 characters. Omitted or empty
    /// applies no text filter.
    /// </summary>
    public string? Search { get; init; }

    /// <summary>
    /// Tags a party must all have (whole-tag, case-insensitive match). Repeat the parameter for several tags, for
    /// example <c>?Tags=backend&amp;Tags=remote</c>. Blank values are ignored and duplicates merged; at most 20
    /// distinct tags. Sensitive parties never match a tag filter.
    /// </summary>
    public string[]? Tags { get; init; }

    /// <summary>
    /// Zero-based page number. Omitted means 0; must not be negative.
    /// </summary>
    public int? PageIndex { get; init; }

    /// <summary>
    /// Number of parties per page, from 1 through 100. Omitted means 24.
    /// </summary>
    public int? PageSize { get; init; }

    /// <summary>
    /// True to include archived parties. Omitted or false excludes them.
    /// </summary>
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

/// <summary>
/// Query-string filters for listing recruitment applications. Every filter is optional.
/// </summary>
internal sealed class RecruitmentApplicationPageApiQuery
{
    /// <summary>
    /// Text matched case-insensitively as a substring of the desired role, the source, the display names of the
    /// candidate, recruiter, hiring manager and target unit, and the candidate's public email addresses. Surrounding
    /// whitespace is ignored; at most 200 characters. Omitted or empty applies no text filter.
    /// </summary>
    public string? Search { get; init; }

    /// <summary>
    /// Stage to include, as the case-sensitive member name (for example <c>Interviewing</c>) or its integer value:
    /// 0 All, 1 Applied, 2 Screening, 3 Interviewing, 4 Offer, 5 Hired, 6 Rejected, 7 Withdrawn. Omitted means All.
    /// </summary>
    public RecruitmentApplicationScope? Scope { get; init; }

    /// <summary>
    /// Zero-based page number. Omitted means 0; must not be negative.
    /// </summary>
    public int? PageIndex { get; init; }

    /// <summary>
    /// Number of applications per page, from 1 through 100. Omitted means 12.
    /// </summary>
    public int? PageSize { get; init; }

    public RecruitmentApplicationQuery ToQuery()
        => new(
            Search ?? string.Empty,
            Scope ?? RecruitmentApplicationScope.All,
            PageIndex ?? 0,
            PageSize ?? RecruitmentApplicationQueryLimits.DefaultPageSize);
}

/// <summary>
/// Request of <c>POST /api/crm-hr/parties</c>: a new CRM/HR party with its roles, public contact points and addresses.
/// A party is always created, never updated. Every member is optional except <c>displayName</c>; an omitted member
/// takes the default stated on it, and null text or lists are treated as empty.
/// </summary>
internal sealed class PartyCreateApiRequest
{
    /// <summary>
    /// Kind of party to create, as a JSON integer: 0 Person, 1 Organization, 2 OrganizationUnit, 3 AiAgent. Omitted
    /// means Person (0).
    /// </summary>
    public PartyType PartyType { get; init; } = PartyType.Person;

    /// <summary>
    /// Initial lifecycle status of the party, as a JSON integer: 0 Draft, 1 Active, 2 Inactive, 3 Archived, 4 Former,
    /// 5 Candidate, 6 Prospect. Omitted means Draft (0).
    /// </summary>
    public PartyLifecycleStatus LifecycleStatus { get; init; } = PartyLifecycleStatus.Draft;

    /// <summary>
    /// Name shown for the party in lists and lookups; required and trimmed. It is returned for sensitive parties too.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Registered legal name, for example of a company; trimmed. Omitted means empty.
    /// </summary>
    public string LegalName { get; init; } = string.Empty;

    /// <summary>
    /// Name the party prefers to be addressed by; trimmed. Omitted means empty.
    /// </summary>
    public string PreferredName { get; init; } = string.Empty;

    /// <summary>
    /// Business reference from another system, for example an ERP or customer number; trimmed. It is not checked for
    /// uniqueness, so it is not an idempotency key. It is searchable and returned only while the party is not
    /// sensitive.
    /// </summary>
    public string ExternalCode { get; init; } = string.Empty;

    /// <summary>
    /// Short description of the party; trimmed. Withheld from directory reads while the party is sensitive.
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Free-text labels used by tag filters. Trimmed; blank tags and case-insensitive duplicates are dropped. Omitted
    /// means no tags. Withheld from directory reads while the party is sensitive.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Region or state of the party as free text; trimmed, not validated.
    /// </summary>
    public string Region { get; init; } = string.Empty;

    /// <summary>
    /// Country of the party as free text, for example a two-letter code such as <c>CZ</c>; trimmed, not validated.
    /// </summary>
    public string CountryCode { get; init; } = string.Empty;

    /// <summary>
    /// Time zone of the party as free text, for example an IANA name such as <c>Europe/Prague</c>; trimmed, not
    /// validated.
    /// </summary>
    public string TimeZone { get; init; } = string.Empty;

    /// <summary>
    /// True marks the party for restricted handling: directory reads return its external code, summary and tags empty
    /// and the party is kept out of the application search index. Omitted means false. Other reads, such as the
    /// workforce and recruitment workspaces, still return some of its data.
    /// </summary>
    public bool IsSensitive { get; init; }

    /// <summary>
    /// Business roles of the party, for example customer, employee or recruiter. Omitted means none. These are CRM/HR
    /// classifications, not authorization roles.
    /// </summary>
    public IReadOnlyList<PartyRoleCreateApiRequest> Roles { get; init; } = [];

    /// <summary>
    /// Contact points of the party, such as email addresses and phone numbers. Each is stored with the public flag, a
    /// data classification that grants no access. At most one contact point per contact type can be primary. Omitted
    /// means none.
    /// </summary>
    public IReadOnlyList<PartyPublicContactCreateApiRequest> PublicContacts { get; init; } = [];

    /// <summary>
    /// Postal addresses of the party. Omitted means none.
    /// </summary>
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

/// <summary>
/// A business role of a new party, for example customer, employee or recruiter. It classifies the party in CRM/HR; it
/// is not an authorization role or a task assignment.
/// </summary>
internal sealed class PartyRoleCreateApiRequest
{
    /// <summary>
    /// Kind of role, as a JSON integer: 0 Customer, 1 CustomerContact, 2 Partner, 3 Vendor, 4 Employee, 5 Contractor,
    /// 6 Freelancer, 7 DeliveryUnit, 8 Candidate, 9 AiSteward, 10 AccountManager, 11 Recruiter, 12 Stakeholder. Omitted
    /// means Customer (0).
    /// </summary>
    public PartyRoleKind RoleKind { get; init; }

    /// <summary>
    /// Title of the role for this party, for example <c>Key account</c>; trimmed. Omitted means empty.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// True marks the party's main role; not checked for uniqueness. Omitted means false.
    /// </summary>
    public bool IsPrimary { get; init; }

    /// <summary>
    /// Start of the role's validity as an instant with an offset. Null or omitted means no stated start.
    /// </summary>
    public DateTimeOffset? ValidFromUtc { get; init; }

    /// <summary>
    /// End of the role's validity as an instant with an offset; not checked against the start. Null or omitted means no
    /// stated end.
    /// </summary>
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

/// <summary>
/// A contact point of a new party, such as an email address or a phone number. It is stored with the public flag set;
/// the flag classifies the contact data and does not make it readable without authorization. A contact point is not
/// itself a party or a contact person.
/// </summary>
internal sealed class PartyPublicContactCreateApiRequest
{
    /// <summary>
    /// Kind of contact value, as a JSON integer: 0 Email, 1 Phone, 2 Website, 3 Messaging, 4 Social, 5 Other. Omitted
    /// means Email (0).
    /// </summary>
    public PartyContactType ContactType { get; init; }

    /// <summary>
    /// Label of the contact point, for example <c>Work</c> or <c>Mobile</c>; trimmed. Omitted means empty.
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// The contact value, for example <c>jordan.lee@example.com</c> or <c>+420 555 010 010</c>; trimmed and stored as
    /// sent. Its format is not validated.
    /// </summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// True marks the party's primary contact point of this contact type; at most one per contact type. Workspace and
    /// list reads prefer the primary email and phone. Omitted means false.
    /// </summary>
    public bool IsPrimary { get; init; }

    /// <summary>
    /// Labels of the contact point; trimmed, blank values and case-insensitive duplicates dropped. Omitted means none.
    /// </summary>
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

/// <summary>
/// A postal address of a new party. Every text field is free text, trimmed and not validated; omitted means empty.
/// </summary>
internal sealed class PartyAddressCreateApiRequest
{
    /// <summary>
    /// Kind of address as free text, for example <c>Billing</c> or <c>Office</c>.
    /// </summary>
    public string AddressType { get; init; } = string.Empty;

    /// <summary>
    /// First address line, usually street and number.
    /// </summary>
    public string Line1 { get; init; } = string.Empty;

    /// <summary>
    /// Second address line, for example a building or floor.
    /// </summary>
    public string Line2 { get; init; } = string.Empty;

    /// <summary>
    /// City or town.
    /// </summary>
    public string City { get; init; } = string.Empty;

    /// <summary>
    /// Region, state or province.
    /// </summary>
    public string Region { get; init; } = string.Empty;

    /// <summary>
    /// Postal code.
    /// </summary>
    public string PostalCode { get; init; } = string.Empty;

    /// <summary>
    /// Country as free text, for example a two-letter code such as <c>CZ</c>.
    /// </summary>
    public string CountryCode { get; init; } = string.Empty;

    /// <summary>
    /// True marks the party's main address; not checked for uniqueness. Omitted means false.
    /// </summary>
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

/// <summary>
/// Request of <c>PUT /api/crm-hr/parties/{partyId}/relationships</c>: the complete intended list of the party's
/// relationships. It replaces every existing relationship in which the party is the source or the target.
/// </summary>
internal sealed class PartyRelationshipsReplaceApiRequest
{
    /// <summary>
    /// Every relationship the party should have after the call, described from the route party's point of view. An
    /// empty array, or omitting the member, removes all relationships of the party. Do not send null.
    /// </summary>
    public IReadOnlyList<PartyRelationshipReplaceItemApiRequest> Relationships { get; init; } = [];
}

/// <summary>
/// One relationship of a relationship replacement, described from the point of view of the party in the route. Copy
/// the fields of an item of <c>GET /api/crm-hr/parties/{partyId}/relationships</c> to keep a relationship.
/// </summary>
internal sealed class PartyRelationshipReplaceItemApiRequest
{
    /// <summary>
    /// Identifier of the other party, as returned by <c>GET /api/crm-hr/parties</c>; required. It must exist and must
    /// differ from the route party.
    /// </summary>
    public Guid RelatedPartyId { get; init; }

    /// <summary>
    /// Kind of relationship, read as source, kind, target, as a JSON integer: 0 MemberOf, 1 PartOf, 2 ReportsTo,
    /// 3 CustomerOf, 4 PartnerOf, 5 VendorTo, 6 Represents, 7 ManagedBy, 8 OwnedBy, 9 Supports. Omitted means
    /// MemberOf (0).
    /// </summary>
    public PartyRelationshipKind RelationshipKind { get; init; } = PartyRelationshipKind.MemberOf;

    /// <summary>
    /// True when the route party is the source (for MemberOf: the route party is a member of the related party); false
    /// when the related party is the source. Omitted means true.
    /// </summary>
    public bool IsOutgoing { get; init; } = true;

    /// <summary>
    /// True marks the relationship as primary; not checked for uniqueness. Omitted means false.
    /// </summary>
    public bool IsPrimary { get; init; }

    /// <summary>
    /// Start of the relationship as an instant with an offset; converted to UTC before saving. Null or omitted means no
    /// stated start.
    /// </summary>
    public DateTimeOffset? StartDateUtc { get; init; }

    /// <summary>
    /// End of the relationship as an instant with an offset; converted to UTC and not checked against the start. Null
    /// or omitted means no stated end.
    /// </summary>
    public DateTimeOffset? EndDateUtc { get; init; }

    /// <summary>
    /// Free-text notes; trimmed. Supports relationships whose notes are exactly <c>Buddy</c> or <c>Mentor</c> are the
    /// recruiting buddy and mentor assignments, so keep these notes when you copy such a relationship.
    /// </summary>
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

/// <summary>
/// Request of <c>POST /api/crm-hr/workforce/profiles</c>: every field of a party's workforce profile. The profile is
/// found by <c>partyId</c> and all of its fields are overwritten; an omitted member takes the default stated on it, so
/// send every field you want to keep. Start from <c>profile</c> of <c>GET /api/crm-hr/workforce/{partyId}</c>.
/// </summary>
internal sealed class WorkforceProfileSaveApiRequest
{
    /// <summary>
    /// Identifier of the party whose profile is saved, as returned by <c>GET /api/crm-hr/workforce</c> or
    /// <c>GET /api/crm-hr/parties</c>; required. Not a workforce profile identifier.
    /// </summary>
    public Guid PartyId { get; init; }

    /// <summary>
    /// Work classification, as a JSON integer: 0 Employee, 1 Contractor, 2 Freelancer, 3 DeliveryUnit. People use the
    /// first three; organizations and organization units must use DeliveryUnit. Omitted means Employee (0). The save
    /// adds the matching party role when it is missing.
    /// </summary>
    public WorkforceKind WorkforceKind { get; init; } = WorkforceKind.Employee;

    /// <summary>
    /// Personnel number or similar code from an HR system; trimmed, not checked for uniqueness. Omitted means empty.
    /// </summary>
    public string EmployeeCode { get; init; } = string.Empty;

    /// <summary>
    /// Job title; trimmed. Omitted means empty.
    /// </summary>
    public string JobTitle { get; init; } = string.Empty;

    /// <summary>
    /// Professional discipline as free text, for example <c>Engineering</c>; trimmed. Omitted means empty.
    /// </summary>
    public string Discipline { get; init; } = string.Empty;

    /// <summary>
    /// Seniority level as free text, for example <c>Senior</c>; trimmed. Omitted means empty.
    /// </summary>
    public string Seniority { get; init; } = string.Empty;

    /// <summary>
    /// Identifier of the organization or organization unit the party belongs to; it must be an existing organization or
    /// organization unit other than the party itself. Null or omitted means none.
    /// </summary>
    public Guid? HomeUnitPartyId { get; init; }

    /// <summary>
    /// Identifier of the person who manages the party; it must be another existing person. Null or omitted means none.
    /// It is separate from the manager support assignment of recruiting.
    /// </summary>
    public Guid? ManagerPartyId { get; init; }

    /// <summary>
    /// Date the work relationship starts, without time (<c>yyyy-MM-dd</c>). Null or omitted means not set.
    /// </summary>
    public DateOnly? StartDate { get; init; }

    /// <summary>
    /// Date the work relationship ends (<c>yyyy-MM-dd</c>); not checked against the start. Null or omitted means
    /// open-ended.
    /// </summary>
    public DateOnly? EndDate { get; init; }

    /// <summary>
    /// Work location as free text; trimmed. Omitted means empty.
    /// </summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>
    /// Time zone as free text, for example <c>Europe/Prague</c>; trimmed, not validated. Omitted means empty.
    /// </summary>
    public string TimeZone { get; init; } = string.Empty;

    /// <summary>
    /// Internal cost of one <c>rateUnit</c> of the party's work in <c>rateCurrencyCode</c>, for example the cost of one
    /// hour; not negative. It is a rate, not a task or project total. Null or omitted means unknown.
    /// </summary>
    public decimal? InternalCostRate { get; init; }

    /// <summary>
    /// Price billed to customers for one <c>rateUnit</c> of the party's work in <c>rateCurrencyCode</c>; not negative
    /// and separate from the internal cost. Null or omitted means unknown.
    /// </summary>
    public decimal? ExternalBillingRate { get; init; }

    /// <summary>
    /// Time unit that both rates refer to, as a JSON integer: 0 Hour, 1 ManDay. Omitted means Hour (0).
    /// </summary>
    public ProjectResourceRateUnit RateUnit { get; init; } = ProjectResourceRateUnit.Hour;

    /// <summary>
    /// Currency of both rates as three letters, for example <c>EUR</c>; trimmed and upper-cased. A format check only:
    /// any three letters A to Z are accepted. Blank or omitted means <c>USD</c>.
    /// </summary>
    public string RateCurrencyCode { get; init; } = "USD";

    /// <summary>
    /// Weekly working capacity in hours; 0 or less is saved as 40. Omitted means 40.
    /// </summary>
    public decimal CapacityHoursPerWeek { get; init; } = 40m;

    /// <summary>
    /// Employment status as free text, for example <c>Planned</c>, <c>Active</c> or <c>Inactive</c>; trimmed. Omitted
    /// means <c>Planned</c>.
    /// </summary>
    public string Status { get; init; } = "Planned";

    /// <summary>
    /// Free-text notes; trimmed. Omitted means empty.
    /// </summary>
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

/// <summary>
/// Request of <c>POST /api/crm-hr/workforce/skills</c>: a skill definition of the workforce skill catalog to create or
/// update. All fields are overwritten.
/// </summary>
internal sealed class SkillDefinitionSaveApiRequest
{
    /// <summary>
    /// Identifier of the skill definition to update, from <c>GET /api/crm-hr/workforce/skills</c>. Omit it to create a
    /// definition or to update the one with the same name. An unknown identifier is never used for a new definition.
    /// </summary>
    public Guid? Id { get; init; }

    /// <summary>
    /// Skill name, unique in the catalog; required and trimmed. Without a known <c>id</c>, the definition with this
    /// exact name is updated instead of creating a new one.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Category that groups skills, for example <c>Backend</c>; free text, trimmed. Omitted means empty.
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Description of the skill; free text, trimmed. Omitted means empty.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// False deactivates the skill: it stays in the catalog, listed after the active skills, and party skills that
    /// refer to it are kept. Omitted means true.
    /// </summary>
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

/// <summary>
/// Request of <c>POST /api/crm-hr/workforce/party-skills</c>: a party's proficiency in one catalog skill. All fields
/// are overwritten.
/// </summary>
internal sealed class PartySkillSaveApiRequest
{
    /// <summary>
    /// Identifier of the party skill to update, from <c>skills[].id</c> of the workforce workspace. Omit it to update
    /// the record of the same party and skill, or to create one. An unknown identifier is never used for a new record.
    /// </summary>
    public Guid? Id { get; init; }

    /// <summary>
    /// Identifier of the party that has the skill; required and must exist.
    /// </summary>
    public Guid PartyId { get; init; }

    /// <summary>
    /// Identifier of the skill definition from <c>GET /api/crm-hr/workforce/skills</c>; required and must exist.
    /// </summary>
    public Guid SkillId { get; init; }

    /// <summary>
    /// Proficiency level, lowest first, as a JSON integer: 0 Basic, 1 Working, 2 Strong, 3 Expert. Omitted means
    /// Basic (0).
    /// </summary>
    public SkillProficiencyLevel Proficiency { get; init; } = SkillProficiencyLevel.Basic;

    /// <summary>
    /// Years of experience with the skill, a whole number; negative values are saved as 0. Omitted means 0.
    /// </summary>
    public int YearsExperience { get; init; }

    /// <summary>
    /// Certification state as free text, for example <c>Certified</c>; trimmed. Omitted means empty.
    /// </summary>
    public string CertificationStatus { get; init; } = string.Empty;

    /// <summary>
    /// Date the proficiency was last checked (<c>yyyy-MM-dd</c>). Null or omitted means never.
    /// </summary>
    public DateOnly? LastValidatedOn { get; init; }

    /// <summary>
    /// Free-text notes; trimmed. Omitted means empty.
    /// </summary>
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

/// <summary>
/// Request of <c>POST /api/crm-hr/workforce/capacity-blocks</c>: a date range in which a percentage of a party's
/// capacity is not available. All fields are overwritten.
/// </summary>
internal sealed class CapacityBlockSaveApiRequest
{
    /// <summary>
    /// Identifier of the block to update, from <c>capacityBlocks[].id</c> of the workforce workspace. Omit it to create
    /// a block; an unknown identifier creates a new block with a new identifier.
    /// </summary>
    public Guid? Id { get; init; }

    /// <summary>
    /// Identifier of the party whose capacity is blocked; required and must exist.
    /// </summary>
    public Guid PartyId { get; init; }

    /// <summary>
    /// Reason for the block, as a JSON integer: 0 Leave, 1 Unavailable, 2 Reserve, 3 Tentative; every kind reduces
    /// availability in the same way. Omitted means Leave (0).
    /// </summary>
    public CapacityBlockKind BlockKind { get; init; } = CapacityBlockKind.Leave;

    /// <summary>
    /// First day of the block (<c>yyyy-MM-dd</c>); required.
    /// </summary>
    public DateOnly? StartDate { get; init; }

    /// <summary>
    /// Last day of the block, inclusive (<c>yyyy-MM-dd</c>); required and not before <c>startDate</c>.
    /// </summary>
    public DateOnly? EndDate { get; init; }

    /// <summary>
    /// Share of the party's capacity that the block takes, in percent: greater than 0 and at most 100. Omitted means
    /// 100.
    /// </summary>
    public decimal Percentage { get; init; } = 100m;

    /// <summary>
    /// Project the block relates to, from <c>GET /api/projects</c>; it must exist. It is informational and changes no
    /// project allocation. Null or omitted means none.
    /// </summary>
    public Guid? RelatedProjectId { get; init; }

    /// <summary>
    /// Free-text notes; trimmed. Omitted means empty.
    /// </summary>
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

/// <summary>
/// Request of <c>POST /api/crm-hr/recruiting/applications</c>: a recruitment application and its candidate. All
/// application fields are overwritten; when updating, start from <c>application</c> of the recruitment workspace.
/// </summary>
internal sealed class RecruitmentApplicationSaveApiRequest
{
    /// <summary>
    /// Identifier of the application to update, from <c>GET /api/crm-hr/recruiting/applications</c>. Omit it to create
    /// an application; an unknown identifier creates a new application with a new identifier.
    /// </summary>
    public Guid? Id { get; init; }

    /// <summary>
    /// Identifier of the candidate's existing person or AI agent party. When null or omitted, a new person party is
    /// created from <c>candidateName</c> and the other candidate fields, also when updating an application; so when
    /// updating, always send it.
    /// </summary>
    public Guid? PartyId { get; init; }

    /// <summary>
    /// Display name of a new candidate party; required when <c>partyId</c> is omitted and ignored otherwise.
    /// </summary>
    public string CandidateName { get; init; } = string.Empty;

    /// <summary>
    /// Email address of a new candidate party, stored as its primary public email contact point; ignored when
    /// <c>partyId</c> is sent.
    /// </summary>
    public string CandidateEmail { get; init; } = string.Empty;

    /// <summary>
    /// Phone number of a new candidate party, stored as a public phone contact point (primary when no email is sent);
    /// ignored when <c>partyId</c> is sent.
    /// </summary>
    public string CandidatePhone { get; init; } = string.Empty;

    /// <summary>
    /// Summary of a new candidate party; ignored when <c>partyId</c> is sent.
    /// </summary>
    public string CandidateSummary { get; init; } = string.Empty;

    /// <summary>
    /// Organization or organization unit the candidate would join; it must be an existing organization or organization
    /// unit. Null or omitted means none.
    /// </summary>
    public Guid? TargetUnitPartyId { get; init; }

    /// <summary>
    /// Person who recruits the candidate; it must be an existing person. Null or omitted means none.
    /// </summary>
    public Guid? RecruiterPartyId { get; init; }

    /// <summary>
    /// Person who decides on the hire; it must be an existing person other than the candidate. Null or omitted means
    /// none.
    /// </summary>
    public Guid? HiringManagerPartyId { get; init; }

    /// <summary>
    /// Role the candidate applies for; required and trimmed.
    /// </summary>
    public string DesiredRole { get; init; } = string.Empty;

    /// <summary>
    /// Where the application came from, for example <c>Referral</c>; free text, trimmed. Omitted means empty.
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// Current stage of the application, as a JSON integer: 0 Applied, 1 Screening, 2 Interviewing, 3 Offer, 4 Hired,
    /// 5 Rejected, 6 Withdrawn. Stored as sent without transition checks. Omitted means Applied (0).
    /// </summary>
    public RecruitmentStage Stage { get; init; } = RecruitmentStage.Applied;

    /// <summary>
    /// Date the candidate can start (<c>yyyy-MM-dd</c>). Null or omitted means unknown.
    /// </summary>
    public DateOnly? AvailableFrom { get; init; }

    /// <summary>
    /// Current hiring decision, as a JSON integer: 0 Pending, 1 Approved, 2 Rejected, 3 Withdrawn; conversion requires
    /// Approved. Omitted means Pending (0).
    /// </summary>
    public RecruitmentDecision Decision { get; init; } = RecruitmentDecision.Pending;

    /// <summary>
    /// Note recorded in the stage history when this save creates the application or changes its stage; otherwise
    /// ignored. It is not stored as a field of the application.
    /// </summary>
    public string StageNotes { get; init; } = string.Empty;

    /// <summary>
    /// Free-text notes of the application; trimmed. Omitted means empty.
    /// </summary>
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

/// <summary>
/// Request of <c>POST /api/crm-hr/recruiting/interviews</c>: an interview of a recruitment application. All interview
/// fields are overwritten.
/// </summary>
internal sealed class RecruitmentInterviewSaveApiRequest
{
    /// <summary>
    /// Identifier of the interview to update, from <c>interviews[].id</c> of the recruitment workspace. Omit it to
    /// create an interview; an unknown identifier creates a new interview with a new identifier.
    /// </summary>
    public Guid? Id { get; init; }

    /// <summary>
    /// Identifier of the recruitment application the interview belongs to; required and must exist.
    /// </summary>
    public Guid ApplicationId { get; init; }

    /// <summary>
    /// Start of the interview as an instant with an offset, for example <c>2026-10-01T09:00:00+02:00</c>; required. It
    /// is stored and returned in UTC.
    /// </summary>
    public DateTimeOffset? ScheduledAtUtc { get; init; }

    /// <summary>
    /// Kind of interview, as a JSON integer: 0 Screening, 1 Technical, 2 Manager, 3 Panel, 4 Culture. Omitted means
    /// Screening (0).
    /// </summary>
    public RecruitmentInterviewType InterviewType { get; init; } = RecruitmentInterviewType.Screening;

    /// <summary>
    /// Person who conducts the interview; it must be an existing person. Null or omitted means none.
    /// </summary>
    public Guid? InterviewerPartyId { get; init; }

    /// <summary>
    /// Result of the interview, as a JSON integer: 0 Pending, 1 StrongYes, 2 Yes, 3 Mixed, 4 No, 5 StrongNo. Omitted
    /// means Pending (0). It does not change the application's stage or decision.
    /// </summary>
    public RecruitmentInterviewOutcome Outcome { get; init; } = RecruitmentInterviewOutcome.Pending;

    /// <summary>
    /// Interviewer feedback; free text, trimmed. Omitted means empty.
    /// </summary>
    public string Feedback { get; init; } = string.Empty;

    /// <summary>
    /// Interviewer recommendation as free text, for example <c>Proceed to offer</c>; trimmed. Omitted means empty.
    /// </summary>
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

/// <summary>
/// Request of <c>POST /api/crm-hr/recruiting/lifecycle-tasks</c>: an onboarding, offboarding or training checklist item
/// of a party. It is not a Project Structure task. All task fields are overwritten.
/// </summary>
internal sealed class LifecycleTaskSaveApiRequest
{
    /// <summary>
    /// Identifier of the task to update, from <c>lifecycleTasks[].id</c> of the recruitment workspace. Omit it to
    /// create a task; an unknown identifier creates a new task with a new identifier.
    /// </summary>
    public Guid? Id { get; init; }

    /// <summary>
    /// Identifier of the party the task is for, for example the candidate of an application; required and must exist.
    /// </summary>
    public Guid PartyId { get; init; }

    /// <summary>
    /// Kind of task, as a JSON integer: 0 Onboarding, 1 Offboarding, 2 Training. Omitted means Onboarding (0).
    /// </summary>
    public LifecycleTaskKind TaskKind { get; init; } = LifecycleTaskKind.Onboarding;

    /// <summary>
    /// Title of the task; required and trimmed.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Person responsible for the task; required and must be an existing person.
    /// </summary>
    public Guid? OwnerPartyId { get; init; }

    /// <summary>
    /// Date by which the task should be done (<c>yyyy-MM-dd</c>); required. A task past this date that is neither
    /// Completed nor Cancelled is reported as overdue.
    /// </summary>
    public DateOnly? DueDate { get; init; }

    /// <summary>
    /// Progress of the task, as a JSON integer: 0 NotStarted, 1 InProgress, 2 Completed, 3 Cancelled. Omitted means
    /// NotStarted (0).
    /// </summary>
    public LifecycleTaskStatus Status { get; init; } = LifecycleTaskStatus.NotStarted;

    /// <summary>
    /// Project the task relates to, from <c>GET /api/projects</c>; it must exist. The task does not become part of the
    /// project's plan. Null or omitted means none.
    /// </summary>
    public Guid? RelatedProjectId { get; init; }

    /// <summary>
    /// Free-text notes; trimmed. Omitted means empty.
    /// </summary>
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

/// <summary>
/// Request of <c>POST /api/crm-hr/recruiting/support-assignments</c>: the complete set of support people of a party.
/// An omitted or null assignment is removed.
/// </summary>
internal sealed class RecruitmentSupportAssignmentsSaveApiRequest
{
    /// <summary>
    /// Identifier of the party that receives support, for example a candidate or new hire; required and must exist.
    /// </summary>
    public Guid PartyId { get; init; }

    /// <summary>
    /// Person who manages the party, stored as a ManagedBy relationship. Null or omitted removes every ManagedBy
    /// relationship of the party.
    /// </summary>
    public Guid? ManagerPartyId { get; init; }

    /// <summary>
    /// Person who supports the party as a buddy, stored as a Supports relationship with the notes <c>Buddy</c>. Null
    /// or omitted removes the buddy.
    /// </summary>
    public Guid? BuddyPartyId { get; init; }

    /// <summary>
    /// Person who mentors the party, stored as a Supports relationship with the notes <c>Mentor</c>. Null or omitted
    /// removes the mentor.
    /// </summary>
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

/// <summary>
/// Request of <c>POST /api/crm-hr/recruiting/conversions</c>: the application to convert and the workforce profile
/// values of the candidate. Start from <c>conversion</c> of the recruitment workspace.
/// </summary>
internal sealed class RecruitmentConversionApiRequest
{
    /// <summary>
    /// Identifier of the recruitment application to convert; required. Its decision must be Approved.
    /// </summary>
    public Guid ApplicationId { get; init; }

    /// <summary>
    /// Work classification of the new profile, as a JSON integer: 0 Employee, 1 Contractor, 2 Freelancer,
    /// 3 DeliveryUnit. Omitted means Employee (0); a person candidate cannot use DeliveryUnit.
    /// </summary>
    public WorkforceKind WorkforceKind { get; init; } = WorkforceKind.Employee;

    /// <summary>
    /// Job title of the profile; blank or omitted uses the application's desired role.
    /// </summary>
    public string JobTitle { get; init; } = string.Empty;

    /// <summary>
    /// Professional discipline of the profile as free text. Omitted means empty.
    /// </summary>
    public string Discipline { get; init; } = string.Empty;

    /// <summary>
    /// Seniority level of the profile as free text. Omitted means empty.
    /// </summary>
    public string Seniority { get; init; } = string.Empty;

    /// <summary>
    /// Organization or organization unit of the profile; null or omitted uses the application's target unit.
    /// </summary>
    public Guid? HomeUnitPartyId { get; init; }

    /// <summary>
    /// Manager of the profile, an existing person; null or omitted uses the candidate's support manager, or else the
    /// application's hiring manager.
    /// </summary>
    public Guid? ManagerPartyId { get; init; }

    /// <summary>
    /// Date the work relationship starts (<c>yyyy-MM-dd</c>). Null or omitted means not set.
    /// </summary>
    public DateOnly? StartDate { get; init; }

    /// <summary>
    /// Work location as free text. Omitted means empty.
    /// </summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>
    /// Time zone as free text, for example <c>Europe/Prague</c>. Omitted means empty.
    /// </summary>
    public string TimeZone { get; init; } = string.Empty;

    /// <summary>
    /// Weekly working capacity in hours; 0 or less means 40. Omitted means 40.
    /// </summary>
    public decimal CapacityHoursPerWeek { get; init; } = 40m;

    /// <summary>
    /// Employment status of the profile as free text; blank means <c>Active</c>. Omitted means <c>Active</c>.
    /// </summary>
    public string Status { get; init; } = "Active";

    /// <summary>
    /// Notes of the profile; trimmed. Omitted means empty.
    /// </summary>
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
