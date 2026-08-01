using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectTaskEstimatePolicyTests
{
    [Fact]
    public void Create_converts_man_days_to_canonical_hours_and_normalizes_currency()
    {
        var estimate = ProjectTaskEstimatePolicy.Create(
            1.5m,
            ProjectWorkItemEffortUnit.ManDays,
            1250.50m,
            " eur ");

        Assert.Equal(12m, estimate.ExpectedEffortHours);
        Assert.Equal(ProjectWorkItemEffortUnit.ManDays, estimate.ExpectedEffortUnit);
        Assert.Equal(1.5m, ProjectTaskEstimatePolicy.ToInputValue(estimate));
        Assert.Equal(1250.50m, estimate.ExpectedCostAmount);
        Assert.Equal("EUR", estimate.ExpectedCostCurrencyCode);
    }

    [Fact]
    public void Create_uses_configurable_hours_per_man_day()
    {
        var estimate = ProjectTaskEstimatePolicy.Create(
            2m,
            ProjectWorkItemEffortUnit.ManDays,
            null,
            null,
            hoursPerManDay: 7.5m);

        Assert.Equal(15m, estimate.ExpectedEffortHours);
        Assert.Equal(2m, ProjectTaskEstimatePolicy.ToInputValue(estimate, 7.5m));
        Assert.Equal(string.Empty, estimate.ExpectedCostCurrencyCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_non_positive_effort(decimal effort)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => ProjectTaskEstimatePolicy.Create(
            effort,
            ProjectWorkItemEffortUnit.Hours,
            null,
            null));

        Assert.Contains("greater than zero", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_rejects_cost_without_valid_currency()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => ProjectTaskEstimatePolicy.Create(
            4m,
            ProjectWorkItemEffortUnit.Hours,
            100m,
            "US"));

        Assert.Contains("three-letter currency", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_rejects_cost_above_supported_aggregate_safe_range()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => ProjectTaskEstimatePolicy.Create(
            4m,
            ProjectWorkItemEffortUnit.Hours,
            ProjectTaskEstimatePolicy.MaximumExpectedCostAmount + 1m,
            "USD"));

        Assert.Contains("supported amount range", exception.Message, StringComparison.Ordinal);
    }
}
