using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Projections;

public sealed record ProcessHistoricalRunCostQuery(
    ProcessDefinitionId DefinitionId,
    string DefinitionKey,
    DateTimeOffset ObservedAtUtc,
    int TakeRuns = 5,
    DateTimeOffset? FromUtc = null);

public sealed record ProcessHistoricalRunCostEstimate(
    ProcessDefinitionId DefinitionId,
    string DefinitionKey,
    int CompletedRunCount,
    int PricedRunCount,
    decimal AverageActualCostUsd,
    IReadOnlyList<ProcessHistoricalRunCostSample> Samples)
{
    public bool HasActualCost => PricedRunCount > 0;

    public static ProcessHistoricalRunCostEstimate Empty(ProcessDefinitionId definitionId, string definitionKey)
    {
        return new ProcessHistoricalRunCostEstimate(
            definitionId,
            definitionKey,
            CompletedRunCount: 0,
            PricedRunCount: 0,
            AverageActualCostUsd: 0m,
            Samples: []);
    }
}

public sealed record ProcessHistoricalRunCostSample(
    ProcessRunId RunId,
    DateTimeOffset CompletedAtUtc,
    int UsageObservationCount,
    decimal ActualCostUsd);

public interface IProcessHistoricalRunCostReader
{
    ValueTask<ProcessHistoricalRunCostEstimate> ReadAsync(
        ProcessHistoricalRunCostQuery query,
        CancellationToken cancellationToken = default);
}
