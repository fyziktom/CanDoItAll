using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentChatExecutionOrchestrator(
    IAgentFrameworkWorkspaceActivityExecutionService workspaceExecutionService,
    IAgentChatContextRegistry contextRegistry,
    IAgentChatExecutionNotificationHub notificationHub,
    IAgentExecutionActivityCoordinator activityCoordinator,
    ICanDoItAllAgentWorkspaceFactory workspaceFactory,
    IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor,
    IAgentExecutionProfileGenerationSource executionProfileGenerationSource,
    TimeProvider timeProvider)
    : IAgentChatExecutionOrchestrator
{
    private const string SendAcceptedMessage = "Agent request accepted.";
    private const string ApprovalAcceptedMessage = "Approval response accepted.";
    private const string CapturingContextMessage = "Capturing the current workspace context.";
    private const string PreparingInputMessage = "Preparing the agent invocation from the current workspace context.";
    private const string ResolvingSessionMessage = "Loading the conversation awaiting an approval response.";
    private const string CancelledMessage = "The agent operation was cancelled.";
    private const string FailedMessage = "The agent operation failed.";

    public AgentChatOperationHandle StartSendMessage(
        AgentChatSendRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateAgentId(request.AgentId);
        ValidateOptionalSessionId(request.ChatSessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Prompt);
        ArgumentNullException.ThrowIfNull(request.Behavior);
        cancellationToken.ThrowIfCancellationRequested();

        var operation = AdmitOperation(
            request.AgentId,
            request.ChatSessionId,
            SendAcceptedMessage);
        var completion = SendMessageCoreAsync(
            operation,
            request.AgentId,
            request.ChatSessionId,
            request.Prompt,
            request.AttachmentPaths?.ToArray(),
            request.Behavior,
            cancellationToken);
        return new AgentChatOperationHandle(operation.StreamId, completion);
    }

    public AgentChatOperationHandle StartSendMessage(
        Guid agentId,
        Guid? chatSessionId,
        string prompt,
        IReadOnlyList<string>? attachmentPaths = null,
        CancellationToken cancellationToken = default)
    {
        return StartSendMessage(
            new AgentChatSendRequest(
                agentId,
                chatSessionId,
                prompt)
            {
                AttachmentPaths = attachmentPaths
            },
            cancellationToken);
    }

    public Task<AgentChatRunResult> SendMessageAsync(
        AgentChatSendRequest request,
        CancellationToken cancellationToken = default)
    {
        return StartSendMessage(
            request,
            cancellationToken).Completion;
    }

    public Task<AgentChatRunResult> SendMessageAsync(
        Guid agentId,
        Guid? chatSessionId,
        string prompt,
        IReadOnlyList<string>? attachmentPaths = null,
        CancellationToken cancellationToken = default)
    {
        return SendMessageAsync(
            new AgentChatSendRequest(
                agentId,
                chatSessionId,
                prompt)
            {
                AttachmentPaths = attachmentPaths
            },
            cancellationToken);
    }

    public AgentChatOperationHandle StartApprovalContinuation(
        Guid agentId,
        Guid chatSessionId,
        bool approved,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default)
    {
        ValidateAgentId(agentId);
        ValidateSessionId(chatSessionId);
        cancellationToken.ThrowIfCancellationRequested();

        var operation = AdmitOperation(
            agentId,
            chatSessionId,
            ApprovalAcceptedMessage);
        var completion = ContinueApprovalCoreAsync(
            operation,
            agentId,
            chatSessionId,
            approved,
            autoApprovePendingToolCalls,
            cancellationToken);
        return new AgentChatOperationHandle(operation.StreamId, completion);
    }

    public Task<AgentChatRunResult> RespondToPendingApprovalsAsync(
        Guid agentId,
        Guid chatSessionId,
        bool approved,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default)
    {
        return StartApprovalContinuation(
            agentId,
            chatSessionId,
            approved,
            autoApprovePendingToolCalls,
            cancellationToken).Completion;
    }

    private async Task<AgentChatRunResult> SendMessageCoreAsync(
        IAgentExecutionActivityOperationLease operation,
        Guid agentId,
        Guid? chatSessionId,
        string prompt,
        IReadOnlyList<string>? attachmentPaths,
        AgentChatExecutionBehavior behavior,
        CancellationToken cancellationToken)
    {
        using (operation)
        {
            try
            {
                operation.Report(
                    AgentExecutionActivityPhase.CapturingContext,
                    CapturingContextMessage);
                await Task.Yield();

                var context = await contextRegistry
                    .CaptureAsync(cancellationToken)
                    .ConfigureAwait(false);
                EnsureCurrentProfileGeneration(operation);
                if (context is not null)
                {
                    operation.BindContext(
                        context.Scope.Source,
                        context.Version);
                }

                operation.Report(
                    AgentExecutionActivityPhase.PreparingInput,
                    PreparingInputMessage);
                var invocation = AgentChatContextInvocationFactory.Create(
                    context,
                    agentId,
                    chatSessionId,
                    prompt,
                    operation.StreamId.OperationId,
                    operation.StreamId.DatabaseProfileGeneration,
                    timeProvider.GetUtcNow(),
                    behavior);
                var options = invocation.Options;
                var result = await workspaceExecutionService
                    .SendMessageWithinOperationAsync(
                        operation,
                        agentId,
                        chatSessionId,
                        invocation.Prompt,
                        options,
                        cancellationToken,
                        attachmentPaths)
                    .ConfigureAwait(false);
                EnsureTerminalized(operation);
                await PublishCompletionAsync(result).ConfigureAwait(false);
                return result;
            }
            catch (OperationCanceledException)
            {
                TerminalizeCancellation(operation);
                throw;
            }
            catch
            {
                TerminalizeFailure(operation);
                throw;
            }
        }
    }

    private async Task<AgentChatRunResult> ContinueApprovalCoreAsync(
        IAgentExecutionActivityOperationLease operation,
        Guid agentId,
        Guid chatSessionId,
        bool approved,
        bool autoApprovePendingToolCalls,
        CancellationToken cancellationToken)
    {
        using (operation)
        {
            try
            {
                operation.Report(
                    AgentExecutionActivityPhase.ResolvingSession,
                    ResolvingSessionMessage);
                await Task.Yield();

                var result = await workspaceExecutionService
                    .RespondToPendingApprovalsWithinOperationAsync(
                        operation,
                        agentId,
                        chatSessionId,
                        approved,
                        autoApprovePendingToolCalls,
                        cancellationToken)
                    .ConfigureAwait(false);
                EnsureTerminalized(operation);
                await PublishCompletionAsync(result).ConfigureAwait(false);
                return result;
            }
            catch (OperationCanceledException)
            {
                TerminalizeCancellation(operation);
                throw;
            }
            catch
            {
                TerminalizeFailure(operation);
                throw;
            }
        }
    }

    private IAgentExecutionActivityOperationLease AdmitOperation(
        Guid agentId,
        Guid? chatSessionId,
        string acceptedMessage)
    {
        var firstProfile = databaseProfileRuntimeAccessor
            .ResolveCurrentProfile()
            .Profile;
        var firstProfileGeneration =
            executionProfileGenerationSource.GetGeneration();
        var workspaceScope = workspaceFactory.GetOrganizationScope();
        var confirmedProfile = databaseProfileRuntimeAccessor
            .ResolveCurrentProfile()
            .Profile;
        var confirmedProfileGeneration =
            executionProfileGenerationSource.GetGeneration();
        var expectedScope = WorkspaceScopeDescriptor.Organization(
            confirmedProfile.Id.ToString("N"));
        if (firstProfile.Id != confirmedProfile.Id ||
            !string.Equals(
                firstProfile.Runtime.Fingerprint,
                confirmedProfile.Runtime.Fingerprint,
                StringComparison.Ordinal) ||
            firstProfileGeneration != confirmedProfileGeneration ||
            workspaceScope != expectedScope)
        {
            throw new InvalidOperationException(
                "The current database profile changed while the agent operation was being admitted.");
        }

        var streamId = new AgentExecutionActivityStreamId(
            confirmedProfile.Id,
            workspaceScope,
            confirmedProfileGeneration,
            AgentExecutionOperationId.New());
        return activityCoordinator.AdmitOperation(
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
    }

    private Task PublishCompletionAsync(AgentChatRunResult result)
    {
        return result.ContextCompletionNotification is { } notification
            ? notificationHub.PublishAsync(notification)
            : Task.CompletedTask;
    }

    private void EnsureCurrentProfileGeneration(
        IAgentExecutionActivityOperationLease operation)
    {
        if (executionProfileGenerationSource.GetGeneration() !=
            operation.StreamId.DatabaseProfileGeneration)
        {
            throw new InvalidOperationException(
                "The current database profile changed while the agent context was being captured.");
        }
    }

    private static void EnsureTerminalized(
        IAgentExecutionActivityOperationLease operation)
    {
        if (!operation.IsTerminal)
        {
            throw new InvalidOperationException(
                "The workspace execution completed without a terminal activity outcome.");
        }
    }

    private static void TerminalizeCancellation(
        IAgentExecutionActivityOperationLease operation)
    {
        if (!operation.IsTerminal)
        {
            operation.Cancel(CancelledMessage);
        }
    }

    private static void TerminalizeFailure(
        IAgentExecutionActivityOperationLease operation)
    {
        if (!operation.IsTerminal)
        {
            operation.Fail(
                FailedMessage,
                AgentExecutionActivityFailureCodes.UnhandledExecutionFailure);
        }
    }

    private static void ValidateAgentId(Guid agentId)
    {
        if (agentId == Guid.Empty)
        {
            throw new ArgumentException("Agent id cannot be empty.", nameof(agentId));
        }
    }

    private static void ValidateSessionId(Guid chatSessionId)
    {
        if (chatSessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Chat session id cannot be empty.",
                nameof(chatSessionId));
        }
    }

    private static void ValidateOptionalSessionId(Guid? chatSessionId)
    {
        if (chatSessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Optional chat session id cannot be empty.",
                nameof(chatSessionId));
        }
    }
}
