using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentProviderUsageProjectionSourceTests
{
    [Fact]
    public async Task Unindexed_usage_does_not_trigger_catalog_initialization_or_an_unbounded_read() {
        var catalog = new StaticCatalogStore(SandboxWorkspaceCatalog.Empty);
        var source = new AgentProviderUsageProjectionSource(new UnindexedEvidence(), catalog,
            NullLogger<AgentProviderUsageProjectionSource>.Instance);
        var result = await source.ReadWindowAsync(new(DateTimeOffset.UtcNow.AddDays(-7), DateTimeOffset.UtcNow));
        Assert.Equal(ProviderUsageSourceState.Indexing, result.State);
        Assert.Equal(0, catalog.Reads);
    }

    private sealed class UnindexedEvidence : IBoundedAgentProviderUsageEvidenceStore {
        public Task<AgentProviderUsageEvidence> LoadProviderUsageEvidenceAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unbounded read.");
        public Task<AgentProviderUsageWindowEvidence> LoadProviderUsageWindowAsync(ProviderUsageWindow window, CancellationToken cancellationToken = default)
            => Task.FromResult(new AgentProviderUsageWindowEvidence(AgentProviderUsageEvidence.Empty, ProviderUsageSourceState.Indexing, null, []));
    }

    [Fact]
    public async Task AmbiguousAgentLegacyEvidenceOnlyAppearsInBoth()
    {
        var agentId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var execution = SandboxWorkspaceExecutionState.Empty with
        {
            ProviderUsageObservations =
            [
                CreateObservation(Guid.NewGuid()) with
                {
                    AgentId = agentId,
                    ExecutionRunId = runId,
                    CalculatedCostUsd = 0.25m,
                    PricingProfileHash = new string('a', ProviderPricingSnapshot.ProfileHashLength),
                    PricingVersion = ProviderPricingSnapshot.Version
                },
                CreateObservation(Guid.NewGuid())
            ]
        };
        var source = new AgentProviderUsageProjectionSource(
            new StaticUsageEvidenceStore(execution),
            new StaticCatalogStore(SandboxWorkspaceCatalog.Empty),
            NullLogger<AgentProviderUsageProjectionSource>.Instance);
        var query = new ProviderUsageQueryService([source]);

        var agents = await query.QueryAsync(ProviderUsageWorkloadSelection.Agents);
        var both = await query.QueryAsync(ProviderUsageWorkloadSelection.Both);

        var agentContribution = Assert.Single((await source.ReadAsync()).Contributions,
            contribution => contribution.WorkloadKind == ProviderUsageWorkloadKind.Agent);
        Assert.Equal(ProviderUsageConsumerKind.Agent, agentContribution.ConsumerKind);
        Assert.Equal(agentId.ToString("D"), agentContribution.ConsumerId);
        Assert.Equal(ProviderUsagePricingCompleteness.CalculatedAtExecution, agentContribution.PricingCompleteness);
        Assert.Equal(1, agents.Totals.UsageObservationCount);
        Assert.Equal(0.25m, agents.Totals.KnownCostUsd);
        Assert.Equal(2, both.Totals.UsageObservationCount);
        Assert.Equal(1, both.Totals.UnpricedObservationCount);
    }

    private static ProviderUsageObservation CreateObservation(Guid id)
    {
        return new(
            id,
            DateTimeOffset.UtcNow,
            "Provider",
            ProviderKind.OpenAi,
            "model",
            ProviderTransportKind.Responses,
            ProviderUsageSourcePhases.AgentRuntime,
            ProviderUsageObservationStatus.Observed,
            InputTokens: 10,
            CachedInputTokens: 2,
            OutputTokens: 4,
            ReasoningTokens: 1,
            TotalTokens: 14,
            ToolCallCount: 0);
    }

    private sealed class StaticUsageEvidenceStore(
        SandboxWorkspaceExecutionState state) : IAgentProviderUsageEvidenceStore
    {
        public Task<AgentProviderUsageEvidence> LoadProviderUsageEvidenceAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(new AgentProviderUsageEvidence(
                state.Version,
                state.ExecutionRuns,
                state.ProviderUsageObservations));
    }

    private sealed class StaticCatalogStore(SandboxWorkspaceCatalog catalog) : ISandboxWorkspaceCatalogStore
    {
        public int Reads { get; private set; }
        public Task<SandboxWorkspaceCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default) {
            Reads++;
            return Task.FromResult(catalog);
        }

        public Task<SandboxWorkspaceCatalogSnapshot> LoadCatalogSnapshotAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(new SandboxWorkspaceCatalogSnapshot(catalog, CatalogDataRevision.Initial));

        public Task<SandboxWorkspaceCatalog> SaveCatalogAsync(
            SandboxWorkspaceCatalog updated,
            CancellationToken cancellationToken = default)
            => Task.FromResult(updated);

        public Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(
            Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
            CancellationToken cancellationToken = default)
            => Task.FromResult(update(catalog));

        public Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(
            Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
            long expectedRevision,
            CancellationToken cancellationToken = default)
            => Task.FromResult(update(catalog));
    }
}
