using System.ComponentModel;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.CrmHr;

[Description("Immutable workforce values inside an original profile observation. Send the entire owner-read snapshot unchanged; proposed replacements belong in the editor request fields.")]
public sealed record WorkforceProfileValues(
    [property: Description("Original workforce relationship kind as a JSON integer; retain the owner-read value unchanged. Values: 0 Employee, 1 Contractor, 2 Freelancer, 3 DeliveryUnit.")] WorkforceKind WorkforceKind,
    [property: Description("Original internal worker identifier text; an empty string means no employee code was recorded.")] string EmployeeCode,
    [property: Description("Original worker job title, retained as part of the conditional profile snapshot.")] string JobTitle,
    [property: Description("Original professional discipline text; empty means no discipline was recorded.")] string Discipline,
    [property: Description("Original seniority label; this snapshot does not infer or change the worker level.")] string Seniority,
    [property: Description("Original home organizational-unit party GUID, or null when no home unit was assigned.")] Guid? HomeUnitPartyId,
    [property: Description("Original manager party GUID, or null when no manager was assigned.")] Guid? ManagerPartyId,
    [property: Description("Original workforce start calendar date in yyyy-MM-dd form; null means unknown.")] DateOnly? StartDate,
    [property: Description("Original workforce end calendar date in yyyy-MM-dd form; null means no end date was recorded.")] DateOnly? EndDate,
    [property: Description("Original worker location text, retained exactly in the original observation.")] string Location,
    [property: Description("Original worker time-zone identifier text; it does not change the UTC meaning of other API instants.")] string TimeZone,
    [property: Description("Original internal cost amount per RateUnit in RateCurrencyCode; null means unknown rather than zero.")] decimal? InternalCostRate,
    [property: Description("Original external billing amount per RateUnit in RateCurrencyCode; null means unknown rather than zero.")] decimal? ExternalBillingRate,
    [property: Description("Original rate denominator as a JSON integer ProjectResourceRateUnit; applies to both cost and billing amounts. Values: 0 Hour, 1 ManDay.")] ProjectResourceRateUnit RateUnit,
    [property: Description("Original currency code used by both rate amounts; it is not an exchange rate.")] string RateCurrencyCode,
    [property: Description("Original worker capacity in hours per week; this is an effort capacity rather than elapsed calendar duration.")] decimal CapacityHoursPerWeek,
    [property: Description("Original workforce status text, retained as part of the conditional profile snapshot.")] string Status,
    [property: Description("Original workforce notes visible under the current CRM/HR authority; send unchanged inside the original snapshot.")] string Notes) {
    public static WorkforceProfileValues From(WorkforceProfileEditorModel model) => new(model.WorkforceKind,
        model.EmployeeCode, model.JobTitle, model.Discipline, model.Seniority, model.HomeUnitPartyId, model.ManagerPartyId,
        model.StartDate, model.EndDate, model.Location, model.TimeZone, model.InternalCostRate, model.ExternalBillingRate,
        model.RateUnit, model.RateCurrencyCode, model.CapacityHoursPerWeek, model.Status, model.Notes);

    public WorkforceProfileEditorModel ToEditor(Guid partyId, Guid? id, string actor) {
        var model = new WorkforceProfileEditorModel { Id = id, PartyId = partyId, LastChangedBy = actor };
        ApplyTo(model);
        return model;
    }

    public void ApplyTo(WorkforceProfileEditorModel model) {
        model.WorkforceKind = WorkforceKind;
        model.EmployeeCode = EmployeeCode;
        model.JobTitle = JobTitle;
        model.Discipline = Discipline;
        model.Seniority = Seniority;
        model.HomeUnitPartyId = HomeUnitPartyId;
        model.ManagerPartyId = ManagerPartyId;
        model.StartDate = StartDate;
        model.EndDate = EndDate;
        model.Location = Location;
        model.TimeZone = TimeZone;
        model.InternalCostRate = InternalCostRate;
        model.ExternalBillingRate = ExternalBillingRate;
        model.RateUnit = RateUnit;
        model.RateCurrencyCode = RateCurrencyCode;
        model.CapacityHoursPerWeek = CapacityHoursPerWeek;
        model.Status = Status;
        model.Notes = Notes;
    }
}

