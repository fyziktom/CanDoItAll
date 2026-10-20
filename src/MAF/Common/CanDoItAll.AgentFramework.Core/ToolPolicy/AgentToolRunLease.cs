using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal enum AgentToolRunLeaseMode {
    Dispatch,
    CancelledReceiptReconciliation,
    InterruptedEffectReconciliation,
    SavedEffectDispatch
}

public sealed class AgentToolRunLease : IAsyncDisposable {
    private enum SavedReceiptState { NotAttempted, Attempted, NoReceiptObserved }
    private static readonly AsyncLocal<AgentToolRunLease?> current = new();
    private readonly AgentToolAdmissionJournal owner;
    private readonly IAsyncDisposable handle;
    private AgentToolPinnedRunSnapshot? pinnedSnapshot;
    private AgentSavedEffectAdmission? savedEffect;
    private AgentPlanningLiveWork? planningWork;
    private Guid savedClaimId;
    private int savedReceiptChecked;
    private int savedClaimStarted;
    private int reconciliationStarted;
    private int disposed;

    internal AgentToolRunLease(AgentToolAdmissionJournal owner, AgentToolSessionReference session, Guid id, IAsyncDisposable handle,
        AgentToolRunLeaseMode mode = AgentToolRunLeaseMode.Dispatch, long reconciliationStartedTimestamp = 0) {
        this.owner = owner;
        this.handle = handle;
        Session = session;
        Id = id;
        Mode = mode;
        ReconciliationStartedTimestamp = reconciliationStartedTimestamp;
    }

    public static AgentToolRunLease? Current => current.Value;
    public AgentToolSessionReference Session { get; }
    public Guid Id { get; }
    public AgentPlanningLiveWork? PlanningWork {
        get {
            RequireOwner(owner);
            return planningWork;
        }
    }
    internal AgentToolRunLeaseMode Mode { get; }
    internal long ReconciliationStartedTimestamp { get; }

    internal void AdmitPlanningWork(AgentToolAdmissionJournal candidate, AgentPlanningLiveWork work) {
        RequireOwner(candidate);
        var original = Mode switch {
            AgentToolRunLeaseMode.Dispatch => work.OriginalRun,
            AgentToolRunLeaseMode.SavedEffectDispatch => RequireSavedEffect(candidate).Run,
            _ => throw new AgentToolAdmissionException("tool-admission.lease-lost", "This lease cannot own planning work.")
        };
        if (original.ToolAdmission?.PlanningDecision is null || original.ToolAdmission.ActiveDispatchLeaseId != Id ||
            original.ToolAdmission.Session.Reference != Session || !ReferenceEquals(work.Lease, this) ||
            Interlocked.CompareExchange(ref planningWork, work, null) is not null) {
            throw new AgentToolAdmissionException("tool-admission.lease-lost", "The acknowledged planning interval cannot be replaced.");
        }
    }

    internal void AdmitSavedEffect(AgentToolAdmissionJournal candidate, AgentSavedEffectAdmission admission) {
        RequireOwner(candidate);
        if (Mode != AgentToolRunLeaseMode.SavedEffectDispatch || admission.Run.ToolAdmission?.ActiveDispatchLeaseId != Id ||
            admission.Run.ToolAdmission.Session.Reference != Session ||
            Interlocked.CompareExchange(ref savedEffect, admission, null) is not null) {
            throw new AgentToolAdmissionException("tool-admission.lease-lost", "The selected saved-effect admission cannot be replaced.");
        }
    }

    internal AgentSavedEffectAdmission RequireSavedEffect(AgentToolAdmissionJournal candidate) {
        RequireOwner(candidate);
        return Mode == AgentToolRunLeaseMode.SavedEffectDispatch && savedEffect is { } admission
            ? admission
            : throw new AgentToolAdmissionException("tool-admission.lease-lost", "This lease has no original selected saved effect.");
    }

