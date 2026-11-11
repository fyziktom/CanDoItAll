using System.Runtime.ExceptionServices;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public interface IAgentChatOperationExecutor {
    AgentTurnContextCaptureResult? LastCapture { get; }
    AgentConversationContextBinding? AdoptContext() => LastCapture?.ConversationBinding;
    Task<AgentChatRunResult> SendAsync(IAgentExecutionActivityOperationLease operation, AgentChatSendRequest request,
        AgentTurnContextObservation? observation, CancellationToken cancellationToken);
    Task<AgentChatRunResult> ApproveAsync(IAgentExecutionActivityOperationLease operation, Guid agentId, Guid sessionId,
        IReadOnlyList<PendingToolApprovalDecision> decisions, bool autoApprove, CancellationToken cancellationToken);
    Task<ExecutionRunResult> RecoverAsync(IAgentExecutionActivityOperationLease operation, Guid runId, CancellationToken cancellationToken);
    Task<ExecutionRunDetail> PlanAsync(IAgentExecutionActivityOperationLease operation, AgentPlanningActionRequest request,
        CancellationToken cancellationToken);
    Task<ExecutionRunDetail> CancelPendingAsync(IAgentExecutionActivityOperationLease operation, Guid runId,
        CancellationToken cancellationToken);
}

