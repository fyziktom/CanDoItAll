using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.CrmHr;

public sealed record RecruitmentTrainingRequest(
    Guid InterviewId,
    Guid AttemptId,
    AgentRecruitingAssessmentClassification Classification,
    IReadOnlyList<string> Gaps);

/// <summary>
/// One recruitment application in the list returned by <c>GET /api/crm-hr/recruiting/applications</c>.
/// </summary>
/// <param name="Id">
/// Identifier of the recruitment application; use it with
/// <c>GET /api/crm-hr/recruiting/applications/{applicationId}</c>.
/// </param>
/// <param name="PartyId">Identifier of the candidate's party; not the application identifier.</param>
/// <param name="CandidatePartyType">
/// Kind of the candidate party, as a JSON integer: 0 Person or 3 AiAgent (1 Organization and 2 OrganizationUnit are not
/// accepted as candidates).
/// </param>
/// <param name="CandidateName">Display name of the candidate party.</param>
/// <param name="DesiredRole">Role the candidate applies for.</param>
/// <param name="Stage">
/// Stage of the application, as a JSON integer: 0 Applied, 1 Screening, 2 Interviewing, 3 Offer, 4 Hired, 5 Rejected,
/// 6 Withdrawn.
/// </param>
/// <param name="Decision">
/// Hiring decision, as a JSON integer: 0 Pending, 1 Approved, 2 Rejected, 3 Withdrawn.
/// </param>
/// <param name="RecruiterName">Display name of the recruiter; empty when none.</param>
/// <param name="HiringManagerName">Display name of the hiring manager; empty when none.</param>
/// <param name="TargetUnitName">Display name of the target organization or unit; empty when none.</param>
/// <param name="PrimaryEmail">
/// The candidate's primary public email address, or another public one when none is primary; empty when there is none.
/// Returned also for sensitive candidates.
/// </param>
/// <param name="PrimaryPhone">
/// The candidate's primary public phone number, or another public one when none is primary; empty when there is none.
/// Returned also for sensitive candidates.
/// </param>
/// <param name="AvailableFrom">Date the candidate can start (<c>yyyy-MM-dd</c>); null when unknown.</param>
/// <param name="HasWorkforceProfile">
/// True when the candidate party has a workforce profile, for example after conversion.
/// </param>
/// <param name="UpdatedAtUtc">
/// When the application last changed, in UTC; the list is ordered by it, newest first.
/// </param>
public sealed record RecruitmentApplicationListItemModel(
    Guid Id,
    Guid PartyId,
    PartyType CandidatePartyType,
    string CandidateName,
    string DesiredRole,
    RecruitmentStage Stage,
    RecruitmentDecision Decision,
    string RecruiterName,
    string HiringManagerName,
    string TargetUnitName,
    string PrimaryEmail,
    string PrimaryPhone,
    DateOnly? AvailableFrom,
    bool HasWorkforceProfile,
    DateTimeOffset UpdatedAtUtc);

/// <summary>
/// Stage filter of the recruitment application list, as a JSON integer or, in the query string, as the case-sensitive
/// member name: 0 All (every stage), 1 Applied, 2 Screening, 3 Interviewing, 4 Offer, 5 Hired, 6 Rejected,
/// 7 Withdrawn; each value except All selects the applications in that stage. It filters records; it is not an
/// authorization scope.
/// </summary>
public enum RecruitmentApplicationScope
{
    All,
    Applied,
    Screening,
    Interviewing,
    Offer,
    Hired,
    Rejected,
    Withdrawn
}

public static class RecruitmentApplicationQueryLimits
{
    public const int DefaultPageSize = 12;
    public const int MaximumPageSize = 100;
    public const int MaximumSearchLength = 200;
}

public sealed record RecruitmentApplicationQuery(
    string SearchText = "",
    RecruitmentApplicationScope Scope = RecruitmentApplicationScope.All,
    int PageIndex = 0,
    int PageSize = RecruitmentApplicationQueryLimits.DefaultPageSize);

/// <summary>
/// One page of recruitment applications matching a query. It has no page count: request the next page while
/// (<c>pageIndex</c> + 1) multiplied by <c>pageSize</c> is less than <c>totalCount</c>.
/// </summary>
/// <param name="Items">Applications on this page, most recently updated first.</param>
/// <param name="PageIndex">Zero-based index of this page.</param>
/// <param name="PageSize">Maximum number of applications per page that was applied.</param>
/// <param name="TotalCount">Number of applications matching the filters across all pages.</param>
public sealed record RecruitmentApplicationPage(
    IReadOnlyList<RecruitmentApplicationListItemModel> Items,
    int PageIndex,
    int PageSize,
    int TotalCount);

