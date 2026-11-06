using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Recruiting;

public sealed class RecruitmentConversionReview(RecruitmentConversionEditorModel original, RecruitmentConversionEditorModel local, RecruitmentConversionEditorModel current) {
    public RecruitmentConversionObservation? Original { get; } = original.Original;
    public RecruitmentConversionEditorModel Current { get; } = current.Snapshot();
    public MergeField<WorkforceKind> Kind { get; } = new(original.WorkforceKind, local.WorkforceKind, current.WorkforceKind);
    public MergeField<string> Title { get; } = new(original.JobTitle, local.JobTitle, current.JobTitle);
    public MergeField<string> Discipline { get; } = new(original.Discipline, local.Discipline, current.Discipline);
    public MergeField<string> Seniority { get; } = new(original.Seniority, local.Seniority, current.Seniority);
    public MergeField<Guid?> HomeUnit { get; } = new(original.HomeUnitPartyId, local.HomeUnitPartyId, current.HomeUnitPartyId);
    public MergeField<Guid?> Manager { get; } = new(original.ManagerPartyId, local.ManagerPartyId, current.ManagerPartyId);
    public MergeField<DateOnly?> StartDate { get; } = new(original.StartDate, local.StartDate, current.StartDate);
    public MergeField<string> Location { get; } = new(original.Location, local.Location, current.Location);
    public MergeField<string> TimeZone { get; } = new(original.TimeZone, local.TimeZone, current.TimeZone);
    public MergeField<decimal> Capacity { get; } = new(original.CapacityHoursPerWeek, local.CapacityHoursPerWeek, current.CapacityHoursPerWeek);
    public MergeField<string> Status { get; } = new(original.Status, local.Status, current.Status);
    public MergeField<string> Notes { get; } = TextMerge.Compare(original.Notes, local.Notes, current.Notes).Field;
    public bool CanApply => Kind.IsResolved && Title.IsResolved && Discipline.IsResolved && Seniority.IsResolved && HomeUnit.IsResolved && Manager.IsResolved && StartDate.IsResolved && Location.IsResolved && TimeZone.IsResolved && Capacity.IsResolved && Status.IsResolved && Notes.IsResolved;

    public RecruitmentConversionEditorModel Candidate() {
        var model = Current.Snapshot();
        model.WorkforceKind = Kind.Result;
        model.JobTitle = Title.Result;
        model.Discipline = Discipline.Result;
        model.Seniority = Seniority.Result;
        model.HomeUnitPartyId = HomeUnit.Result;
        model.ManagerPartyId = Manager.Result;
        model.StartDate = StartDate.Result;
        model.Location = Location.Result;
        model.TimeZone = TimeZone.Result;
        model.CapacityHoursPerWeek = Capacity.Result;
        model.Status = Status.Result;
        model.Notes = Notes.Result;
        return model;
    }
    public void PreserveChoices(RecruitmentConversionReview previous) {
        Keep(previous.Kind, Kind);
        Keep(previous.Title, Title);
        Keep(previous.Discipline, Discipline);
        Keep(previous.Seniority, Seniority);
        Keep(previous.HomeUnit, HomeUnit);
        Keep(previous.Manager, Manager);
        Keep(previous.StartDate, StartDate);
        Keep(previous.Location, Location);
        Keep(previous.TimeZone, TimeZone);
        Keep(previous.Capacity, Capacity);
        Keep(previous.Status, Status);
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
