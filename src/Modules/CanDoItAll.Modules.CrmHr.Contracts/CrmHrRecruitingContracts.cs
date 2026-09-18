using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.CrmHr;

public sealed record RecruitmentTrainingRequest(
    Guid InterviewId,
    Guid AttemptId,
    AgentRecruitingAssessmentClassification Classification,
    IReadOnlyList<string> Gaps);

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

public sealed record RecruitmentApplicationPage(
    IReadOnlyList<RecruitmentApplicationListItemModel> Items,
    int PageIndex,
    int PageSize,
    int TotalCount);

public sealed record RecruitmentApplicationSummary(
    int TotalCount,
    int InterviewingCount,
    int OfferOrHiredCount);

public sealed class RecruitmentApplicationEditorModel
{
    public Guid? Id { get; set; }
    public Guid? PartyId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateEmail { get; set; } = string.Empty;
    public string CandidatePhone { get; set; } = string.Empty;
    public string CandidateSummary { get; set; } = string.Empty;
    public Guid? TargetUnitPartyId { get; set; }
    public Guid? RecruiterPartyId { get; set; }
    public Guid? HiringManagerPartyId { get; set; }
    public string DesiredRole { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public RecruitmentStage Stage { get; set; } = RecruitmentStage.Applied;
    public DateOnly? AvailableFrom { get; set; }
    public RecruitmentDecision Decision { get; set; } = RecruitmentDecision.Pending;
    public string StageNotes { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string LastChangedBy { get; set; } = "crm-hr-ui";

    // An independent submission; the model holds no mutable descendants.
    public RecruitmentApplicationEditorModel Snapshot() => (RecruitmentApplicationEditorModel)MemberwiseClone();
}

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

public sealed record RecruitmentSupportAssignmentsModel(
    Guid PartyId,
    Guid? ManagerPartyId,
    string ManagerName,
    Guid? BuddyPartyId,
    string BuddyName,
    Guid? MentorPartyId,
    string MentorName);

public sealed class RecruitmentConversionEditorModel
{
    public Guid ApplicationId { get; set; }
    public WorkforceKind WorkforceKind { get; set; } = WorkforceKind.Employee;
    public string JobTitle { get; set; } = string.Empty;
    public string Discipline { get; set; } = string.Empty;
    public string Seniority { get; set; } = string.Empty;
    public Guid? HomeUnitPartyId { get; set; }
    public Guid? ManagerPartyId { get; set; }
    public DateOnly? StartDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public decimal CapacityHoursPerWeek { get; set; } = 40m;
    public string Status { get; set; } = "Active";
    public string Notes { get; set; } = string.Empty;
    public string LastChangedBy { get; set; } = "crm-hr-ui";

    // An independent submission; the model holds no mutable descendants.
    public RecruitmentConversionEditorModel Snapshot() => (RecruitmentConversionEditorModel)MemberwiseClone();
}

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