public sealed record RecruitmentApplicationSummary(
    int TotalCount,
    int InterviewingCount,
    int OfferOrHiredCount);

/// <summary>
/// The recruitment application as returned in the recruitment workspace, in the shape of the application save
/// request: change it and send it back with <c>POST /api/crm-hr/recruiting/applications</c>.
/// </summary>
public sealed class RecruitmentApplicationEditorModel
{
    /// <summary>
    /// Identifier of the recruitment application.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Identifier of the candidate's party; send it back unchanged when saving, otherwise a new candidate party is
    /// created.
    /// </summary>
    public Guid? PartyId { get; set; }

    /// <summary>
    /// Display name of the candidate party. A save that sends <c>partyId</c> ignores it.
    /// </summary>
    public string CandidateName { get; set; } = string.Empty;

    /// <summary>
    /// The candidate's primary public email address, or another public one; empty when there is none. A save that sends
    /// <c>partyId</c> ignores it.
    /// </summary>
    public string CandidateEmail { get; set; } = string.Empty;

    /// <summary>
    /// The candidate's primary public phone number, or another public one; empty when there is none. A save that sends
    /// <c>partyId</c> ignores it.
    /// </summary>
    public string CandidatePhone { get; set; } = string.Empty;

    /// <summary>
    /// Always empty in this response; the candidate's summary is <c>candidateSummary</c> of the workspace.
    /// </summary>
    public string CandidateSummary { get; set; } = string.Empty;

    /// <summary>
    /// Identifier of the organization or organization unit the candidate would join; null when none.
    /// </summary>
    public Guid? TargetUnitPartyId { get; set; }

    /// <summary>
    /// Identifier of the person who recruits the candidate; null when none.
    /// </summary>
    public Guid? RecruiterPartyId { get; set; }

    /// <summary>
    /// Identifier of the person who decides on the hire; null when none.
    /// </summary>
    public Guid? HiringManagerPartyId { get; set; }

    /// <summary>
    /// Role the candidate applies for.
    /// </summary>
    public string DesiredRole { get; set; } = string.Empty;

    /// <summary>
    /// Where the application came from, as free text; empty when not set.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Stage of the application, as a JSON integer: 0 Applied, 1 Screening, 2 Interviewing, 3 Offer, 4 Hired,
    /// 5 Rejected, 6 Withdrawn.
    /// </summary>
    public RecruitmentStage Stage { get; set; } = RecruitmentStage.Applied;

    /// <summary>
    /// Date the candidate can start (<c>yyyy-MM-dd</c>); null when unknown.
    /// </summary>
    public DateOnly? AvailableFrom { get; set; }

    /// <summary>
    /// Hiring decision, as a JSON integer: 0 Pending, 1 Approved, 2 Rejected, 3 Withdrawn.
    /// </summary>
    public RecruitmentDecision Decision { get; set; } = RecruitmentDecision.Pending;

    /// <summary>
    /// Always empty in this response; recorded stage notes are in <c>stageHistory</c> of the workspace.
    /// </summary>
    public string StageNotes { get; set; } = string.Empty;

    /// <summary>
    /// Free-text notes of the application; empty when not set.
    /// </summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Not a stored value: always <c>crm-hr-ui</c> in this response. The save request does not carry it.
    /// </summary>
    public string LastChangedBy { get; set; } = "crm-hr-ui";

    // An independent submission; the model holds no mutable descendants.
    public RecruitmentApplicationEditorModel Snapshot() => (RecruitmentApplicationEditorModel)MemberwiseClone();
}

/// <summary>
/// A recorded stage of a recruitment application: its creation or a stage change.
/// </summary>
/// <param name="Id">Identifier of the history entry.</param>
/// <param name="Stage">
/// Stage the application entered, as a JSON integer: 0 Applied, 1 Screening, 2 Interviewing, 3 Offer, 4 Hired,
/// 5 Rejected, 6 Withdrawn.
/// </param>
/// <param name="Summary">English description of the change, for display; its wording can change.</param>
/// <param name="Notes">
/// Stage notes sent with the save that recorded the entry; empty when none. A conversion records
/// <c>Converted to workforce profile.</c>
/// </param>
/// <param name="ChangedAtUtc">When the entry was recorded, in UTC.</param>
/// <param name="ChangedBy">Actor that recorded the entry, for example <c>crm-hr-api</c>.</param>
public sealed record RecruitmentStageHistoryItemModel(
    Guid Id,
    RecruitmentStage Stage,
    string Summary,
    string Notes,
    DateTimeOffset ChangedAtUtc,
    string ChangedBy);

