using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Recruiting;

public enum RecruitmentApplicationField { CandidateParty, CandidateName, CandidateEmail, CandidatePhone, CandidateSummary, DesiredRole, TargetUnit, Recruiter, HiringManager, Source, Stage, AvailableFrom, Decision, StageNotes, Notes }
public enum RecruitmentInterviewField { Schedule, Type, Interviewer, Outcome, Feedback, Recommendation }
public enum RecruitmentTaskField { Kind, Title, Owner, DueDate, Status, Project, Notes }
public enum RecruitmentConversionField { Kind, Title, Discipline, Seniority, HomeUnit, Manager, StartDate, Location, TimeZone, Capacity, Status, Notes }
public enum RecruitmentSupportField { Manager, Buddy, Mentor }

internal static class RecruitmentDraftValues {
    private static readonly IReadOnlyDictionary<RecruitmentApplicationField, (Func<RecruitmentApplicationEditorModel, RecruitmentApplicationEditorModel, bool> Same, Action<RecruitmentApplicationEditorModel, RecruitmentApplicationEditorModel> Copy)> ApplicationFields =
        new Dictionary<RecruitmentApplicationField, (Func<RecruitmentApplicationEditorModel, RecruitmentApplicationEditorModel, bool> Same, Action<RecruitmentApplicationEditorModel, RecruitmentApplicationEditorModel> Copy)> {
            [RecruitmentApplicationField.CandidateParty] = ((a, b) => a.PartyId == b.PartyId, (target, source) => target.PartyId = source.PartyId),
            [RecruitmentApplicationField.CandidateName] = ((a, b) => a.CandidateName == b.CandidateName, (target, source) => target.CandidateName = source.CandidateName),
            [RecruitmentApplicationField.CandidateEmail] = ((a, b) => a.CandidateEmail == b.CandidateEmail, (target, source) => target.CandidateEmail = source.CandidateEmail),
            [RecruitmentApplicationField.CandidatePhone] = ((a, b) => a.CandidatePhone == b.CandidatePhone, (target, source) => target.CandidatePhone = source.CandidatePhone),
            [RecruitmentApplicationField.CandidateSummary] = ((a, b) => a.CandidateSummary == b.CandidateSummary, (target, source) => target.CandidateSummary = source.CandidateSummary),
            [RecruitmentApplicationField.DesiredRole] = ((a, b) => a.DesiredRole == b.DesiredRole, (target, source) => target.DesiredRole = source.DesiredRole),
            [RecruitmentApplicationField.TargetUnit] = ((a, b) => a.TargetUnitPartyId == b.TargetUnitPartyId, (target, source) => target.TargetUnitPartyId = source.TargetUnitPartyId),
            [RecruitmentApplicationField.Recruiter] = ((a, b) => a.RecruiterPartyId == b.RecruiterPartyId, (target, source) => target.RecruiterPartyId = source.RecruiterPartyId),
            [RecruitmentApplicationField.HiringManager] = ((a, b) => a.HiringManagerPartyId == b.HiringManagerPartyId, (target, source) => target.HiringManagerPartyId = source.HiringManagerPartyId),
            [RecruitmentApplicationField.Source] = ((a, b) => a.Source == b.Source, (target, source) => target.Source = source.Source),
            [RecruitmentApplicationField.Stage] = ((a, b) => a.Stage == b.Stage, (target, source) => target.Stage = source.Stage),
            [RecruitmentApplicationField.AvailableFrom] = ((a, b) => a.AvailableFrom == b.AvailableFrom, (target, source) => target.AvailableFrom = source.AvailableFrom),
            [RecruitmentApplicationField.Decision] = ((a, b) => a.Decision == b.Decision, (target, source) => target.Decision = source.Decision),
            [RecruitmentApplicationField.StageNotes] = ((a, b) => a.StageNotes == b.StageNotes, (target, source) => target.StageNotes = source.StageNotes),
            [RecruitmentApplicationField.Notes] = ((a, b) => a.Notes == b.Notes, (target, source) => target.Notes = source.Notes),
        };

