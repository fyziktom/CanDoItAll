using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.CrmHr.UI.Assignments;

public sealed class AssignmentReview(ProjectPartyAssignmentUpsertRequest original, ProjectPartyAssignmentUpsertRequest local, ProjectPartyAssignmentUpsertRequest current) {
    public ProjectPartyAssignmentUpsertRequest Current { get; } = current.Snapshot();
    public ProjectPartyAssignmentUpsertRequest Local { get; } = local.Snapshot();
    public MergeField<Guid> Party { get; } = new(original.PartyId, local.PartyId, current.PartyId);
    public MergeField<Guid?> Affiliation { get; } = new(original.PartyAffiliationId, local.PartyAffiliationId, current.PartyAffiliationId);
    public MergeField<ProjectPartyAssignmentRole> Role { get; } = new(original.Role, local.Role, current.Role);
    public MergeField<string> Node { get; } = new(original.NodeKey, local.NodeKey, current.NodeKey);
    public MergeField<bool> Primary { get; } = new(original.IsPrimary, local.IsPrimary, current.IsPrimary);
    public MergeField<decimal?> Allocation { get; } = new(original.AllocationPercent, local.AllocationPercent, current.AllocationPercent);
    public MergeField<DateOnly?> Start { get; } = new(original.StartsOn, local.StartsOn, current.StartsOn);
    public MergeField<DateOnly?> End { get; } = new(original.EndsOn, local.EndsOn, current.EndsOn);
    public MergeField<string> Source { get; } = new(original.Source, local.Source, current.Source);
    public MergeField<string> Notes { get; } = TextMerge.Compare(original.Notes, local.Notes, current.Notes).Field;
    public string? RetainedNotes { get; private set; }
    public bool CanApply => Party.IsResolved &&
        Affiliation.IsResolved &&
        Role.IsResolved &&
        Node.IsResolved &&
        Primary.IsResolved &&
        Allocation.IsResolved &&
        Start.IsResolved &&
        End.IsResolved &&
        Source.IsResolved &&
        Notes.IsResolved;

    public ProjectPartyAssignmentUpsertRequest Candidate() {
        if (!CanApply) {
            throw new InvalidOperationException("Resolve the comparison before applying it.");
        }
        var model = Current.Snapshot();
        model.PartyId = Party.Result;
        model.PartyAffiliationId = Affiliation.Result;
        model.Role = Role.Result;
        model.NodeKey = Node.Result;
        model.IsPrimary = Primary.Result;
        model.AllocationPercent = Allocation.Result;
        model.StartsOn = Start.Result;
        model.EndsOn = End.Result;
        model.Source = Source.Result;
        model.Notes = Notes.Result;
        return model;
    }

    public void PreserveChoices(AssignmentReview previous) {
        RetainedNotes = previous.Notes.Choice == MergeChoice.Custom ? previous.Notes.Result : previous.RetainedNotes;
        AssignmentReviewChoices.Keep(previous.Party, Party);
        AssignmentReviewChoices.Keep(previous.Affiliation, Affiliation);
        AssignmentReviewChoices.Keep(previous.Role, Role);
        AssignmentReviewChoices.Keep(previous.Node, Node);
        AssignmentReviewChoices.Keep(previous.Primary, Primary);
        AssignmentReviewChoices.Keep(previous.Allocation, Allocation);
        AssignmentReviewChoices.Keep(previous.Start, Start);
        AssignmentReviewChoices.Keep(previous.End, End);
        AssignmentReviewChoices.Keep(previous.Source, Source);
        AssignmentReviewChoices.Keep(previous.Notes, Notes);
    }
}
