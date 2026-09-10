using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentToolRunLease : IAsyncDisposable {
    private static readonly AsyncLocal<AgentToolRunLease?> current = new();
    private readonly AgentToolAdmissionJournal owner;
    private readonly IAsyncDisposable handle;
    private int disposed;

    internal AgentToolRunLease(AgentToolAdmissionJournal owner, AgentToolSessionReference session, Guid id, IAsyncDisposable handle,
        bool reconciliationOnly = false) {
        this.owner = owner;
        this.handle = handle;
        Session = session;
        Id = id;
        ReconciliationOnly = reconciliationOnly;
    }

    public static AgentToolRunLease? Current => current.Value;
    public AgentToolSessionReference Session { get; }
    public Guid Id { get; }
    internal bool ReconciliationOnly { get; }

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
        if (!ReferenceEquals(owner, candidate) || !ReferenceEquals(AgentToolRunLease.Current, RunLease)) {
            throw new AgentToolAdmissionException("tool-admission.claim-lost", "The current tool claim is not bound to its live runtime lease.");
        }
    }
}
