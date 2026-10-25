using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Web.Api;

internal static class AgentEffectRecoveryApiResults {
    internal const string CheckpointIncomplete = "agents.effect-checkpoint-incomplete";
    internal const string LeaseReleaseUnconfirmed = "agents.effect-lease-release-unconfirmed";
    internal const string ReadDenied = "agents.effect-read-denied";
    internal const string ReadUnavailable = "agents.effect-read-unavailable";
    internal const string ActivityFinalizationFailed = "agents.effect-activity-finalization-failed";

    internal static bool Handles(Exception exception) => exception is AgentEffectReconciliationIncompleteException or AgentEffectReconciliationCleanupException or AgentEffectReconciliationLeaseException or
        AgentToolReceiptAccessDeniedException or AgentToolReceiptReadUnavailableException or AgentExecutionActivityPublicationException { HasEffectOutcome: true };

    internal static IResult ToHttp(HttpContext context, Exception exception, Guid executionRunId) {
        var status = exception switch {
            AgentExecutionActivityPublicationException { HasEffectOutcome: true, EffectFailureKind: AgentEffectReconciliationFailureKind.ReadDenied } => StatusCodes.Status403Forbidden,
            AgentExecutionActivityPublicationException { HasEffectOutcome: true } => StatusCodes.Status503ServiceUnavailable,
            AgentEffectReconciliationLeaseException { FailureKind: AgentEffectReconciliationFailureKind.ReadDenied } => StatusCodes.Status403Forbidden,
            AgentEffectReconciliationIncompleteException { Continuation.Persistence: AgentEffectCheckpointPersistence.NotAttempted } => StatusCodes.Status409Conflict,
            AgentEffectReconciliationIncompleteException or AgentEffectReconciliationCleanupException or AgentEffectReconciliationLeaseException or AgentToolReceiptReadUnavailableException => StatusCodes.Status503ServiceUnavailable,
            AgentToolReceiptAccessDeniedException => StatusCodes.Status403Forbidden,
            _ => throw new ArgumentException("The failure is not a supported effect reconciliation outcome.", nameof(exception))
        };
        return Results.Json(CreateResponse(context, exception, executionRunId), statusCode: status);
    }

    internal static ApiErrorResponse CreateResponse(HttpContext context, Exception exception, Guid? executionRunId) {
        var (code, message) = exception switch {
            AgentExecutionActivityPublicationException { HasEffectOutcome: true, EffectFailureKind: AgentEffectReconciliationFailureKind.ReadDenied } => (ReadDenied, "Current receipt access was denied and activity finalization failed. No additional effect evidence is disclosed; preserve prior knowledge and inspect the original execution."),
            AgentExecutionActivityPublicationException { HasEffectOutcome: true, EffectFailureKind: AgentEffectReconciliationFailureKind.ReadUnavailable } => (ReadUnavailable, "Current receipt access could not be verified and activity finalization failed. No additional effect evidence is disclosed; preserve prior knowledge and inspect the original execution."),
            AgentExecutionActivityPublicationException { HasEffectOutcome: true } => (ActivityFinalizationFailed, "Saved-effect activity could not be finalized. Preserve the supplied checkpoint knowledge and original effect identities; inspect the original execution before continuing. This is not permission to retry an effect."),
            AgentEffectReconciliationIncompleteException { LeaseReleaseUnconfirmed: true } => (CheckpointIncomplete, "Neither the execution checkpoint acknowledgement nor coordination lease release is confirmed. Preserve the original effect identities; this is not permission to retry an effect."),
            AgentEffectReconciliationIncompleteException => (CheckpointIncomplete, "The saved execution checkpoint was not confirmed. Preserve the original effect identities; this is not permission to retry an effect."),
            AgentEffectReconciliationCleanupException => (LeaseReleaseUnconfirmed, "The original checkpoint is confirmed, but coordination lease release is unconfirmed. Inspect cleanup and the original run before any further slice; do not retry effects."),
            AgentEffectReconciliationLeaseException { FailureKind: AgentEffectReconciliationFailureKind.ReadDenied } => (ReadDenied, "Current receipt access was denied and coordination lease release is unconfirmed. No effect evidence is disclosed; inspect cleanup before any further slice."),
            AgentEffectReconciliationLeaseException { FailureKind: AgentEffectReconciliationFailureKind.ReadUnavailable } => (ReadUnavailable, "Current receipt access could not be verified and coordination lease release is unconfirmed. No effect evidence is disclosed; inspect cleanup before any further slice."),
            AgentEffectReconciliationLeaseException { FailureKind: AgentEffectReconciliationFailureKind.Cancelled } => (LeaseReleaseUnconfirmed, "Receipt reconciliation was cancelled and coordination lease release is unconfirmed. No additional effect evidence is disclosed; inspect cleanup before any further slice."),
            AgentEffectReconciliationLeaseException => (LeaseReleaseUnconfirmed, "Receipt reconciliation failed and coordination lease release is unconfirmed. No additional effect evidence is disclosed; inspect cleanup before any further slice."),
            AgentToolReceiptAccessDeniedException => (ReadDenied, "Current access does not permit reading this saved execution's effect evidence."),
            AgentToolReceiptReadUnavailableException => (ReadUnavailable, "Current read access could not be verified. No additional effect evidence is disclosed."),
            _ => throw new ArgumentException("The failure is not a supported effect reconciliation outcome.", nameof(exception))
        };
        var continuation = exception switch {
            AgentExecutionActivityPublicationException { HasEffectOutcome: true } publication => publication.EffectContinuation,
            AgentEffectReconciliationIncompleteException incomplete => incomplete.Continuation,
            AgentEffectReconciliationCleanupException cleanup => cleanup.Continuation,
            _ => null
        };
        return new([new(code, message, ErrorSeverity.Error)]) {
            CorrelationId = context.TraceIdentifier,
            ExecutionRunId = continuation?.ExecutionRunId ?? executionRunId,
            ChatSessionId = continuation?.ChatSessionId,
            EffectContinuation = continuation,
            EffectLeaseReleaseUnconfirmed = exception is AgentEffectReconciliationCleanupException or AgentEffectReconciliationLeaseException or AgentEffectReconciliationIncompleteException { LeaseReleaseUnconfirmed: true } or
                AgentExecutionActivityPublicationException { HasEffectOutcome: true, EffectLeaseReleaseUnconfirmed: true },
            EffectActivityFinalizationFailed = exception is AgentExecutionActivityPublicationException { HasEffectOutcome: true }
        };
    }
}