    internal void BeginSavedReceipt(AgentToolAdmissionJournal candidate) {
        RequireSavedEffect(candidate);
        if (Interlocked.CompareExchange(ref savedReceiptChecked, (int)SavedReceiptState.Attempted,
                (int)SavedReceiptState.NotAttempted) != (int)SavedReceiptState.NotAttempted) {
            throw new AgentToolAdmissionException("tool-admission.claim-lost", "This saved admission already attempted its original receipt preflight.");
        }
    }

    internal void ConfirmSavedReceiptAbsence(AgentToolAdmissionJournal candidate) {
        RequireSavedEffect(candidate);
        if (Interlocked.CompareExchange(ref savedReceiptChecked, (int)SavedReceiptState.NoReceiptObserved,
                (int)SavedReceiptState.Attempted) != (int)SavedReceiptState.Attempted) {
            throw new AgentToolAdmissionException("tool-admission.claim-lost", "No active original receipt preflight can be confirmed.");
        }
    }

    internal void BeginSavedClaim(AgentToolAdmissionJournal candidate) {
        RequireSavedEffect(candidate);
        if (Volatile.Read(ref savedReceiptChecked) != (int)SavedReceiptState.NoReceiptObserved ||
            Interlocked.Exchange(ref savedClaimStarted, 1) != 0) {
            throw new AgentToolAdmissionException("tool-admission.claim-lost", "One saved claim requires its successful original receipt preflight.");
        }
    }

    internal void SetSavedClaim(AgentToolAdmissionJournal candidate, Guid claimId) {
        RequireSavedEffect(candidate);
        if (Volatile.Read(ref savedClaimStarted) == 0 || claimId == Guid.Empty || savedClaimId != Guid.Empty) {
            throw new AgentToolAdmissionException("tool-admission.claim-lost", "The saved dispatch claim cannot be replaced.");
        }
        savedClaimId = claimId;
    }

    internal bool OwnsSavedClaim(AgentToolApprovalBinding binding, Guid claimId)
        => Mode == AgentToolRunLeaseMode.SavedEffectDispatch && savedEffect?.Binding == binding &&
            claimId != Guid.Empty && savedClaimId == claimId;

    internal void Pin(AgentToolAdmissionJournal candidate, ExecutionRunDetail detail) {
        RequireOwner(candidate);
        var journal = detail.Run.ToolAdmission;
        if (Mode != AgentToolRunLeaseMode.InterruptedEffectReconciliation || journal is null || journal.ActiveDispatchLeaseId != Id ||
            journal.Session.Reference != Session) {
            throw new AgentToolAdmissionException("tool-admission.lease-lost", "Only the admitted interrupted lease can pin its committed run snapshot.");
        }

        var chat = detail.ChatSession;
        var snapshot = new AgentToolPinnedRunSnapshot(new(detail.Run with {
            PendingApprovals = detail.Run.PendingApprovals.ToImmutableArray()
        }, chat is null ? null : chat with {
            Messages = chat.Messages.ToImmutableArray(),
            Compatibility = chat.Compatibility is null ? null : chat.Compatibility with {
                PendingApprovals = chat.Compatibility.PendingApprovals.ToImmutableArray()
            }
        }), detail.Approvals.ToImmutableArray(), new(Id, detail.Run.Revision, journal.Revision, detail.Run.CreatedAtUtc));
        if (Interlocked.CompareExchange(ref pinnedSnapshot, snapshot, null) is not null) {
            throw new AgentToolAdmissionException("tool-admission.lease-lost", "An admitted reconciliation snapshot cannot be replaced.");
        }
    }

    internal AgentToolPinnedRunSnapshot RequirePinnedSnapshot(AgentToolAdmissionJournal candidate) {
        RequireOwner(candidate);
        return Mode == AgentToolRunLeaseMode.InterruptedEffectReconciliation && pinnedSnapshot is { } snapshot
            ? snapshot
            : throw new AgentToolAdmissionException("tool-admission.lease-lost", "The live reconciliation lease has no committed admission snapshot.");
    }

