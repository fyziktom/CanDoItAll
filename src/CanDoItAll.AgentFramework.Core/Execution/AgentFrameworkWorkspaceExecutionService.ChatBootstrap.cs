using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceExecutionService
{
    public async Task<ChatPageBootstrapSnapshot> GetChatPageBootstrapAsync(
        IReadOnlyList<AgentDefinition> agents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agents);

        if (agents.Count == 0)
        {
            return new ChatPageBootstrapSnapshot([], null, null);
        }

        var initialAgentId = await ResolveInitialAgentIdAsync(agents, cancellationToken);
        var selectedAgentWorkspace = initialAgentId.HasValue
            ? await GetChatAgentWorkspaceAsync(initialAgentId.Value, cancellationToken: cancellationToken)
            : null;

        return new ChatPageBootstrapSnapshot(agents, initialAgentId, selectedAgentWorkspace);
    }

    public async Task<ChatAgentWorkspaceSnapshot> GetChatAgentWorkspaceAsync(
        Guid agentId,
        Guid? preferredSessionId = null,
        CancellationToken cancellationToken = default)
    {
        var sessionSummaries = await LoadChatSessionSummariesAsync(agentId, cancellationToken);

        Guid? selectedSessionId = null;
        if (preferredSessionId.HasValue && sessionSummaries.Any(item => item.Id == preferredSessionId.Value))
        {
            selectedSessionId = preferredSessionId.Value;
        }
        else if (sessionSummaries.Count > 0)
        {
            selectedSessionId = sessionSummaries[0].Id;
        }

        var selectedSession = selectedSessionId.HasValue
            ? await LoadChatSessionAsync(selectedSessionId.Value, cancellationToken)
            : null;

        var latestRun = await LoadLatestChatRunSummaryAsync(agentId, selectedSessionId, cancellationToken);
        var selectedRun = await LoadLatestChatRunAsync(agentId, selectedSessionId, cancellationToken);
        return new ChatAgentWorkspaceSnapshot(agentId, sessionSummaries, selectedSession, selectedSessionId, latestRun)
        {
            SelectedRun = selectedRun
        };
    }

    public Task<ChatRuntimeSnapshot> GetChatRuntimeSnapshotAsync(
        Guid agentId,
        Guid? chatSessionId = null,
        CancellationToken cancellationToken = default)
        => LoadChatRuntimeSnapshotAsync(agentId, chatSessionId, cancellationToken);

    private async Task<Guid?> ResolveInitialAgentIdAsync(
        IReadOnlyList<AgentDefinition> agents,
        CancellationToken cancellationToken)
    {
        if (agents.Count == 0)
        {
            return null;
        }

        var sessionSummaries = await LoadChatSessionSummariesAsync(agentId: null, cancellationToken);
        var latestSessionByAgent = sessionSummaries
            .GroupBy(item => item.AgentId)
            .ToDictionary(
                group => group.Key,
                group => group.MaxBy(item => item.UpdatedAtUtc),
                EqualityComparer<Guid>.Default);

        Guid? preferredAgentId = null;
        var preferredUpdatedAt = DateTimeOffset.MinValue;

        foreach (var agent in agents)
        {
            if (!latestSessionByAgent.TryGetValue(agent.Id, out var latestSession) ||
                latestSession is null ||
                latestSession.UpdatedAtUtc <= preferredUpdatedAt)
            {
                continue;
            }

            preferredAgentId = agent.Id;
            preferredUpdatedAt = latestSession.UpdatedAtUtc;
        }

        return preferredAgentId ?? agents[0].Id;
    }

    private async Task<IReadOnlyList<ChatSessionSummaryRecord>> LoadChatSessionSummariesAsync(
        Guid? agentId,
        CancellationToken cancellationToken)
    {
        if (store is ISandboxWorkspaceChatQueryStore chatQueryStore)
        {
            return await chatQueryStore.ListChatSessionSummariesAsync(agentId, cancellationToken);
        }

        var executionState = await store.LoadExecutionAsync(cancellationToken);
        var projection = WorkspaceChatProjectionBuilder.Build(
            executionState.ChatSessions,
            executionState.ExecutionRuns,
            executionState.ExecutionLog);
        return projection.SessionSummaries
            .Where(item => !agentId.HasValue || item.AgentId == agentId.Value)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();
    }

    private async Task<ChatSessionRecord?> LoadChatSessionAsync(
        Guid chatSessionId,
        CancellationToken cancellationToken)
    {
        if (store is ISandboxWorkspaceChatQueryStore chatQueryStore)
        {
            return await chatQueryStore.GetChatSessionAsync(chatSessionId, cancellationToken);
        }

        var executionState = await store.LoadExecutionAsync(cancellationToken);
        return executionState.ChatSessions.FirstOrDefault(item => item.Id == chatSessionId);
    }

    private async Task<ChatRunSummaryRecord?> LoadLatestChatRunSummaryAsync(
        Guid agentId,
        Guid? chatSessionId,
        CancellationToken cancellationToken)
    {
        if (store is ISandboxWorkspaceChatQueryStore chatQueryStore)
        {
            var summaries = await chatQueryStore.ListChatRunSummariesAsync(agentId, chatSessionId, cancellationToken);
            return summaries.FirstOrDefault();
        }

        var executionState = await store.LoadExecutionAsync(cancellationToken);
        var projection = WorkspaceChatProjectionBuilder.Build(
            executionState.ChatSessions,
            executionState.ExecutionRuns,
            executionState.ExecutionLog);
        if (chatSessionId.HasValue)
        {
            var session = executionState.ChatSessions.FirstOrDefault(item => item.Id == chatSessionId.Value);
            if (session?.LatestExecutionRunId.HasValue == true)
            {
                return projection.RunSummaries.FirstOrDefault(item =>
                    item.ExecutionRunId == session.LatestExecutionRunId.Value
                    && item.AgentId == agentId
                    && item.ChatSessionId == chatSessionId.Value);
            }
        }

        return projection.RunSummaries
            .Where(item => item.AgentId == agentId && (!chatSessionId.HasValue || item.ChatSessionId == chatSessionId.Value))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefault();
    }

    private async Task<ExecutionRunRecord?> LoadLatestChatRunAsync(
        Guid agentId,
        Guid? chatSessionId,
        CancellationToken cancellationToken)
    {
        if (!chatSessionId.HasValue)
        {
            return null;
        }

        var executionState = await store.LoadExecutionAsync(cancellationToken);
        var session = executionState.ChatSessions.FirstOrDefault(item => item.Id == chatSessionId.Value);
        if (session?.LatestExecutionRunId.HasValue == true)
        {
            var latestRun = executionState.ExecutionRuns.FirstOrDefault(item =>
                item.Id == session.LatestExecutionRunId.Value
                && item.AgentId == agentId
                && item.ChatSessionId == chatSessionId.Value);
            if (latestRun is not null)
            {
                return latestRun;
            }
        }

        return executionState.ExecutionRuns
            .Where(item => item.AgentId == agentId && item.ChatSessionId == chatSessionId.Value)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault();
    }

    private async Task<ChatRuntimeSnapshot> LoadChatRuntimeSnapshotAsync(
        Guid agentId,
        Guid? chatSessionId,
        CancellationToken cancellationToken)
    {
        if (store is ISandboxWorkspaceChatQueryStore chatQueryStore)
        {
            return await chatQueryStore.LoadChatRuntimeSnapshotAsync(agentId, chatSessionId, cancellationToken);
        }

        var executionState = await store.LoadExecutionAsync(cancellationToken);
        return new ChatRuntimeSnapshot(
            executionState.ExecutionLog
                .Where(item => item.AgentId == agentId && (!chatSessionId.HasValue || item.ChatSessionId == chatSessionId.Value))
                .OrderByDescending(item => item.CreatedAtUtc)
                .ToList(),
            executionState.Metrics
                .Where(item => item.AgentId == agentId && (!chatSessionId.HasValue || item.ChatSessionId == chatSessionId.Value))
                .OrderByDescending(item => item.CreatedAtUtc)
                .ToList());
    }
}
