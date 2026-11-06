using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.CrmHr.UI.Assignments;

public sealed class StaffingRequestReview(StaffingRequestEditorModel original, StaffingRequestEditorModel local, StaffingRequestEditorModel current) {
    public StaffingRequestEditorModel Current { get; } = current.Snapshot();
    public StaffingRequestEditorModel Local { get; } = local.Snapshot();
    public MergeField<string> Title { get; } = new(original.Title, local.Title, current.Title);
    public MergeField<string> Role { get; } = new(original.NeededRole, local.NeededRole, current.NeededRole);
    public MergeField<Guid?> Requester { get; } = new(original.RequestedByPartyId, local.RequestedByPartyId, current.RequestedByPartyId);
    public MergeField<Guid?> DeliveryUnit { get; } = new(original.DeliveryUnitPartyId, local.DeliveryUnitPartyId, current.DeliveryUnitPartyId);
    public MergeField<DateOnly?> Start { get; } = new(original.StartDate, local.StartDate, current.StartDate);
    public MergeField<DateOnly?> End { get; } = new(original.EndDate, local.EndDate, current.EndDate);
    public MergeField<decimal> Allocation { get; } = new(original.AllocationPercent, local.AllocationPercent, current.AllocationPercent);
    public MergeField<StaffingRequestStatus> Status { get; } = new(original.Status, local.Status, current.Status);
    public MergeField<string> Notes { get; } = TextMerge.Compare(original.Notes, local.Notes, current.Notes).Field;
    public IReadOnlyList<Guid> Skills { get; } = CollectionMerge.Membership(original.SkillIds, local.SkillIds, current.SkillIds);
    public string? RetainedNotes { get; private set; }
    public bool CanApply => Title.IsResolved &&
        Role.IsResolved &&
        Requester.IsResolved &&
        DeliveryUnit.IsResolved &&
        Start.IsResolved &&
        End.IsResolved &&
        Allocation.IsResolved &&
        Status.IsResolved &&
        Notes.IsResolved;

    public StaffingRequestEditorModel Candidate() {
        if (!CanApply) {
            throw new InvalidOperationException("Resolve the comparison before applying it.");
        }
        var model = Current.Snapshot();
        model.Title = Title.Result;
        model.NeededRole = Role.Result;
        model.RequestedByPartyId = Requester.Result;
        model.DeliveryUnitPartyId = DeliveryUnit.Result;
        model.StartDate = Start.Result;
        model.EndDate = End.Result;
        model.AllocationPercent = Allocation.Result;
        model.Status = Status.Result;
        model.Notes = Notes.Result;
        model.SkillIds = [.. Skills];
        return model;
    }

    public void PreserveChoices(StaffingRequestReview previous) {
        RetainedNotes = previous.Notes.Choice == MergeChoice.Custom ? previous.Notes.Result : previous.RetainedNotes;
        AssignmentReviewChoices.Keep(previous.Title, Title);
        AssignmentReviewChoices.Keep(previous.Role, Role);
        AssignmentReviewChoices.Keep(previous.Requester, Requester);
        AssignmentReviewChoices.Keep(previous.DeliveryUnit, DeliveryUnit);
        AssignmentReviewChoices.Keep(previous.Start, Start);
        AssignmentReviewChoices.Keep(previous.End, End);
        AssignmentReviewChoices.Keep(previous.Allocation, Allocation);
        AssignmentReviewChoices.Keep(previous.Status, Status);
        AssignmentReviewChoices.Keep(previous.Notes, Notes);
    }
}
