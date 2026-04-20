using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed record WorkspaceChatProjection(
    IReadOnlyList<ChatSessionSummaryRecord> SessionSummaries,
    IReadOnlyList<ChatRunSummaryRecord> RunSummaries,
    IReadOnlyDictionary<Guid, ExecutionRunRecord> LatestRunBySessionId);

internal static class WorkspaceChatProjectionBuilder
{
    public static WorkspaceChatProjection Build(
        IReadOnlyList<ChatSessionRecord> sessions,
        IReadOnlyList<ExecutionRunRecord> runs,
        IReadOnlyList<ExecutionLogEntry> executionLog)
    {
        var logsByRun = executionLog
            .GroupBy(item => item.ExecutionRunId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ExecutionLogEntry>)group.ToList());
        var latestRunBySessionId = BuildLatestRunBySessionId(runs);

        return new WorkspaceChatProjection(
            SessionSummaries: sessions
                .Select(session => CreateChatSessionSummary(
                    session,
                    latestRunBySessionId.TryGetValue(session.Id, out var latestRun) ? latestRun : null))
                .OrderByDescending(item => item.UpdatedAtUtc)
                .ToList(),
            RunSummaries: runs
                .Select(run => CreateChatRunSummary(
                    run,
                    logsByRun.TryGetValue(run.Id, out var runLogs) ? runLogs : []))
                .OrderByDescending(item => item.UpdatedAtUtc)
                .ToList(),
            LatestRunBySessionId: latestRunBySessionId);
    }

    public static ChatSessionSummaryRecord CreateChatSessionSummary(
        ChatSessionRecord session,
        ExecutionRunRecord? latestRun = null)
    {
        var lastMessage = session.Messages.LastOrDefault();
        var preview = lastMessage?.Content ?? "No messages yet.";
        if (preview.Length > 180)
        {
            preview = $"{preview[..177].TrimEnd()}...";
        }

        return new ChatSessionSummaryRecord(
            session.Id,
            session.AgentId,
            session.Title,
            session.CreatedAtUtc,
            session.UpdatedAtUtc,
            session.Messages.Count,
            preview,
            latestRun?.PendingApprovals.Count ?? session.Compatibility?.PendingApprovals.Count ?? 0,
            latestRun?.AutoApprovePendingToolCalls ?? session.Compatibility?.AutoApprovePendingToolCalls ?? false);
    }

    public static ChatRunSummaryRecord CreateChatRunSummary(
        ExecutionRunRecord run,
        IEnumerable<ExecutionLogEntry> executionLog)
    {
        var latestEntry = executionLog
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault();

        return new ChatRunSummaryRecord(
            run.Id,
            run.AgentId,
            run.ChatSessionId,
            run.UpdatedAtUtc,
            latestEntry?.State ?? run.State,
            latestEntry?.Phase ?? "Run",
            latestEntry?.Message ?? (!string.IsNullOrWhiteSpace(run.ResultSummary) ? run.ResultSummary : run.Title),
            run.Outcome);
    }

    public static IReadOnlyDictionary<Guid, ExecutionRunRecord> BuildLatestRunBySessionId(
        IReadOnlyList<ExecutionRunRecord> runs)
    {
        return runs
            .Where(run => run.ChatSessionId.HasValue)
            .GroupBy(run => run.ChatSessionId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(run => run.UpdatedAtUtc)
                    .ThenByDescending(run => run.CreatedAtUtc)
                    .First());
    }
}