    internal static bool Same(RecruitmentApplicationEditorModel a, RecruitmentApplicationEditorModel b) => ApplicationFields.Values.All(field => field.Same(a, b));
    internal static bool Changed(RecruitmentApplicationEditorModel a, RecruitmentApplicationEditorModel b, RecruitmentApplicationField field) => !ApplicationFields[field].Same(a, b);

    internal static void Copy(RecruitmentApplicationEditorModel target, RecruitmentApplicationEditorModel source) {
        target.Id = source.Id;
        target.Original = source.Original;
        target.LastChangedBy = source.LastChangedBy;
        foreach (var field in ApplicationFields.Values) {
            field.Copy(target, source);
        }
    }

    internal static void CopyField(RecruitmentApplicationEditorModel target, RecruitmentApplicationEditorModel source, RecruitmentApplicationField field) => ApplicationFields[field].Copy(target, source);

    private static readonly IReadOnlyDictionary<RecruitmentInterviewField, (Func<RecruitmentInterviewEditorModel, RecruitmentInterviewEditorModel, bool> Same, Action<RecruitmentInterviewEditorModel, RecruitmentInterviewEditorModel> Copy)> InterviewFields =
        new Dictionary<RecruitmentInterviewField, (Func<RecruitmentInterviewEditorModel, RecruitmentInterviewEditorModel, bool> Same, Action<RecruitmentInterviewEditorModel, RecruitmentInterviewEditorModel> Copy)> {
            [RecruitmentInterviewField.Schedule] = ((a, b) => a.ScheduledAtLocal == b.ScheduledAtLocal, (target, source) => target.ScheduledAtLocal = source.ScheduledAtLocal),
            [RecruitmentInterviewField.Type] = ((a, b) => a.InterviewType == b.InterviewType, (target, source) => target.InterviewType = source.InterviewType),
            [RecruitmentInterviewField.Interviewer] = ((a, b) => a.InterviewerPartyId == b.InterviewerPartyId, (target, source) => target.InterviewerPartyId = source.InterviewerPartyId),
            [RecruitmentInterviewField.Outcome] = ((a, b) => a.Outcome == b.Outcome, (target, source) => target.Outcome = source.Outcome),
            [RecruitmentInterviewField.Feedback] = ((a, b) => a.Feedback == b.Feedback, (target, source) => target.Feedback = source.Feedback),
            [RecruitmentInterviewField.Recommendation] = ((a, b) => a.Recommendation == b.Recommendation, (target, source) => target.Recommendation = source.Recommendation),
        };

    internal static bool Same(RecruitmentInterviewEditorModel a, RecruitmentInterviewEditorModel b) => InterviewFields.Values.All(field => field.Same(a, b));
    internal static bool Changed(RecruitmentInterviewEditorModel a, RecruitmentInterviewEditorModel b, RecruitmentInterviewField field) => !InterviewFields[field].Same(a, b);

    internal static void Copy(RecruitmentInterviewEditorModel target, RecruitmentInterviewEditorModel source) {
        target.Id = source.Id;
        target.ApplicationId = source.ApplicationId;
        target.Original = source.Original;
        foreach (var field in InterviewFields.Values) {
            field.Copy(target, source);
        }
    }

    internal static void CopyField(RecruitmentInterviewEditorModel target, RecruitmentInterviewEditorModel source, RecruitmentInterviewField field) => InterviewFields[field].Copy(target, source);