[Description("Owner-read identity and values of a party workforce profile. A null profile ID and null Values establish absence for create-only intent; otherwise retain the full observation for replacement.")]
public sealed record WorkforceProfileObservation(
    [property: Description("GUID of the party owning the observed workforce profile; it must match the addressed worker.")] Guid PartyId,
    [property: Description("Original party type as a JSON integer, included to reject replacement after the party changes type. Values: 0 Person, 1 Organization, 2 OrganizationUnit, 3 AiAgent.")] PartyType PartyType,
    [property: Description("Observed workforce profile GUID; null records that no profile existed at the owner read.")] Guid? ProfileId,
    [property: Description("Complete original workforce values; null accompanies an absent profile and is not an empty replacement.")] WorkforceProfileValues? Values);

[Description("Immutable owner-read worker skill row used to reject stale replacement; retain all fields together and do not move the row between workers.")]
public sealed record PartySkillObservation(
    [property: Description("GUID of the observed worker skill row, distinct from the skill definition identifier.")] Guid Id,
    [property: Description("GUID of the worker party owning this observed skill row.")] Guid PartyId,
    [property: Description("GUID of the skill definition referenced by the original worker skill row.")] Guid SkillId,
    [property: Description("Original proficiency level as a JSON integer SkillProficiencyLevel. Values: 0 Basic, 1 Working, 2 Strong, 3 Expert.")] SkillProficiencyLevel Proficiency,
    [property: Description("Original whole number of experience years recorded for this worker skill.")] int YearsExperience,
    [property: Description("Original certification status text; an empty string means none was recorded.")] string CertificationStatus,
    [property: Description("Original validation calendar date in yyyy-MM-dd form; null means not recorded.")] DateOnly? LastValidatedOn,
    [property: Description("Original worker skill notes; retain them unchanged inside the concurrency observation.")] string Notes);

[Description("Immutable owner-read capacity block used as the original for conditional replacement; proposed schedule and allocation values are separate request fields.")]
public sealed record CapacityBlockObservation(
    [property: Description("GUID of the observed capacity block, retained for replacement of that exact row.")] Guid Id,
    [property: Description("GUID of the worker party owning the original capacity block.")] Guid PartyId,
    [property: Description("Original capacity block kind as a JSON integer CapacityBlockKind. Values: 0 Leave, 1 Unavailable, 2 Reserve, 3 Tentative.")] CapacityBlockKind BlockKind,
    [property: Description("Original start calendar date in yyyy-MM-dd form; retain the owner-read range unchanged.")] DateOnly StartDate,
    [property: Description("Original end calendar date in yyyy-MM-dd form; retain the owner-read range unchanged.")] DateOnly EndDate,
    [property: Description("Original allocation percentage; this number is a percent rather than hours or a fractional ratio.")] decimal Percentage,
    [property: Description("Original related project GUID, or null when the block was not attached to a project.")] Guid? RelatedProjectId,
    [property: Description("Original capacity block notes, retained unchanged for concurrency comparison.")] string Notes);

public sealed record WorkforceSaveReceipt<TModel>(Guid Id, TModel Accepted);

public sealed record WorkforceDeleteReceipt(Guid PartyId, Guid Id);

public interface IWorkforceProfileEditing {
    Task<WorkforceProfileWorkspaceModel?> GetWorkforceProfileWorkspaceAsync(Guid partyId, CancellationToken cancellationToken = default);
    Task<(WorkforceProfileEditorModel Profile, IReadOnlyDictionary<Guid, string> Names)?> ReadWorkforceProfileEditorAsync(Guid partyId, CancellationToken cancellationToken);
    Task<Result<Guid>> SaveWorkforceProfileAsync(WorkforceProfileEditorModel model, CancellationToken cancellationToken = default,
        Action<WorkforceSaveReceipt<WorkforceProfileEditorModel>>? onCommitted = null);
}
