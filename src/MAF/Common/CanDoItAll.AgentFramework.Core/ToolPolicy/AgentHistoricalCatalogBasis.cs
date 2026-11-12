using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentHistoricalCatalogBasis(
    IAgentHistoricalCatalogReadStore catalog,
    AgentHistoricalReceiptReadFence fence,
    Func<bool> matchesCurrentFence) {
    private readonly IAgentHistoricalCatalogReadStore reader = catalog ?? throw new ArgumentNullException(nameof(catalog));
    private readonly Func<bool> matchesFence = matchesCurrentFence ?? throw new ArgumentNullException(nameof(matchesCurrentFence));
    private AgentHistoricalAgentRead? executor;
    private AgentHistoricalAgentRead? source;
    private bool sealedSelection;
    private int retired;
    private int reading;

    public AgentHistoricalReceiptReadFence Fence { get; } = fence ?? throw new ArgumentNullException(nameof(fence));

    public void RequireCurrent(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        if (Volatile.Read(ref retired) != 0) {
            throw new InvalidOperationException("The historical catalog basis has retired.");
        }
        if (!matchesFence()) {
            throw new AgentHistoricalReceiptContextChangedException();
        }
    }

    internal async Task CaptureExecutorAsync(Guid agentId, CancellationToken cancellationToken) {
        RequireCurrent(cancellationToken);
        if (executor is not null || sealedSelection) {
            throw new InvalidOperationException("The original historical executor is already captured.");
        }
        executor = await CaptureAsync(agentId, cancellationToken);
    }

    public AgentDefinition RequireExecutor(Guid agentId, CancellationToken cancellationToken = default) {
        RequireCurrent(cancellationToken);
        return executor?.Agent is { } selected && selected.Id == agentId
            ? selected : throw new AgentToolReceiptAccessDeniedException();
    }

    public async Task<AgentDefinition> GetOriginalProcessSourceAsync(Guid agentId, CancellationToken cancellationToken = default) {
        RequireCurrent(cancellationToken);
        if (executor?.Agent is not { } captured) {
            throw new InvalidOperationException("The executor must precede the original Process source capture.");
        }
        if (captured.Id == agentId) {
            return captured;
        }
        if (source is { Agent: { } selected }) {
            return selected.Id == agentId ? selected : throw new AgentToolReceiptAccessDeniedException();
        }
        if (sealedSelection) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        var observed = await CaptureAsync(agentId, cancellationToken);
        RequireSameWitness(executor, observed);
        source = observed;
        return observed.Agent!;
    }

    public AgentDefinition RequireSelected(Guid agentId, CancellationToken cancellationToken = default) {
        RequireCurrent(cancellationToken);
        if (!sealedSelection) {
            throw new InvalidOperationException("Historical target disclosure requires a sealed catalog basis.");
        }
        return executor?.Agent is { } selected && selected.Id == agentId ? selected :
            source?.Agent is { } originalSource && originalSource.Id == agentId ? originalSource :
            throw new AgentToolReceiptAccessDeniedException();
    }

    internal void Seal(CancellationToken cancellationToken) {
        RequireCurrent(cancellationToken);
        if (executor is null || sealedSelection || Volatile.Read(ref reading) != 0) {
            throw new InvalidOperationException("The historical catalog selection is not ready to seal.");
        }
        sealedSelection = true;
    }

    internal async Task ObserveFinalAsync(CancellationToken cancellationToken) {
        RequireCurrent(cancellationToken);
        if (!sealedSelection || executor?.Agent is not { } selected) {
            throw new InvalidOperationException("The historical catalog basis was not sealed.");
        }
        var final = await CaptureAsync(selected.Id, cancellationToken, finalObservation: true);
        RequireSameWitness(executor, final);
        RequireCurrent(cancellationToken);
    }

    public void Retire() => Interlocked.Exchange(ref retired, 1);

    private async Task<AgentHistoricalAgentRead> CaptureAsync(Guid agentId, CancellationToken cancellationToken, bool finalObservation = false) {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, Guid.Empty);
        if (Interlocked.CompareExchange(ref reading, 1, 0) != 0) {
            throw new InvalidOperationException("Historical catalog observations cannot overlap within one invocation.");
        }
        try {
            var observed = await reader.ReadHistoricalAgentAsync(agentId, cancellationToken);
            RequireCurrent(cancellationToken);
            if (observed.Status == AgentHistoricalStorageReadStatus.SnapshotChanged ||
                finalObservation && observed.Status == AgentHistoricalStorageReadStatus.Missing) {
                throw new AgentHistoricalCatalogChangedException();
            }
            if (observed.Status == AgentHistoricalStorageReadStatus.Missing) {
                throw new AgentToolReceiptAccessDeniedException();
            }
            if (observed.Status != AgentHistoricalStorageReadStatus.Ready || !observed.Revision.IsAssigned ||
                string.IsNullOrEmpty(observed.FileDigest.Value)) {
                throw new IOException("The historical catalog observation is unavailable.");
            }
            if (observed.Scope != Fence.Scope || observed.Agent is not { } agent || agent.Id != agentId) {
                throw new AgentToolReceiptAccessDeniedException();
            }
            return observed with {
                Agent = agent with {
                    Permissions = agent.Permissions with { AllowedSecrets = agent.Permissions.NormalizedAllowedSecrets.ToImmutableArray() },
                    Capabilities = agent.Capabilities.ToImmutableArray(),
                    Tags = agent.Tags.ToImmutableArray()
                }
            };
        } finally {
            Volatile.Write(ref reading, 0);
        }
    }

    private static void RequireSameWitness(AgentHistoricalAgentRead original, AgentHistoricalAgentRead current) {
        if (original.Scope != current.Scope || original.Revision != current.Revision || original.FileDigest != current.FileDigest) {
            throw new AgentHistoricalCatalogChangedException();
        }
    }
}

public sealed class AgentHistoricalCatalogChangedException : InvalidOperationException {
    public AgentHistoricalCatalogChangedException() : base("The historical catalog witness changed during the read.") { }
}

public sealed record AgentHistoricalReceiptReadParticipants {
    public AgentHistoricalReceiptReadParticipants(IEnumerable<IAgentToolHistoricalBackgroundSourcePolicy> sources,
        IEnumerable<IAgentToolHistoricalReceiptReadProvider> providers) {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(providers);
        Sources = sources.ToImmutableArray();
        Providers = providers.ToImmutableArray();
    }

    public ImmutableArray<IAgentToolHistoricalBackgroundSourcePolicy> Sources { get; }
    public ImmutableArray<IAgentToolHistoricalReceiptReadProvider> Providers { get; }
}
