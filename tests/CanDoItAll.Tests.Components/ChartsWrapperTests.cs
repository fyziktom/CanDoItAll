using ApexCharts;
using CanDoItAll.Components.Charts;

namespace CanDoItAll.Tests.Components;

public sealed class ChartsWrapperTests
{
    [Fact]
    public void Builds_datetime_area_options_with_unit_formatting()
    {
        var chartOptions = new CdaChartOptions
        {
            Type = CdaChartType.Area,
            Unit = "kWh",
            YAxisTitle = "Consumption",
            FillOpacity = 0.42,
            Palette = ["#0f766e"]
        };

        var series = new[]
        {
            new CdaChartSeries
            {
                Name = "Main meter",
                Points = [new CdaChartPoint(new DateTime(2026, 4, 30, 8, 0, 0, DateTimeKind.Utc), 1.25m)]
            }
        };

        var apexOptions = CdaApexChartOptionsFactory.Build(chartOptions, series, "Energy");

        Assert.Equal("Energy", apexOptions.Title.Text);
        Assert.Equal(XAxisType.Datetime, apexOptions.Xaxis.Type);
        Assert.Equal("Consumption", apexOptions.Yaxis[0].Title.Text);
        Assert.Contains("kWh", apexOptions.Yaxis[0].Labels.Formatter, StringComparison.Ordinal);
        Assert.Equal(0.42, apexOptions.Fill.Opacity);
        Assert.Equal(AreaFillTo.Origin, apexOptions.PlotOptions.Area.FillTo);
    }

    [Fact]
    public void Builds_pie_options_without_axes_or_zoom()
    {
        var chartOptions = new CdaChartOptions
        {
            Type = CdaChartType.Pie,
            XAxisType = CdaChartAxisType.Category,
            ShowDataLabels = true
        };

        var series = new[]
        {
            new CdaChartSeries
            {
                Name = "Share",
                Points =
                [
                    new CdaChartPoint("Solar", 35m, "#16a34a"),
                    new CdaChartPoint("Grid", 65m, "#2563eb")
                ]
            }
        };

        var apexOptions = CdaApexChartOptionsFactory.Build(chartOptions, series, "Sources");

        Assert.Null(apexOptions.Xaxis);
        Assert.Null(apexOptions.Yaxis);
        Assert.True(apexOptions.DataLabels.Enabled);
        Assert.False(apexOptions.Chart.Zoom.Enabled);
    }

    [Fact]
    public void Chart_points_resolve_typed_x_values_without_apex_contracts()
    {
        var timestamp = new DateTime(2026, 4, 30, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(timestamp, new CdaChartPoint(timestamp, 10m).XValue);
        Assert.Equal("Battery", new CdaChartPoint("Battery", 20m).XValue);
        Assert.Equal(42d, new CdaChartPoint(42d, 30m).XValue);
    }
}
