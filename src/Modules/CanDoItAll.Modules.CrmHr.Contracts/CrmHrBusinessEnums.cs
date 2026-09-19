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

/// <summary>
/// Work classification of a workforce profile, as a JSON integer: 0 Employee, 1 Contractor, 2 Freelancer,
/// 3 DeliveryUnit (an organization or organization unit that delivers work). People can use the first three;
/// organizations and organization units must use DeliveryUnit. Saving a profile gives the party the role of the same
/// name when it is missing.
/// </summary>
public enum WorkforceKind
{
    Employee,
    Contractor,
    Freelancer,
    DeliveryUnit
}

/// <summary>
/// Proficiency of a party in a workforce skill, lowest first, as a JSON integer: 0 Basic, 1 Working, 2 Strong,
/// 3 Expert.
/// </summary>
public enum SkillProficiencyLevel
{
    Basic,
    Working,
    Strong,
    Expert
}

/// <summary>
/// Reason for a capacity block, as a JSON integer: 0 Leave, 1 Unavailable, 2 Reserve, 3 Tentative. The kind is
/// descriptive: every active block reduces the party's available capacity by its percentage, whatever its kind.
/// </summary>
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

/// <summary>
/// Stage of a recruitment application, as a JSON integer: 0 Applied, 1 Screening, 2 Interviewing, 3 Offer, 4 Hired,
/// 5 Rejected, 6 Withdrawn. The application save accepts any stage in any order; conversion sets Hired, and Rejected or
/// Withdrawn applications cannot be converted.
/// </summary>
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

/// <summary>
/// Hiring decision of a recruitment application, as a JSON integer: 0 Pending, 1 Approved (required before
/// conversion), 2 Rejected, 3 Withdrawn.
/// </summary>
public enum RecruitmentDecision
{
    Pending,
    Approved,
    Rejected,
    Withdrawn
}

/// <summary>
/// Kind of recruitment interview, as a JSON integer: 0 Screening, 1 Technical, 2 Manager, 3 Panel, 4 Culture.
/// </summary>
public enum RecruitmentInterviewType
{
    Screening,
    Technical,
    Manager,
    Panel,
    Culture
}

/// <summary>
/// Result of a recruitment interview, as a JSON integer: 0 Pending (no result yet), 1 StrongYes, 2 Yes, 3 Mixed, 4 No,
/// 5 StrongNo. The outcome is recorded only; it does not change the application's stage or decision.
/// </summary>
public enum RecruitmentInterviewOutcome
{
    Pending,
    StrongYes,
    Yes,
    Mixed,
    No,
    StrongNo
}

/// <summary>
/// Kind of lifecycle task of a party, as a JSON integer: 0 Onboarding, 1 Offboarding, 2 Training.
/// </summary>
public enum LifecycleTaskKind
{
    Onboarding,
    Offboarding,
    Training
}

/// <summary>
/// Progress of a lifecycle task, as a JSON integer: 0 NotStarted, 1 InProgress, 2 Completed, 3 Cancelled. Completed and
/// Cancelled tasks are never reported as overdue.
/// </summary>
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