public sealed class RecruitmentInterviewEditorModel
{
    public Guid? Id { get; set; }
    public Guid ApplicationId { get; set; }
    public DateTime? ScheduledAtLocal { get; set; }
    public RecruitmentInterviewType InterviewType { get; set; } = RecruitmentInterviewType.Screening;
    public Guid? InterviewerPartyId { get; set; }
    public RecruitmentInterviewOutcome Outcome { get; set; } = RecruitmentInterviewOutcome.Pending;
    public string Feedback { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;

    // An independent submission; the model holds no mutable descendants.
    public RecruitmentInterviewEditorModel Snapshot() => (RecruitmentInterviewEditorModel)MemberwiseClone();
}

/// <summary>
/// An interview of a recruitment application as returned in the recruitment workspace.
/// </summary>
/// <param name="Id">Identifier of the interview; send it as <c>id</c> to update the interview.</param>
/// <param name="ApplicationId">Identifier of the recruitment application the interview belongs to.</param>
/// <param name="ScheduledAtUtc">Start of the interview, in UTC.</param>
/// <param name="InterviewType">
/// Kind of interview, as a JSON integer: 0 Screening, 1 Technical, 2 Manager, 3 Panel, 4 Culture.
/// </param>
/// <param name="InterviewerPartyId">Identifier of the interviewing person; null when none.</param>
/// <param name="InterviewerName">Display name of the interviewing person; empty when none.</param>
/// <param name="Outcome">
/// Result of the interview, as a JSON integer: 0 Pending, 1 StrongYes, 2 Yes, 3 Mixed, 4 No, 5 StrongNo.
/// </param>
/// <param name="Recommendation">Interviewer recommendation as free text; empty when not set.</param>
/// <param name="Feedback">Interviewer feedback; empty when not set.</param>
public sealed record RecruitmentInterviewItemModel(
    Guid Id,
    Guid ApplicationId,
    DateTimeOffset ScheduledAtUtc,
    RecruitmentInterviewType InterviewType,
    Guid? InterviewerPartyId,
    string InterviewerName,
    RecruitmentInterviewOutcome Outcome,
    string Recommendation,
    string Feedback);

public sealed class LifecycleTaskEditorModel
{
    public Guid? Id { get; set; }
    public Guid PartyId { get; set; }
    public LifecycleTaskKind TaskKind { get; set; } = LifecycleTaskKind.Onboarding;
    public string Title { get; set; } = string.Empty;
    public Guid? OwnerPartyId { get; set; }
    public DateOnly? DueDate { get; set; }
    public LifecycleTaskStatus Status { get; set; } = LifecycleTaskStatus.NotStarted;
    public Guid? RelatedProjectId { get; set; }
    public string Notes { get; set; } = string.Empty;

    // An independent submission; the model holds no mutable descendants.
    public LifecycleTaskEditorModel Snapshot() => (LifecycleTaskEditorModel)MemberwiseClone();
}

/// <summary>
/// A lifecycle task of a party (an onboarding, offboarding or training checklist item) as returned in the recruitment
/// workspace. It is not a Project Structure task.
/// </summary>
/// <param name="Id">Identifier of the lifecycle task; send it as <c>id</c> to update the task.</param>
/// <param name="PartyId">Identifier of the party the task is for.</param>
/// <param name="TaskKind">Kind of task, as a JSON integer: 0 Onboarding, 1 Offboarding, 2 Training.</param>
/// <param name="Title">Title of the task.</param>
/// <param name="OwnerPartyId">Identifier of the person responsible for the task.</param>
/// <param name="OwnerName">Display name of the responsible person; empty when unknown.</param>
/// <param name="DueDate">Date by which the task should be done (<c>yyyy-MM-dd</c>).</param>
/// <param name="Status">
/// Progress of the task, as a JSON integer: 0 NotStarted, 1 InProgress, 2 Completed, 3 Cancelled.
/// </param>
/// <param name="RelatedProjectId">Identifier of the project the task relates to; null when none.</param>
/// <param name="RelatedProjectName">
/// Name of the related project; empty when none or when the project no longer exists.
/// </param>
/// <param name="Notes">Free-text notes; empty when not set.</param>
/// <param name="IsOverdue">
/// True when the due date is before the current UTC date and the task is neither Completed nor Cancelled.
/// </param>
public sealed record LifecycleTaskItemModel(
    Guid Id,
    Guid PartyId,
    LifecycleTaskKind TaskKind,
    string Title,
    Guid? OwnerPartyId,
    string OwnerName,
    DateOnly? DueDate,
    LifecycleTaskStatus Status,
    Guid? RelatedProjectId,
    string RelatedProjectName,
    string Notes,
    bool IsOverdue);

public sealed class RecruitmentSupportAssignmentsEditorModel
{
    public Guid PartyId { get; set; }
    public Guid? ManagerPartyId { get; set; }
    public Guid? BuddyPartyId { get; set; }
    public Guid? MentorPartyId { get; set; }
    public string LastChangedBy { get; set; } = "crm-hr-ui";

