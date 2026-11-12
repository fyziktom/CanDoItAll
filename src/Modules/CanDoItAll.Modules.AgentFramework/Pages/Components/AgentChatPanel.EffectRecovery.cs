using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Chat;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class AgentChatPanel {
    private AgentChatEffectRecovery? effectRecovery;
    private bool effectLeaseReleaseUnconfirmed;
    private bool effectActivityFinalizationFailed;
    private AgentChatRecoveryTarget? recoveryTarget;
    private DatabaseProfileGeneration? effectRecoveryProfile;

    private bool IsWaitingForEffectContinuation => workspace?.SelectedRun is { State: ExecutionState.WaitingOnTool } run &&
        (run.ToolAdmission?.ToolContinuationRequirement is AgentToolContinuationRequirement.EffectReconciliationRequired or
            AgentToolContinuationRequirement.ProviderInputRequired ||
            effectRecovery?.Latest is { Persistence: AgentEffectCheckpointPersistence.Confirmed } continuation &&
            continuation.ExecutionRunId == run.Id);

    private void ObserveEffectContinuation(AgentEffectContinuation continuation, Guid agentId, Guid sessionId,
        AgentExecutionActivityStreamId stream) {
        if (continuation.ChatSessionId != sessionId || stream.DatabaseProfileGeneration != ProfileGenerationSource.GetGeneration()) {
            throw new InvalidOperationException("Receipt recovery returned a result outside the original thread or profile.");
        }
        if (recoveryTarget is { } original && (original.AgentId != agentId || original.ChatSessionId != sessionId ||
                original.ExecutionRunId != continuation.ExecutionRunId || original.Stream.DatabaseProfileGeneration != stream.DatabaseProfileGeneration)) {
            throw new InvalidOperationException("Receipt recovery cannot replace the original run identity.");
        }
        effectRecovery = AgentChatEffectRecovery.Observe(effectRecovery, continuation);
        effectRecoveryProfile = stream.DatabaseProfileGeneration;
        recoveryTarget ??= new(agentId, sessionId, continuation.ExecutionRunId, stream);
    }

    private void ObserveCurrentEffectRead() {
        var profile = ProfileGenerationSource.GetGeneration();
        if (chatSession.EffectContinuation is not { } current || workspace?.SelectedRun?.Id != current.ExecutionRunId ||
                effectRecoveryProfile is { } originalProfile && originalProfile != profile) {
            return;
        }
        if (effectRecovery is null || effectRecovery.Latest.ExecutionRunId == current.ExecutionRunId &&
                effectRecovery.Latest.RunRevision <= current.RunRevision) {
            effectRecovery = AgentChatEffectRecovery.Observe(effectRecovery, current);
            if (effectLeaseReleaseUnconfirmed) {
                effectRecovery = effectRecovery with { LeaseReleaseUnconfirmed = true };
            }
            if (effectActivityFinalizationFailed) {
                effectRecovery = effectRecovery with { ActivityFinalizationFailed = true };
            }
            effectRecoveryProfile = profile;
        }
    }

    private void ObserveReceiptReadFailure(AgentChatReceiptReadState state) {
        if (effectRecovery is not null) {
            effectRecovery = effectRecovery with { ReadState = state };
        }
        SetMessage("Receipt recovery needs attention", "warning", state == AgentChatReceiptReadState.Denied
            ? "Current access to receipt details was denied. Previously observed commitment remains known."
            : "Current receipt access could not be verified. Previously observed commitment remains known.");
    }

    private void ObserveEffectFailure(Exception exception, Guid agentId, Guid? sessionId, AgentExecutionActivityStreamId stream,
        AgentExecutionActivityStreamId? activityStream = null) {
        if (exception is AgentExecutionActivityPublicationException { HasEffectOutcome: true } publication) {
            if (publication.StreamId != (activityStream ?? stream)) {
                throw new InvalidOperationException("Receipt recovery activity belongs to a different stream.");
            }
            if (publication.EffectContinuation is { ChatSessionId: { } originalSession } continuation &&
                    (!sessionId.HasValue || sessionId == originalSession)) {
                ObserveEffectContinuation(continuation, agentId, originalSession, stream);
            }
            if (publication.EffectFailureKind == AgentEffectReconciliationFailureKind.ReadDenied) {
                ObserveReceiptReadFailure(AgentChatReceiptReadState.Denied);
            } else if (publication.EffectFailureKind == AgentEffectReconciliationFailureKind.ReadUnavailable) {
                ObserveReceiptReadFailure(AgentChatReceiptReadState.Unavailable);
            }
            if (publication.EffectLeaseReleaseUnconfirmed) {
                ObserveLeaseReleaseUnconfirmed();
            }
            effectActivityFinalizationFailed = true;
            if (effectRecovery is not null) {
                effectRecovery = effectRecovery with { ActivityFinalizationFailed = true };
            }
            ResolveRunState();
            SetMessage("Recovery needs attention", "warning", AgentChatEffectRecovery.ActivityFinalizationFailedMessage);
        } else if (exception is AgentEffectReconciliationLeaseException leaseFailure) {
            if (leaseFailure.FailureKind == AgentEffectReconciliationFailureKind.ReadDenied) {
                ObserveReceiptReadFailure(AgentChatReceiptReadState.Denied);
            } else if (leaseFailure.FailureKind == AgentEffectReconciliationFailureKind.ReadUnavailable) {
                ObserveReceiptReadFailure(AgentChatReceiptReadState.Unavailable);
            }
            ObserveLeaseReleaseUnconfirmed();
            SetMessage("Recovery needs attention", "warning", AgentChatEffectRecovery.ReleaseUnconfirmedMessage);
        } else if (exception is AgentEffectReconciliationCleanupException cleanup && cleanup.Continuation.ChatSessionId is { } savedSession &&
                (!sessionId.HasValue || sessionId == savedSession)) {
            ObserveEffectContinuation(cleanup.Continuation, agentId, savedSession, stream);
            ObserveLeaseReleaseUnconfirmed();
            ShowEffectContinuation();
        } else if (exception is AgentEffectReconciliationIncompleteException incomplete && incomplete.Continuation.ChatSessionId is { } originalSession &&
                (!sessionId.HasValue || sessionId == originalSession)) {
            ObserveEffectContinuation(incomplete.Continuation, agentId, originalSession, stream);
            if (incomplete.LeaseReleaseUnconfirmed) {
                ObserveLeaseReleaseUnconfirmed();
            }
            ShowEffectContinuation();
        } else if (exception is AgentToolReceiptAccessDeniedException) {
            ObserveReceiptReadFailure(AgentChatReceiptReadState.Denied);
        } else {
            ObserveReceiptReadFailure(AgentChatReceiptReadState.Unavailable);
        }
        LogOperationFailure(exception, agentId, sessionId, ActiveChatHandleId, "receipt-recovery", false);
    }

    private void ObserveLeaseReleaseUnconfirmed() {
        effectLeaseReleaseUnconfirmed = true;
        if (effectRecovery is not null) {
            effectRecovery = effectRecovery with { LeaseReleaseUnconfirmed = true };
        }
    }

    private void ShowEffectContinuation() {
        ResolveRunState();
        SetMessage("Continuation needed", "warning", effectRecovery!.ActivityFinalizationFailed
            ? AgentChatEffectRecovery.ActivityFinalizationFailedMessage
            : effectRecovery.LeaseReleaseUnconfirmed ? effectRecovery.ReleaseStatus : effectRecovery.Status);
    }

    private sealed record AgentChatRecoveryTarget(Guid AgentId, Guid ChatSessionId, Guid ExecutionRunId,
        AgentExecutionActivityStreamId Stream);
}
