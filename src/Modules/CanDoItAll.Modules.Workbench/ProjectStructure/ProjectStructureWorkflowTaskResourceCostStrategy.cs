using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureWorkflowTaskResourceCostStrategy(
    IWorkflowRunStore workflowRunStore,
    IWorkflowUsageAnalyticsStore workflowUsageAnalyticsStore,
    TimeProvider timeProvider) : IProjectStructureTaskResourceCostStrategy
{
    private const string Source = "Workflow run history";

    public ProjectStructureTaskResourceKind Kind => ProjectStructureTaskResourceKind.Workflow;

    public async Task<ProjectStructureTaskResourceCostQuote> GetQuoteAsync(
        ProjectStructureTaskResourceCostRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var calculatedAtUtc = timeProvider.GetUtcNow();
        WorkflowVersionId? versionId = request.Resource.VersionId.HasValue
            ? new WorkflowVersionId(request.Resource.VersionId.Value)
            : null;
        var runPage = await workflowRunStore.ListRunPageAsync(
            new WorkflowRunPageRequest(
                WorkflowId: new WorkflowId(request.Resource.ResourceId),
                State: WorkflowRunState.Completed,
                PageSize: 5,
                VersionId: versionId,
                IncludeTotalCount: false),
            cancellationToken);
        if (runPage.Items.Count == 0)
        {
            return ProjectStructureTaskResourceCostQuote.Unavailable(
                Source,
                "No completed run is available for the selected workflow version.",
                calculatedAtUtc,
                ProjectStructureTaskResourceCostSource.WorkflowRunHistory);
        }

        var runIds = runPage.Items.Select(static run => run.RunId).ToArray();
        var usageSnapshot = await workflowUsageAnalyticsStore.AggregateAsync(
            new WorkflowUsageAnalyticsStoreQuery(runIds),
            cancellationToken);
        var runUsage = runIds
            .Select(runId => usageSnapshot.Runs.GetValueOrDefault(runId) ?? WorkflowUsageAnalyticsTotals.Empty)
            .ToArray();
        if (runUsage.Any(static usage =>
                usage.PricingUnknownObservationCount > 0 ||
                usage.PricingKnownObservationCount == 0))
        {
            return ProjectStructureTaskResourceCostQuote.Unavailable(
                Source,
                "Recent completed workflow history contains missing or unresolved usage pricing, so a complete estimate cannot be calculated.",
                calculatedAtUtc,
                ProjectStructureTaskResourceCostSource.WorkflowRunHistory);
        }

        var averageCost = decimal.Round(
            runUsage.Sum(static usage => usage.KnownCostUsd) / runUsage.Length,
            2,
            MidpointRounding.AwayFromZero);
        return new ProjectStructureTaskResourceCostQuote(
            ProjectStructureTaskResourceCostQuoteStatus.Available,
            averageCost,
            "USD",
            Source,
            $"Average complete usage cost from {runUsage.Length} recent completed run(s) for the selected workflow version.",
            calculatedAtUtc,
            ProjectStructureTaskResourceCostSource.WorkflowRunHistory);
    }
}
