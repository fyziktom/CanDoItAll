using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.CrmHr.UI.Assignments;

public enum AssignmentField { Party, Affiliation, Role, Node, Primary, Allocation, Start, End, Source, Notes }
public enum StaffingField { Title, Role, Skills, Requester, DeliveryUnit, Start, End, Allocation, Status, Notes }
public enum AssignmentEditorKind { Relationship, Allocation, Staffing }

public sealed class AssignmentDrafts {
    public AssignmentDrafts() {
        Comparisons = new(this);
        Relationship.Open(new());
        Allocation.Open(new());
        Staffing.Open(new());
    }

    public RecordDraftState<ProjectPartyAssignmentUpsertRequest, AssignmentField> Relationship { get; } =
        new(model => model.Snapshot(), Same, Copy);
    public RecordDraftState<ProjectPartyAssignmentUpsertRequest, AssignmentField> Allocation { get; } =
        new(model => model.Snapshot(), Same, Copy);
    public RecordDraftState<StaffingRequestEditorModel, StaffingField> Staffing { get; } =
        new(model => model.Snapshot(), Same, Copy);
    public AssignmentComparisons Comparisons { get; }
    public bool IsDirty => Relationship.IsDirty || Allocation.IsDirty || Staffing.IsDirty;
    public bool IsUnknown => Relationship.IsUnknown || Allocation.IsUnknown || Staffing.IsUnknown;

    public static void Accept(RecordDraftState<ProjectPartyAssignmentUpsertRequest, AssignmentField> state,
        RecordSubmission<ProjectPartyAssignmentUpsertRequest, AssignmentField> submission, ProjectPartyAssignmentUpsertRequest accepted) {
        state.Accept(submission, accepted, () => {
            var live = state.Draft;
            var sent = submission.Model;
            void Keep<T>(AssignmentField field, T original, T current, T value, Action<T> assign) {
                if (!state.Changed(submission, field) && EqualityComparer<T>.Default.Equals(original, current)) {
                    assign(value);
                }
            }
            Keep(AssignmentField.Party, sent.PartyId, live.PartyId, accepted.PartyId, value => live.PartyId = value);
            Keep(AssignmentField.Affiliation, sent.PartyAffiliationId, live.PartyAffiliationId, accepted.PartyAffiliationId, value => live.PartyAffiliationId = value);
            Keep(AssignmentField.Role, sent.Role, live.Role, accepted.Role, value => live.Role = value);
            Keep(AssignmentField.Node, sent.NodeKey, live.NodeKey, accepted.NodeKey, value => live.NodeKey = value);
            Keep(AssignmentField.Primary, sent.IsPrimary, live.IsPrimary, accepted.IsPrimary, value => live.IsPrimary = value);
            Keep(AssignmentField.Allocation, sent.AllocationPercent, live.AllocationPercent, accepted.AllocationPercent, value => live.AllocationPercent = value);
            Keep(AssignmentField.Start, sent.StartsOn, live.StartsOn, accepted.StartsOn, value => live.StartsOn = value);
            Keep(AssignmentField.End, sent.EndsOn, live.EndsOn, accepted.EndsOn, value => live.EndsOn = value);
            Keep(AssignmentField.Source, sent.Source, live.Source, accepted.Source, value => live.Source = value);
            Keep(AssignmentField.Notes, sent.Notes, live.Notes, accepted.Notes, value => live.Notes = value);
            live.AssignmentId = accepted.AssignmentId;
            live.Original = accepted.Original;
            live.ProjectId = accepted.ProjectId;
            live.ExpectedProjectAdmission = accepted.ExpectedProjectAdmission;
        });
    }

