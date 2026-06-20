using CanDoItAll.AgentFramework.Models;

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
        IReadOnlyList<ProjectStructureProcessStartEstimateAssignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(assignments);

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
