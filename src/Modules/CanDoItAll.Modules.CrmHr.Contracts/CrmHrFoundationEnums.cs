namespace CanDoItAll.Modules.CrmHr;

public enum PartyType
{
    Person,
    Organization,
    OrganizationUnit,
    AiAgent
}

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

public enum PartyContactType
{
    Email,
    Phone,
    Website,
    Messaging,
    Social,
    Other
}

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
