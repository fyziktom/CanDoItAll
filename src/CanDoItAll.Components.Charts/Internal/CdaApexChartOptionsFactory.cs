using ApexCharts;

namespace CanDoItAll.Components.Charts;

internal static class CdaApexChartOptionsFactory
{
    public static ApexChartOptions<CdaChartPoint> Build(
        CdaChartOptions options,
        IReadOnlyList<CdaChartSeries> series,
        string? title)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(series);

        var palette = ResolvePalette(options, series);
        var noAxis = IsNoAxisChart(options.Type);

        var apexOptions = new ApexChartOptions<CdaChartPoint>
        {
            Chart = new ApexCharts.Chart
            {
                Animations = new Animations { Enabled = options.Animate },
                Toolbar = new Toolbar
                {
                    Show = options.ShowToolbar,
                    Tools = new Tools
                    {
                        Download = true,
                        Selection = true,
                        Zoom = true,
                        Zoomin = true,
                        Zoomout = true,
                        Pan = true,
                        Reset = true
                    }
                },
                Zoom = new Zoom
                {
                    Enabled = options.EnableZoom && !noAxis,
                    Type = AxisType.X,
                    AutoScaleYaxis = true
                },
                DropShadow = new DropShadow { Enabled = false },
                Stacked = options.Stacked
            },
            Colors = palette,
            DataLabels = new DataLabels { Enabled = options.ShowDataLabels },
            Fill = BuildFill(options, series),
            Grid = BuildGrid(options, noAxis),
            Legend = BuildLegend(options),
            PlotOptions = new PlotOptions
            {
                Area = new PlotOptionsArea
                {
                    FillTo = options.FillAreaToOrigin ? AreaFillTo.Origin : AreaFillTo.End
                }
            },
            Stroke = new Stroke
            {
                Curve = new CurveSelections(ResolveCurve(options.Curve)),
                Width = ResolveStrokeWidth(options),
                Colors = palette
            },
            Title = string.IsNullOrWhiteSpace(title) ? null : new Title { Text = title },
            Tooltip = BuildTooltip(options, noAxis),
            Xaxis = noAxis ? null : BuildXAxis(options),
            Yaxis = noAxis ? null : [BuildYAxis(options)]
        };