public sealed class AgentChatOperationExecutor(
    IAgentFrameworkWorkspaceActivityExecutionService workspaceExecutionService,
    IAgentTurnContextCaptureService turnContextCaptureService,
    IAgentChatExecutionNotificationHub notificationHub,
    IAgentConversationContextService? conversationContextService = null) : IAgentChatOperationExecutor {
    private const string CapturingContextMessage = "Capturing the current workspace context.";
    private const string PreparingInputMessage = "Preparing the agent invocation from the current workspace context.";
    private const string ResolvingSessionMessage = "Loading the conversation awaiting an approval response.";
    private const string CancelledMessage = "The agent operation was cancelled.";
    private const string FailedMessage = "The agent operation failed.";

    public AgentTurnContextCaptureResult? LastCapture { get; private set; }
    private AgentConversationKey? captureKey;
    private bool contextAdopted;

    public AgentConversationContextBinding? AdoptContext() {
        if (captureKey is not { } key || LastCapture is not { } capture) {
            return null;
        }
        if (contextAdopted) {
            return capture.ConversationBinding;
        }
        CommitConversationAdoption(key, capture);
        var binding = conversationContextService?.TryGetBinding(key) ?? capture.ConversationBinding;
        LastCapture = capture with { ConversationBinding = binding };
        contextAdopted = true;
        return binding;
    }

    public Task<AgentChatRunResult> SendAsync(IAgentExecutionActivityOperationLease operation, AgentChatSendRequest request,
        AgentTurnContextObservation? observation, CancellationToken cancellationToken)
        => SendMessageCoreAsync(operation, request.AgentId, request.ChatSessionId, request.Prompt,
            request.AttachmentPaths, request.Behavior, request.ChatSessionId is { } sessionId
                ? AgentConversationKey.ForSession(sessionId)
                : request.ConversationHandleId is { } handleId ? AgentConversationKey.ForHandle(handleId) : null,
            request.PlanningOwner, cancellationToken, observation);

    public Task<AgentChatRunResult> ApproveAsync(IAgentExecutionActivityOperationLease operation, Guid agentId, Guid sessionId,
        IReadOnlyList<PendingToolApprovalDecision> decisions, bool autoApprove, CancellationToken cancellationToken)
        => ContinueApprovalCoreAsync(operation, agentId, sessionId, decisions, autoApprove, cancellationToken);

    public Task<ExecutionRunResult> RecoverAsync(IAgentExecutionActivityOperationLease operation, Guid runId, CancellationToken cancellationToken)
        => RecoverRunCoreAsync(operation, runId, cancellationToken);

    public Task<ExecutionRunDetail> PlanAsync(IAgentExecutionActivityOperationLease operation, AgentPlanningActionRequest request,
        CancellationToken cancellationToken)
        => ApplyPlanningActionAsync(operation, request, cancellationToken);

    public async Task<ExecutionRunDetail> CancelPendingAsync(IAgentExecutionActivityOperationLease operation, Guid runId,
        CancellationToken cancellationToken) {
        using (operation) {
            try {
                var result = await workspaceExecutionService.CancelPendingExecutionApprovalsWithinOperationAsync(operation, runId, cancellationToken)
                    .ConfigureAwait(false);
                EnsureTerminalized(operation);
                return result;
            } catch (OperationCanceledException) {
                TerminalizeCancellation(operation);
                throw;
            } catch {
                TerminalizeFailure(operation);
                throw;
            }
        }
    }

    private async Task<ExecutionRunDetail> ApplyPlanningActionAsync(IAgentExecutionActivityOperationLease operation,
        AgentPlanningActionRequest request, CancellationToken cancellationToken) {
        using (operation) {
            try {
                operation.Report(AgentExecutionActivityPhase.ResolvingSession, "Loading the original planning decision.");
                await Task.Yield();

                var result = await workspaceExecutionService.ApplyPlanningActionWithinOperationAsync(operation, request, cancellationToken)
                    .ConfigureAwait(false);
                EnsureTerminalized(operation);
                await PublishCompletionAsync(AgentChatContextInvocationFactory.CreateCompletionNotification(result.Run)).ConfigureAwait(false);
                return result;
            } catch (OperationCanceledException) {
                TerminalizeCancellation(operation);
                throw;
            } catch {
                TerminalizeFailure(operation);
                throw;
            }
        }
    }

    private async Task<AgentChatRunResult> SendMessageCoreAsync(
        IAgentExecutionActivityOperationLease operation,
        Guid agentId,
        Guid? chatSessionId,
        string prompt,
        IReadOnlyList<string>? attachmentPaths,
        AgentChatExecutionBehavior behavior,
        AgentConversationKey? conversationKey,
        AgentPlanningOwner? planningOwner,
        CancellationToken cancellationToken,
        AgentTurnContextObservation? observation = null) {
        using (operation) {
            try {
                operation.Report(
                    AgentExecutionActivityPhase.CapturingContext,
                    CapturingContextMessage);
                await Task.Yield();

                if (observation?.ConversationBinding is { } originalBinding && conversationKey is { } key) {
                    conversationContextService?.RestoreBinding(key, originalBinding);
                }
                var command = new AgentTurnContextCaptureCommand(agentId, chatSessionId, prompt,
                    operation.StreamId.OperationId, operation.StreamId.DatabaseProfileGeneration,
                    behavior, conversationKey, planningOwner);
                var capture = observation is null
                    ? await turnContextCaptureService.CaptureAsync(command, cancellationToken).ConfigureAwait(false)
                    : await turnContextCaptureService.CaptureAsync(command, observation, cancellationToken).ConfigureAwait(false);
                LastCapture = capture;
                captureKey = conversationKey;
                contextAdopted = false;
                if (capture.Context is { } context) {
                    operation.BindContext(
                        context.Scope.Source,
                        context.Version);
                }

                operation.Report(
                    AgentExecutionActivityPhase.PreparingInput,
                    PreparingInputMessage);
                var invocation = capture.Invocation;
                var result = await workspaceExecutionService
                    .SendMessageWithinOperationAsync(
                        operation,
                        agentId,
                        chatSessionId,
                        invocation.Prompt,
                        invocation.Options,
                        cancellationToken,
                        attachmentPaths)
                    .ConfigureAwait(false);
                EnsureTerminalized(operation);
                CommitConversationAdoption(conversationKey, capture);
                if (conversationKey is { } completedKey && conversationContextService?.TryGetBinding(completedKey) is { } adopted) {
                    LastCapture = capture with { ConversationBinding = adopted };
                }
                await PublishCompletionAsync(result.ContextCompletionNotification).ConfigureAwait(false);
                return result;
            } catch (OperationCanceledException) {
                TerminalizeCancellation(operation);
                throw;
            } catch {
                TerminalizeFailure(operation);
                throw;
            }
        }
    }

    private async Task<AgentChatRunResult> ContinueApprovalCoreAsync(
        IAgentExecutionActivityOperationLease operation,
        Guid agentId,
        Guid chatSessionId,
        IReadOnlyList<PendingToolApprovalDecision> decisions,
        bool autoApprovePendingToolCalls,
        CancellationToken cancellationToken) {
        using (operation) {
            try {
                operation.Report(
                    AgentExecutionActivityPhase.ResolvingSession,
                    ResolvingSessionMessage);
                await Task.Yield();

                var result = await workspaceExecutionService
                    .RespondToPendingApprovalsWithinOperationAsync(
                        operation,
                        agentId,
                        chatSessionId,
                        decisions,
                        autoApprovePendingToolCalls,
                        cancellationToken)
                    .ConfigureAwait(false);
                EnsureTerminalized(operation);
                await PublishCompletionAsync(result.ContextCompletionNotification).ConfigureAwait(false);
                return result;
            } catch (OperationCanceledException) {
                TerminalizeCancellation(operation);
                throw;
            } catch {
                TerminalizeFailure(operation);
                throw;
            }
        }
    }

    private async Task<ExecutionRunResult> RecoverRunCoreAsync(IAgentExecutionActivityOperationLease operation,
        Guid executionRunId, CancellationToken cancellationToken) {
        ExecutionRunResult? result = null;
        Exception? primary = null;
        try {
            operation.Report(AgentExecutionActivityPhase.ResolvingSession, "Loading the original run for recovery.");
            await Task.Yield();
            result = await workspaceExecutionService.RecoverExecutionRunWithinOperationAsync(
                operation, executionRunId, cancellationToken).ConfigureAwait(false);
        } catch (Exception exception) {
            primary = exception;
        }
        if (result?.EffectContinuation is { } || AgentEffectActivityFinalization.IsEffectFailure(primary)) {
            primary = AgentEffectActivityFinalization.Publish(operation, result?.EffectContinuation, primary);
            primary = AgentEffectActivityFinalization.Dispose(operation, result?.EffectContinuation, primary);
            if (primary is not null) {
                ExceptionDispatchInfo.Capture(primary).Throw();
            }
            return result!;
        }
        using (operation) {
            try {
                if (primary is not null) {
                    ExceptionDispatchInfo.Capture(primary).Throw();
                }
                EnsureTerminalized(operation);
                await PublishCompletionAsync(result!.ContextCompletionNotification).ConfigureAwait(false);
                return result;
            } catch (OperationCanceledException) {
                TerminalizeCancellation(operation);
                throw;
            } catch {
                TerminalizeFailure(operation);
                throw;
            }
        }
    }

    private Task PublishCompletionAsync(AgentChatExecutionCompleted? notification) {
        return notification is not null
            ? notificationHub.PublishAsync(notification)
            : Task.CompletedTask;
    }

    private void CommitConversationAdoption(
        AgentConversationKey? conversationKey,
        AgentTurnContextCaptureResult capture) {
        // The binding advances only for an admitted, executed turn. A lost
        // compare-and-swap means a newer turn or an explicit mode change
        // already moved the conversation; the stale update is skipped.
        if (conversationContextService is null ||
            conversationKey is not { } key ||
            capture.ConversationBinding is not { } binding ||
            capture.TurnReference is not { } turnReference ||
            capture.Context is not { } context) {
            return;
        }

        conversationContextService.TryCommitTurnAdoption(
            key,
            binding.Revision,
            turnReference.ContextEpochId,
            turnReference.SourceKind,
            turnReference.SourceId,
            context.Scope.DisplayName,
            turnReference.Surface,
            turnReference.View,
            turnReference.ModelContextDigest,
            context.Scope.SurfacePosition?.PrimarySelection?.Id ?? string.Empty);
    }

    private static void EnsureTerminalized(
        IAgentExecutionActivityOperationLease operation) {
        if (!operation.IsTerminal) {
            throw new InvalidOperationException(
                "The workspace execution completed without a terminal activity outcome.");
        }
    }

    private static void TerminalizeCancellation(
        IAgentExecutionActivityOperationLease operation) {
        if (!operation.IsTerminal) {
            operation.Cancel(CancelledMessage);
        }
    }

    private static void TerminalizeFailure(
        IAgentExecutionActivityOperationLease operation) {
        if (!operation.IsTerminal) {
            operation.Fail(
                FailedMessage,
                AgentExecutionActivityFailureCodes.UnhandledExecutionFailure);
        }
    }

}
