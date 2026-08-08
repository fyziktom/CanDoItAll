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
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureRequiredActivityOperationId(
            request.InitialActivityOperationId,
            nameof(request));
        return ExecuteNewActivityOperationAsync(
            request.InitialActivityOperationId,
            request.AgentId,
            request.ChatSessionId,
            "Agent execution request accepted.",
            operation => executionService.ExecuteRunWithinOperationAsync(
                operation,
                request,
                cancellationToken));
    }

    public Task<ExecutionRunResult> ExecuteRunWithinOperationAsync(
        IAgentExecutionActivityOperationLease operation,
        ExecutionRunRequest request,
        CancellationToken cancellationToken = default)
        => executionService.ExecuteRunWithinOperationAsync(
            operation,
            request,
            cancellationToken);

    public Task<ExecutionRunSourceExecutionResult> ExecuteSameSourceRunAsync(
        ExecutionRunSourceKey source,
        ExecutionRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureRequiredActivityOperationId(
            request.InitialActivityOperationId,
            nameof(request));
        return ExecuteNewActivityOperationAsync(
            request.InitialActivityOperationId,
            request.AgentId,
            request.ChatSessionId,
            "Source execution request accepted.",
            operation => executionService.ExecuteSameSourceRunWithinOperationAsync(
                operation,
                source,
                request,
                cancellationToken));
    }

    public Task<ExecutionRunSourceExecutionResult> ExecuteSameSourceRunWithinOperationAsync(
        IAgentExecutionActivityOperationLease operation,
        ExecutionRunSourceKey source,
        ExecutionRunRequest request,
        CancellationToken cancellationToken = default)
        => executionService.ExecuteSameSourceRunWithinOperationAsync(
            operation,
            source,
            request,
            cancellationToken);

    public Task<ExecutionRunResult> ContinueExecutionRunAsync(
        Guid executionRunId,
        AgentExecutionOperationId activityOperationId,
        IReadOnlyList<PendingToolApprovalDecision> decisions,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        EnsureRequiredActivityOperationId(
            activityOperationId,
            nameof(activityOperationId));
        return ExecuteNewActivityOperationAsync(
            activityOperationId,
            agentId: null,
            chatSessionId: null,
            "Execution continuation accepted.",
            operation => executionService.ContinueExecutionRunWithinOperationAsync(
                operation,
                executionRunId,
                decisions,
                autoApprovePendingToolCalls,
                cancellationToken));
    }

    public Task<ExecutionRunResult> ContinueExecutionRunWithinOperationAsync(
        IAgentExecutionActivityOperationLease operation,
        Guid executionRunId,
        IReadOnlyList<PendingToolApprovalDecision> decisions,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default)
        => executionService.ContinueExecutionRunWithinOperationAsync(
            operation,
            executionRunId,
            decisions,
            autoApprovePendingToolCalls,
            cancellationToken);

    public Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(
        ExecutionRunQuery query,
        CancellationToken cancellationToken = default)
        => executionService.ListExecutionRunsAsync(query, cancellationToken);

    public Task<AgentExecutionReportPage> QueryExecutionReportAsync(
        AgentExecutionReportQuery query,
        CancellationToken cancellationToken = default)
        => executionService.QueryExecutionReportAsync(query, cancellationToken);

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
        AgentChatRunOptions options,
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? attachmentPaths = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        EnsureRequiredActivityOperationId(
            options.InitialActivityOperationId,
            nameof(options));
        return ExecuteNewActivityOperationAsync(
            options.InitialActivityOperationId,
            agentId,
            chatSessionId,
            "Agent chat request accepted.",
            operation => executionService.SendMessageWithinOperationAsync(
                operation,
                agentId,
                chatSessionId,
                prompt,
                options,
                cancellationToken,
                attachmentPaths));
    }

    public Task<AgentChatRunResult> SendMessageWithinOperationAsync(
        IAgentExecutionActivityOperationLease operation,
        Guid agentId,
        Guid? chatSessionId,
        string prompt,
        AgentChatRunOptions options,
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? attachmentPaths = null)
        => executionService.SendMessageWithinOperationAsync(
            operation,
            agentId,
            chatSessionId,
            prompt,
            options,
            cancellationToken,
            attachmentPaths);

    public Task<AgentChatRunResult> RespondToPendingApprovalsAsync(
        Guid agentId,
        Guid chatSessionId,
        AgentExecutionOperationId activityOperationId,
        IReadOnlyList<PendingToolApprovalDecision> decisions,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        EnsureRequiredActivityOperationId(
            activityOperationId,
            nameof(activityOperationId));
        return ExecuteNewActivityOperationAsync(
            activityOperationId,
            agentId,
            chatSessionId,
            "Approval response accepted.",
            operation => executionService.RespondToPendingApprovalsWithinOperationAsync(
                operation,
                agentId,
                chatSessionId,
                decisions,
                autoApprovePendingToolCalls,
                cancellationToken));
    }

    public Task<AgentChatRunResult> RespondToPendingApprovalsWithinOperationAsync(
        IAgentExecutionActivityOperationLease operation,
        Guid agentId,
        Guid chatSessionId,
        IReadOnlyList<PendingToolApprovalDecision> decisions,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default)
        => executionService.RespondToPendingApprovalsWithinOperationAsync(
            operation,
            agentId,
            chatSessionId,
            decisions,
            autoApprovePendingToolCalls,
            cancellationToken);

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

    private Task<TResult> ExecuteNewActivityOperationAsync<TResult>(
        AgentExecutionOperationId operationId,
        Guid? agentId,
        Guid? chatSessionId,
        string acceptedMessage,
        Func<IAgentExecutionActivityOperationLease, Task<TResult>> dispatch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acceptedMessage);
        ArgumentNullException.ThrowIfNull(dispatch);

        var streamId = activityWorkspaceIdentity.CreateStreamId(operationId);
        var operation = activityCoordinator.AdmitOperation(
            streamId,
            agentId,
            chatSessionId,
            acceptedMessage) switch
        {
            AgentExecutionActivityAdmitted admitted => admitted.Operation,
            AgentExecutionActivityRejected rejected =>
                throw new AgentExecutionActivityAdmissionException(
                    rejected.StreamId,
                    rejected.Reason),
            _ => throw new InvalidOperationException(
                "The activity coordinator returned an unknown admission result.")
        };

        Task<TResult> completion;
        try
        {
            completion = dispatch(operation);
        }
        catch (OperationCanceledException)
        {
            TerminalizeOwnedCancellation(operation);
            operation.Dispose();
            throw;
        }
        catch
        {
            TerminalizeOwnedFailure(operation);
            operation.Dispose();
            throw;
        }

        return AwaitOwnedActivityOperationAsync(
            operation,
            completion);
    }

    private static async Task<TResult> AwaitOwnedActivityOperationAsync<TResult>(
        IAgentExecutionActivityOperationLease operation,
        Task<TResult> completion)
    {
        try
        {
            var result = await completion.ConfigureAwait(false);
            if (!operation.IsTerminal)
            {
                TerminalizeOwnedFailure(operation);
                throw new InvalidOperationException(
                    "The workspace execution completed without a terminal activity outcome.");
            }

            return result;
        }
        catch (AgentExecutionActivityPublicationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            TerminalizeOwnedCancellation(operation);
            throw;
        }
        catch
        {
            TerminalizeOwnedFailure(operation);
            throw;
        }
        finally
        {
            operation.Dispose();
        }
    }

    private static void EnsureRequiredActivityOperationId(
        AgentExecutionOperationId operationId,
        string parameterName)
    {
        if (operationId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "An activity operation id is required.",
                parameterName);
        }
    }

    private static void TerminalizeOwnedCancellation(
        IAgentExecutionActivityOperationLease operation)
    {
        if (!operation.IsTerminal)
        {
            operation.Cancel("The agent operation was cancelled.");
        }
    }

    private static void TerminalizeOwnedFailure(
        IAgentExecutionActivityOperationLease operation)
    {
        if (!operation.IsTerminal)
        {
            operation.Fail(
                "The agent operation failed.",
                AgentExecutionActivityFailureCodes.UnhandledExecutionFailure);
        }
    }
}
