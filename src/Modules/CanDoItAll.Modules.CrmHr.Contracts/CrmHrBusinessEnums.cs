using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.CrmHr;

public enum InteractionType
{
    Meeting,
    Call,
    Email,
    Message,
    Note
}

public enum InteractionPartyRole
{
    Author,
    Account,
    Contact,
    Attendee,
    Recipient,
    Stakeholder
}

public enum CrmAccountRelationshipStage
{
    Prospect,
    ActiveCustomer,
    DormantCustomer,
    LostCustomer
}

public enum CrmAccountConnectionRole
{
    PrimaryContact,
    Stakeholder,
    BillingContact,
    ContractContact,
    AccountManager,
    DeliveryLead,
    Sponsor,
    TechnicalContact
}

public enum OpportunityStage
{
    Identified,
    Qualified,
    Proposal,
    Negotiation,
    Won,
    Lost
}

public enum OpportunitySource
{
    Direct,
    Partner,
    Renewal,
    Upsell
}

public enum OpportunityPartyRole
{
    Customer,
    Partner,
    Sponsor,
    TechnicalContact,
    BillingContact,
    DeliveryLead,
    Stakeholder
}

public enum WorkforceKind
{
    Employee,
    Contractor,
    Freelancer,
    DeliveryUnit
}

public enum SkillProficiencyLevel
{
    Basic,
    Working,
    Strong,
    Expert
}

public enum CapacityBlockKind
{
    Leave,
    Unavailable,
    Reserve,
    Tentative
}

public enum StaffingRequestStatus
{
    Draft,
    Open,
    Proposed,
    Confirmed,
    Closed,
    Cancelled
}

public enum RecruitmentStage
{
    Applied,
    Screening,
    Interviewing,
    Offer,
    Hired,
    Rejected,
    Withdrawn
}

public enum RecruitmentDecision
{
    Pending,
    Approved,
    Rejected,
    Withdrawn
}

public enum RecruitmentInterviewType
{
    Screening,
    Technical,
    Manager,
    Panel,
    Culture
}

public enum RecruitmentInterviewOutcome
{
    Pending,
    StrongYes,
    Yes,
    Mixed,
    No,
    StrongNo
}

public enum LifecycleTaskKind
{
    Onboarding,
    Offboarding,
    Training
}

public enum LifecycleTaskStatus
{
    NotStarted,
    InProgress,
    Completed,
    Cancelled
}

public enum AiExecutionMode
{
    Local,
    Remote,
    ThirdParty
}

public enum AiValidationStatus
{
    Draft,
    ReviewRequired,
    Approved,
    Suspended
}

public enum ProjectPartyAssignmentKind
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
