using System.Collections.Immutable;
using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.AgentFramework.UI.History;

public enum HistorySearchPhase { NotRequested, Loading, Canceled, Failed, Ready }
public sealed record HistoryResultsPresentation(HistorySearchPhase Phase, ProviderRequestHistoryQuery? AppliedQuery,
    bool DraftChanged, HistoryFailure? Failure, ImmutableArray<HistoryEntry> Entries,
    HistoryCoverage? Coverage, DateTimeOffset? QueriedAtUtc, int PageNumber,
    bool CanPrevious, bool CanNext, bool HasEarlierPages) {
    public static HistoryResultsPresentation Initial { get; } = new(HistorySearchPhase.NotRequested, null, false, null, [], null, null, 1, false, false, false);
}

public abstract record HistoryResultsIntent {
    public sealed record Previous : HistoryResultsIntent;
    public sealed record Next : HistoryResultsIntent;
    public sealed record Cancel : HistoryResultsIntent;
    public sealed record Clear : HistoryResultsIntent;
    public sealed record Details(HistoryEntryId EntryId) : HistoryResultsIntent;
}

public static class HistoryPublicErrors {
    public static string Message(HistoryFailure failure) => failure switch {
        HistoryFailure.Denied => "Access denied. This evidence is not available to the current identity.",
        HistoryFailure.StaleContext => "The profile or authorization scope changed. Search again in the current scope.",
        HistoryFailure.InvalidQuery => "The filters are invalid. Correct them and Search again.",
        HistoryFailure.InvalidCursor => "This page cursor is no longer valid. Search again to return to the first page.",
        HistoryFailure.TimedOut => "The history read timed out. Retry or narrow the selected interval.",
        HistoryFailure.Conflict => "The evidence changed during the read. Search again to load its current state.",
        _ => "History is unavailable. Retry or narrow the selected interval."
    };
}
