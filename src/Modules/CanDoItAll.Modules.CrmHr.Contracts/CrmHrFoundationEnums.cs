namespace CanDoItAll.Modules.CrmHr;

/// <summary>
/// Kind of CRM/HR party, as a JSON integer: 0 Person (a human), 1 Organization (a company or other legal entity),
/// 2 OrganizationUnit (a department or team of an organization), 3 AiAgent (an AI agent recorded in the CRM/HR
/// directory; its executable agent definition is kept by the Agents module).
/// </summary>
public enum PartyType
{
    Person,
    Organization,
    OrganizationUnit,
    AiAgent
}

/// <summary>
/// Lifecycle status of a CRM/HR party record, as a JSON integer: 0 Draft, 1 Active, 2 Inactive, 3 Archived (party lists
/// include archived parties only on request), 4 Former, 5 Candidate (given to candidate parties created by recruiting),
/// 6 Prospect. Converting a recruitment candidate sets Active.
/// </summary>
public enum PartyLifecycleStatus
{
    Draft,
    Active,
    Inactive,
    Archived,
    Former,
    Candidate,
    Prospect
}

/// <summary>
/// Business role of a CRM/HR party, as a JSON integer: 0 Customer, 1 CustomerContact, 2 Partner, 3 Vendor, 4 Employee,
/// 5 Contractor, 6 Freelancer, 7 DeliveryUnit, 8 Candidate, 9 AiSteward, 10 AccountManager, 11 Recruiter,
/// 12 Stakeholder. A role classifies the party in CRM/HR; it is not an authorization role or a task assignment.
/// </summary>
public enum PartyRoleKind
{
    Customer,
    CustomerContact,
    Partner,
    Vendor,
    Employee,
    Contractor,
    Freelancer,
    DeliveryUnit,
    Candidate,
    AiSteward,
    AccountManager,
    Recruiter,
    Stakeholder
}

/// <summary>
/// Kind of contact value of a party's contact point, as a JSON integer: 0 Email, 1 Phone, 2 Website, 3 Messaging,
/// 4 Social, 5 Other.
/// </summary>
public enum PartyContactType
{
    Email,
    Phone,
    Website,
    Messaging,
    Social,
    Other
}

/// <summary>
/// Kind of a directed relationship between two parties, read as source, kind, target (for example a person MemberOf an
/// organization), as a JSON integer: 0 MemberOf, 1 PartOf, 2 ReportsTo, 3 CustomerOf, 4 PartnerOf, 5 VendorTo,
/// 6 Represents, 7 ManagedBy, 8 OwnedBy, 9 Supports. Recruiting support assignments use ManagedBy (the party is managed
/// by its manager) and Supports (a buddy or mentor supports the party).
/// </summary>
public enum PartyRelationshipKind
{
    MemberOf,
    PartOf,
    ReportsTo,
    CustomerOf,
    PartnerOf,
    VendorTo,
    Represents,
    ManagedBy,
    OwnedBy,
    Supports
}

public enum LookupCatalogKind
{
    OpportunityStage,
    RelationshipStage,
    AssignmentKind
}
