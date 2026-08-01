namespace CanDoItAll.AgentFramework.Core;

public sealed record AgentUsageTotals(
    long ObservedTokens,
    decimal KnownCostUsd,
    int UnknownUsageObservationCount,
    DateTimeOffset UpdatedAtUtc);

public interface IAgentUsageTotalsQueryService
{
    Task<AgentUsageTotals> GetTotalsAsync(CancellationToken cancellationToken = default);
}

public sealed class AgentUsageTotalsQueryService(
    ISandboxWorkspaceStore store) : IAgentUsageTotalsQueryService
{
    public async Task<AgentUsageTotals> GetTotalsAsync(CancellationToken cancellationToken = default)
    {
        var projection = await store.LoadUsageProjectionAsync(cancellationToken);
        long observedTokens = 0;
        decimal knownCostUsd = 0m;
        var unknownUsageObservationCount = 0;

        checked
        {
            foreach (var provider in projection.Providers)
            {
                observedTokens += provider.TotalTokens;
                knownCostUsd += provider.KnownCostUsd;
                unknownUsageObservationCount += provider.UnknownUsageObservationCount;
            }
        }

        return new AgentUsageTotals(
            observedTokens,
            knownCostUsd,
            unknownUsageObservationCount,
            projection.UpdatedAtUtc);
    }
}
