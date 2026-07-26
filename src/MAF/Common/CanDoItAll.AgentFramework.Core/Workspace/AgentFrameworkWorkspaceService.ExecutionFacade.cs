using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed partial class AgentFrameworkWorkspaceService
{
    public Task<IReadOnlyList<ChatSessionRecord>> ListChatSessionsAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
        => executionService.ListChatSessionsAsync(agentId, cancellationToken);

    public async Task<ChatPageBootstrapSnapshot> GetChatPageBootstrapAsync(
        bool includeTemplates = false,
        CancellationToken cancellationToken = default)
    {
        var agents = await catalogService.ListAgentsAsync(includeTemplates, cancellationToken);
        return await executionService.GetChatPageBootstrapAsync(agents, cancellationToken);
    }

    public Task<ChatAgentWorkspaceSnapshot> GetChatAgentWorkspaceAsync(
        Guid agentId,
        Guid? preferredSessionId = null,
        CancellationToken cancellationToken = default)
        => executionService.GetChatAgentWorkspaceAsync(agentId, preferredSessionId, cancellationToken);

    public Task<ChatSessionRecord> GetOrCreateChatSessionAsync(
        Guid agentId,
        Guid? chatSessionId = null,
        CancellationToken cancellationToken = default)
        => executionService.GetOrCreateChatSessionAsync(agentId, chatSessionId, cancellationToken);

    public Task<ChatSessionRecord> RenameChatSessionAsync(
        Guid agentId,
        Guid chatSessionId,
        string title,
        CancellationToken cancellationToken = default)
        => executionService.RenameChatSessionAsync(agentId, chatSessionId, title, cancellationToken);

    public Task<ExecutionRunResult> ExecuteRunAsync(
        ExecutionRunRequest request,
        CancellationToken cancellationToken = default)
        => executionService.ExecuteRunAsync(request, cancellationToken);

    public Task<ExecutionRunSourceExecutionResult> ExecuteSameSourceRunAsync(
        ExecutionRunSourceKey source,
        ExecutionRunRequest request,
        CancellationToken cancellationToken = default)
        => executionService.ExecuteSameSourceRunAsync(source, request, cancellationToken);

    public Task<ExecutionRunResult> ContinueExecutionRunAsync(
        Guid executionRunId,
        bool approved,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default)
        => executionService.ContinueExecutionRunAsync(executionRunId, approved, autoApprovePendingToolCalls, cancellationToken);

    public Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(
        ExecutionRunQuery query,
        CancellationToken cancellationToken = default)
        => executionService.ListExecutionRunsAsync(query, cancellationToken);

    public Task<ExecutionRunDetail> GetExecutionRunDetailAsync(
        Guid executionRunId,
        CancellationToken cancellationToken = default)
        => executionService.GetExecutionRunDetailAsync(executionRunId, cancellationToken);

    public Task<IReadOnlyList<ExecutionArtifactRecord>> ListExecutionArtifactsAsync(
        Guid executionRunId,
        CancellationToken cancellationToken = default)
        => executionService.ListExecutionArtifactsAsync(executionRunId, cancellationToken);

    public Task<IReadOnlyList<ExecutionWorkflowCheckpointRecord>> ListExecutionWorkflowCheckpointsAsync(
        Guid executionRunId,
        CancellationToken cancellationToken = default)
        => executionService.ListExecutionWorkflowCheckpointsAsync(executionRunId, cancellationToken);

    public Task<IReadOnlyList<ToolExecutionReceiptRecord>> ListToolExecutionReceiptsAsync(
        Guid executionRunId,
        CancellationToken cancellationToken = default)
        => executionService.ListToolExecutionReceiptsAsync(executionRunId, cancellationToken);

    public Task<AgentChatRunResult> SendMessageAsync(
        Guid agentId,
        Guid? chatSessionId,
        string prompt,
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? attachmentPaths = null,
        AgentChatRunOptions? options = null)
        => executionService.SendMessageAsync(agentId, chatSessionId, prompt, cancellationToken, attachmentPaths, options);

    public Task<AgentChatRunResult> RespondToPendingApprovalsAsync(
        Guid agentId,
        Guid chatSessionId,
        bool approved,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default)
        => executionService.RespondToPendingApprovalsAsync(agentId, chatSessionId, approved, autoApprovePendingToolCalls, cancellationToken);

    public Task<IReadOnlyList<ExecutionLogEntry>> ListExecutionLogAsync(
        Guid agentId,
        Guid? chatSessionId = null,
        CancellationToken cancellationToken = default)
        => executionService.ListExecutionLogAsync(agentId, chatSessionId, cancellationToken);

    public Task<ChatRuntimeSnapshot> GetChatRuntimeSnapshotAsync(
        Guid agentId,
        Guid? chatSessionId = null,
        CancellationToken cancellationToken = default)
        => executionService.GetChatRuntimeSnapshotAsync(agentId, chatSessionId, cancellationToken);

    public Task<IReadOnlyList<AgentRunMetric>> ListMetricsAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
        => executionService.ListMetricsAsync(agentId, cancellationToken);
}
