using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class FileSandboxWorkspaceChatProjectionStore(
    FileSandboxWorkspaceStorageLayout layout,
    FileSandboxWorkspaceJsonStore jsonStore)
{
    public bool HasPersistedChatIndex() => File.Exists(layout.ExecutionChatIndexPath);

    public async Task<IReadOnlyList<ChatSessionSummaryRecord>> ListChatSessionSummariesAsync(
        Guid? agentId,
        CancellationToken cancellationToken)
    {
        var chatIndex = await LoadOrBuildChatIndexAsync(cancellationToken);
        return chatIndex.SessionSummaries
            .Where(item => !agentId.HasValue || item.AgentId == agentId.Value)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();
    }

    public Task<ChatSessionRecord?> GetChatSessionAsync(
        Guid chatSessionId,
        CancellationToken cancellationToken)
        => jsonStore.ReadJsonAsync<ChatSessionRecord>(layout.SessionPath(chatSessionId), cancellationToken);

    public async Task<IReadOnlyList<ChatRunSummaryRecord>> ListChatRunSummariesAsync(
        Guid agentId,
        Guid? chatSessionId,
        CancellationToken cancellationToken)
    {
        var chatIndex = await LoadOrBuildChatIndexAsync(cancellationToken);
        return chatIndex.RunSummaries
            .Where(item => item.AgentId == agentId && (!chatSessionId.HasValue || item.ChatSessionId == chatSessionId.Value))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();
    }

    public async Task<ChatRuntimeSnapshot> LoadChatRuntimeSnapshotAsync(
        Guid agentId,
        Guid? chatSessionId,
        CancellationToken cancellationToken)
    {
        var chatIndex = await LoadOrBuildChatIndexAsync(cancellationToken);
        var runSummaries = chatIndex.RunSummaries
            .Where(item => item.AgentId == agentId && (!chatSessionId.HasValue || item.ChatSessionId == chatSessionId.Value))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();

        var executionLog = new List<ExecutionLogEntry>();
        var metrics = new List<AgentRunMetric>();

        foreach (var runSummary in runSummaries)
        {
            executionLog.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<ExecutionLogEntry>(
                layout.RunLogsRoot(runSummary.ExecutionRunId),
                cancellationToken));
            metrics.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<AgentRunMetric>(
                layout.RunMetricsRoot(runSummary.ExecutionRunId),
                cancellationToken));
        }

        return new ChatRuntimeSnapshot(
            executionLog
                .Where(item => item.AgentId == agentId && (!chatSessionId.HasValue || item.ChatSessionId == chatSessionId.Value))
                .OrderByDescending(item => item.CreatedAtUtc)
                .ToList(),
            metrics
                .Where(item => item.AgentId == agentId && (!chatSessionId.HasValue || item.ChatSessionId == chatSessionId.Value))
                .OrderByDescending(item => item.CreatedAtUtc)
                .ToList());
    }

    public async Task<bool> SaveRunDetailAsync(
        ExecutionRunDetail? previousDetail,
        ExecutionRunDetail detail,
        ExecutionStorageIndex executionIndex,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(executionIndex);

        var currentChatIndex = await LoadOrBuildChatIndexAsync(cancellationToken);
        var runSummaries = currentChatIndex.RunSummaries
            .Where(item => item.ExecutionRunId != detail.Run.Id)
            .Append(WorkspaceChatProjectionBuilder.CreateChatRunSummary(detail.Run, detail.ExecutionLog))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();

        var affectedSessionIds = new HashSet<Guid>();
        if (previousDetail?.Run.ChatSessionId is Guid previousRunSessionId)
        {
            affectedSessionIds.Add(previousRunSessionId);
        }

        if (detail.Run.ChatSessionId is Guid currentRunSessionId)
        {
            affectedSessionIds.Add(currentRunSessionId);
        }

        if (previousDetail?.ChatSession?.Id is Guid previousSessionId)
        {
            affectedSessionIds.Add(previousSessionId);
        }

        if (detail.ChatSession?.Id is Guid currentSessionId)
        {
            affectedSessionIds.Add(currentSessionId);
        }

        var sessionSummaries = currentChatIndex.SessionSummaries
            .Where(item => !affectedSessionIds.Contains(item.Id))
            .ToList();

        foreach (var sessionId in affectedSessionIds)
        {
            var session = await jsonStore.ReadJsonAsync<ChatSessionRecord>(layout.SessionPath(sessionId), cancellationToken);
            if (session is null)
            {
                continue;
            }

            var latestRun = await ResolveLatestRunForSessionAsync(session, cancellationToken);
            sessionSummaries.Add(WorkspaceChatProjectionBuilder.CreateChatSessionSummary(session, latestRun));
        }

        var nextChatIndex = new ExecutionChatIndex(
            Version: executionIndex.Version,
            Revision: executionIndex.Revision,
            UpdatedAtUtc: executionIndex.UpdatedAtUtc,
            SessionSummaries: sessionSummaries
                .OrderByDescending(item => item.UpdatedAtUtc)
                .ToList(),
            RunSummaries: runSummaries);

        if (currentChatIndex is not null &&
            File.Exists(layout.ExecutionChatIndexPath) &&
            !jsonStore.RequiresSave(currentChatIndex, nextChatIndex))
        {
            return false;
        }

        await jsonStore.WriteJsonAtomicallyAsync(layout.ExecutionChatIndexPath, nextChatIndex, cancellationToken);
        return true;
    }

    private async Task<ExecutionChatIndex> LoadOrBuildChatIndexAsync(CancellationToken cancellationToken)
    {
        var chatIndex = await jsonStore.ReadJsonAsync<ExecutionChatIndex>(layout.ExecutionChatIndexPath, cancellationToken);
        if (chatIndex is not null)
        {
            return chatIndex;
        }

        return await BuildChatIndexAsync(cancellationToken);
    }

    private async Task<ExecutionChatIndex> BuildChatIndexAsync(CancellationToken cancellationToken)
    {
        var sessions = await jsonStore.LoadRecordsFromDirectoryAsync<ChatSessionRecord>(layout.ExecutionSessionsRoot, cancellationToken);
        var runs = new List<ExecutionRunRecord>();
        var executionLog = new List<ExecutionLogEntry>();
        if (Directory.Exists(layout.ExecutionRunsRoot))
        {
            foreach (var runDirectory in Directory.EnumerateDirectories(layout.ExecutionRunsRoot).OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            {
                var run = await jsonStore.ReadJsonAsync<ExecutionRunRecord>(Path.Combine(runDirectory, "run.json"), cancellationToken);
                if (run is null)
                {
                    continue;
                }

                runs.Add(run);
                executionLog.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<ExecutionLogEntry>(Path.Combine(runDirectory, "logs"), cancellationToken));
            }
        }

        var index = await jsonStore.ReadJsonAsync<ExecutionStorageIndex>(layout.ExecutionIndexPath, cancellationToken);
        var projection = WorkspaceChatProjectionBuilder.Build(sessions, runs, executionLog);
        return new ExecutionChatIndex(
            Version: string.IsNullOrWhiteSpace(index?.Version) ? "1.0" : index.Version,
            Revision: index?.Revision ?? 1L,
            UpdatedAtUtc: index?.UpdatedAtUtc ?? DateTimeOffset.UtcNow,
            SessionSummaries: projection.SessionSummaries,
            RunSummaries: projection.RunSummaries);
    }

    private async Task<ExecutionRunRecord?> ResolveLatestRunForSessionAsync(
        ChatSessionRecord session,
        CancellationToken cancellationToken)
    {
        if (session.LatestExecutionRunId.HasValue)
        {
            var latestRun = await jsonStore.ReadJsonAsync<ExecutionRunRecord>(
                layout.RunPath(session.LatestExecutionRunId.Value),
                cancellationToken);
            if (latestRun?.ChatSessionId == session.Id)
            {
                return latestRun;
            }
        }

        return await LoadLatestRunForSessionAsync(session.Id, cancellationToken);
    }

    private async Task<ExecutionRunRecord?> LoadLatestRunForSessionAsync(
        Guid chatSessionId,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(layout.ExecutionRunsRoot))
        {
            return null;
        }

        ExecutionRunRecord? latestRun = null;
        foreach (var runDirectory in Directory.EnumerateDirectories(layout.ExecutionRunsRoot))
        {
            var run = await jsonStore.ReadJsonAsync<ExecutionRunRecord>(
                Path.Combine(runDirectory, "run.json"),
                cancellationToken);
            if (run?.ChatSessionId != chatSessionId)
            {
                continue;
            }

            if (latestRun is null ||
                run.UpdatedAtUtc > latestRun.UpdatedAtUtc ||
                (run.UpdatedAtUtc == latestRun.UpdatedAtUtc && run.CreatedAtUtc > latestRun.CreatedAtUtc))
            {
                latestRun = run;
            }
        }

        return latestRun;
    }
}
