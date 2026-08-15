using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages.Components.ProjectStructure;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectManagerSummaryChartOptionsTests
{
    [Theory]
    [InlineData(ProjectManagerSummaryTimeRange.Day, "HH:mm", "Hour")]
    [InlineData(ProjectManagerSummaryTimeRange.Week, "dd MMM", "Day")]
    [InlineData(ProjectManagerSummaryTimeRange.Month, "dd MMM", "Day")]
    [InlineData(ProjectManagerSummaryTimeRange.Quarter, "dd MMM", "Day")]
    [InlineData(ProjectManagerSummaryTimeRange.Year, "MMM", "Month")]
    [InlineData(ProjectManagerSummaryTimeRange.All, "MMM yyyy", "Month")]
    public void Expense_axis_uses_the_selected_reporting_scale(
        ProjectManagerSummaryTimeRange range,
        string expectedFormat,
        string expectedTitle)
    {
        var options = ProjectManagerSummaryChartOptions.ResolveExpenseOptions(range);

        Assert.Equal(expectedFormat, options.DateTimeLabelFormat);
        Assert.Equal(expectedTitle, options.XAxisTitle);
    }
}
