using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record AgentToolPriorInvocation(AgentToolBatchId BatchId, AgentToolProposalRecord Proposal);

public sealed class AgentToolPriorInvocationObservation {
    internal AgentToolPriorInvocationObservation(AgentToolSessionAdmission session, AgentToolApprovalBinding current,
        long runRevision, long journalRevision, ImmutableArray<AgentToolPriorInvocation> invocations) {
        Session = session;
        Current = current;
        RunRevision = runRevision;
        JournalRevision = journalRevision;
        Invocations = invocations;
    }

    public AgentToolSessionAdmission Session { get; }
    public AgentToolApprovalBinding Current { get; }
    public long RunRevision { get; }
    public long JournalRevision { get; }
    public ImmutableArray<AgentToolPriorInvocation> Invocations { get; }
}

public sealed class AgentToolPriorReceiptObservation {
    internal AgentToolPriorReceiptObservation(AgentToolSessionAdmission session, AgentToolApprovalBinding current,
        long runRevision, long journalRevision, ImmutableArray<AgentToolPriorInvocation> invocations,
        AgentToolPinnedReceiptEvidence? pinnedEvidence) {
        Session = session;
        Current = current;
        RunRevision = runRevision;
        JournalRevision = journalRevision;
        Invocations = invocations;
        PinnedEvidence = pinnedEvidence;
    }

    public AgentToolSessionAdmission Session { get; }
    public AgentToolApprovalBinding Current { get; }
    public long RunRevision { get; }
    public long JournalRevision { get; }
    public ImmutableArray<AgentToolPriorInvocation> Invocations { get; }
    public AgentToolPinnedReceiptEvidence? PinnedEvidence { get; }
}

internal static class AgentToolPriorInvocationEvidence {
    internal static ImmutableArray<AgentToolPriorInvocation> Capture(AgentToolJournalRecord journal,
        AgentToolApprovalBinding current) {
        var prior = ImmutableArray.CreateBuilder<AgentToolPriorInvocation>();
        foreach (var batch in journal.Batches) {
            foreach (var proposal in batch.Proposals) {
                if (batch.Id == current.BatchId && proposal.IntentId == current.IntentId) {
                    if (proposal.Payload.SemanticVersion != current.SemanticVersion || proposal.Payload.Digest != current.Digest) {
                        throw new AgentToolAdmissionException("tool-admission.prior-invocations-mismatch",
                            "The original proposal binding changed before its prior invocation evidence was read.");
                    }
                    return prior.ToImmutable();
                }
                prior.Add(new(batch.Id, proposal));
            }
        }
        throw new AgentToolAdmissionException("tool-admission.prior-invocations-missing",
            "The verified journal does not contain the original proposal position.");
    }

    internal static AgentToolReceiptReconciliationAdmission Receipt(ExecutionRunAdmissionSnapshot snapshot,
        AgentToolApprovalBinding binding, AgentToolPreparedPayload payload, bool includePrior,
        AgentToolPinnedReceiptEvidence? pinnedEvidence = null) {
        var journal = snapshot.Run.ToolAdmission!;
        return new(journal.Session, binding, payload) {
            PinnedEvidence = pinnedEvidence,
            Planning = AgentPlanningDecisionObservation.From(snapshot.Run),
            PriorInvocations = includePrior ? new(journal.Session, binding, snapshot.Run.Revision, journal.Revision,
                Capture(journal, binding), pinnedEvidence) : null
        };
    }
}
