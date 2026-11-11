using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentChatExecutionOrchestrator(
    IAgentFrameworkWorkspaceActivityExecutionService workspaceExecutionService,
    IAgentTurnContextCaptureService turnContextCaptureService,
    IAgentChatExecutionNotificationHub notificationHub,
    IAgentExecutionActivityCoordinator activityCoordinator,
    ICanDoItAllAgentWorkspaceFactory workspaceFactory,
    IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor,
    IAgentExecutionProfileGenerationSource executionProfileGenerationSource,
    IAgentConversationContextService? conversationContextService = null,
    Func<IAgentHistoricalReceiptReviewService>? historicalReceipts = null,
    IDatabaseRuntimeState? historicalRuntimeState = null,
    AgentChatBackgroundExecution? background = null,
    IAgentChatOwnerProvider? ownerProvider = null,
    IAgentChatContextRegistry? contextRegistry = null)
    : IAgentChatExecutionOrchestrator {
    private const string SendAcceptedMessage = "Agent request accepted.";
    private const string ApprovalAcceptedMessage = "Approval response accepted.";
    private const string RecoveryAcceptedMessage = "Original run recovery accepted.";
    private readonly IAgentChatOperationExecutor inlineExecutor = new AgentChatOperationExecutor(
        workspaceExecutionService, turnContextCaptureService, notificationHub, conversationContextService);

    public (AgentExecutionActivityStreamId StreamId, Task<ExecutionRunDetail> Completion) StartPlanningAction(
        Guid agentId, AgentPlanningActionRequest request, CancellationToken cancellationToken = default) {
        ValidateAgentId(agentId);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var operation = AdmitOperation(agentId, request.ChatSessionId, "Planning action accepted.");
        return (operation.StreamId, background is null
            ? inlineExecutor.PlanAsync(operation, request, cancellationToken)
            : background.PlanAsync(GetOwner(cancellationToken), operation, request));
    }

    public Task<AgentHistoricalReceiptReview> ReadSavedEffectReceiptsAsync(AgentHistoricalReceiptReviewRequest request,
        CancellationToken cancellationToken = default) {
        AgentHistoricalReceiptReviewService.RequireInitialized(historicalRuntimeState);
        return (historicalReceipts ?? throw new NotSupportedException("Historical receipt review is not registered."))()
            .ReadSavedEffectReceiptsAsync(request, cancellationToken);
    }

    public AgentChatOperationHandle StartSendMessage(
        AgentChatSendRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        ValidateAgentId(request.AgentId);
        ValidateOptionalSessionId(request.ChatSessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Prompt);
        ArgumentNullException.ThrowIfNull(request.Behavior);
        cancellationToken.ThrowIfCancellationRequested();

        var conversationKey = ResolveConversationKey(request);
        var frozenRequest = request with { AttachmentPaths = request.AttachmentPaths?.ToArray() };
        var observation = background is null ? null : CaptureObservation(conversationKey);
        var operation = AdmitOperation(
            request.AgentId,
            request.ChatSessionId,
            SendAcceptedMessage);
        var completion = background is null
            ? inlineExecutor.SendAsync(operation, frozenRequest, observation, cancellationToken)
            : background.SendAsync(GetOwner(cancellationToken), operation, frozenRequest, observation!);
        return new AgentChatOperationHandle(operation.StreamId, completion);
    }

    public AgentChatOperationHandle StartSendMessage(
        Guid agentId,
        Guid? chatSessionId,
        string prompt,
        IReadOnlyList<string>? attachmentPaths = null,
        CancellationToken cancellationToken = default) {
        return StartSendMessage(
            new AgentChatSendRequest(
                agentId,
                chatSessionId,
                prompt) {
                AttachmentPaths = attachmentPaths
            },
            cancellationToken);
    }

    public Task<AgentChatRunResult> SendMessageAsync(
        AgentChatSendRequest request,
        CancellationToken cancellationToken = default) {
        return StartSendMessage(
            request,
            cancellationToken).Completion;
    }

    public Task<AgentChatRunResult> SendMessageAsync(
        Guid agentId,
        Guid? chatSessionId,
        string prompt,
        IReadOnlyList<string>? attachmentPaths = null,
        CancellationToken cancellationToken = default) {
        return SendMessageAsync(
            new AgentChatSendRequest(
                agentId,
                chatSessionId,
                prompt) {
                AttachmentPaths = attachmentPaths
            },
            cancellationToken);
    }

    public AgentChatOperationHandle StartApprovalContinuation(
        Guid agentId,
        Guid chatSessionId,
        IReadOnlyList<PendingToolApprovalDecision> decisions,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default) {
        ValidateAgentId(agentId);
        ValidateSessionId(chatSessionId);
        ArgumentNullException.ThrowIfNull(decisions);
        cancellationToken.ThrowIfCancellationRequested();

        var operation = AdmitOperation(
            agentId,
            chatSessionId,
            ApprovalAcceptedMessage);
        var frozenDecisions = decisions.ToArray();
        var completion = background is null
            ? inlineExecutor.ApproveAsync(operation, agentId, chatSessionId, frozenDecisions, autoApprovePendingToolCalls, cancellationToken)
            : background.ApproveAsync(GetOwner(cancellationToken), operation, frozenDecisions, autoApprovePendingToolCalls);
        return new AgentChatOperationHandle(operation.StreamId, completion);
    }

    public (AgentExecutionActivityStreamId StreamId, Task<ExecutionRunResult> Completion) StartRunRecovery(
        Guid agentId, Guid chatSessionId, Guid executionRunId, AgentExecutionActivityStreamId rejectedStreamId,
        CancellationToken cancellationToken = default) {
        ValidateAgentId(agentId);
        ValidateSessionId(chatSessionId);
        if (executionRunId == Guid.Empty) {
            throw new ArgumentException("Execution run id cannot be empty.", nameof(executionRunId));
        }
        ArgumentNullException.ThrowIfNull(rejectedStreamId);
        cancellationToken.ThrowIfCancellationRequested();
        var operation = AdmitOperation(agentId, chatSessionId, RecoveryAcceptedMessage, rejectedStreamId);
        return (operation.StreamId, background is null
            ? inlineExecutor.RecoverAsync(operation, executionRunId, cancellationToken)
            : background.RecoverAsync(GetOwner(cancellationToken), operation, executionRunId));
    }

    public Task<AgentChatRunResult> RespondToPendingApprovalsAsync(
        Guid agentId,
        Guid chatSessionId,
        IReadOnlyList<PendingToolApprovalDecision> decisions,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default) {
        return StartApprovalContinuation(
            agentId,
            chatSessionId,
            decisions,
            autoApprovePendingToolCalls,
            cancellationToken).Completion;
    }

    public (AgentExecutionActivityStreamId StreamId, Task<ExecutionRunDetail> Completion) StartPendingApprovalCancellation(
        Guid agentId, Guid chatSessionId, Guid executionRunId, CancellationToken cancellationToken = default) {
        ValidateAgentId(agentId);
        ValidateSessionId(chatSessionId);
        if (executionRunId == Guid.Empty) {
            throw new ArgumentException("An execution run id is required.", nameof(executionRunId));
        }
        cancellationToken.ThrowIfCancellationRequested();
        var operation = AdmitOperation(agentId, chatSessionId, "Pending approval cancellation accepted.");
        return (operation.StreamId, background is null
            ? inlineExecutor.CancelPendingAsync(operation, executionRunId, cancellationToken)
            : background.CancelPendingAsync(GetOwner(cancellationToken), operation, executionRunId));
    }

    private async ValueTask<AgentChatOwnerId> GetOwner(CancellationToken cancellationToken)
        => await (ownerProvider ?? throw new InvalidOperationException("Background chat execution requires an owner provider."))
            .GetOwnerAsync(cancellationToken).ConfigureAwait(false);

    private AgentTurnContextObservation CaptureObservation(AgentConversationKey? key) {
        var binding = key is { } conversation ? conversationContextService?.GetOrCreateBinding(conversation) : null;
        var context = binding?.Mode == AgentConversationContextMode.Detached ? null
            : (contextRegistry ?? throw new InvalidOperationException("Background chat execution requires a context registry."))
                .CaptureForExecution();
        return new(context, binding);
    }

    private IAgentExecutionActivityOperationLease AdmitOperation(
        Guid agentId,
        Guid? chatSessionId,
        string acceptedMessage,
        AgentExecutionActivityStreamId? expectedStreamId = null) {
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
            workspaceScope != expectedScope) {
            throw new InvalidOperationException(
                "The current database profile changed while the agent operation was being admitted.");
        }

        if (expectedStreamId is not null) {
            AgentExecutionActivityAccessPolicy.EnsureAuthorized(expectedStreamId, confirmedProfile.Id, confirmedProfileGeneration);
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
            acceptedMessage) switch {
            AgentExecutionActivityAdmitted admitted => admitted.Operation,
            AgentExecutionActivityRejected rejected =>
                throw new AgentExecutionActivityAdmissionException(
                    rejected.StreamId,
                    rejected.Reason),
            _ => throw new InvalidOperationException(
                "The activity coordinator returned an unknown admission result.")
        };
    }

    private AgentConversationKey? ResolveConversationKey(AgentChatSendRequest request) {
        if (conversationContextService is null) {
            return null;
        }

        if (request.ChatSessionId is { } sessionId && sessionId != Guid.Empty) {
            return AgentConversationKey.ForSession(sessionId);
        }

        return request.ConversationHandleId is { IsEmpty: false } handleId
            ? AgentConversationKey.ForHandle(handleId)
            : null;
    }

    private static void ValidateAgentId(Guid agentId) {
        if (agentId == Guid.Empty) {
            throw new ArgumentException("Agent id cannot be empty.", nameof(agentId));
        }
    }

    private static void ValidateSessionId(Guid chatSessionId) {
        if (chatSessionId == Guid.Empty) {
            throw new ArgumentException(
                "Chat session id cannot be empty.",
                nameof(chatSessionId));
        }
    }

    private static void ValidateOptionalSessionId(Guid? chatSessionId) {
        if (chatSessionId == Guid.Empty) {
            throw new ArgumentException(
                "Optional chat session id cannot be empty.",
                nameof(chatSessionId));
        }
    }
}
