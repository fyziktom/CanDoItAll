namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentToolInvocationPublicationLease : IAsyncDisposable {
    private readonly AgentToolAdmissionJournal owner;
    private readonly AgentToolInvocationClaim claim;
    private IAgentExecutionAdmissionReadLease? publication;

    internal AgentToolInvocationPublicationLease(AgentToolAdmissionJournal owner, AgentToolInvocationClaim claim,
        IAgentExecutionAdmissionReadLease publication) {
        this.owner = owner;
        this.claim = claim;
        this.publication = publication;
    }

    public ValueTask RequireCurrentAsync(CancellationToken cancellationToken = default) {
        var held = Volatile.Read(ref publication);
        ObjectDisposedException.ThrowIf(held is null, this);
        return owner.RequireCurrentClaimIdentityAsync(claim, held.Snapshot, cancellationToken);
    }

    public AgentExecutionPublicationRecovery CaptureRecovery() {
        var held = Volatile.Read(ref publication);
        ObjectDisposedException.ThrowIf(held is null, this);
        return held.Recovery;
    }

    public async ValueTask DisposeAsync() {
        var acquired = Interlocked.Exchange(ref publication, null);
        if (acquired is not null) {
            await acquired.DisposeAsync();
        }
    }
}
