using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.UI.History;
using CanDoItAll.AppComponents.Navigation;

namespace CanDoItAll.Modules.AgentFramework.Pages.Navigation;

public sealed record AgentsHistoryUrlState(ProviderHistoryRange Range = ProviderHistoryRange.Last24Hours) {
    public DateTimeOffset? To { get; init; }
    public DateTimeOffset? From { get; init; }
    public Guid? ProviderId { get; init; }
    public string? Model { get; init; }
    public HistoryWorkload? Workload { get; init; }
    public HistoryOperation? Operation { get; init; }
    public HistoryOutcome? Outcome { get; init; }
    public HistoryPriceState? PriceState { get; init; }
    public Guid? CredentialId { get; init; }
    public Guid? RequestId { get; init; }
    public Guid? AttemptId { get; init; }
    public string? CorrelationId { get; init; }
    public int PageSize { get; init; } = ProviderHistoryAppliedFilter.DefaultPageSize;
    public string? Cursor { get; init; }
    public int Page { get; init; } = 1;
    public Guid? EntryId { get; init; }

    public bool IsAnchored => To is not null;

    public AgentsHistoryUrlState AnchoredAt(DateTimeOffset now)
        => To is null ? this with { To = new(now.UtcTicks - now.UtcTicks % TimeSpan.TicksPerSecond, TimeSpan.Zero), Cursor = null, Page = 1 } : this;

    public ProviderHistoryLocation ToLocation() {
        var to = To ?? throw new InvalidOperationException("Anchor the history state before it is shown.");
        return new(new(Range, to) {
            From = From,
            ProviderId = ProviderId,
            Model = Model,
            Workload = Workload,
            Operation = Operation,
            Outcome = Outcome,
            PriceState = PriceState,
            CredentialId = CredentialId,
            RequestId = RequestId,
            AttemptId = AttemptId,
            CorrelationId = CorrelationId,
            PageSize = PageSize
        }, Cursor, Page, EntryId is { } entry ? new HistoryEntryId(entry) : null);
    }

    public static AgentsHistoryUrlState? FromLocation(ProviderHistoryLocation? location) {
        if (location is null) {
            return null;
        }
        var filter = location.Filter;
        return new(filter.Range) {
            To = filter.To,
            From = filter.Range == ProviderHistoryRange.Custom ? filter.From : null,
            ProviderId = filter.ProviderId,
            Model = filter.Model,
            Workload = filter.Workload,
            Operation = filter.Operation,
            Outcome = filter.Outcome,
            PriceState = filter.PriceState,
            CredentialId = filter.CredentialId,
            RequestId = filter.RequestId,
            AttemptId = filter.AttemptId,
            CorrelationId = filter.CorrelationId,
            PageSize = filter.PageSize,
            Cursor = location.Cursor,
            Page = location.Page,
            EntryId = location.EntryId?.Value
        };
    }
}

public static class AgentsHistoryUrl {
    public const string RangeKey = "range";
    public const string ToKey = "to";
    public const string FromKey = "from";
    public const string ModelKey = "model";
    public const string WorkloadKey = "workload";
    public const string OperationKey = "operation";
    public const string OutcomeKey = "outcome";
    public const string PriceKey = "price";
    public const string CredentialKey = "credentialId";
    public const string RequestKey = "requestId";
    public const string AttemptKey = "attemptId";
    public const string CorrelationKey = "correlationId";
    public const string SizeKey = "size";
    public const string CursorKey = "cursor";
    public const string PageKey = "page";
    public const string EntryKey = "entryId";

    public const int MaximumModelLength = 512;
    public const int MaximumCorrelationLength = 256;
    public const int MaximumCursorLength = 4096;
    public const int MaximumCustomRangeDays = 31;

    public static IReadOnlyList<string> Keys { get; } = [
        RangeKey, ToKey, FromKey, ModelKey, WorkloadKey, OperationKey, OutcomeKey, PriceKey, CredentialKey,
        RequestKey, AttemptKey, CorrelationKey, SizeKey, CursorKey, PageKey, EntryKey
    ];

    public static UrlTokenMap<ProviderHistoryRange> Ranges { get; } = new(
        (ProviderHistoryRange.Last24Hours, "24h"),
        (ProviderHistoryRange.Last7Days, "7d"),
        (ProviderHistoryRange.Custom, "custom"));

    public static UrlTokenMap<HistoryWorkload> Workloads { get; } = new(
        (HistoryWorkload.Direct, "direct"),
        (HistoryWorkload.Agent, "agent"),
        (HistoryWorkload.SimpleChat, "simple-chat"),
        (HistoryWorkload.Workflow, "workflow"),
        (HistoryWorkload.Process, "process"),
        (HistoryWorkload.Batch, "batch"),
        (HistoryWorkload.SharedRelay, "shared-relay"),
        (HistoryWorkload.Diagnostic, "diagnostic"));

