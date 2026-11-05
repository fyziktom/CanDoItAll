using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Workforce;

public sealed class WorkforceSkillReview(PartySkillEditorModel original, PartySkillEditorModel local, PartySkillEditorModel current) {
    public PartySkillEditorModel Current { get; } = current.Snapshot();
    public MergeField<SkillProficiencyLevel> Proficiency { get; } = new(original.Proficiency, local.Proficiency, current.Proficiency);
    public MergeField<int> Years { get; } = new(original.YearsExperience, local.YearsExperience, current.YearsExperience);
    public MergeField<string> Certification { get; } = new(original.CertificationStatus, local.CertificationStatus, current.CertificationStatus);
    public MergeField<DateOnly?> Validated { get; } = new(original.LastValidatedOn, local.LastValidatedOn, current.LastValidatedOn);
    public MergeField<string> Notes { get; } = TextMerge.Compare(original.Notes, local.Notes, current.Notes).Field;
    public bool CanApply => Proficiency.IsResolved && Years.IsResolved && Certification.IsResolved && Validated.IsResolved && Notes.IsResolved;

    public PartySkillEditorModel Candidate() {
        if (!CanApply) {
            throw new InvalidOperationException("Resolve the skill comparison before applying it.");
        }
        var model = Current.Snapshot();
        model.Proficiency = Proficiency.Result;
        model.YearsExperience = Years.Result;
        model.CertificationStatus = Certification.Result;
        model.LastValidatedOn = Validated.Result;
        model.Notes = Notes.Result;
        return model;
    }

    public void PreserveChoices(WorkforceSkillReview previous) {
        Keep(previous.Proficiency, Proficiency);
        Keep(previous.Years, Years);
        Keep(previous.Certification, Certification);
        Keep(previous.Validated, Validated);
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
