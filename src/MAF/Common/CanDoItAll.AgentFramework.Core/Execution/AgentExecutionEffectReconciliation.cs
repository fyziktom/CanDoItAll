using System.Runtime.ExceptionServices;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentEffectReconciliationIncompleteException : InvalidOperationException {
    public AgentEffectReconciliationIncompleteException(AgentEffectContinuation continuation)
        : this(continuation, null, false) {
    }

    private AgentEffectReconciliationIncompleteException(AgentEffectContinuation continuation, Exception? failure, bool leaseReleaseUnconfirmed)
        : base("Receipt observations could not be confirmed in the original execution checkpoint. Keep the original effect identities.", failure) {
        ArgumentNullException.ThrowIfNull(continuation);
        if (continuation.Persistence == AgentEffectCheckpointPersistence.Confirmed) {
            throw new ArgumentException("A confirmed waiting checkpoint is not an incomplete reconciliation.", nameof(continuation));
        }
        Continuation = continuation;
        LeaseReleaseUnconfirmed = leaseReleaseUnconfirmed;
    }

    public AgentEffectContinuation Continuation { get; }
    public bool LeaseReleaseUnconfirmed { get; }

    internal AgentEffectReconciliationIncompleteException WithCleanupFailure(Exception cleanupFailure) {
        ArgumentNullException.ThrowIfNull(cleanupFailure);
        return new(Continuation, new AggregateException("The original checkpoint and coordination lease cleanup both failed.", this, cleanupFailure), true);
    }
}
public sealed class AgentEffectReconciliationCleanupException : InvalidOperationException {
    public AgentEffectReconciliationCleanupException(AgentEffectContinuation continuation, Exception cleanupFailure)
        : base("The execution checkpoint is confirmed, but release of its file coordination lease is unconfirmed. Preserve the original effects and inspect cleanup before continuing.", cleanupFailure) {
        ArgumentNullException.ThrowIfNull(continuation);
        ArgumentNullException.ThrowIfNull(cleanupFailure);
        if (continuation.Persistence != AgentEffectCheckpointPersistence.Confirmed || continuation.Stop == AgentEffectReconciliationStop.SnapshotRead) {
            throw new ArgumentException("Cleanup failure requires an already confirmed checkpoint.", nameof(continuation));
        }
        Continuation = continuation;
    }

    public AgentEffectContinuation Continuation { get; }
}

public enum AgentEffectReconciliationFailureKind { ReadDenied, ReadUnavailable, Cancelled, AdmissionRejected, Failed }

internal sealed class AgentEffectReconciliationCancellationException(OperationCanceledException original)
    : OperationCanceledException("Receipt reconciliation was cancelled.", original, original.CancellationToken);

public sealed class AgentEffectReconciliationLeaseException : InvalidOperationException {
    public AgentEffectReconciliationLeaseException(Exception originalFailure, Exception cleanupFailure)
        : base("Receipt reconciliation failed and release of its coordination lease is unconfirmed. Inspect the original run before another slice; do not retry effects.",
            new AggregateException(originalFailure ?? throw new ArgumentNullException(nameof(originalFailure)),
                cleanupFailure ?? throw new ArgumentNullException(nameof(cleanupFailure)))) {
        if (originalFailure is AgentEffectReconciliationIncompleteException or AgentEffectReconciliationCleanupException or AgentEffectReconciliationLeaseException) {
            throw new ArgumentException("An existing typed reconciliation failure must preserve its original knowledge.", nameof(originalFailure));
        }
        FailureKind = originalFailure switch {
            AgentToolReceiptAccessDeniedException => AgentEffectReconciliationFailureKind.ReadDenied,
            AgentToolReceiptReadUnavailableException => AgentEffectReconciliationFailureKind.ReadUnavailable,
            OperationCanceledException => AgentEffectReconciliationFailureKind.Cancelled,
            AgentToolAdmissionException => AgentEffectReconciliationFailureKind.AdmissionRejected,
            _ => AgentEffectReconciliationFailureKind.Failed
        };
    }

    public AgentEffectReconciliationFailureKind FailureKind { get; }
}