    private static readonly IReadOnlyDictionary<RecruitmentTaskField, (Func<LifecycleTaskEditorModel, LifecycleTaskEditorModel, bool> Same, Action<LifecycleTaskEditorModel, LifecycleTaskEditorModel> Copy)> TaskFields =
        new Dictionary<RecruitmentTaskField, (Func<LifecycleTaskEditorModel, LifecycleTaskEditorModel, bool> Same, Action<LifecycleTaskEditorModel, LifecycleTaskEditorModel> Copy)> {
            [RecruitmentTaskField.Kind] = ((a, b) => a.TaskKind == b.TaskKind, (target, source) => target.TaskKind = source.TaskKind),
            [RecruitmentTaskField.Title] = ((a, b) => a.Title == b.Title, (target, source) => target.Title = source.Title),
            [RecruitmentTaskField.Owner] = ((a, b) => a.OwnerPartyId == b.OwnerPartyId, (target, source) => target.OwnerPartyId = source.OwnerPartyId),
            [RecruitmentTaskField.DueDate] = ((a, b) => a.DueDate == b.DueDate, (target, source) => target.DueDate = source.DueDate),
            [RecruitmentTaskField.Status] = ((a, b) => a.Status == b.Status, (target, source) => target.Status = source.Status),
            [RecruitmentTaskField.Project] = ((a, b) => a.RelatedProjectId == b.RelatedProjectId && a.RelatedProjectAdmission == b.RelatedProjectAdmission, (target, source) => {
                target.RelatedProjectId = source.RelatedProjectId;
                target.RelatedProjectAdmission = source.RelatedProjectAdmission;
            }),
            [RecruitmentTaskField.Notes] = ((a, b) => a.Notes == b.Notes, (target, source) => target.Notes = source.Notes),
        };

    internal static bool Same(LifecycleTaskEditorModel a, LifecycleTaskEditorModel b) => TaskFields.Values.All(field => field.Same(a, b));
    internal static bool Changed(LifecycleTaskEditorModel a, LifecycleTaskEditorModel b, RecruitmentTaskField field) => !TaskFields[field].Same(a, b);

    internal static void Copy(LifecycleTaskEditorModel target, LifecycleTaskEditorModel source) {
        target.Id = source.Id;
        target.PartyId = source.PartyId;
        target.RelatedProjectAdmission = source.RelatedProjectAdmission;
        target.Original = source.Original;
        foreach (var field in TaskFields.Values) {
            field.Copy(target, source);
        }
    }

    internal static void CopyField(LifecycleTaskEditorModel target, LifecycleTaskEditorModel source, RecruitmentTaskField field) => TaskFields[field].Copy(target, source);

    private static readonly IReadOnlyDictionary<RecruitmentConversionField, (Func<RecruitmentConversionEditorModel, RecruitmentConversionEditorModel, bool> Same, Action<RecruitmentConversionEditorModel, RecruitmentConversionEditorModel> Copy)> ConversionFields =
        new Dictionary<RecruitmentConversionField, (Func<RecruitmentConversionEditorModel, RecruitmentConversionEditorModel, bool> Same, Action<RecruitmentConversionEditorModel, RecruitmentConversionEditorModel> Copy)> {
            [RecruitmentConversionField.Kind] = ((a, b) => a.WorkforceKind == b.WorkforceKind, (target, source) => target.WorkforceKind = source.WorkforceKind),
            [RecruitmentConversionField.Title] = ((a, b) => a.JobTitle == b.JobTitle, (target, source) => target.JobTitle = source.JobTitle),
            [RecruitmentConversionField.Discipline] = ((a, b) => a.Discipline == b.Discipline, (target, source) => target.Discipline = source.Discipline),
            [RecruitmentConversionField.Seniority] = ((a, b) => a.Seniority == b.Seniority, (target, source) => target.Seniority = source.Seniority),
            [RecruitmentConversionField.HomeUnit] = ((a, b) => a.HomeUnitPartyId == b.HomeUnitPartyId, (target, source) => target.HomeUnitPartyId = source.HomeUnitPartyId),
            [RecruitmentConversionField.Manager] = ((a, b) => a.ManagerPartyId == b.ManagerPartyId, (target, source) => target.ManagerPartyId = source.ManagerPartyId),
            [RecruitmentConversionField.StartDate] = ((a, b) => a.StartDate == b.StartDate, (target, source) => target.StartDate = source.StartDate),
            [RecruitmentConversionField.Location] = ((a, b) => a.Location == b.Location, (target, source) => target.Location = source.Location),
            [RecruitmentConversionField.TimeZone] = ((a, b) => a.TimeZone == b.TimeZone, (target, source) => target.TimeZone = source.TimeZone),
            [RecruitmentConversionField.Capacity] = ((a, b) => a.CapacityHoursPerWeek == b.CapacityHoursPerWeek, (target, source) => target.CapacityHoursPerWeek = source.CapacityHoursPerWeek),
            [RecruitmentConversionField.Status] = ((a, b) => a.Status == b.Status, (target, source) => target.Status = source.Status),
            [RecruitmentConversionField.Notes] = ((a, b) => a.Notes == b.Notes, (target, source) => target.Notes = source.Notes),
        };

