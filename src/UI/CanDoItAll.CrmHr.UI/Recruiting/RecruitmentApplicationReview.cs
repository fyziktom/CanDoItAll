using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Recruiting;

public sealed record RecruitmentDecisionValues(RecruitmentStage Stage, RecruitmentDecision Decision);

public sealed class RecruitmentApplicationReview(RecruitmentApplicationEditorModel original,
    RecruitmentApplicationEditorModel local, RecruitmentApplicationEditorModel current) {
    public RecruitmentApplicationEditorModel Current { get; } = current.Snapshot();
    public MergeField<string> Role { get; } = new(original.DesiredRole, local.DesiredRole, current.DesiredRole);
    public MergeField<Guid?> TargetUnit { get; } = new(original.TargetUnitPartyId, local.TargetUnitPartyId, current.TargetUnitPartyId);
    public MergeField<Guid?> Recruiter { get; } = new(original.RecruiterPartyId, local.RecruiterPartyId, current.RecruiterPartyId);
    public MergeField<Guid?> HiringManager { get; } = new(original.HiringManagerPartyId, local.HiringManagerPartyId, current.HiringManagerPartyId);
    public MergeField<string> Source { get; } = new(original.Source, local.Source, current.Source);
    public MergeField<DateOnly?> AvailableFrom { get; } = new(original.AvailableFrom, local.AvailableFrom, current.AvailableFrom);
    public MergeField<RecruitmentDecisionValues> Decision { get; } = new(new(original.Stage, original.Decision), new(local.Stage, local.Decision), new(current.Stage, current.Decision));
    public MergeField<string> Notes { get; } = TextMerge.Compare(original.Notes, local.Notes, current.Notes).Field;
    public bool CanApply => Role.IsResolved && TargetUnit.IsResolved && Recruiter.IsResolved && HiringManager.IsResolved &&
        Source.IsResolved && AvailableFrom.IsResolved && Decision.IsResolved && Notes.IsResolved;

    public RecruitmentApplicationEditorModel Candidate(RecruitmentApplicationEditorModel local) {
        var model = Current.Snapshot();
        model.DesiredRole = Role.Result;
        model.TargetUnitPartyId = TargetUnit.Result;
        model.RecruiterPartyId = Recruiter.Result;
        model.HiringManagerPartyId = HiringManager.Result;
        model.Source = Source.Result;
        model.AvailableFrom = AvailableFrom.Result;
        model.Stage = Decision.Result.Stage;
        model.Decision = Decision.Result.Decision;
        model.Notes = Notes.Result;
        model.StageNotes = local.StageNotes;
        return model;
    }

    public void PreserveChoices(RecruitmentApplicationReview previous) {
        Keep(previous.Role, Role);
        Keep(previous.TargetUnit, TargetUnit);
        Keep(previous.Recruiter, Recruiter);
        Keep(previous.HiringManager, HiringManager);
        Keep(previous.Source, Source);
        Keep(previous.AvailableFrom, AvailableFrom);
        Keep(previous.Decision, Decision);
        Keep(previous.Notes, Notes);
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
