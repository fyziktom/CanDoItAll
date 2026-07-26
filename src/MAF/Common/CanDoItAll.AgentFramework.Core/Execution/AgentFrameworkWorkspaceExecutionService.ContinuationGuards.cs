using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceExecutionService
{
    private enum ExecutionRunContinuationDisposition
    {
        Started,
        AlreadyFinalized,
        AlreadyInProgress
    }

    private sealed record PreparedExecutionRunContinuation(
        SandboxWorkspaceCatalog Catalog,
        ExecutionRunRecord OriginalRun,
        ExecutionRunRecord TransitionedRun,
        ChatSessionRecord? Session,
        AgentDefinition Agent,
        IReadOnlyList<ExecutionApprovalRecord> RunApprovals,
        IReadOnlyList<ExecutionApprovalRecord> DecidedApprovals);

    private sealed record ExecutionRunContinuationStart(
        ExecutionRunContinuationDisposition Disposition,
        PreparedExecutionRunContinuation? Prepared = null);

    private async Task<ExecutionRunRecord> LoadExecutionRunAsync(
        Guid executionRunId,
        CancellationToken cancellationToken)
    {
        return (await LoadExecutionRunDetailAsync(executionRunId, cancellationToken)).Run;
    }

    private async Task<ExecutionRunResult> LoadExistingExecutionRunResultAsync(
        Guid executionRunId,
        CancellationToken cancellationToken)
    {
        var detail = await LoadExecutionRunDetailAsync(executionRunId, cancellationToken);
        var metric = detail.Metrics
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("This execution run does not have a persisted metric result yet.");
        var assistantMessage = detail.ChatSession?.Messages.LastOrDefault(item => item.Role == ChatMessageRole.Assistant);
        var responseText = !string.IsNullOrWhiteSpace(detail.Run.StructuredOutputRawOutput)
            ? detail.Run.StructuredOutputRawOutput
            : assistantMessage?.Content ?? detail.Run.ResultSummary;
        var jsonSchemaOutput = AgentJsonSchemaOutputContractProcessor.Restore(detail.Run);
        var structuredOutput = jsonSchemaOutput is null ||
                               string.IsNullOrEmpty(detail.Run.StructuredOutputValidationStatus)
            ? null
            : AgentJsonSchemaOutputContractProcessor.ValidateOutput(jsonSchemaOutput, responseText);

        return new ExecutionRunResult(
            executionRunId,
            detail.Run.ChatSessionId,
            responseText,
            assistantMessage,
            metric)
        {
            State = detail.Run.State,
            StructuredOutput = structuredOutput
        };
    }

    private async Task<ExecutionRunContinuationStart> BeginPendingApprovalContinuationAsync(
        ExecutionRunRecord expectedRun,
        bool approved,
        bool autoApprovePendingToolCalls,
        CancellationToken cancellationToken)
    {
        if (store is ISandboxWorkspaceExecutionRunMutationStore mutationStore)
        {
            return await BeginPendingApprovalContinuationWithSplitStoreAsync(
                mutationStore,
                expectedRun,
                approved,
                autoApprovePendingToolCalls,
                cancellationToken);
        }

        ExecutionRunContinuationStart? result = null;

        await store.UpdateWorkspaceAsync(document =>
        {
            var catalog = document.ToCatalog();
            var executionState = document.ToExecutionState();
            var currentRun = executionState.ExecutionRuns.FirstOrDefault(item => item.Id == expectedRun.Id)
                ?? throw new InvalidOperationException("Execution run was not found.");
            var currentSession = currentRun.ChatSessionId.HasValue
                ? executionState.ChatSessions.FirstOrDefault(item => item.Id == currentRun.ChatSessionId.Value)
                    ?? throw new InvalidOperationException("Chat session was not found.")
                : null;

            if (currentRun.PendingApprovals.Count == 0)
            {
                result = new(
                    currentRun.State is ExecutionState.Completed or ExecutionState.Failed
                        ? ExecutionRunContinuationDisposition.AlreadyFinalized
                        : ExecutionRunContinuationDisposition.AlreadyInProgress);
                return document;
            }

            if (currentRun.State != ExecutionState.WaitingOnTool)
            {
                result = new(ExecutionRunContinuationDisposition.AlreadyInProgress);
                return document;
            }

            if (!PendingApprovalStateMatches(expectedRun, currentRun))
            {
                throw new InvalidOperationException(
                    "This execution run's pending approval state changed before the continuation could start. Reload the workspace and try again.");
            }

            var agent = catalog.Agents.FirstOrDefault(item => item.Id == currentRun.AgentId)
                ?? throw new InvalidOperationException("Agent was not found.");
            if (!agent.ProviderProfileId.HasValue)
            {
                throw new InvalidOperationException("The selected agent does not have a provider profile.");
            }

            var decidedAtUtc = DateTimeOffset.UtcNow;
            var effectiveAutoApprove = approved && (autoApprovePendingToolCalls || currentRun.AutoApprovePendingToolCalls);
            var approvalDecision = ExecutionRunStateTransitions.ApplyApprovalDecision(
                executionState.ExecutionApprovals.Where(item => item.ExecutionRunId == currentRun.Id).ToList(),
                currentRun,
                approved,
                decidedAtUtc,
                currentRun.ChatSessionId.HasValue ? "chat-session" : "execution-run",
                currentRun.ChatSessionId?.ToString("N") ?? currentRun.Id.ToString("N"));
            var transitionedRun = ExecutionRunStateTransitions.CreateContinuationStartRun(
                currentRun,
                approved,
                effectiveAutoApprove,
                decidedAtUtc);
            var transitionedSession = currentSession is null
                ? null
                : ChatSessionRuntimeCompatibilityAdapter.ClearCompatibility(
                    currentSession,
                    transitionedRun.UpdatedAtUtc,
                    currentRun.Id);
            var updatedExecutionState = executionState with
            {
                ExecutionRuns = ReplaceExecutionRun(executionState.ExecutionRuns, transitionedRun),
                ChatSessions = transitionedSession is null
                    ? executionState.ChatSessions
                    : ReplaceChatSession(executionState.ChatSessions, transitionedSession),
                ExecutionApprovals = ReplaceRunApprovals(
                    executionState.ExecutionApprovals,
                    transitionedRun.Id,
                    approvalDecision.RunApprovals)
            };

            result = new(
                ExecutionRunContinuationDisposition.Started,
                new PreparedExecutionRunContinuation(
                    catalog,
                    currentRun,
                    transitionedRun,
                    transitionedSession,
                    agent,
                    approvalDecision.RunApprovals,
                    approvalDecision.Decided));

            return SandboxWorkspaceDocument.Combine(catalog, updatedExecutionState);
        }, cancellationToken);

        return result ?? throw new InvalidOperationException("Execution run continuation could not be prepared.");
    }

    private async Task<ExecutionRunContinuationStart> BeginPendingApprovalContinuationWithSplitStoreAsync(
        ISandboxWorkspaceExecutionRunMutationStore mutationStore,
        ExecutionRunRecord expectedRun,
        bool approved,
        bool autoApprovePendingToolCalls,
        CancellationToken cancellationToken)
    {
        ExecutionRunContinuationStart? result = null;

        await mutationStore.UpdateExecutionRunDetailAsync(
            expectedRun.Id,
            (catalog, currentDetail) =>
            {
                var currentRun = currentDetail.Run;
                var currentSession = currentRun.ChatSessionId.HasValue
                    ? currentDetail.ChatSession
                        ?? throw new InvalidOperationException("Chat session was not found.")
                    : null;

                if (currentRun.PendingApprovals.Count == 0)
                {
                    result = new(
                        currentRun.State is ExecutionState.Completed or ExecutionState.Failed
                            ? ExecutionRunContinuationDisposition.AlreadyFinalized
                            : ExecutionRunContinuationDisposition.AlreadyInProgress);
                    return currentDetail;
                }

                if (currentRun.State != ExecutionState.WaitingOnTool)
                {
                    result = new(ExecutionRunContinuationDisposition.AlreadyInProgress);
                    return currentDetail;
                }

                if (!PendingApprovalStateMatches(expectedRun, currentRun))
                {
                    throw new InvalidOperationException(
                        "This execution run's pending approval state changed before the continuation could start. Reload the workspace and try again.");
                }

                var agent = catalog.Agents.FirstOrDefault(item => item.Id == currentRun.AgentId)
                    ?? throw new InvalidOperationException("Agent was not found.");
                if (!agent.ProviderProfileId.HasValue)
                {
                    throw new InvalidOperationException("The selected agent does not have a provider profile.");
                }

                var decidedAtUtc = DateTimeOffset.UtcNow;
                var effectiveAutoApprove = approved && (autoApprovePendingToolCalls || currentRun.AutoApprovePendingToolCalls);
                var approvalDecision = ExecutionRunStateTransitions.ApplyApprovalDecision(
                    currentDetail.Approvals,
                    currentRun,
                    approved,
                    decidedAtUtc,
                    currentRun.ChatSessionId.HasValue ? "chat-session" : "execution-run",
                    currentRun.ChatSessionId?.ToString("N") ?? currentRun.Id.ToString("N"));
                var transitionedRun = ExecutionRunStateTransitions.CreateContinuationStartRun(
                    currentRun,
                    approved,
                    effectiveAutoApprove,
                    decidedAtUtc);
                var transitionedSession = currentSession is null
                    ? null
                    : ChatSessionRuntimeCompatibilityAdapter.ClearCompatibility(
                        currentSession,
                        transitionedRun.UpdatedAtUtc,
                        currentRun.Id);

                result = new(
                    ExecutionRunContinuationDisposition.Started,
                    new PreparedExecutionRunContinuation(
                        catalog,
                        currentRun,
                        transitionedRun,
                        transitionedSession,
                        agent,
                        approvalDecision.RunApprovals,
                        approvalDecision.Decided));

                return currentDetail with
                {
                    Run = transitionedRun,
                    ChatSession = transitionedSession,
                    Approvals = approvalDecision.RunApprovals
                };
            },
            cancellationToken);

        return result ?? throw new InvalidOperationException("Execution run continuation could not be prepared.");
    }

    private static bool PendingApprovalStateMatches(
        ExecutionRunRecord expectedRun,
        ExecutionRunRecord currentRun)
        => ExecutionRunStateTransitions.PendingApprovalStateMatches(expectedRun, currentRun);
}
