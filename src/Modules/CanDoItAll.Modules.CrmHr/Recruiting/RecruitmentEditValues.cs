using System.Text.Json;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.CrmHr;

internal static class RecruitmentEditValues {
    internal static string ApplicationScope(Guid id) => $"crmhr:recruitment:application:{id:N}";
    internal static string InterviewScope(Guid id) => $"crmhr:recruitment:interview:{id:N}";
    internal static string TasksScope(Guid id) => $"crmhr:recruitment:tasks:{id:N}";
    internal static string Actor(string? value) => string.IsNullOrWhiteSpace(value) ? "crm-hr-ui" : value.Trim();
    internal static string Route(Guid id) => $"/crm-hr/recruiting?applicationId={id:D}";
    internal static string PartyRoute(Guid id) => $"/crm-hr/recruiting?partyId={id:D}";
    internal static DateOnly? Date(DateTimeOffset? value) => value.HasValue ? DateOnly.FromDateTime(value.Value.UtcDateTime) : null;
    internal static DateTimeOffset? UtcDate(DateOnly? value) => value.HasValue ? new(value.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null;
    internal static DateTimeOffset UtcTime(DateTime value) => value.Kind switch {
        DateTimeKind.Utc => new(value),
        DateTimeKind.Local => new DateTimeOffset(value).ToUniversalTime(),
        _ => new DateTimeOffset(value, TimeZoneInfo.Local.GetUtcOffset(value)).ToUniversalTime()
    };
    internal static Error Conflict(string code = RecruitmentEditingErrors.Conflict) => Error.Failure("The saved recruitment record changed. Review its current values before replacing it.", code);
    internal static Error OriginalRequired() => Error.Validation("The original observed recruitment values are required for this command.", RecruitmentEditingErrors.OriginalRequired);
    internal static Error IdentityInvalid() => Error.Validation("The original observation belongs to another recruitment record.", RecruitmentEditingErrors.IdentityInvalid);
    internal static RecruitmentApplicationObservation Application(RecruitmentApplication row) => new(row.Id, row.PartyId,
        row.TargetUnitPartyId, row.RecruiterPartyId, row.HiringManagerPartyId, row.DesiredRole, row.Source, row.Stage,
        row.AvailableFromUtc, row.Decision, row.Notes, row.UpdatedAtUtc);
    internal static RecruitmentApplicationEditorModel Accepted(RecruitmentApplication row) => new() {
        Id = row.Id, PartyId = row.PartyId, TargetUnitPartyId = row.TargetUnitPartyId,
        RecruiterPartyId = row.RecruiterPartyId, HiringManagerPartyId = row.HiringManagerPartyId,
        DesiredRole = row.DesiredRole, Source = row.Source, Stage = row.Stage,
        AvailableFrom = Date(row.AvailableFromUtc), Decision = row.Decision, Notes = row.Notes,
        Original = Application(row)
    };
    internal static RecruitmentInterviewObservation Interview(RecruitmentInterview row) => new(row.Id, row.ApplicationId,
        row.ScheduledAtUtc, row.InterviewType, row.InterviewerPartyId, row.Outcome, row.Feedback, row.Recommendation);
    internal static RecruitmentInterviewEditorModel Accepted(RecruitmentInterview row) => new() {
        Id = row.Id, ApplicationId = row.ApplicationId, ScheduledAtLocal = row.ScheduledAtUtc.LocalDateTime,
        InterviewType = row.InterviewType, InterviewerPartyId = row.InterviewerPartyId,
        Outcome = row.Outcome, Feedback = row.Feedback, Recommendation = row.Recommendation,
        Original = Interview(row)
    };
    internal static LifecycleTaskObservation Task(OnboardingTask row) => new(row.Id, row.PartyId, row.TaskKind,
        row.Title, row.OwnerPartyId, Date(row.DueDateUtc), row.Status, row.RelatedProjectId, row.Notes);
    internal static LifecycleTaskEditorModel Accepted(OnboardingTask row) => new() {
        Id = row.Id, PartyId = row.PartyId, TaskKind = row.TaskKind, Title = row.Title,
        OwnerPartyId = row.OwnerPartyId, DueDate = Date(row.DueDateUtc), Status = row.Status,
        RelatedProjectId = row.RelatedProjectId, Notes = row.Notes, Original = Task(row)
    };
    internal static void AddAudit(CrmHrDbContext context, Guid applicationId, string action, string summary,
        string detail, string actor, DateTimeOffset at) => context.Set<CrmHrAuditEntry>().Add(new() {
            EntityType = nameof(RecruitmentApplication), EntityId = applicationId, Action = action,
            Summary = summary.Trim(), DetailJson = detail, Actor = actor, CreatedAtUtc = at
        });
    internal static string StageDetail(RecruitmentStage? previous, RecruitmentStage current, string notes) =>
        JsonSerializer.Serialize(new { PreviousStage = previous?.ToString(), Stage = current.ToString(), Notes = notes.Trim() });
}