    public static void Accept(RecordDraftState<StaffingRequestEditorModel, StaffingField> state,
        RecordSubmission<StaffingRequestEditorModel, StaffingField> submission, StaffingRequestEditorModel accepted) {
        state.Accept(submission, accepted, () => {
            var live = state.Draft;
            var sent = submission.Model;
            void Keep<T>(StaffingField field, T original, T current, T value, Action<T> assign) {
                if (!state.Changed(submission, field) && EqualityComparer<T>.Default.Equals(original, current)) {
                    assign(value);
                }
            }
            Keep(StaffingField.Title, sent.Title, live.Title, accepted.Title, value => live.Title = value);
            Keep(StaffingField.Role, sent.NeededRole, live.NeededRole, accepted.NeededRole, value => live.NeededRole = value);
            Keep(StaffingField.Requester, sent.RequestedByPartyId, live.RequestedByPartyId, accepted.RequestedByPartyId, value => live.RequestedByPartyId = value);
            Keep(StaffingField.DeliveryUnit, sent.DeliveryUnitPartyId, live.DeliveryUnitPartyId, accepted.DeliveryUnitPartyId, value => live.DeliveryUnitPartyId = value);
            Keep(StaffingField.Start, sent.StartDate, live.StartDate, accepted.StartDate, value => live.StartDate = value);
            Keep(StaffingField.End, sent.EndDate, live.EndDate, accepted.EndDate, value => live.EndDate = value);
            Keep(StaffingField.Allocation, sent.AllocationPercent, live.AllocationPercent, accepted.AllocationPercent, value => live.AllocationPercent = value);
            Keep(StaffingField.Status, sent.Status, live.Status, accepted.Status, value => live.Status = value);
            Keep(StaffingField.Notes, sent.Notes, live.Notes, accepted.Notes, value => live.Notes = value);
            if (!state.Changed(submission, StaffingField.Skills) && sent.SkillIds.SequenceEqual(live.SkillIds)) {
                live.SkillIds = [.. accepted.SkillIds];
            }
            live.Id = accepted.Id;
            live.Original = accepted.Original;
            live.ProjectId = accepted.ProjectId;
            live.ExpectedProjectAdmission = accepted.ExpectedProjectAdmission;
            live.ExpectedSourceProjectReference = accepted.ExpectedSourceProjectReference;
        });
    }

    internal static bool Same(ProjectPartyAssignmentUpsertRequest a, ProjectPartyAssignmentUpsertRequest b) =>
        (a.AssignmentId, a.ProjectId, a.PartyId, a.PartyAffiliationId, a.Role, a.NodeKey, a.IsPrimary, a.AllocationPercent,
            a.StartsOn, a.EndsOn, a.Source, a.Notes) ==
        (b.AssignmentId, b.ProjectId, b.PartyId, b.PartyAffiliationId, b.Role, b.NodeKey, b.IsPrimary, b.AllocationPercent,
            b.StartsOn, b.EndsOn, b.Source, b.Notes);

    internal static bool Same(StaffingRequestEditorModel a, StaffingRequestEditorModel b) =>
        (a.Id, a.ProjectId, a.Title, a.NeededRole, a.RequestedByPartyId, a.DeliveryUnitPartyId, a.StartDate, a.EndDate,
            a.AllocationPercent, a.Status, a.Notes) ==
        (b.Id, b.ProjectId, b.Title, b.NeededRole, b.RequestedByPartyId, b.DeliveryUnitPartyId, b.StartDate, b.EndDate,
            b.AllocationPercent, b.Status, b.Notes) && a.SkillIds.SequenceEqual(b.SkillIds);

    internal static void Copy(ProjectPartyAssignmentUpsertRequest target, ProjectPartyAssignmentUpsertRequest source) {
        target.AssignmentId = source.AssignmentId;
        target.Original = source.Original;
        target.PrimaryOriginal = source.PrimaryOriginal;
        target.ProjectId = source.ProjectId;
        target.ExpectedProjectAdmission = source.ExpectedProjectAdmission;
        target.PartyId = source.PartyId;
        target.PartyAffiliationId = source.PartyAffiliationId;
        target.Role = source.Role;
        target.NodeKey = source.NodeKey;
        target.IsPrimary = source.IsPrimary;
        target.AllocationPercent = source.AllocationPercent;
        target.StartsOn = source.StartsOn;
        target.EndsOn = source.EndsOn;
        target.Source = source.Source;
        target.Notes = source.Notes;
    }

    internal static void Copy(StaffingRequestEditorModel target, StaffingRequestEditorModel source) {
        target.Id = source.Id;
        target.Original = source.Original;
        target.ProjectId = source.ProjectId;
        target.ExpectedProjectAdmission = source.ExpectedProjectAdmission;
        target.ExpectedSourceProjectReference = source.ExpectedSourceProjectReference;
        target.Title = source.Title;
        target.NeededRole = source.NeededRole;
        target.RequestedByPartyId = source.RequestedByPartyId;
        target.DeliveryUnitPartyId = source.DeliveryUnitPartyId;
        target.SkillIds = [.. source.SkillIds];
        target.StartDate = source.StartDate;
        target.EndDate = source.EndDate;
        target.AllocationPercent = source.AllocationPercent;
        target.Status = source.Status;
        target.Notes = source.Notes;
    }
}
