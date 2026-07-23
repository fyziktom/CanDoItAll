using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructurePersonTaskResourceCostStrategy(
    IProjectPartyCostRateBridge partyCostRateBridge,
    TimeProvider timeProvider) : IProjectStructureTaskResourceCostStrategy
{
    private const string Source = "CRM workforce rate";

    public ProjectStructureTaskResourceKind Kind => ProjectStructureTaskResourceKind.Person;

    public async Task<ProjectStructureTaskResourceCostQuote> GetQuoteAsync(
        ProjectStructureTaskResourceCostRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var calculatedAtUtc = timeProvider.GetUtcNow();
        var costRate = await partyCostRateBridge.GetInternalCostRateAsync(
            request.Resource.ResourceId,
            cancellationToken);
        if (costRate is null)
        {
            return ProjectStructureTaskResourceCostQuote.Unavailable(
                Source,
                "This resource has no internal cost rate in CRM workforce.",
                calculatedAtUtc,
                ProjectStructureTaskResourceCostSource.CrmWorkforceRate);
        }

        if (!request.Estimate.ExpectedEffortHours.HasValue)
        {
            return ProjectStructureTaskResourceCostQuote.Unavailable(
                Source,
                "Enter the task's pure effort before calculating its resource cost.",
                calculatedAtUtc,
                ProjectStructureTaskResourceCostSource.CrmWorkforceRate);
        }

        var quantity = costRate.Unit switch
        {
            ProjectResourceRateUnit.Hour => request.Estimate.ExpectedEffortHours.Value,
            ProjectResourceRateUnit.ManDay => request.Estimate.ExpectedEffortHours.Value /
                ProjectTaskEstimatePolicy.DefaultHoursPerManDay,
            _ => throw new ArgumentOutOfRangeException(
                nameof(costRate),
                costRate.Unit,
                "Unknown workforce rate unit.")
        };
        var amount = decimal.Round(quantity * costRate.Rate, 2, MidpointRounding.AwayFromZero);
        var unitLabel = costRate.Unit == ProjectResourceRateUnit.Hour ? "hour" : "man-day";
        return new ProjectStructureTaskResourceCostQuote(
            ProjectStructureTaskResourceCostQuoteStatus.Available,
            amount,
            costRate.CurrencyCode,
            Source,
            $"Calculated from {quantity:0.##} {unitLabel}(s) at {costRate.CurrencyCode} {costRate.Rate:0.##} per {unitLabel}.",
            calculatedAtUtc,
            ProjectStructureTaskResourceCostSource.CrmWorkforceRate);
    }
}
