using CanDoItAll.Components.Charts;

namespace CanDoItAll.Modules.Workbench.Pages.Components.ProjectStructure;

internal static class ProjectManagerSummaryChartOptions
{
    private static readonly CdaChartOptions Day = Create("HH:mm", "Hour");
    private static readonly CdaChartOptions Week = Create("dd MMM", "Day");
    private static readonly CdaChartOptions Month = Create("dd MMM", "Day");
    private static readonly CdaChartOptions Quarter = Create("dd MMM", "Day");
    private static readonly CdaChartOptions Year = Create("MMM", "Month");
    private static readonly CdaChartOptions All = Create("MMM yyyy", "Month");

    public static CdaChartOptions ResolveExpenseOptions(
        ProjectManagerSummaryTimeRange range)
        => range switch
        {
            ProjectManagerSummaryTimeRange.Day => Day,
            ProjectManagerSummaryTimeRange.Week => Week,
            ProjectManagerSummaryTimeRange.Month => Month,
            ProjectManagerSummaryTimeRange.Quarter => Quarter,
            ProjectManagerSummaryTimeRange.Year => Year,
            ProjectManagerSummaryTimeRange.All => All,
            _ => throw new ArgumentOutOfRangeException(
                nameof(range),
                range,
                "The Manager Summary chart range is not supported.")
        };

    private static CdaChartOptions Create(
        string dateTimeLabelFormat,
        string xAxisTitle)
        => new()
        {
            Type = CdaChartType.Line,
            XAxisType = CdaChartAxisType.DateTime,
            Unit = "USD",
            XAxisTitle = xAxisTitle,
            YAxisTitle = "USD",
            DateTimeLabelFormat = dateTimeLabelFormat,
            TooltipDateTimeFormat = "dd MMM yyyy HH:mm",
            ShowToolbar = false,
            EnableZoom = false,
            ShowLegend = true,
            ValuePrecision = 4,
            TooltipPrecision = 4,
            LegendPosition = CdaChartLegendPosition.Bottom,
            Palette = CdaChartPalette.Calm
        };
}