    // An independent submission; the model holds no mutable descendants.
    public RecruitmentSupportAssignmentsEditorModel Snapshot() => (RecruitmentSupportAssignmentsEditorModel)MemberwiseClone();
}

/// <summary>
/// The manager, buddy and mentor of a party, read from its party relationships (ManagedBy, and Supports relationships
/// with the notes <c>Buddy</c> or <c>Mentor</c>).
/// </summary>
/// <param name="PartyId">Identifier of the supported party.</param>
/// <param name="ManagerPartyId">
/// Identifier of the person the party is managed by; null when none. When the party has several ManagedBy
/// relationships, one of them is returned.
/// </param>
/// <param name="ManagerName">Display name of the manager; empty when none.</param>
/// <param name="BuddyPartyId">Identifier of the buddy; null when none.</param>
/// <param name="BuddyName">Display name of the buddy; empty when none.</param>
/// <param name="MentorPartyId">Identifier of the mentor; null when none.</param>
/// <param name="MentorName">Display name of the mentor; empty when none.</param>
public sealed record RecruitmentSupportAssignmentsModel(
    Guid PartyId,
    Guid? ManagerPartyId,
    string ManagerName,
    Guid? BuddyPartyId,
    string BuddyName,
    Guid? MentorPartyId,
    string MentorName);

/// <summary>
/// A conversion request prepared from the application and from the candidate's existing workforce profile, returned in
/// the recruitment workspace. Review it and send it to <c>POST /api/crm-hr/recruiting/conversions</c>.
/// </summary>
public sealed class RecruitmentConversionEditorModel
{
    /// <summary>
    /// Identifier of the application to convert.
    /// </summary>
    public Guid ApplicationId { get; set; }

    /// <summary>
    /// Work classification, as a JSON integer: 0 Employee, 1 Contractor, 2 Freelancer, 3 DeliveryUnit; taken from the
    /// existing workforce profile, otherwise Employee.
    /// </summary>
    public WorkforceKind WorkforceKind { get; set; } = WorkforceKind.Employee;

    /// <summary>
    /// Job title from the existing workforce profile, otherwise the application's desired role.
    /// </summary>
    public string JobTitle { get; set; } = string.Empty;

    /// <summary>
    /// Discipline from the existing workforce profile, otherwise empty.
    /// </summary>
    public string Discipline { get; set; } = string.Empty;

    /// <summary>
    /// Seniority from the existing workforce profile, otherwise empty.
    /// </summary>
    public string Seniority { get; set; } = string.Empty;

    /// <summary>
    /// Home unit from the existing workforce profile, otherwise the application's target unit; null when neither.
    /// </summary>
    public Guid? HomeUnitPartyId { get; set; }

    /// <summary>
    /// Manager from the existing workforce profile, otherwise the candidate's support manager, otherwise the
    /// application's hiring manager; null when none.
    /// </summary>
    public Guid? ManagerPartyId { get; set; }

    /// <summary>
    /// Start date from the existing workforce profile (<c>yyyy-MM-dd</c>); null otherwise.
    /// </summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>
    /// Location from the existing workforce profile, otherwise empty.
    /// </summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Time zone from the existing workforce profile, otherwise empty.
    /// </summary>
    public string TimeZone { get; set; } = string.Empty;

    /// <summary>
    /// Weekly capacity in hours from the existing workforce profile, otherwise 40.
    /// </summary>
    public decimal CapacityHoursPerWeek { get; set; } = 40m;

    /// <summary>
    /// Employment status from the existing workforce profile, otherwise <c>Active</c>.
    /// </summary>
    public string Status { get; set; } = "Active";

    /// <summary>
    /// Notes from the existing workforce profile, otherwise empty.
    /// </summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Not a stored value: always <c>crm-hr-ui</c> in this response. The conversion request does not carry it.
    /// </summary>
    public string LastChangedBy { get; set; } = "crm-hr-ui";