    internal void BeginReconciliation(AgentToolAdmissionJournal candidate) {
        RequirePinnedSnapshot(candidate);
        if (Interlocked.Exchange(ref reconciliationStarted, 1) != 0) {
            throw new AgentToolAdmissionException("tool-admission.reconciliation-already-started",
                "An admitted immutable snapshot permits one explicit receipt reconciliation slice.");
        }
    }

    public IDisposable Bind() {
        RequireOwner(owner);
        var previous = current.Value;
        current.Value = this;
        return new Bound(() => {
            if (ReferenceEquals(current.Value, this)) {
                current.Value = previous;
            }
        });
    }

    internal IAgentToolAdmissionVerifier AdmissionVerifier {
        get {
            RequireOwner(owner);
            return owner;
        }
    }

    internal void RequireOwner(AgentToolAdmissionJournal candidate) {
        if (!ReferenceEquals(owner, candidate) || Volatile.Read(ref disposed) != 0) {
            throw new AgentToolAdmissionException("tool-admission.lease-lost", "The runtime does not hold the active run dispatch lease.");
        }
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref disposed, 1) == 0) {
            await handle.DisposeAsync();
        }
    }

    internal sealed class Bound(Action restore) : IDisposable {
        private int disposed;

        public void Dispose() {
            if (Interlocked.Exchange(ref disposed, 1) == 0) {
                restore();
            }
        }
    }
}

internal sealed record AgentToolPinnedRunSnapshot(ExecutionRunAdmissionSnapshot Admission,
    ImmutableArray<ExecutionApprovalRecord> Approvals, AgentToolPinnedReceiptEvidence Evidence);

internal sealed record AgentSavedEffectAdmission(ExecutionRunRecord Run, AgentToolApprovalBinding Binding,
    Func<AgentToolSessionObservation, CancellationToken, ValueTask<bool>> CurrentRead,
    AgentInheritedSavedEffectContinuation? Parent = null);

public sealed class AgentToolInvocationClaim {
    private static readonly AsyncLocal<AgentToolInvocationClaim?> current = new();
    private readonly AgentToolAdmissionJournal owner;

    internal AgentToolInvocationClaim(AgentToolAdmissionJournal owner, AgentToolRunLease runLease,
        AgentToolApprovalBinding binding, AgentToolProposalRecord proposal, Guid id, long journalRevision) {
        this.owner = owner;
        RunLease = runLease;
        Binding = binding;
        Proposal = proposal;
        Id = id;
        JournalRevision = journalRevision;
    }

    public static AgentToolInvocationClaim? Current => current.Value;
    public AgentToolRunLease RunLease { get; }
    public AgentToolApprovalBinding Binding { get; }
    public AgentToolProposalRecord Proposal { get; }
    public Guid Id { get; }
    public long JournalRevision { get; }

    public ValueTask RequireCurrentIdentityAsync(CancellationToken cancellationToken = default)
        => owner.RequireCurrentClaimIdentityAsync(this, null, cancellationToken);

    public ValueTask<AgentToolInvocationPublicationLease> AcquirePublicationAsync(IAgentCatalogReadLease heldCatalog,
        CancellationToken cancellationToken = default)
        => owner.AcquireClaimPublicationAsync(this, heldCatalog, cancellationToken);

    public IDisposable Bind() {
        RequireOwner(owner);
        var previous = current.Value;
        current.Value = this;
        return new AgentToolRunLease.Bound(() => {
            if (ReferenceEquals(current.Value, this)) {
                current.Value = previous;
            }
        });
    }

    internal void RequireOwner(AgentToolAdmissionJournal candidate) {
        RunLease.RequireOwner(candidate);
        if (!ReferenceEquals(owner, candidate) ||
            RunLease.Mode != AgentToolRunLeaseMode.Dispatch && !RunLease.OwnsSavedClaim(Binding, Id) ||
            !ReferenceEquals(AgentToolRunLease.Current, RunLease)) {
            throw new AgentToolAdmissionException("tool-admission.claim-lost", "The current tool claim is not bound to its live runtime lease.");
        }
    }
}
