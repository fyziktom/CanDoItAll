using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureProcessTaskResourceCostStrategy(
    ProcessDefinitionCatalogProjectionService processDefinitionCatalogService,
    IProcessHistoricalRunCostReader processHistoricalRunCostReader,
    TimeProvider timeProvider) : IProjectStructureTaskResourceCostStrategy
{
    private const string Source = "Process run history";

    public ProjectStructureTaskResourceKind Kind => ProjectStructureTaskResourceKind.Process;

    public async Task<ProjectStructureTaskResourceCostQuote> GetQuoteAsync(
        ProjectStructureTaskResourceCostRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var calculatedAtUtc = timeProvider.GetUtcNow();
        var definitionId = new ProcessDefinitionId(request.Resource.ResourceId);
        var definitionKey = processDefinitionCatalogService.ResolveDefinitionKey(definitionId);
        if (string.IsNullOrWhiteSpace(definitionKey))
        {
            return ProjectStructureTaskResourceCostQuote.Unavailable(
                Source,
                "The selected process definition is no longer available.",
                calculatedAtUtc,
                ProjectStructureTaskResourceCostSource.ProcessRunHistory);
        }

        var estimate = await processHistoricalRunCostReader.ReadAsync(
            new ProcessHistoricalRunCostQuery(
                definitionId,
                definitionKey,
                calculatedAtUtc),
            cancellationToken);
        if (!estimate.HasActualCost)
        {
            return ProjectStructureTaskResourceCostQuote.Unavailable(
                Source,
                estimate.CompletedRunCount == 0
                    ? "No completed process run is available for a historical price estimate."
                    : $"{estimate.CompletedRunCount} completed process run(s) were found, but none has resolvable usage pricing.",
                calculatedAtUtc,
                ProjectStructureTaskResourceCostSource.ProcessRunHistory);
        }

        return new ProjectStructureTaskResourceCostQuote(
            ProjectStructureTaskResourceCostQuoteStatus.Available,
            decimal.Round(estimate.AverageActualCostUsd, 2, MidpointRounding.AwayFromZero),
            "USD",
            Source,
            $"Average actual usage cost from {estimate.PricedRunCount} priced run(s) out of {estimate.CompletedRunCount} recent completed run(s).",
            calculatedAtUtc,
            ProjectStructureTaskResourceCostSource.ProcessRunHistory);
    }
}
