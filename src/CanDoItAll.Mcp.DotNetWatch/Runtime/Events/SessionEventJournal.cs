using System.Collections.Concurrent;

namespace CanDoItAll.Mcp.DotNetWatch.Runtime.Events;

public sealed class SessionEventJournal
{
    private readonly ConcurrentQueue<AppEventData> _entries = new();
    private long _sequence;

    public AppEventData Append(
        string logicalAppId,
        string sessionId,
        string eventType,
        string summary,
        RuntimeRevisionData? revision = null,
        string? transactionId = null,
        string? slotId = null)
    {
        var entry = new AppEventData(
            Sequence: Interlocked.Increment(ref _sequence),
            TimestampUtc: DateTimeOffset.UtcNow,
            LogicalAppId: logicalAppId,
            SessionId: sessionId,
            EventType: eventType,
            Summary: summary,
            Revision: revision,
            TransactionId: transactionId,
            SlotId: slotId);

        _entries.Enqueue(entry);
        while (_entries.Count > 5_000 && _entries.TryDequeue(out _))
        {
        }

        return entry;
    }

    public AppEventsData Read(string? logicalAppId, string? sessionId, long? cursor, int limit)
    {
        var normalizedLimit = Math.Clamp(limit, 1, 500);
        var filtered = _entries
            .Where(entry => entry.Sequence > (cursor ?? 0))
            .Where(entry => string.IsNullOrWhiteSpace(logicalAppId) || string.Equals(entry.LogicalAppId, logicalAppId, StringComparison.OrdinalIgnoreCase))
            .Where(entry => string.IsNullOrWhiteSpace(sessionId) || string.Equals(entry.SessionId, sessionId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Sequence)
            .ToArray();

        var page = filtered.Take(normalizedLimit).ToArray();
        var nextCursor = page.LastOrDefault()?.Sequence ?? (cursor ?? 0);
        return new AppEventsData(
            Entries: page,
            NextCursor: nextCursor,
            Truncated: filtered.Length > page.Length,
            TotalAvailableAfterCursor: Math.Max(0, filtered.Length - page.Length));
    }
}