internal sealed class AgentExecutionEffectReconciliation(
    AgentToolAdmissionJournal journal,
    IAgentExecutionAuthorityResolver? authorityResolver,
    AgentExecutionActivityWorkspaceIdentity workspace,
    IReadOnlyList<IAgentToolReceiptReconciliationProvider> providers) {
    internal static bool Supports(ExecutionRunRecord run) {
        if (run.ToolAdmission is not { Support: AgentToolAdmissionSupport.EffectJournalOnly } admission) {
            return false;
        }
        admission.Validate();
        return true;
    }

    internal async Task<ExecutionRunResult> ReconcileAsync(ExecutionRunRecord original, CancellationToken cancellationToken) {
        var admission = RequireOriginal(original);
        if (original.Outcome == RunOutcome.Cancelled) {
            throw new AgentToolAdmissionException("tool-admission.cancelled-reconciliation-required",
                "Use cancelled-run receipt reconciliation; cancellation does not authorize effect continuation.");
        }
        AgentToolRunLease lease;
        try {
            lease = await journal.AcquireInterruptedReconciliationAsync(admission.Reference, cancellationToken);
        } catch (OperationCanceledException cancelled) {
            throw new AgentEffectReconciliationCancellationException(cancelled);
        }
        AgentEffectContinuation? continuation = null;
        Exception? primary = null;
        try {
            using var bound = lease.Bind();
            continuation = await journal.ReconcileInterruptedAsync(lease, providers,
                (observation, token) => CanReadAsync(original, observation, token), cancellationToken);
            if (continuation.Persistence != AgentEffectCheckpointPersistence.Confirmed) {
                throw new AgentEffectReconciliationIncompleteException(continuation);
            }
        } catch (Exception exception) {
            primary = exception;
        }
        try {
            await lease.DisposeAsync();
        } catch (Exception cleanup) {
            if (primary is AgentEffectReconciliationIncompleteException incomplete) {
                throw incomplete.WithCleanupFailure(cleanup);
            }
            if (primary is not null) {
                throw new AgentEffectReconciliationLeaseException(primary, cleanup);
            }
            if (continuation?.Persistence == AgentEffectCheckpointPersistence.Confirmed) {
                throw new AgentEffectReconciliationCleanupException(continuation, cleanup);
            }
            throw;
        }
        if (primary is OperationCanceledException cancelledPrimary) {
            throw new AgentEffectReconciliationCancellationException(cancelledPrimary);
        }
        if (primary is not null) {
            ExceptionDispatchInfo.Capture(primary).Throw();
        }
        if (continuation is null) {
            throw new InvalidOperationException("Receipt reconciliation returned no checkpoint knowledge.");
        }
        return ExecutionRunResult.ForEffectContinuation(continuation);
    }

    internal Task<AgentEffectContinuation> ReadContinuationAsync(ExecutionRunDetail detail, CancellationToken cancellationToken) {
        RequireOriginal(detail.Run);
        return journal.ReadInterruptedContinuationAsync(detail,
            (observation, token) => CanReadAsync(detail.Run, observation, token), cancellationToken);
    }

    private AgentToolSessionAdmission RequireOriginal(ExecutionRunRecord run) {
        if (!Supports(run)) {
            throw new AgentToolAdmissionException("tool-admission.effect-journal-required", "This operation requires the original validated effects-only journal.");
        }
        var admission = run.ToolAdmission!.Session;
        if (admission.Profile.ProfileId != workspace.DatabaseProfileId || admission.Profile.Generation != workspace.DatabaseProfileGeneration ||
            admission.Reference.ExecutionRunId != run.Id || admission.AgentId != run.AgentId ||
            (admission.Reference.BackgroundSource is null ? admission.Reference.ChatSessionId != run.ChatSessionId : run.ChatSessionId is not null)) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        return admission;
    }

    private async ValueTask<bool> CanReadAsync(ExecutionRunRecord original, AgentToolSessionObservation observation,
        CancellationToken cancellationToken) {
        var admitted = RequireOriginal(original);
        if (observation.Session != admitted) {
            return false;
        }
        if (admitted.Reference.BackgroundSource is { } source) {
            return observation.TurnContext is null && observation.Governance is null &&
                admitted.Purpose == AgentRuntimeContextPurpose.GovernedProcessAutomation &&
                original.SourceKind == source.SourceKind && original.SourceId == source.SourceId;
        }
        var governance = observation.Governance;
        var reference = observation.TurnContext;
        var scope = original.ToolAdmission?.PlanningDecision is not null
            ? AgentPlanningRunMetadata.ReadScope(original) : ExecutionInvocationMetadata.ResolveContextWorkspaceScope(original);
        if (admitted.Purpose != AgentRuntimeContextPurpose.InteractiveChat || governance is null || reference is null ||
            reference != AgentTurnContextMetadata.TryReadTurnContextReference(original.MetadataJson) ||
            governance.AuthorityId != admitted.Reference.AuthorityId || governance.AgentId != original.AgentId ||
            governance.DatabaseProfileId != workspace.DatabaseProfileId || governance.DatabaseProfileGeneration != workspace.DatabaseProfileGeneration ||
            !governance.ReadAllowed || (scope is null ? !governance.WorkspaceScope.IsDefaultSandbox : scope != governance.WorkspaceScope) ||
            string.Equals(governance.PolicyVersion, "unknown", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(governance.PolicyFingerprint, "unknown", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }
        var resolver = authorityResolver ?? throw new InvalidOperationException("Effect receipt read requires the canonical current authority resolver.");
        AgentExecutionAuthorityRecord current;
        try {
            current = await resolver.ResolveAsync(AgentExecutionAuthorityResolutionRequest.FromCaptured(reference, governance), cancellationToken);
        } catch (Exception exception) when (exception is AgentExecutionAuthorityMismatchException or AgentChatContextAccessDeniedException) {
            return false;
        }
        var currentRead = AgentExecutionGovernanceSnapshot.FromAuthority(current);
        return current.ReadAllowed && current.AgentId == governance.AgentId && current.DatabaseProfileId == governance.DatabaseProfileId &&
            current.DatabaseProfileGeneration == governance.DatabaseProfileGeneration && current.WorkspaceScope == governance.WorkspaceScope &&
            current.SchemaVersion == governance.EffectiveSchemaVersion && current.SourceProjectLifetime == governance.SourceProjectLifetime &&
            Covers(currentRead.AllowedCapabilityKeys, governance.AllowedCapabilityKeys) &&
            Covers(currentRead.ReadOnlyExternalTargetAliases.Union(currentRead.WritableExternalTargetAliases),
                governance.ReadOnlyExternalTargetAliases.Union(governance.WritableExternalTargetAliases));
    }

    private static bool Covers(IReadOnlySet<string> current, IReadOnlySet<string> original)
        => current.Count == 0 || original.Count != 0 && current.IsSupersetOf(original);
}