    public static UrlTokenMap<HistoryOperation> Operations { get; } = new(
        (HistoryOperation.CompleteChat, "complete-chat"),
        (HistoryOperation.AnalyzeImage, "analyze-image"),
        (HistoryOperation.GenerateImage, "generate-image"),
        (HistoryOperation.EditImage, "edit-image"),
        (HistoryOperation.TranscribeSpeech, "transcribe-speech"),
        (HistoryOperation.SynthesizeSpeech, "synthesize-speech"),
        (HistoryOperation.ListModels, "list-models"),
        (HistoryOperation.TestHealth, "test-health"),
        (HistoryOperation.CreateOrUpdateModel, "create-or-update-model"));

    public static UrlTokenMap<HistoryOutcome> Outcomes { get; } = new(
        (HistoryOutcome.Started, "started"),
        (HistoryOutcome.Succeeded, "succeeded"),
        (HistoryOutcome.Failed, "failed"),
        (HistoryOutcome.Cancelled, "cancelled"),
        (HistoryOutcome.TimedOut, "timed-out"),
        (HistoryOutcome.Interrupted, "interrupted"),
        (HistoryOutcome.Unknown, "unknown"));

    public static UrlTokenMap<HistoryPriceState> Prices { get; } = new(
        (HistoryPriceState.Unpriced, "unpriced"),
        (HistoryPriceState.ProviderReported, "provider-reported"),
        (HistoryPriceState.CalculatedAtExecution, "calculated-at-execution"),
        (HistoryPriceState.ExplicitFree, "explicit-free"),
        (HistoryPriceState.PartialEstimate, "partial-estimate"),
        (HistoryPriceState.MissingTariff, "missing-tariff"),
        (HistoryPriceState.MissingUsage, "missing-usage"),
        (HistoryPriceState.UnsupportedUnit, "unsupported-unit"),
        (HistoryPriceState.InvalidEvidence, "invalid-evidence"));

    public static AgentsHistoryUrlState? Read(UrlQuery query, string providerKey, bool allowProvider) {
        ArgumentNullException.ThrowIfNull(query);
        if (!Keys.Any(query.Contains) && !(allowProvider && query.Contains(providerKey))) {
            return null;
        }
        var range = query.GetToken(RangeKey, Ranges, ProviderHistoryRange.Last24Hours);
        var to = query.GetInstant(ToKey);
        var from = range == ProviderHistoryRange.Custom ? query.GetInstant(FromKey) : null;
        if (range == ProviderHistoryRange.Custom &&
            (to is not { } end || from is not { } start || end <= start || end - start > TimeSpan.FromDays(MaximumCustomRangeDays))) {
            return null;
        }
        var cursor = query.GetText(CursorKey, MaximumCursorLength);
        return new(range) {
            To = to,
            From = from,
            ProviderId = allowProvider ? query.GetId(providerKey) : null,
            Model = query.GetText(ModelKey, MaximumModelLength),
            Workload = query.GetToken(WorkloadKey, Workloads),
            Operation = query.GetToken(OperationKey, Operations),
            Outcome = query.GetToken(OutcomeKey, Outcomes),
            PriceState = query.GetToken(PriceKey, Prices),
            CredentialId = query.GetId(CredentialKey),
            RequestId = query.GetId(RequestKey),
            AttemptId = query.GetId(AttemptKey),
            CorrelationId = query.GetText(CorrelationKey, MaximumCorrelationLength),
            PageSize = query.GetNumber(SizeKey, 1, 200) ?? ProviderHistoryAppliedFilter.DefaultPageSize,
            Cursor = to is null ? null : cursor,
            Page = to is not null && cursor is not null ? query.GetPage(PageKey) : 1,
            EntryId = query.GetId(EntryKey)
        };
    }

    public static void Write(AgentsHistoryUrlState? state, UrlQueryWriter query, string providerKey, bool allowProvider) {
        ArgumentNullException.ThrowIfNull(query);
        if (state is null) {
            return;
        }
        query.Token(RangeKey, (ProviderHistoryRange?)state.Range, Ranges);
        query.Instant(ToKey, state.To);
        if (state.Range == ProviderHistoryRange.Custom) {
            query.Instant(FromKey, state.From);
        }
        if (allowProvider) {
            query.Id(providerKey, state.ProviderId);
        }
        query.Text(ModelKey, state.Model);
        query.Token(WorkloadKey, state.Workload, Workloads);
        query.Token(OperationKey, state.Operation, Operations);
        query.Token(OutcomeKey, state.Outcome, Outcomes);
        query.Token(PriceKey, state.PriceState, Prices);
        query.Id(CredentialKey, state.CredentialId);
        query.Id(RequestKey, state.RequestId);
        query.Id(AttemptKey, state.AttemptId);
        query.Text(CorrelationKey, state.CorrelationId);
        query.Number(SizeKey, state.PageSize, ProviderHistoryAppliedFilter.DefaultPageSize);
        if (state.To is not null && state.Cursor is not null) {
            query.Text(CursorKey, state.Cursor);
            query.Page(PageKey, state.Page);
        }
        query.Id(EntryKey, state.EntryId);
    }
}
