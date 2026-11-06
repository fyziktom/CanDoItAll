using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Recruiting;

public sealed class RecruitmentRecordDrafts {
    public RecruitmentRecordDrafts() => Clear();
    public void Clear() {
        Interview.Open(new());
        Task.Open(new());
        ConversionComparison.Open(new());
        SupportComparison.Open(new());
        submittedConversionHint = submittedSupportHint = 0;
    }
    private long submittedConversionHint;
    private long submittedSupportHint;
    public RecordDraftState<RecruitmentInterviewEditorModel, RecruitmentInterviewField> Interview { get; } =
        new(model => model.Snapshot(), RecruitmentDraftValues.Same, RecruitmentDraftValues.Copy);
    public RecordDraftState<LifecycleTaskEditorModel, RecruitmentTaskField> Task { get; } =
        new(model => model.Snapshot(), RecruitmentDraftValues.Same, RecruitmentDraftValues.Copy);
    public RecruitmentReplacementReview<RecruitmentConversionEditorModel, RecruitmentConversionField, RecruitmentConversionReview> ConversionComparison { get; } =
        new(model => model.Snapshot(), RecruitmentDraftValues.Same, RecruitmentDraftValues.Copy, (a, b) => a.Original == b.Original,
            (b, l, r) => new(b, l, r), (previous, current) => current.PreserveChoices(previous), review => review.CanApply, review => review.Candidate());
    public RecruitmentReplacementReview<RecruitmentSupportAssignmentsEditorModel, RecruitmentSupportField, RecruitmentSupportReview> SupportComparison { get; } =
        new(model => model.Snapshot(), RecruitmentDraftValues.Same, RecruitmentDraftValues.Copy, (a, b) => SameSupportOriginal(a.Original, b.Original),
            (b, l, r) => new(b, l, r), (previous, current) => current.PreserveChoices(previous), review => review.CanApply, review => review.Candidate());
    public RecordDraftState<RecruitmentConversionEditorModel, RecruitmentConversionField> Conversion => ConversionComparison.State;
    public RecordDraftState<RecruitmentSupportAssignmentsEditorModel, RecruitmentSupportField> Support => SupportComparison.State;
    public bool ConversionOutdated => ConversionComparison.IsOutdated;
    public bool SupportOutdated => SupportComparison.IsOutdated;
    public bool IsUnknown => Interview.IsUnknown || Task.IsUnknown || Conversion.IsUnknown || Support.IsUnknown;
    public bool IsSaving => Interview.IsSaving || Task.IsSaving || Conversion.IsSaving || Support.IsSaving;
    public bool IsDirty => Interview.IsDirty || Task.IsDirty || Conversion.IsDirty || Support.IsDirty;
    public bool HasPendingInput => Interview.HasPendingInput || Task.HasPendingInput || Conversion.HasPendingInput || Support.HasPendingInput;

    public void Open(RecruitmentWorkspaceModel workspace) {
        Interview.Open(NewInterview(workspace.Application.Id ?? Guid.Empty));
        Task.Open(NewTask(workspace.Application.PartyId ?? Guid.Empty));
        ConversionComparison.Open(workspace.Conversion);
        SupportComparison.Open(FromSupport(workspace.SupportAssignments));
        submittedConversionHint = submittedSupportHint = 0;
    }

    public void Observe(RecruitmentWorkspaceModel workspace, bool explicitPull, long? conversionReadHint = null, long? supportReadHint = null) {
        ConversionComparison.Observe(workspace.Conversion, conversionReadHint ?? ConversionComparison.HintRevision, explicitPull);
        SupportComparison.Observe(FromSupport(workspace.SupportAssignments), supportReadHint ?? SupportComparison.HintRevision, explicitPull);
    }
    public void HintConversion() => ConversionComparison.Hint();
    public void HintSupport() => SupportComparison.Hint();
    public void RetargetCreated(Guid applicationId, Guid partyId) {
        Interview.Draft.ApplicationId = applicationId;
        Task.Draft.PartyId = partyId;
        Conversion.Draft.ApplicationId = applicationId;
        Support.Draft.PartyId = partyId;
    }
    public RecordSubmission<RecruitmentConversionEditorModel, RecruitmentConversionField>? BeginConversion() {
        if (!ConversionComparison.CanSave || Conversion.Draft.Original is null) {
            return null;
        }
        submittedConversionHint = ConversionComparison.HintRevision;
        return Conversion.Begin();
    }
    public RecordSubmission<RecruitmentSupportAssignmentsEditorModel, RecruitmentSupportField>? BeginSupport() {
        if (!SupportComparison.CanSave || Support.Draft.Original is null) {
            return null;
        }
        submittedSupportHint = SupportComparison.HintRevision;
        return Support.Begin();
    }

