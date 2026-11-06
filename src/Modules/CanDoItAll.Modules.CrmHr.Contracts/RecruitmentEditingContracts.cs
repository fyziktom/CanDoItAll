using System.ComponentModel;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.CrmHr;

[Description("Immutable recruitment application read used for conditional application replacement and candidate conversion. Retain the complete original, including its owner-issued update instant.")]
public sealed record RecruitmentApplicationObservation(
    [property: Description("GUID of the observed recruitment application; it is not the candidate party GUID.")] Guid Id,
    [property: Description("GUID of the candidate party owning the application; an existing application cannot be moved to another candidate.")] Guid PartyId,
    [property: Description("Original target organizational-unit party GUID, or null when none was selected.")] Guid? TargetUnitPartyId,
    [property: Description("Original recruiter party GUID, or null when no recruiter was assigned.")] Guid? RecruiterPartyId,
    [property: Description("Original hiring-manager party GUID, or null when none was assigned.")] Guid? HiringManagerPartyId,
    [property: Description("Original desired job-role text, retained unchanged inside the observation.")] string DesiredRole,
    [property: Description("Original recruiting source text, such as the candidate acquisition channel; it is not a transport or authority identifier.")] string Source,
    [property: Description("Original application stage as a JSON integer RecruitmentStage. Values: 0 Applied, 1 Screening, 2 Interviewing, 3 Offer, 4 Hired, 5 Rejected, 6 Withdrawn.")] RecruitmentStage Stage,
    [property: Description("Original availability instant with UTC offset; null means unknown. Retain the exact returned instant.")] DateTimeOffset? AvailableFromUtc,
    [property: Description("Original application decision as a JSON integer RecruitmentDecision. Values: 0 Pending, 1 Approved, 2 Rejected, 3 Withdrawn.")] RecruitmentDecision Decision,
    [property: Description("Original application notes visible to the current CRM/HR caller.")] string Notes,
    [property: Description("Owner-issued original update instant with UTC offset; retain exactly rather than substituting a client clock.")] DateTimeOffset UpdatedAtUtc);

[Description("Immutable original recruitment interview row used for conditional replacement; retain its application identity and original interview values together.")]
public sealed record RecruitmentInterviewObservation(
    [property: Description("GUID of the observed interview row, distinct from its application GUID.")] Guid Id,
    [property: Description("GUID of the recruitment application owning this original interview; replacement cannot move the row.")] Guid ApplicationId,
    [property: Description("Original scheduled interview instant with UTC offset; retain the exact returned value.")] DateTimeOffset ScheduledAtUtc,
    [property: Description("Original interview kind as a JSON integer RecruitmentInterviewType. Values: 0 Screening, 1 Technical, 2 Manager, 3 Panel, 4 Culture.")] RecruitmentInterviewType InterviewType,
    [property: Description("Original interviewer party GUID, or null when none was assigned.")] Guid? InterviewerPartyId,
    [property: Description("Original interview outcome as a JSON integer RecruitmentInterviewOutcome. Values: 0 Pending, 1 StrongYes, 2 Yes, 3 Mixed, 4 No, 5 StrongNo.")] RecruitmentInterviewOutcome Outcome,
    [property: Description("Original interview feedback text retained for concurrency comparison.")] string Feedback,
    [property: Description("Original interviewer recommendation text; an empty string means none was recorded.")] string Recommendation);

[Description("Immutable original onboarding, offboarding or training task used for conditional replacement. Related project lifetime admission remains a separate request precondition.")]
public sealed record LifecycleTaskObservation(
    [property: Description("GUID of the observed lifecycle task row.")] Guid Id,
    [property: Description("GUID of the party owning the original lifecycle task; replacement cannot move the task to another party.")] Guid PartyId,
    [property: Description("Original task kind as a JSON integer LifecycleTaskKind. Values: 0 Onboarding, 1 Offboarding, 2 Training.")] LifecycleTaskKind TaskKind,
    [property: Description("Original lifecycle task title retained unchanged inside the observation.")] string Title,
    [property: Description("Original responsible party GUID, or null when no owner was assigned.")] Guid? OwnerPartyId,
    [property: Description("Original due calendar date in yyyy-MM-dd form; null means no due date.")] DateOnly? DueDate,
    [property: Description("Original task status as a JSON integer LifecycleTaskStatus. Values: 0 NotStarted, 1 InProgress, 2 Completed, 3 Cancelled.")] LifecycleTaskStatus Status,
    [property: Description("Original related project GUID, or null when the task was not attached to a project.")] Guid? RelatedProjectId,
    [property: Description("Original lifecycle task notes retained unchanged for concurrency comparison.")] string Notes);

[Description("Coherent original application, workforce and candidate lifecycle used by candidate conversion. Send the entire observation from one owner read; a changed participant rejects the conversion.")]
public sealed record RecruitmentConversionObservation(
    [property: Description("Original application identity, stage, decision and fields participating in conversion.")] RecruitmentApplicationObservation Application,
    [property: Description("Original candidate workforce profile, including an explicit absence observation when none exists.")] WorkforceProfileObservation Workforce,
    [property: Description("Original candidate party lifecycle as a JSON integer PartyLifecycleStatus. Values: 0 Draft, 1 Active, 2 Inactive, 3 Archived, 4 Former, 5 Candidate, 6 Prospect.")] PartyLifecycleStatus PartyLifecycle,
    [property: Description("Owner-issued original candidate-party update instant with UTC offset; retain exactly.")] DateTimeOffset PartyUpdatedAtUtc);

public sealed record RecruitmentSaveReceipt<T>(Guid Id, T Accepted);
public sealed record RecruitmentDeleteReceipt(Guid OwnerId, Guid Id);
public sealed record RecruitmentConversionReceipt(Guid ApplicationId, Guid PartyId,
    RecruitmentApplicationEditorModel Application, WorkforceProfileEditorModel Workforce, RecruitmentConversionObservation Original);

public sealed record RecruitmentApplicationRead(RecruitmentApplicationEditorModel Model, IReadOnlyDictionary<Guid, string> Names);

public interface IRecruitmentApplicationEditing {
    Task<RecruitmentApplicationRead?> ReadApplicationAsync(Guid applicationId, IReadOnlyList<Guid> referenceIds, CancellationToken cancellationToken);
    Task<Result<Guid>> SaveRecruitmentApplicationAsync(RecruitmentApplicationEditorModel model, CancellationToken cancellationToken = default,
        Action<RecruitmentSaveReceipt<RecruitmentApplicationEditorModel>>? onCommitted = null);
}

public static class RecruitmentEditingErrors {
    public const string OriginalRequired = "crmhr.recruiting.original-required";
    public const string IdentityInvalid = "crmhr.recruiting.identity-invalid";
    public const string Conflict = "crmhr.recruiting.conflict";
    public const string ConversionConflict = "crmhr.recruiting.convert.conflict";
    public const string PartyTypeConflict = "crmhr.recruiting.party-type-conflict";
}