        return apexOptions;
    }

    public static SeriesType ResolveSeriesType(CdaChartType type)
    {
        return type switch
        {
            CdaChartType.Area => SeriesType.Area,
            CdaChartType.Bar => SeriesType.Bar,
            CdaChartType.Pie => SeriesType.Pie,
            CdaChartType.Donut => SeriesType.Donut,
            _ => SeriesType.Line
        };
    }

    public static XAxisType ResolveXAxisType(CdaChartAxisType type)
    {
        return type switch
        {
            CdaChartAxisType.Category => XAxisType.Category,
            CdaChartAxisType.Numeric => XAxisType.Numeric,
            _ => XAxisType.Datetime
        };
    }

    public static LegendPosition ResolveLegendPosition(CdaChartLegendPosition position)
    {
        return position switch
        {
            CdaChartLegendPosition.Left => LegendPosition.Left,
            CdaChartLegendPosition.Right => LegendPosition.Right,
            CdaChartLegendPosition.Top => LegendPosition.Top,
            _ => LegendPosition.Bottom
        };
    }

    private static Fill BuildFill(CdaChartOptions options, IReadOnlyList<CdaChartSeries> series)
    {
        var firstType = series.Select(item => item.Type ?? options.Type).DefaultIfEmpty(options.Type).First();
        var opacity = firstType == CdaChartType.Bar || IsNoAxisChart(firstType) ? 1 : options.FillOpacity;

        return new Fill
        {
            Opacity = opacity,
            Type = FillType.Solid
        };
    }

    private static ApexCharts.Grid? BuildGrid(CdaChartOptions options, bool noAxis)
    {
        if (noAxis)
        {
            return null;
        }

        return new ApexCharts.Grid
        {
            Row = options.UseAlternatingGridRows
                ? new GridRow
                {
                    Colors = ["#f8fafc", "transparent"],
                    Opacity = 0.65
                }
                : null
        };
    }

    private static Legend BuildLegend(CdaChartOptions options)
    {
        return new Legend
        {
            Show = options.ShowLegend,
            Position = ResolveLegendPosition(options.LegendPosition),
            HorizontalAlign = Align.Left,
            FontSize = "13px",
            Labels = new LegendLabels { Colors = "#0f172a" }
        };
    }

    private static Tooltip BuildTooltip(CdaChartOptions options, bool noAxis)
    {
        return new Tooltip
        {
            Shared = !noAxis,
            Intersect = false,
            Enabled = true,
            X = noAxis ? null : new TooltipX { Format = options.TooltipDateTimeFormat },
            Y = new TooltipY { Formatter = BuildFormatter(options.Unit, options.TooltipPrecision) }
        };
    }

    private static XAxis BuildXAxis(CdaChartOptions options)
    {
        return new XAxis
        {
            Type = ResolveXAxisType(options.XAxisType),
            Labels = new XAxisLabels
            {
                Format = options.XAxisType == CdaChartAxisType.DateTime ? options.DateTimeLabelFormat : null,
                RotateAlways = options.RotateXAxisLabels,
                Rotate = options.RotateXAxisLabels ? -45 : 0,
                Style = new AxisLabelStyle { FontSize = "12px" },
                MinHeight = options.RotateXAxisLabels ? 70 : null
            },
            Title = new AxisTitle
            {
                Text = options.XAxisTitle ?? string.Empty,
                Style = new AxisTitleStyle { FontSize = "13px", FontWeight = "600" }
            }
        };
    }

    private static YAxis BuildYAxis(CdaChartOptions options)
    {
        return new YAxis
        {
            Labels = new YAxisLabels
            {
                Formatter = BuildFormatter(options.Unit, options.ValuePrecision),
                Style = new AxisLabelStyle { FontSize = "12px" }
            },
            Title = new AxisTitle
            {
                Text = options.YAxisTitle ?? (string.IsNullOrWhiteSpace(options.Unit) ? string.Empty : $"Value ({options.Unit})"),
                Style = new AxisTitleStyle { FontSize = "13px", FontWeight = "600" }
            },
            Min = options.YAxisMin is { } min ? (double?)min : null,
            Max = options.YAxisMax is { } max ? (double?)max : null
        };
    }

    private static string BuildFormatter(string? unit, int precision)
    {
        precision = Math.Clamp(precision, 0, 6);

        if (string.IsNullOrWhiteSpace(unit))
        {
            return $"function (val) {{ return Number(val).toFixed({precision}); }}";
        }

        var escapedUnit = unit.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);
        return $"function (val) {{ return Number(val).toFixed({precision}) + ' {escapedUnit}'; }}";
    }

    private static List<string> ResolvePalette(CdaChartOptions options, IReadOnlyList<CdaChartSeries> series)
    {
        var seriesColors = series
            .Select(item => item.Color)
            .Where(color => !string.IsNullOrWhiteSpace(color))
            .Select(color => color!)
            .ToList();

        if (seriesColors.Count > 0)
        {
            return seriesColors;
        }

        if (options.Palette.Count > 0)
        {
            return options.Palette.ToList();
        }

        return CdaChartPalette.Default.ToList();
    }

    private static int ResolveStrokeWidth(CdaChartOptions options)
    {
        return options.Type == CdaChartType.Bar || IsNoAxisChart(options.Type)
            ? 0
            : Math.Max(0, options.StrokeWidth);
    }

    private static Curve ResolveCurve(CdaChartCurve curve)
    {
        return curve switch
        {
            CdaChartCurve.Smooth => Curve.Smooth,
            CdaChartCurve.Step => Curve.Stepline,
            _ => Curve.Straight
        };
    }

    private static bool IsNoAxisChart(CdaChartType type)
    {
        return type is CdaChartType.Pie or CdaChartType.Donut;
    }
}