    public void AcceptInterview(RecordSubmission<RecruitmentInterviewEditorModel, RecruitmentInterviewField> submission,
        RecruitmentSaveReceipt<RecruitmentInterviewEditorModel> receipt) =>
        Interview.ResetAcceptedCreate(submission, receipt.Accepted, NewInterview(submission.Model.ApplicationId));

    public void AcceptTask(RecordSubmission<LifecycleTaskEditorModel, RecruitmentTaskField> submission,
        RecruitmentSaveReceipt<LifecycleTaskEditorModel> receipt) =>
        Task.ResetAcceptedCreate(submission, receipt.Accepted, NewTask(submission.Model.PartyId));

    public void AcceptConversion(RecordSubmission<RecruitmentConversionEditorModel, RecruitmentConversionField> submission,
        RecruitmentConversionReceipt receipt) {
        if (!Conversion.IsCurrent(submission)) {
            return;
        }
        if (receipt.ApplicationId != submission.Model.ApplicationId || receipt.Original.Application.Id != receipt.ApplicationId ||
            receipt.PartyId != submission.Model.Original?.Application.PartyId || receipt.Workforce.PartyId != receipt.PartyId ||
            receipt.Original.Workforce.PartyId != receipt.PartyId) {
            throw new InvalidOperationException("The conversion receipt belongs to a different recruitment opening.");
        }
        var profile = receipt.Workforce;
        var accepted = new RecruitmentConversionEditorModel {
            ApplicationId = receipt.ApplicationId, Original = receipt.Original,
            WorkforceKind = profile.WorkforceKind, JobTitle = profile.JobTitle, Discipline = profile.Discipline,
            Seniority = profile.Seniority, HomeUnitPartyId = profile.HomeUnitPartyId, ManagerPartyId = profile.ManagerPartyId,
            StartDate = profile.StartDate, Location = profile.Location, TimeZone = profile.TimeZone,
            CapacityHoursPerWeek = profile.CapacityHoursPerWeek, Status = profile.Status, Notes = profile.Notes,
            LastChangedBy = profile.LastChangedBy
        };
        Accept(Conversion, submission, accepted, Conversion.Draft.Snapshot(), RecruitmentDraftValues.Changed, RecruitmentDraftValues.CopyField, RecruitmentDraftValues.Copy);
        if (Conversion.IsCurrent(submission)) {
            ConversionComparison.Accept(accepted, submittedConversionHint);
        }
    }

    public void AcceptSupport(RecordSubmission<RecruitmentSupportAssignmentsEditorModel, RecruitmentSupportField> submission,
        RecruitmentSupportAssignmentsEditorModel accepted) {
        if (!Support.IsCurrent(submission)) {
            return;
        }
        if (accepted.PartyId != submission.Model.PartyId || accepted.Original is null) {
            throw new InvalidOperationException("The support receipt belongs to a different recruitment opening.");
        }
        Accept(Support, submission, accepted, Support.Draft.Snapshot(), RecruitmentDraftValues.Changed, RecruitmentDraftValues.CopyField, RecruitmentDraftValues.Copy);
        if (Support.IsCurrent(submission)) {
            SupportComparison.Accept(accepted, submittedSupportHint);
        }
    }

    private static void Accept<TModel, TField>(RecordDraftState<TModel, TField> state, RecordSubmission<TModel, TField> submission, TModel accepted, TModel local,
        Func<TModel, TModel, TField, bool> changed, Action<TModel, TModel, TField> copyField, Action<TModel, TModel> copy) where TModel : class where TField : struct, Enum {
        if (!state.IsCurrent(submission)) {
            return;
        }
        var later = Enum.GetValues<TField>().Where(field => state.Changed(submission, field) || changed(submission.Model, local, field)).ToArray();
        state.Accept(submission, accepted, () => {
            copy(state.Draft, accepted);
            foreach (var field in later) {
                copyField(state.Draft, local, field);
            }
        });
    }

    public static RecruitmentInterviewEditorModel NewInterview(Guid applicationId) => new() {
        ApplicationId = applicationId, ScheduledAtLocal = DateTime.Today.AddHours(9)
    };
    public static LifecycleTaskEditorModel NewTask(Guid partyId) => new() {
        PartyId = partyId, DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7))
    };
    public static RecruitmentSupportAssignmentsEditorModel FromSupport(RecruitmentSupportAssignmentsModel model) {
        var draft = new RecruitmentSupportAssignmentsEditorModel {
            PartyId = model.PartyId, ManagerPartyId = model.ManagerPartyId, BuddyPartyId = model.BuddyPartyId, MentorPartyId = model.MentorPartyId
        };
        draft.SetOriginal(model.Original);
        return draft;
    }
    private static bool SameSupportOriginal(PartyRelationshipObservation? a, PartyRelationshipObservation? b) =>
        a is null || b is null ? a is null && b is null : a.Rows.OrderBy(row => row.Id).SequenceEqual(b.Rows.OrderBy(row => row.Id));
}
