using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class ProjectStructureAgentTaskResourceCostStrategy(
    IAiTechnicalAgentBridge technicalAgentBridge,
    HrAgentUsageAnalyticsService usageAnalyticsService,
    TimeProvider timeProvider) : IProjectStructureTaskResourceCostStrategy
{
    private const string Source = "Agent run history";

    public ProjectStructureTaskResourceKind Kind => ProjectStructureTaskResourceKind.Agent;

    public async Task<ProjectStructureTaskResourceCostQuote> GetQuoteAsync(
        ProjectStructureTaskResourceCostRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var calculatedAtUtc = timeProvider.GetUtcNow();
        var partyId = request.Resource.ResourceId;
        var directory = await technicalAgentBridge.GetDirectorySummariesAsync(
            [partyId],
            cancellationToken);
        if (!directory.TryGetValue(partyId, out var summary) ||
            summary.BindingStatus != AiResourceBindingStatus.Bound ||
            !summary.TechnicalAgentId.HasValue ||
            summary.TechnicalAgentId.Value == Guid.Empty ||
            !summary.HasTechnicalProfile)
        {
            return ProjectStructureTaskResourceCostQuote.Unavailable(
                Source,
                "The selected CRM AI resource is not bound to an available technical agent.",
                calculatedAtUtc,
                ProjectStructureTaskResourceCostSource.AgentRunHistory);
        }

        var usage = await usageAnalyticsService.GetAsync(
            new HrAgentUsageInput(summary.TechnicalAgentId.Value, HrAgentUsageScope.All),
            cancellationToken);
        if (usage.RunCount == 0)
        {
            return ProjectStructureTaskResourceCostQuote.Unavailable(
                Source,
                "No completed or attempted agent run is available for a historical price estimate.",
                calculatedAtUtc,
                ProjectStructureTaskResourceCostSource.AgentRunHistory);
        }

        if (!usage.IsComplete)
        {
            return ProjectStructureTaskResourceCostQuote.Unavailable(
                Source,
                $"Agent usage history is incomplete: {usage.CostQualification}",
                calculatedAtUtc,
                ProjectStructureTaskResourceCostSource.AgentRunHistory);
        }

        if (usage.KnownCostObservationCount == 0)
        {
            return ProjectStructureTaskResourceCostQuote.Unavailable(
                Source,
                "Agent runs are available, but none has resolvable provider usage pricing.",
                calculatedAtUtc,
                ProjectStructureTaskResourceCostSource.AgentRunHistory);
        }

        var averageCost = decimal.Round(
            usage.KnownCostUsd / usage.RunCount,
            2,
            MidpointRounding.AwayFromZero);
        return new ProjectStructureTaskResourceCostQuote(
            ProjectStructureTaskResourceCostQuoteStatus.Available,
            averageCost,
            "USD",
            Source,
            $"Average complete provider usage cost from {usage.RunCount} historical agent run(s).",
            calculatedAtUtc,
            ProjectStructureTaskResourceCostSource.AgentRunHistory);
    }
}
