using System.Collections.Immutable;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.CrmHr;

public sealed record StaffingRequestObservation(Guid Id, Guid? ProjectId, Guid? ProjectLifetimeId,
    Guid? RequestedByPartyId, Guid? DeliveryUnitPartyId, string Title, string NeededRole,
    ImmutableArray<Guid> SkillIds, DateTimeOffset? StartDateUtc, DateTimeOffset? EndDateUtc,
    decimal AllocationPercent, StaffingRequestStatus Status, string Notes);

public sealed record StaffingRequestSaveReceipt(Guid Id, StaffingRequestEditorModel Accepted);
public sealed record StaffingRequestDeleteReceipt(Guid Id, StaffingRequestObservation Original);

public interface IStaffingRequestEditing {
    Task<StaffingRequestEditorModel?> ReadAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<Guid>> SaveAsync(StaffingRequestEditorModel model, CancellationToken cancellationToken = default,
        Action<StaffingRequestSaveReceipt>? onCommitted = null);
    Task<Result> DeleteAsync(Guid id, StaffingRequestObservation? original, CancellationToken cancellationToken = default,
        Action<StaffingRequestDeleteReceipt>? onCommitted = null);
}

public static class StaffingRequestEditingErrors {
    public const string OriginalRequired = "crmhr.staffing-request.original-required";
    public const string IdentityInvalid = "crmhr.staffing-request.identity-invalid";
    public const string Conflict = "crmhr.staffing-request.conflict";
    public const string ProjectReferenceInvalid = "crmhr.staffing-request.project-reference-invalid";
    public const string TitleRequired = "crmhr.staffing-request.title-required";
    public const string RoleRequired = "crmhr.staffing-request.role-required";
    public const string AllocationRange = "crmhr.staffing-request.allocation-range";
    public const string DateRange = "crmhr.staffing-request.date-range-invalid";
    public const string RequesterMissing = "crmhr.staffing-request.requester-not-found";
    public const string DeliveryUnitMissing = "crmhr.staffing-request.delivery-unit-not-found";
    public const string SkillMissing = "crmhr.staffing-request.skill-not-found";
}
