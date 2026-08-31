using System.ComponentModel.DataAnnotations;
using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components.History;

public enum ProviderHistoryRange { Last24Hours, Last7Days, Custom }

public sealed class ProviderHistoryFilterDraft(DateTimeOffset now) : IValidatableObject {
    public ProviderHistoryRange Range { get; set; } = ProviderHistoryRange.Last24Hours;
    public DateTime FromUtc { get; set; } = now.UtcDateTime.AddDays(-1);
    public DateTime ToUtc { get; set; } = now.UtcDateTime;
    public string ProviderId { get; set; } = "";
    [StringLength(512)]
    public string Model { get; set; } = "";
    public HistoryWorkload? Workload { get; set; }
    public HistoryOperation? Operation { get; set; }
    public HistoryOutcome? Outcome { get; set; }
    public HistoryPriceState? PriceState { get; set; }
    public string CredentialId { get; set; } = "";
    [StringLength(512)]
    public string Subject { get; set; } = "";
    [StringLength(512)]
    public string Issuer { get; set; } = "";
    public string RequestId { get; set; } = "";
    public string AttemptId { get; set; } = "";
    [StringLength(256)]
    public string CorrelationId { get; set; } = "";
    [StringLength(HistoryExternalReference.MaximumTypeLength)]
    public string ExternalReferenceType { get; set; } = "";
    [StringLength(HistoryExternalReference.MaximumValueLength)]
    public string ExternalReferenceValue { get; set; } = "";
    [Range(1, 200)]
    public int PageSize { get; set; } = 50;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) {
        if (Model.Length > 0 && string.IsNullOrWhiteSpace(Model)) {
            yield return new("Enter an exact model identity or leave it blank.", [nameof(Model)]);
        }
        if (!Enum.IsDefined(Range)) {
            yield return new("Choose a supported time range.", [nameof(Range)]);
        }
        if (Range == ProviderHistoryRange.Custom && (ToUtc <= FromUtc || ToUtc - FromUtc > TimeSpan.FromDays(31))) {
            yield return new("The UTC interval must be positive and no longer than 31 days.", [nameof(FromUtc), nameof(ToUtc)]);
        }
        foreach (var (text, member) in new[] {
            (ProviderId, nameof(ProviderId)), (CredentialId, nameof(CredentialId)),
            (RequestId, nameof(RequestId)), (AttemptId, nameof(AttemptId))
        }) {
            if (!string.IsNullOrEmpty(text) && (!Guid.TryParse(text, out var id) || id == Guid.Empty)) {
                yield return new("Enter a valid nonempty identifier or leave it blank.", [member]);
            }
        }
        var externalReferenceType = Optional(ExternalReferenceType);
        var externalReferenceValue = Optional(ExternalReferenceValue);
        if (externalReferenceType is not null && externalReferenceValue is null) {
            yield return new("Enter an external reference value when a type is specified.",
                [nameof(ExternalReferenceType), nameof(ExternalReferenceValue)]);
        } else if (externalReferenceValue is not null &&
            !HistoryExternalReference.TryCreate(externalReferenceValue, externalReferenceType, out _)) {
            yield return new("Enter an exact external reference and an optional canonical lowercase type.",
                [nameof(ExternalReferenceType), nameof(ExternalReferenceValue)]);
        }
    }

    public ProviderRequestHistoryQuery ToQuery(HistoryProviderScope fixedScope, DateTimeOffset requestedAtUtc) {
        Validator.ValidateObject(this, new(this), validateAllProperties: true);
        var to = Range == ProviderHistoryRange.Custom ? AsUtc(ToUtc) : requestedAtUtc.ToUniversalTime();
        var from = Range switch {
            ProviderHistoryRange.Last24Hours => to.AddDays(-1),
            ProviderHistoryRange.Last7Days => to.AddDays(-7),
            ProviderHistoryRange.Custom => AsUtc(FromUtc),
            _ => throw new ValidationException("Choose a supported time range.")
        };
        var scope = fixedScope is HistoryProviderScope.AllAuthorized && ParseId(ProviderId) is { } provider
            ? new HistoryProviderScope.SingleProvider(new(provider)) : fixedScope;
        return new(scope, from, to) {
            Model = Optional(Model) is { } model ? new ProviderModelIdentity(model) : null,
            Workload = Workload, Operation = Operation, Outcome = Outcome, PriceState = PriceState,
            CredentialId = ParseId(CredentialId) is { } credential ? new ManagedCredentialId(credential) : null,
            Subject = Optional(Subject), Issuer = Optional(Issuer),
            RequestId = ParseId(RequestId) is { } request ? new ProviderRequestId(request) : null,
            AttemptId = ParseId(AttemptId) is { } attempt ? new ProviderAttemptId(attempt) : null,
            CorrelationId = Optional(CorrelationId),
            ExternalReference = CreateExternalReference(),
            PageSize = PageSize
        };
    }

    private HistoryExternalReference? CreateExternalReference() {
        var value = Optional(ExternalReferenceValue);
        if (value is null) {
            return null;
        }
        return new(value, Optional(ExternalReferenceType));
    }

    private static DateTimeOffset AsUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    private static string? Optional(string value) => string.IsNullOrEmpty(value) ? null : value;
    private static Guid? ParseId(string value) => string.IsNullOrEmpty(value) ? null : Guid.Parse(value);
}
