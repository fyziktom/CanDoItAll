using System.ComponentModel;
using System.Text.Json;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.CrmHr;

[Description("Immutable business-stewardship observation for an AI party. It protects the stewardship fields and party type; it grants no technical Agent execution or provider authority.")]
public sealed record AiStewardProfileObservation(
    [property: Description("GUID of the AI party owning this stewardship observation.")] Guid PartyId,
    [property: Description("Original party type as a JSON integer; replacement rejects a party that is no longer an AI party. Values: 0 Person, 1 Organization, 2 OrganizationUnit, 3 AiAgent.")] PartyType PartyType,
    [property: Description("Observed stewardship profile GUID; null records an absent profile for create-only intent.")] Guid? ProfileId,
    [property: Description("Original responsible steward party GUID, or null when none was assigned.")] Guid? OwnerPartyId,
    [property: Description("Original business review status as a JSON integer AiValidationStatus; it does not grant runtime permission.")] AiValidationStatus ValidationStatus,
    [property: Description("Original stored review instant with UTC offset; null means no review was recorded.")] DateTimeOffset? LastReviewedAtUtc,
    [property: Description("Original business stewardship notes; technical Agent configuration is outside this observation.")] string Notes);

public sealed record AiProfileTechnicalInput(Guid? ProviderProfileId, string DefaultModel, AiExecutionMode ExecutionMode,
    string ExtendedDataJson, string CapabilitiesJson) {
    public static AiProfileTechnicalInput From(AiAgentProfileEditorModel model) => new(model.ProviderProfileId,
        model.DefaultModel, model.ExecutionMode, model.ExtendedDataJson, JsonSerializer.Serialize(model.Capabilities));
}

public sealed record AiAgentProfileObservation(AiStewardProfileObservation Business, AiProfileTechnicalInput Technical);

[Description("Editable business stewardship profile for an AI party, accompanied by its exact original. Technical Agent provider, model, capabilities and execution settings are not part of this save.")]
public sealed class AiStewardProfileEditorModel {
    [Description("Existing stewardship profile GUID, or null for create-only intent while the original proves profile absence.")]
    public Guid? Id { get; set; }
    [Description("GUID of the addressed AI party; it must match the party in Original.")]
    public Guid PartyId { get; set; }
    [Description("Proposed responsible steward party GUID; null clears the assignment.")]
    public Guid? OwnerPartyId { get; set; }
    [Description("Proposed business review status as a JSON integer AiValidationStatus; this is not a runtime capability grant.")]
    public AiValidationStatus ValidationStatus { get; set; } = AiValidationStatus.Draft;
    [Description("Proposed review calendar date in yyyy-MM-dd form; null means no review date.")]
    public DateOnly? LastReviewedOn { get; set; }
    [Description("Proposed business stewardship notes, replacing the prior notes on an accepted save.")]
    public string Notes { get; set; } = string.Empty;
    [Description("Caller-supplied audit display label; it never establishes authentication or authorization.")]
    public string LastChangedBy { get; set; } = "crm-hr-ui";
    [Description("Required exact stewardship observation returned by GET for this AI party. Retain it unchanged, including when ProfileId is null and proves an absent profile; it grants no write authority.")]
    public AiStewardProfileObservation? Original { get; set; }
    public AiStewardProfileEditorModel Snapshot() => (AiStewardProfileEditorModel)MemberwiseClone();
}

[Description("Exact accepted stewardship result returned after its native save; retain the returned identity and accepted original before choosing a later write.")]
public sealed record AiStewardProfileSaveReceipt(
    [property: Description("GUID of the accepted stewardship profile record.")] Guid Id,
    [property: Description("Normalized accepted stewardship model with the new owner-read original for a subsequent conditional save.")] AiStewardProfileEditorModel Accepted);

public interface IAiStewardProfileEditing {
    Task<AiStewardProfileEditorModel?> ReadAsync(Guid partyId, CancellationToken cancellationToken = default);
    Task<Result<Guid>> SaveAsync(AiStewardProfileEditorModel model, CancellationToken cancellationToken = default,
        Action<AiStewardProfileSaveReceipt>? onCommitted = null);
}

public static class AiStewardEditingErrors {
    public const string OriginalRequired = "crmhr.ai-steward.original-required";
    public const string IdentityInvalid = "crmhr.ai-steward.identity-invalid";
    public const string Conflict = "crmhr.ai-steward.conflict";
    public const string TechnicalReadOnly = "crmhr.ai-steward.technical-read-only";
    public const string InvalidTechnicalJson = "crmhr.ai-agent.extended-data-invalid";
    public const string OwnerInvalid = "crmhr.ai-steward.owner-invalid";
    public const string PartyTypeConflict = "crmhr.ai-steward.party-type-conflict";
}
