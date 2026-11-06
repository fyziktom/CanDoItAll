using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.CrmHr.UI.Workforce;

public enum WorkforceProfileField { Kind, EmployeeCode, JobTitle, Discipline, Seniority, HomeUnit, Manager, Dates, Location, TimeZone, Rates, Capacity, Status, Notes }
public readonly record struct WorkforceProfileInput(WorkforceProfileField Field, bool Pending = false);

public sealed record WorkforceDateRange(DateOnly? Start, DateOnly? End) {
    public static WorkforceDateRange From(WorkforceProfileEditorModel model) => new(model.StartDate, model.EndDate);
    public void ApplyTo(WorkforceProfileEditorModel model) {
        model.StartDate = Start;
        model.EndDate = End;
    }
}

public sealed record WorkforceRates(decimal? Internal, decimal? External, ProjectResourceRateUnit Unit, string Currency) {
    public static WorkforceRates From(WorkforceProfileEditorModel model) => new(model.InternalCostRate, model.ExternalBillingRate, model.RateUnit, model.RateCurrencyCode);
    public void ApplyTo(WorkforceProfileEditorModel model) {
        model.InternalCostRate = Internal;
        model.ExternalBillingRate = External;
        model.RateUnit = Unit;
        model.RateCurrencyCode = Currency;
    }
}

public sealed class WorkforceProfileReview(WorkforceProfileEditorModel original, WorkforceProfileEditorModel local, WorkforceProfileEditorModel current) {
    public WorkforceProfileEditorModel Current { get; } = current.Snapshot();
    public MergeField<WorkforceKind> Kind { get; } = new(original.WorkforceKind, local.WorkforceKind, current.WorkforceKind);
    public MergeField<string> EmployeeCode { get; } = new(original.EmployeeCode, local.EmployeeCode, current.EmployeeCode);
    public MergeField<string> JobTitle { get; } = new(original.JobTitle, local.JobTitle, current.JobTitle);
    public MergeField<string> Discipline { get; } = new(original.Discipline, local.Discipline, current.Discipline);
    public MergeField<string> Seniority { get; } = new(original.Seniority, local.Seniority, current.Seniority);
    public MergeField<Guid?> HomeUnit { get; } = new(original.HomeUnitPartyId, local.HomeUnitPartyId, current.HomeUnitPartyId);
    public MergeField<Guid?> Manager { get; } = new(original.ManagerPartyId, local.ManagerPartyId, current.ManagerPartyId);
    public MergeField<WorkforceDateRange> Dates { get; } = new(WorkforceDateRange.From(original), WorkforceDateRange.From(local), WorkforceDateRange.From(current));
    public MergeField<string> Location { get; } = new(original.Location, local.Location, current.Location);
    public MergeField<string> TimeZone { get; } = new(original.TimeZone, local.TimeZone, current.TimeZone);
    public MergeField<WorkforceRates> Rates { get; } = new(WorkforceRates.From(original), WorkforceRates.From(local), WorkforceRates.From(current));
    public MergeField<decimal> Capacity { get; } = new(original.CapacityHoursPerWeek, local.CapacityHoursPerWeek, current.CapacityHoursPerWeek);
    public MergeField<string> Status { get; } = new(original.Status, local.Status, current.Status);
    public MergeField<string> Notes { get; } = TextMerge.Compare(original.Notes, local.Notes, current.Notes).Field;
    public bool CanApply => Kind.IsResolved && EmployeeCode.IsResolved && JobTitle.IsResolved && Discipline.IsResolved && Seniority.IsResolved &&
        HomeUnit.IsResolved && Manager.IsResolved && Dates.IsResolved && Location.IsResolved && TimeZone.IsResolved && Rates.IsResolved &&
        Capacity.IsResolved && Status.IsResolved && Notes.IsResolved;

    public WorkforceProfileEditorModel Candidate() {
        var model = Current.Snapshot();
        model.WorkforceKind = Chosen(Kind);
        model.EmployeeCode = Chosen(EmployeeCode);
        model.JobTitle = Chosen(JobTitle);
        model.Discipline = Chosen(Discipline);
        model.Seniority = Chosen(Seniority);
        model.HomeUnitPartyId = Chosen(HomeUnit);
        model.ManagerPartyId = Chosen(Manager);
        Chosen(Dates).ApplyTo(model);
        model.Location = Chosen(Location);
        model.TimeZone = Chosen(TimeZone);
        Chosen(Rates).ApplyTo(model);
        model.CapacityHoursPerWeek = Chosen(Capacity);
        model.Status = Chosen(Status);
        model.Notes = Chosen(Notes);
        return model;
    }

    public void PreserveChoices(WorkforceProfileReview previous) {
        Keep(previous.Kind, Kind);
        Keep(previous.EmployeeCode, EmployeeCode);
        Keep(previous.JobTitle, JobTitle);
        Keep(previous.Discipline, Discipline);
        Keep(previous.Seniority, Seniority);
        Keep(previous.HomeUnit, HomeUnit);
        Keep(previous.Manager, Manager);
        Keep(previous.Dates, Dates);
        Keep(previous.Location, Location);
        Keep(previous.TimeZone, TimeZone);
        Keep(previous.Rates, Rates);
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

    private static T Chosen<T>(MergeField<T> field) => field.IsResolved ? field.Result : field.Local;
}
