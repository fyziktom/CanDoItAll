using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record AgentToolReceiptReconciliationAdmission(
    AgentToolSessionAdmission Session,
    AgentToolApprovalBinding Binding,
    AgentToolPreparedPayload Payload) {
    [JsonIgnore]
    public AgentToolPinnedReceiptEvidence? PinnedEvidence { get; internal init; }
}

public sealed class AgentToolPinnedReceiptEvidence {
    internal AgentToolPinnedReceiptEvidence(Guid dispatchLeaseId, long runRevision, long journalRevision,
        DateTimeOffset executionCreatedAtUtc) {
        DispatchLeaseId = dispatchLeaseId;
        RunRevision = runRevision;
        JournalRevision = journalRevision;
        ExecutionCreatedAtUtc = executionCreatedAtUtc;
    }

    public Guid DispatchLeaseId { get; }
    public long RunRevision { get; }
    public long JournalRevision { get; }
    public DateTimeOffset ExecutionCreatedAtUtc { get; }
}

public sealed record AgentToolReceiptObservation(AgentToolCommittedEffect? CommittedEffect, AgentToolProtocolEnvelope? Receipt) {
    public static AgentToolReceiptObservation NotObserved { get; } = new(null, null);
}

public interface IAgentToolReceiptReconciliationProvider {
    bool Supports(string toolName);
    ValueTask<AgentToolReceiptObservation> ReconcileAsync(AgentToolReceiptReconciliationClaim claim,
        CancellationToken cancellationToken = default);
}

public sealed class AgentToolReceiptAccessDeniedException()
    : InvalidOperationException("Current authorization does not permit reading this proposal's owner receipt.");

public sealed class AgentToolReceiptReadUnavailableException(Exception innerException)
    : InvalidOperationException("Current authorization for this proposal's owner receipt could not be verified.", innerException);

internal enum AgentToolReceiptReconciliationPurpose {
    UncertainEffect,
    CachedReceiptDisclosure,
    PreparedSavedEffect
}

public sealed class AgentToolReceiptReconciliationClaim {
    private readonly AgentToolAdmissionJournal owner;

    internal AgentToolReceiptReconciliationClaim(AgentToolAdmissionJournal owner, AgentToolRunLease lease,
        AgentToolApprovalBinding binding, AgentToolReceiptReconciliationPurpose purpose = AgentToolReceiptReconciliationPurpose.UncertainEffect,
        Func<AgentToolSessionObservation, CancellationToken, ValueTask<bool>>? currentRead = null,
        AgentToolReceiptReconciliationCursor? position = null) {
        this.owner = owner;
        Lease = lease;
        Binding = binding;
        Purpose = purpose;
        CurrentRead = currentRead;
        Position = position;
    }

    internal AgentToolRunLease Lease { get; }
    internal AgentToolApprovalBinding Binding { get; }
    internal AgentToolReceiptReconciliationPurpose Purpose { get; }
    internal Func<AgentToolSessionObservation, CancellationToken, ValueTask<bool>>? CurrentRead { get; }
    internal AgentToolReceiptReconciliationCursor? Position { get; }

    public ValueTask<AgentToolReceiptReconciliationAdmission> RequireAsync(CancellationToken cancellationToken = default)
        => owner.RequireReceiptReconciliationAsync(this, cancellationToken);

    internal void RequireOwner(AgentToolAdmissionJournal candidate) {
        Lease.RequireOwner(candidate);
        if (!ReferenceEquals(candidate, owner) || Lease.Mode is not (AgentToolRunLeaseMode.CancelledReceiptReconciliation or
                AgentToolRunLeaseMode.InterruptedEffectReconciliation or AgentToolRunLeaseMode.SavedEffectDispatch) ||
            Lease.Mode == AgentToolRunLeaseMode.SavedEffectDispatch && Purpose != AgentToolReceiptReconciliationPurpose.PreparedSavedEffect ||
            !ReferenceEquals(AgentToolRunLease.Current, Lease)) {
            throw new AgentToolAdmissionException("tool-admission.reconciliation-claim-lost",
                "A live canonical read-only receipt reconciliation claim is required.");
        }
    }
}
