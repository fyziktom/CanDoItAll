using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Modules.Workbench;

public enum ProjectStructureTaskResourceCostQuoteStatus
{
    Available,
    Unavailable
}

public sealed record ProjectStructureTaskResourceCostRequest(
    Guid ProjectId,
    ProjectStructureTaskResourceSelection Resource,
    ProjectTaskEstimate Estimate);

public sealed record ProjectStructureTaskResourceCostQuote(
    ProjectStructureTaskResourceCostQuoteStatus Status,
    decimal? Amount,
    string CurrencyCode,
    string Source,
    string Summary,
    DateTimeOffset CalculatedAtUtc)
{
    public bool IsAvailable => Status == ProjectStructureTaskResourceCostQuoteStatus.Available && Amount.HasValue;

    public static ProjectStructureTaskResourceCostQuote Unavailable(
        string source,
        string summary,
        DateTimeOffset calculatedAtUtc)
        => new(
            ProjectStructureTaskResourceCostQuoteStatus.Unavailable,
            null,
            string.Empty,
            source,
            summary,
            calculatedAtUtc);
}

public sealed class ProjectStructureTaskResourceCostService(
    IProjectPartyCostRateBridge partyCostRateBridge,
    IWorkflowRunStore workflowRunStore,
    IWorkflowUsageAnalyticsStore workflowUsageAnalyticsStore,
    ProcessDefinitionCatalogProjectionService processDefinitionCatalogService,
    IProcessHistoricalRunCostReader processHistoricalRunCostReader,
    TimeProvider timeProvider)
{
    public async Task<ProjectStructureTaskResourceCostQuote> GetQuoteAsync(
        ProjectStructureTaskResourceCostRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ProjectId == Guid.Empty)
        {
            throw new ArgumentException("A project is required to estimate task resource cost.", nameof(request));
        }

        ArgumentNullException.ThrowIfNull(request.Resource);
        var estimate = ProjectTaskEstimatePolicy.ValidateAndNormalize(request.Estimate);
        return request.Resource.Kind switch
        {
            ProjectStructureTaskResourceKind.Person or ProjectStructureTaskResourceKind.Agent =>
                await QuotePartyAsync(request.Resource, estimate, cancellationToken),
            ProjectStructureTaskResourceKind.Workflow =>
                await QuoteWorkflowAsync(request.Resource, cancellationToken),
            ProjectStructureTaskResourceKind.Process =>
                await QuoteProcessAsync(request.Resource, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(
                nameof(request),
                request.Resource.Kind,
                "Unknown task resource kind.")
        };
    }

    private async Task<ProjectStructureTaskResourceCostQuote> QuotePartyAsync(
        ProjectStructureTaskResourceSelection resource,
        ProjectTaskEstimate estimate,
        CancellationToken cancellationToken)
    {
        var calculatedAtUtc = timeProvider.GetUtcNow();
        var costRate = await partyCostRateBridge.GetInternalCostRateAsync(resource.ResourceId, cancellationToken);
        if (costRate is null)
        {
            return ProjectStructureTaskResourceCostQuote.Unavailable(
                "CRM workforce rate",
                "This resource has no internal cost rate. Add one in CRM workforce or enter the task cost manually.",
                calculatedAtUtc);
        }

        if (!estimate.ExpectedEffortHours.HasValue)
        {
            return ProjectStructureTaskResourceCostQuote.Unavailable(
                "CRM workforce rate",
                "Enter the task's pure effort before calculating its resource cost.",
                calculatedAtUtc);
        }

        var quantity = costRate.Unit switch
        {
            ProjectResourceRateUnit.Hour => estimate.ExpectedEffortHours.Value,
            ProjectResourceRateUnit.ManDay => estimate.ExpectedEffortHours.Value / ProjectTaskEstimatePolicy.DefaultHoursPerManDay,
            _ => throw new ArgumentOutOfRangeException(nameof(costRate), costRate.Unit, "Unknown workforce rate unit.")
        };
        var amount = decimal.Round(quantity * costRate.Rate, 2, MidpointRounding.AwayFromZero);
        var unitLabel = costRate.Unit == ProjectResourceRateUnit.Hour ? "hour" : "man-day";
        return new ProjectStructureTaskResourceCostQuote(
            ProjectStructureTaskResourceCostQuoteStatus.Available,
            amount,
            costRate.CurrencyCode,
            "CRM workforce rate",
            $"Calculated from {quantity:0.##} {unitLabel}(s) at {costRate.CurrencyCode} {costRate.Rate:0.##} per {unitLabel}.",
            calculatedAtUtc);
    }

    private async Task<ProjectStructureTaskResourceCostQuote> QuoteWorkflowAsync(
        ProjectStructureTaskResourceSelection resource,
        CancellationToken cancellationToken)
    {
        var calculatedAtUtc = timeProvider.GetUtcNow();
        WorkflowVersionId? versionId = resource.VersionId.HasValue
            ? new WorkflowVersionId(resource.VersionId.Value)
            : null;
        var runPage = await workflowRunStore.ListRunPageAsync(
            new WorkflowRunPageRequest(
                WorkflowId: new WorkflowId(resource.ResourceId),
                State: WorkflowRunState.Completed,
                PageSize: 5,
                VersionId: versionId,
                IncludeTotalCount: false),
            cancellationToken);
        if (runPage.Items.Count == 0)
        {
            return ProjectStructureTaskResourceCostQuote.Unavailable(
                "Workflow run history",
                "No completed run is available for the selected workflow version.",
                calculatedAtUtc);
        }

        var runIds = runPage.Items.Select(static run => run.RunId).ToArray();
        var usageSnapshot = await workflowUsageAnalyticsStore.AggregateAsync(
            new WorkflowUsageAnalyticsStoreQuery(runIds),
            cancellationToken);
        var pricedRuns = runIds
            .Select(runId => usageSnapshot.Runs.GetValueOrDefault(runId) ?? WorkflowUsageAnalyticsTotals.Empty)
            .Where(static usage => usage.PricingKnownObservationCount > 0)
            .ToArray();
        if (pricedRuns.Length == 0)
        {
            return ProjectStructureTaskResourceCostQuote.Unavailable(
                "Workflow run history",
                "No completed workflow run has resolvable usage pricing yet. Enter a manual cost or refresh after a priced run completes.",
                calculatedAtUtc);
        }

        var averageCost = decimal.Round(
            pricedRuns.Sum(static usage => usage.KnownCostUsd) / pricedRuns.Length,
            2,
            MidpointRounding.AwayFromZero);
        return new ProjectStructureTaskResourceCostQuote(
            ProjectStructureTaskResourceCostQuoteStatus.Available,
            averageCost,
            "USD",
            "Workflow run history",
            $"Average known usage cost from {pricedRuns.Length} priced run(s) out of {runPage.Items.Count} recent completed run(s) for the selected workflow version.",
            calculatedAtUtc);
    }

    private async Task<ProjectStructureTaskResourceCostQuote> QuoteProcessAsync(
        ProjectStructureTaskResourceSelection resource,
        CancellationToken cancellationToken)
    {
        var calculatedAtUtc = timeProvider.GetUtcNow();
        var definitionId = new ProcessDefinitionId(resource.ResourceId);
        var definitionKey = processDefinitionCatalogService.ResolveDefinitionKey(definitionId);
        if (string.IsNullOrWhiteSpace(definitionKey))
        {
            return ProjectStructureTaskResourceCostQuote.Unavailable(
                "Process run history",
                "The selected process definition is no longer available.",
                calculatedAtUtc);
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
                "Process run history",
                estimate.CompletedRunCount == 0
                    ? "No completed process run is available for a historical price estimate."
                    : $"{estimate.CompletedRunCount} completed process run(s) were found, but none has resolvable usage pricing.",
                calculatedAtUtc);
        }

        return new ProjectStructureTaskResourceCostQuote(
            ProjectStructureTaskResourceCostQuoteStatus.Available,
            decimal.Round(estimate.AverageActualCostUsd, 2, MidpointRounding.AwayFromZero),
            "USD",
            "Process run history",
            $"Average actual usage cost from {estimate.PricedRunCount} priced run(s) out of {estimate.CompletedRunCount} recent completed run(s).",
            calculatedAtUtc);
    }
}
