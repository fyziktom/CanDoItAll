using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Recruiting;

public sealed class RecruitmentSupportReview(RecruitmentSupportAssignmentsEditorModel original, RecruitmentSupportAssignmentsEditorModel local, RecruitmentSupportAssignmentsEditorModel current) {
    public RecruitmentSupportAssignmentsEditorModel Current { get; } = current.Snapshot();
    public MergeField<Guid?> Manager { get; } = new(original.ManagerPartyId, local.ManagerPartyId, current.ManagerPartyId);
    public MergeField<Guid?> Buddy { get; } = new(original.BuddyPartyId, local.BuddyPartyId, current.BuddyPartyId);
    public MergeField<Guid?> Mentor { get; } = new(original.MentorPartyId, local.MentorPartyId, current.MentorPartyId);
    public bool CanApply => Manager.IsResolved && Buddy.IsResolved && Mentor.IsResolved;

    public RecruitmentSupportAssignmentsEditorModel Candidate() {
        var model = Current.Snapshot();
        model.ManagerPartyId = Manager.Result;
        model.BuddyPartyId = Buddy.Result;
        model.MentorPartyId = Mentor.Result;
        return model;
    }
    public void PreserveChoices(RecruitmentSupportReview previous) {
        Keep(previous.Manager, Manager);
        Keep(previous.Buddy, Buddy);
        Keep(previous.Mentor, Mentor);
    }
    private static void Keep<T>(MergeField<T> previous, MergeField<T> current) {
        if (!EqualityComparer<T>.Default.Equals(previous.Local, current.Local)) {
            return;
        }
        switch (previous.Choice) {
            case MergeChoice.Local:
                current.UseLocal();
                break;
            case MergeChoice.Current:
                current.UseCurrent();
                break;
            case MergeChoice.Custom:
                current.SetResult(previous.Result);
                break;
        }
    }
}
