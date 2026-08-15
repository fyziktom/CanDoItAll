using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureTaskPricingFeedbackTests
{
    [Fact]
    public void Unavailable_authoritative_quote_surfaces_a_bounded_reason()
    {
        const string outcome =
            " The stale expected cost was removed because the authoritative source could not provide a price.";
        var summary = $"  CRM   rate unavailable {string.Join(' ', Enumerable.Repeat("pending-rate-setup", 30))}  ";
        var quote = ProjectStructureTaskResourceCostQuote.Unavailable(
            "CRM workforce rate",
            summary,
            DateTimeOffset.Parse("2026-07-23T17:00:00Z"),
            ProjectStructureTaskResourceCostSource.CrmWorkforceRate);
        var pricing = new ProjectStructureTaskEstimateRefreshResult(
            ProjectTaskEstimate.Empty(),
            ProjectStructureTaskEstimateRefreshStatus.Cleared,
            ProjectStructureTaskEstimateRefreshReason.AuthoritativeQuoteUnavailable,
            new ProjectStructureTaskResourceSelection(
                ProjectStructureTaskResourceKind.Person,
                Guid.Parse("80000000-0000-0000-0000-000000000008")),
            quote,
            CalculatedCostBasis: null,
            ReplacesCostBasis: true);

        var suffix = ProjectStructureTaskPricingFeedback.BuildNotificationSuffix(pricing);

        Assert.StartsWith(outcome, suffix, StringComparison.Ordinal);
        Assert.Contains("CRM rate unavailable", suffix, StringComparison.Ordinal);
        Assert.EndsWith("...", suffix, StringComparison.Ordinal);
        Assert.InRange(suffix.Length, outcome.Length + 1, outcome.Length + 1 + 240);
    }
}
