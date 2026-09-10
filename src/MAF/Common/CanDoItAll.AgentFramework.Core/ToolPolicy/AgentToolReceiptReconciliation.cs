using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record AgentToolReceiptReconciliationAdmission(
    AgentToolSessionAdmission Session,
    AgentToolApprovalBinding Binding,
    AgentToolPreparedPayload Payload);

public sealed record AgentToolReceiptObservation(AgentToolCommittedEffect? CommittedEffect, AgentToolProtocolEnvelope? Receipt) {
    public static AgentToolReceiptObservation NotObserved { get; } = new(null, null);
}

public interface IAgentToolReceiptReconciliationProvider {
    bool Supports(string toolName);
    ValueTask<AgentToolReceiptObservation> ReconcileAsync(AgentToolReceiptReconciliationClaim claim,
        CancellationToken cancellationToken = default);
}

public sealed class AgentToolReceiptAccessDeniedException()
    : InvalidOperationException("Current authorization does not permit reading this cancelled proposal's owner receipt.");

internal enum AgentToolReceiptReconciliationPurpose {
    UncertainEffect,
    CachedReceiptDisclosure
}

public sealed class AgentToolReceiptReconciliationClaim {
    private readonly AgentToolAdmissionJournal owner;

    internal AgentToolReceiptReconciliationClaim(AgentToolAdmissionJournal owner, AgentToolRunLease lease,
        AgentToolApprovalBinding binding, AgentToolReceiptReconciliationPurpose purpose = AgentToolReceiptReconciliationPurpose.UncertainEffect) {
        this.owner = owner;
        Lease = lease;
        Binding = binding;
        Purpose = purpose;
    }

    internal AgentToolRunLease Lease { get; }
    internal AgentToolApprovalBinding Binding { get; }
    internal AgentToolReceiptReconciliationPurpose Purpose { get; }

    public ValueTask<AgentToolReceiptReconciliationAdmission> RequireAsync(CancellationToken cancellationToken = default)
        => owner.RequireReceiptReconciliationAsync(this, cancellationToken);

    internal void RequireOwner(AgentToolAdmissionJournal candidate) {
        Lease.RequireOwner(candidate);
        if (!ReferenceEquals(candidate, owner) || !Lease.ReconciliationOnly || !ReferenceEquals(AgentToolRunLease.Current, Lease)) {
            throw new AgentToolAdmissionException("tool-admission.reconciliation-claim-lost",
                "A live canonical read-only cancellation reconciliation claim is required.");
        }
    }
}