    // An independent submission; the model holds no mutable descendants.
    public RecruitmentConversionEditorModel Snapshot() => (RecruitmentConversionEditorModel)MemberwiseClone();
}

/// <summary>
/// The recruitment view of one application returned by <c>GET /api/crm-hr/recruiting/applications/{applicationId}</c>:
/// the application, its candidate, stage history and interviews, and the candidate party's lifecycle tasks, support
/// assignments and a prepared conversion request. It contains personal data.
/// </summary>
/// <param name="Application">The application, in the shape of the application save request.</param>
/// <param name="HasSelectedApplication">
/// True when the application was found. This operation answers 404 otherwise, so the value is always true here.
/// </param>
/// <param name="CandidateDisplayName">Display name of the candidate party.</param>
/// <param name="CandidateSummary">Summary of the candidate party; returned also for sensitive candidates.</param>
/// <param name="CandidatePrimaryEmail">
/// The candidate's primary public email address, or another public one when none is primary; empty when there is none.
/// </param>
/// <param name="CandidatePrimaryPhone">
/// The candidate's primary public phone number, or another public one when none is primary; empty when there is none.
/// </param>
/// <param name="CandidatePartyType">
/// Kind of the candidate party, as a JSON integer: 0 Person or 3 AiAgent; null only when the party record is missing.
/// </param>
/// <param name="CandidateTechnicalAgentId">
/// For an AI agent candidate, the identifier of the technical agent definition in the Agents module that the party is
/// linked to; null for people and for AI agents without a link.
/// </param>
/// <param name="CandidateBindingStatus">
/// Link state of an AI agent candidate to its technical agent definition, as a JSON integer: 0 Unbound,
/// 1 PendingBackfill, 2 Bound, 3 Error; Unbound for people.
/// </param>
/// <param name="RecruiterDisplayName">Display name of the recruiter; empty when none.</param>
/// <param name="HiringManagerDisplayName">Display name of the hiring manager; empty when none.</param>
/// <param name="HasWorkforceProfile">True when the candidate party already has a workforce profile.</param>
/// <param name="StageHistory">Recorded stages of the application, newest first.</param>
/// <param name="Interviews">Interviews of the application, ordered by scheduled time.</param>
/// <param name="LifecycleTasks">
/// All lifecycle tasks of the candidate party (they are stored per party, not per application), ordered by kind, due
/// date and title.
/// </param>
/// <param name="SupportAssignments">Manager, buddy and mentor of the candidate party.</param>
/// <param name="Conversion">
/// Conversion request prepared from the application and any existing workforce profile.
/// </param>
public sealed record RecruitmentWorkspaceModel(
    RecruitmentApplicationEditorModel Application,
    bool HasSelectedApplication,
    string CandidateDisplayName,
    string CandidateSummary,
    string CandidatePrimaryEmail,
    string CandidatePrimaryPhone,
    PartyType? CandidatePartyType,
    Guid? CandidateTechnicalAgentId,
    AiResourceBindingStatus CandidateBindingStatus,
    string RecruiterDisplayName,
    string HiringManagerDisplayName,
    bool HasWorkforceProfile,
    IReadOnlyList<RecruitmentStageHistoryItemModel> StageHistory,
    IReadOnlyList<RecruitmentInterviewItemModel> Interviews,
    IReadOnlyList<LifecycleTaskItemModel> LifecycleTasks,
    RecruitmentSupportAssignmentsModel SupportAssignments,
    RecruitmentConversionEditorModel Conversion);

public static class RecruitmentConversionPolicy
{
    public const string IneligibleStageErrorCode = "crmhr.recruiting.convert.stage-ineligible";
    public const string DecisionNotApprovedErrorCode = "crmhr.recruiting.convert.decision-not-approved";
    public const string AssessmentNotReadyErrorCode = "crmhr.recruiting.convert.assessment-not-ready";

    public static Error? Evaluate(
        RecruitmentStage stage,
        RecruitmentDecision decision,
        bool assessmentRequired = false,
        bool assessmentReady = false)
    {
        if (stage is RecruitmentStage.Rejected or RecruitmentStage.Withdrawn)
        {
            return Error.Validation(
                "Rejected or withdrawn applications cannot be converted. Reopen the application and complete approval first.",
                IneligibleStageErrorCode);
        }

        if (decision != RecruitmentDecision.Approved)
        {
            return Error.Validation(
                "Approve the recruitment decision before converting the candidate to workforce.",
                DecisionNotApprovedErrorCode);
        }

        return assessmentRequired && !assessmentReady
            ? Error.Validation(
                "Complete the application-specific technical assessment and protected human approval before converting this AI candidate.",
                AssessmentNotReadyErrorCode)
            : null;
    }
}