    internal static bool Same(RecruitmentConversionEditorModel a, RecruitmentConversionEditorModel b) => ConversionFields.Values.All(field => field.Same(a, b));
    internal static bool Changed(RecruitmentConversionEditorModel a, RecruitmentConversionEditorModel b, RecruitmentConversionField field) => !ConversionFields[field].Same(a, b);

    internal static void Copy(RecruitmentConversionEditorModel target, RecruitmentConversionEditorModel source) {
        target.ApplicationId = source.ApplicationId;
        target.Original = source.Original;
        target.LastChangedBy = source.LastChangedBy;
        foreach (var field in ConversionFields.Values) {
            field.Copy(target, source);
        }
    }

    internal static void CopyField(RecruitmentConversionEditorModel target, RecruitmentConversionEditorModel source, RecruitmentConversionField field) => ConversionFields[field].Copy(target, source);

    private static readonly IReadOnlyDictionary<RecruitmentSupportField, (Func<RecruitmentSupportAssignmentsEditorModel, RecruitmentSupportAssignmentsEditorModel, bool> Same, Action<RecruitmentSupportAssignmentsEditorModel, RecruitmentSupportAssignmentsEditorModel> Copy)> SupportFields =
        new Dictionary<RecruitmentSupportField, (Func<RecruitmentSupportAssignmentsEditorModel, RecruitmentSupportAssignmentsEditorModel, bool> Same, Action<RecruitmentSupportAssignmentsEditorModel, RecruitmentSupportAssignmentsEditorModel> Copy)> {
            [RecruitmentSupportField.Manager] = ((a, b) => a.ManagerPartyId == b.ManagerPartyId, (target, source) => target.ManagerPartyId = source.ManagerPartyId),
            [RecruitmentSupportField.Buddy] = ((a, b) => a.BuddyPartyId == b.BuddyPartyId, (target, source) => target.BuddyPartyId = source.BuddyPartyId),
            [RecruitmentSupportField.Mentor] = ((a, b) => a.MentorPartyId == b.MentorPartyId, (target, source) => target.MentorPartyId = source.MentorPartyId),
        };

    internal static bool Same(RecruitmentSupportAssignmentsEditorModel a, RecruitmentSupportAssignmentsEditorModel b) => SupportFields.Values.All(field => field.Same(a, b));
    internal static bool Changed(RecruitmentSupportAssignmentsEditorModel a, RecruitmentSupportAssignmentsEditorModel b, RecruitmentSupportField field) => !SupportFields[field].Same(a, b);

    internal static void Copy(RecruitmentSupportAssignmentsEditorModel target, RecruitmentSupportAssignmentsEditorModel source) {
        target.PartyId = source.PartyId;
        target.LastChangedBy = source.LastChangedBy;
        target.SetOriginal(source.Original!);
        foreach (var field in SupportFields.Values) {
            field.Copy(target, source);
        }
    }

    internal static void CopyField(RecruitmentSupportAssignmentsEditorModel target, RecruitmentSupportAssignmentsEditorModel source, RecruitmentSupportField field) => SupportFields[field].Copy(target, source);

}
