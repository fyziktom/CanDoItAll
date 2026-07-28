using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class FileSandboxWorkspaceChatProjectionStore(
    FileSandboxWorkspaceStorageLayout layout,
    FileSandboxWorkspaceJsonStore jsonStore)
{
    public async Task<ChatWorkspaceProjectionSnapshot> LoadChatWorkspaceProjectionAsync(
        Guid agentId,
        CancellationToken cancellationToken)
    {
        var chatIndex = await LoadOrBuildChatIndexAsync(cancellationToken);
        return new ChatWorkspaceProjectionSnapshot(
            chatIndex.SessionSummaries
                .Where(item => item.AgentId == agentId)
                .OrderByDescending(item => item.UpdatedAtUtc)
                .ToList(),
            chatIndex.RunSummaries
                .Where(item => item.AgentId == agentId)
                .OrderByDescending(item => item.UpdatedAtUtc)
                .ToList());
    }

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

    public async Task<IReadOnlyList<ChatRunSummaryRecord>> ListAllRunSummariesAsync(CancellationToken cancellationToken)
    {
        var chatIndex = await LoadOrBuildChatIndexAsync(cancellationToken);
        return chatIndex.RunSummaries
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
        var nextChatIndex = await CreateRunDetailChatIndexAsync(
            currentChatIndex,
            previousDetail,
            detail,
            executionIndex,
            cancellationToken);
        if (File.Exists(layout.ExecutionChatIndexPath) &&
            !jsonStore.RequiresSave(currentChatIndex, nextChatIndex))
        {
            return false;
        }

        await jsonStore.WriteJsonAtomicallyAsync(
            layout.ExecutionChatIndexPath,
            nextChatIndex,
            cancellationToken);
        return true;
    }

    public async Task<GenericNewExecutionRunChatProjectionPlan> PrepareGenericNewRunAsync(
        GenericNewExecutionRunPersistencePlan persistencePlan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(persistencePlan);

        var previousChatIndex = await LoadOrBuildChatIndexAsync(
            cancellationToken);
        var targetChatIndex = await CreateRunDetailChatIndexAsync(
            previousChatIndex,
            previousDetail: null,
            persistencePlan.Detail,
            persistencePlan.TargetIndex,
            cancellationToken);
        return new GenericNewExecutionRunChatProjectionPlan(
            previousChatIndex,
            targetChatIndex);
    }

    public async Task ValidateGenericNewRunPlanAsync(
        GenericNewExecutionRunPersistencePlan persistencePlan,
        GenericNewExecutionRunChatProjectionPlan chatProjectionPlan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(persistencePlan);
        ArgumentNullException.ThrowIfNull(chatProjectionPlan);
        ArgumentNullException.ThrowIfNull(chatProjectionPlan.PreviousIndex);
        ArgumentNullException.ThrowIfNull(chatProjectionPlan.TargetIndex);

        var expectedTarget = await CreateRunDetailChatIndexAsync(
            chatProjectionPlan.PreviousIndex,
            previousDetail: null,
            persistencePlan.Detail,
            persistencePlan.TargetIndex,
            cancellationToken);
        if (jsonStore.RequiresSave(
                expectedTarget,
                chatProjectionPlan.TargetIndex))
        {
            throw new InvalidDataException(
                $"Pending generic execution-run creation '{persistencePlan.Detail.Run.Id:N}' contains an invalid target chat index.");
        }

        var currentChatIndex =
            await jsonStore.ReadJsonAsync<ExecutionChatIndex>(
                layout.ExecutionChatIndexPath,
                cancellationToken);
        if (currentChatIndex is null ||
            jsonStore.RequiresSave(
                currentChatIndex,
                chatProjectionPlan.PreviousIndex) &&
            jsonStore.RequiresSave(
                currentChatIndex,
                chatProjectionPlan.TargetIndex))
        {
            throw new InvalidDataException(
                $"Pending generic execution-run creation '{persistencePlan.Detail.Run.Id:N}' found an unexpected chat index.");
        }
    }

    public async Task PersistGenericNewRunAsync(
        GenericNewExecutionRunPersistencePlan persistencePlan,
        GenericNewExecutionRunChatProjectionPlan chatProjectionPlan,
        bool validatePersistedState,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(persistencePlan);
        ArgumentNullException.ThrowIfNull(chatProjectionPlan);
        ArgumentNullException.ThrowIfNull(chatProjectionPlan.PreviousIndex);
        ArgumentNullException.ThrowIfNull(chatProjectionPlan.TargetIndex);

        if (validatePersistedState)
        {
            var currentChatIndex =
                await jsonStore.ReadJsonAsync<ExecutionChatIndex>(
                    layout.ExecutionChatIndexPath,
                    cancellationToken);
            if (currentChatIndex is null ||
                jsonStore.RequiresSave(
                    currentChatIndex,
                    chatProjectionPlan.PreviousIndex) &&
                jsonStore.RequiresSave(
                    currentChatIndex,
                    chatProjectionPlan.TargetIndex))
            {
                throw new InvalidDataException(
                    $"Pending generic execution-run creation '{persistencePlan.Detail.Run.Id:N}' found an unexpected chat index.");
            }
        }

        if (validatePersistedState)
        {
            await jsonStore.WriteJsonIfChangedAsync(
                layout.ExecutionChatIndexPath,
                chatProjectionPlan.TargetIndex,
                cancellationToken);
            return;
        }

        await jsonStore.WriteJsonAtomicallyAsync(
            layout.ExecutionChatIndexPath,
            chatProjectionPlan.TargetIndex,
            cancellationToken);
    }

    public async Task<ExistingExecutionRunChatProjectionPlan> PrepareExistingRunUpdateAsync(
        ExistingExecutionRunPersistencePlan persistencePlan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(persistencePlan);

        var previousChatIndex = await LoadOrBuildChatIndexAsync(
            cancellationToken);
        var targetChatIndex = await CreateRunDetailChatIndexAsync(
            previousChatIndex,
            persistencePlan.PreviousDetail,
            persistencePlan.TargetDetail,
            persistencePlan.TargetIndex,
            cancellationToken);
        return new ExistingExecutionRunChatProjectionPlan(
            previousChatIndex,
            targetChatIndex);
    }

    public async Task PersistExistingRunUpdateAsync(
        ExistingExecutionRunPersistencePlan persistencePlan,
        ExistingExecutionRunChatProjectionPlan chatProjectionPlan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(persistencePlan);
        ArgumentNullException.ThrowIfNull(chatProjectionPlan);
        ArgumentNullException.ThrowIfNull(chatProjectionPlan.PreviousIndex);
        ArgumentNullException.ThrowIfNull(chatProjectionPlan.TargetIndex);

        var expectedTarget = await CreateRunDetailChatIndexAsync(
            chatProjectionPlan.PreviousIndex,
            persistencePlan.PreviousDetail,
            persistencePlan.TargetDetail,
            persistencePlan.TargetIndex,
            cancellationToken);
        if (jsonStore.RequiresSave(
                expectedTarget,
                chatProjectionPlan.TargetIndex))
        {
            throw new InvalidDataException(
                $"Pending execution-run update '{persistencePlan.TargetDetail.Run.Id:N}' contains an invalid target chat index.");
        }

        var currentChatIndex = await jsonStore.ReadJsonAsync<ExecutionChatIndex>(
            layout.ExecutionChatIndexPath,
            cancellationToken);
        if (currentChatIndex is null ||
            jsonStore.RequiresSave(
                currentChatIndex,
                chatProjectionPlan.PreviousIndex) &&
            jsonStore.RequiresSave(
                currentChatIndex,
                chatProjectionPlan.TargetIndex))
        {
            throw new InvalidDataException(
                $"Pending execution-run update '{persistencePlan.TargetDetail.Run.Id:N}' found an unexpected chat index.");
        }

        await jsonStore.WriteJsonIfChangedAsync(
            layout.ExecutionChatIndexPath,
            chatProjectionPlan.TargetIndex,
            cancellationToken);
    }

    private async Task<ExecutionChatIndex> CreateRunDetailChatIndexAsync(
        ExecutionChatIndex currentChatIndex,
        ExecutionRunDetail? previousDetail,
        ExecutionRunDetail detail,
        ExecutionStorageIndex executionIndex,
        CancellationToken cancellationToken)
    {
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
            var session = detail.ChatSession?.Id == sessionId
                ? detail.ChatSession
                : await jsonStore.ReadJsonAsync<ChatSessionRecord>(
                    layout.SessionPath(sessionId),
                    cancellationToken);
            if (session is null)
            {
                continue;
            }

            var latestRun = await ResolveLatestRunForSessionAsync(
                session,
                runSummaries,
                detail.Run,
                cancellationToken);
            sessionSummaries.Add(WorkspaceChatProjectionBuilder.CreateChatSessionSummary(session, latestRun));
        }

        return new ExecutionChatIndex(
            Version: executionIndex.Version,
            Revision: executionIndex.Revision,
            UpdatedAtUtc: executionIndex.UpdatedAtUtc,
            SessionSummaries: sessionSummaries
                .OrderByDescending(item => item.UpdatedAtUtc)
                .ToList(),
            RunSummaries: runSummaries);
    }

    public async Task<bool> SaveSessionAsync(
        ChatSessionRecord? previousSession,
        ChatSessionRecord session,
        ExecutionStorageIndex executionIndex,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(executionIndex);

        var currentChatIndex = await LoadOrBuildChatIndexAsync(cancellationToken);
        var affectedSessionIds = new HashSet<Guid> { session.Id };
        if (previousSession is not null)
        {
            affectedSessionIds.Add(previousSession.Id);
        }

        var sessionSummaries = currentChatIndex.SessionSummaries
            .Where(item => !affectedSessionIds.Contains(item.Id))
            .ToList();

        foreach (var sessionId in affectedSessionIds)
        {
            var storedSession = await jsonStore.ReadJsonAsync<ChatSessionRecord>(layout.SessionPath(sessionId), cancellationToken);
            if (storedSession is null)
            {
                continue;
            }

            var latestRun = await ResolveLatestRunForSessionAsync(
                storedSession,
                currentChatIndex.RunSummaries,
                preferredRun: null,
                cancellationToken: cancellationToken);
            sessionSummaries.Add(WorkspaceChatProjectionBuilder.CreateChatSessionSummary(storedSession, latestRun));
        }

        var nextChatIndex = new ExecutionChatIndex(
            Version: executionIndex.Version,
            Revision: executionIndex.Revision,
            UpdatedAtUtc: executionIndex.UpdatedAtUtc,
            SessionSummaries: sessionSummaries
                .OrderByDescending(item => item.UpdatedAtUtc)
                .ToList(),
            RunSummaries: currentChatIndex.RunSummaries);

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

        var rebuilt = await BuildChatIndexAsync(cancellationToken);
        await jsonStore.WriteJsonAtomicallyAsync(layout.ExecutionChatIndexPath, rebuilt, cancellationToken);
        return rebuilt;
    }

    private async Task<ExecutionChatIndex> BuildChatIndexAsync(CancellationToken cancellationToken)
    {
        var sessions = await jsonStore.LoadRecordsFromDirectoryAsync<ChatSessionRecord>(layout.ExecutionSessionsRoot, cancellationToken);
        var runs = new List<ExecutionRunRecord>();
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
            }
        }

        var index = await jsonStore.ReadJsonAsync<ExecutionStorageIndex>(layout.ExecutionIndexPath, cancellationToken);
        var projection = WorkspaceChatProjectionBuilder.Build(sessions, runs, []);
        return new ExecutionChatIndex(
            Version: string.IsNullOrWhiteSpace(index?.Version) ? "1.0" : index.Version,
            Revision: index?.Revision ?? 1L,
            UpdatedAtUtc: index?.UpdatedAtUtc ?? DateTimeOffset.UtcNow,
            SessionSummaries: projection.SessionSummaries,
            RunSummaries: projection.RunSummaries);
    }

    private async Task<ExecutionRunRecord?> ResolveLatestRunForSessionAsync(
        ChatSessionRecord session,
        IReadOnlyList<ChatRunSummaryRecord> runSummaries,
        ExecutionRunRecord? preferredRun,
        CancellationToken cancellationToken)
    {
        if (session.LatestExecutionRunId.HasValue)
        {
            if (preferredRun is not null &&
                session.LatestExecutionRunId.Value == preferredRun.Id &&
                preferredRun.ChatSessionId == session.Id)
            {
                return preferredRun;
            }

            var latestRun = await jsonStore.ReadJsonAsync<ExecutionRunRecord>(
                layout.RunPath(session.LatestExecutionRunId.Value),
                cancellationToken);
            if (latestRun?.ChatSessionId == session.Id)
            {
                return latestRun;
            }
        }

        var latestRunId = runSummaries
            .Where(item =>
                item.ChatSessionId == session.Id &&
                item.ExecutionRunId != session.LatestExecutionRunId)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => (Guid?)item.ExecutionRunId)
            .FirstOrDefault();
        if (!latestRunId.HasValue)
        {
            return null;
        }

        if (preferredRun is not null &&
            latestRunId.Value == preferredRun.Id &&
            preferredRun.ChatSessionId == session.Id)
        {
            return preferredRun;
        }

        var indexedRun = await jsonStore.ReadJsonAsync<ExecutionRunRecord>(
            layout.RunPath(latestRunId.Value),
            cancellationToken);
        return indexedRun?.ChatSessionId == session.Id
            ? indexedRun
            : null;
    }

}

internal sealed record ExistingExecutionRunChatProjectionPlan(
    ExecutionChatIndex PreviousIndex,
    ExecutionChatIndex TargetIndex);

internal sealed record GenericNewExecutionRunChatProjectionPlan(
    ExecutionChatIndex PreviousIndex,
    ExecutionChatIndex TargetIndex);
