using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Modules.Workbench.Pages;

internal sealed record ProjectStructureProcessStartEstimateAssignment(
    Guid? AgentId,
    string AgentModel,
    ProviderProfile? ProviderProfile);

internal static class ProjectStructureProcessStartEstimateCalculator
{
    public const int EstimatedInputTokensPerAssignment = 100_000;
    public const int EstimatedCachedInputTokensPerAssignment = 0;
    public const int EstimatedOutputTokensPerAssignment = 25_000;

    public static ProjectStructureProcessEstimateSummary Calculate(
        string definitionKey,
        int assignmentCount,
        IReadOnlyList<ProjectStructureProcessStartEstimateAssignment> assignments,
        ProcessHistoricalRunCostEstimate? historicalCostEstimate = null)
    {
        ArgumentNullException.ThrowIfNull(assignments);

        if (historicalCostEstimate?.HasActualCost == true)
        {
            return new ProjectStructureProcessEstimateSummary(
                EstimatedCostUsd: decimal.Round(historicalCostEstimate.AverageActualCostUsd, 2, MidpointRounding.AwayFromZero),
                EstimatedElapsedMinutes: Math.Max(15, assignmentCount * 30),
                EstimatedTouchMinutes: Math.Max(10, assignmentCount * 20),
                "Historical",
                "Historical run costs",
                $"{assignmentCount} executable assignment(s) resolved from process template '{definitionKey}'. " +
                $"{historicalCostEstimate.PricedRunCount} completed historical run(s) priced from actual usage; " +
                $"average actual cost is based on {historicalCostEstimate.PricedRunCount} priced run(s) " +
                $"out of {historicalCostEstimate.CompletedRunCount} recent completed run(s).");
        }

        var pricedAssignmentCount = 0;
        var unpricedAssignmentCount = 0;
        var estimatedCostUsd = 0m;

        foreach (var assignment in assignments)
        {
            if (assignment.AgentId is null || assignment.ProviderProfile is null)
            {
                unpricedAssignmentCount++;
                continue;
            }

            var model = FirstNonEmpty(assignment.AgentModel, assignment.ProviderProfile.DefaultModel);
            if (ProviderPricingCalculator.TryCalculate(
                    assignment.ProviderProfile.Name,
                    model,
                    EstimatedInputTokensPerAssignment,
                    EstimatedCachedInputTokensPerAssignment,
                    EstimatedOutputTokensPerAssignment,
                    assignment.ProviderProfile.ModelPrices,
                    out var cost))
            {
                pricedAssignmentCount++;
                estimatedCostUsd += cost.TotalUsd;
                continue;
            }

            unpricedAssignmentCount++;
        }

        var confidence = pricedAssignmentCount == assignmentCount && assignmentCount > 0
            ? "Priced"
            : pricedAssignmentCount > 0
                ? "Partial"
                : "Unpriced";
        var source = pricedAssignmentCount > 0
            ? "Provider price lists"
            : "Provider prices missing";
        var pricingSummary = pricedAssignmentCount == 0
            ? "No selected assignment has provider model pricing yet."
            : unpricedAssignmentCount == 0
                ? $"{pricedAssignmentCount} assignment(s) priced from provider model price lists."
                : $"{pricedAssignmentCount} assignment(s) priced; {unpricedAssignmentCount} assignment(s) missing provider model prices.";
        if (historicalCostEstimate is { CompletedRunCount: > 0, HasActualCost: false })
        {
            pricingSummary += $" {historicalCostEstimate.CompletedRunCount} historical completed run(s) were found, but none had resolvable actual usage cost.";
        }

        return new ProjectStructureProcessEstimateSummary(
            EstimatedCostUsd: decimal.Round(estimatedCostUsd, 2, MidpointRounding.AwayFromZero),
            EstimatedElapsedMinutes: Math.Max(15, assignmentCount * 30),
            EstimatedTouchMinutes: Math.Max(10, assignmentCount * 20),
            confidence,
            source,
            $"{assignmentCount} executable assignment(s) resolved from process template '{definitionKey}'. {pricingSummary}");
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
