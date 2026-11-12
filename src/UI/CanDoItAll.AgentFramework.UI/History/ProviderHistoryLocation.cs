using System.ComponentModel.DataAnnotations;
using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.AgentFramework.UI.History;

public sealed record ProviderHistoryAppliedFilter(ProviderHistoryRange Range, DateTimeOffset To) {
    public const int DefaultPageSize = 50;

    public DateTimeOffset? From { get; init; }
    public Guid? ProviderId { get; init; }
    public string? Model { get; init; }
    public HistoryWorkload? Workload { get; init; }
    public HistoryOperation? Operation { get; init; }
    public HistoryOutcome? Outcome { get; init; }
    public HistoryPriceState? PriceState { get; init; }
    public Guid? CredentialId { get; init; }
    public string? Subject { get; init; }
    public string? Issuer { get; init; }
    public Guid? RequestId { get; init; }
    public Guid? AttemptId { get; init; }
    public string? CorrelationId { get; init; }
    public string? ExternalReferenceType { get; init; }
    public string? ExternalReferenceValue { get; init; }
    public int PageSize { get; init; } = DefaultPageSize;

    public static ProviderHistoryAppliedFilter Capture(ProviderHistoryFilterDraft draft, DateTimeOffset requestedAtUtc) {
        ArgumentNullException.ThrowIfNull(draft);
        Validator.ValidateObject(draft, new(draft), validateAllProperties: true);
        var custom = draft.Range == ProviderHistoryRange.Custom;
        return new(draft.Range, Seconds(custom ? AsUtc(draft.ToUtc) : requestedAtUtc.ToUniversalTime())) {
            From = custom ? Seconds(AsUtc(draft.FromUtc)) : null,
            ProviderId = ParseId(draft.ProviderId),
            Model = Optional(draft.Model),
            Workload = draft.Workload,
            Operation = draft.Operation,
            Outcome = draft.Outcome,
            PriceState = draft.PriceState,
            CredentialId = ParseId(draft.CredentialId),
            Subject = Optional(draft.Subject),
            Issuer = Optional(draft.Issuer),
            RequestId = ParseId(draft.RequestId),
            AttemptId = ParseId(draft.AttemptId),
            CorrelationId = Optional(draft.CorrelationId),
            ExternalReferenceType = Optional(draft.ExternalReferenceType),
            ExternalReferenceValue = Optional(draft.ExternalReferenceValue),
            PageSize = draft.PageSize
        };
    }

    public DateTimeOffset FromUtc => Range switch {
        ProviderHistoryRange.Last24Hours => To.AddDays(-1),
        ProviderHistoryRange.Last7Days => To.AddDays(-7),
        ProviderHistoryRange.Custom => From ?? throw new ValidationException("A custom history interval needs its start."),
        _ => throw new ValidationException("Choose a supported time range.")
    };

    public ProviderRequestHistoryQuery ToQuery(HistoryProviderScope fixedScope) {
        ArgumentNullException.ThrowIfNull(fixedScope);
        var scope = fixedScope is HistoryProviderScope.AllAuthorized && ProviderId is { } provider
            ? new HistoryProviderScope.SingleProvider(new(provider)) : fixedScope;
        return new(scope, FromUtc, To) {
            Model = Model is { } model ? new ProviderModelIdentity(model) : null,
            Workload = Workload, Operation = Operation, Outcome = Outcome, PriceState = PriceState,
            CredentialId = CredentialId is { } credential ? new ManagedCredentialId(credential) : null,
            Subject = Subject, Issuer = Issuer,
            RequestId = RequestId is { } request ? new ProviderRequestId(request) : null,
            AttemptId = AttemptId is { } attempt ? new ProviderAttemptId(attempt) : null,
            CorrelationId = CorrelationId,
            ExternalReference = ExternalReferenceValue is { } value ? new(value, ExternalReferenceType) : null,
            PageSize = PageSize
        };
    }

    public ProviderHistoryFilterDraft ToDraft() => new(To) {
        Range = Range,
        FromUtc = FromUtc.UtcDateTime,
        ToUtc = To.UtcDateTime,
        ProviderId = ProviderId?.ToString("D") ?? "",
        Model = Model ?? "",
        Workload = Workload,
        Operation = Operation,
        Outcome = Outcome,
        PriceState = PriceState,
        CredentialId = CredentialId?.ToString("D") ?? "",
        Subject = Subject ?? "",
        Issuer = Issuer ?? "",
        RequestId = RequestId?.ToString("D") ?? "",
        AttemptId = AttemptId?.ToString("D") ?? "",
        CorrelationId = CorrelationId ?? "",
        ExternalReferenceType = ExternalReferenceType ?? "",
        ExternalReferenceValue = ExternalReferenceValue ?? "",
        PageSize = PageSize
    };

    private static DateTimeOffset AsUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    private static DateTimeOffset Seconds(DateTimeOffset value) => new(value.UtcTicks - value.UtcTicks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
    private static string? Optional(string value) => string.IsNullOrEmpty(value) ? null : value;
    private static Guid? ParseId(string value) => string.IsNullOrEmpty(value) ? null : Guid.Parse(value);
}

public sealed record ProviderHistoryLocation(ProviderHistoryAppliedFilter Filter, string? Cursor = null, int Page = 1, HistoryEntryId? EntryId = null);

public enum ProviderHistoryLocationChangeKind { Search, Page, OpenEntry, CloseEntry, Clear }

public sealed record ProviderHistoryLocationChange(ProviderHistoryLocation? Location, ProviderHistoryLocationChangeKind Kind);
