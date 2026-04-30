namespace CanDoItAll.Components.Charts;

public sealed class CdaChartOptions
{
    public CdaChartType Type { get; set; } = CdaChartType.Line;

    public CdaChartAxisType XAxisType { get; set; } = CdaChartAxisType.DateTime;

    public CdaChartCurve Curve { get; set; } = CdaChartCurve.Straight;

    public string? Unit { get; set; }

    public string? XAxisTitle { get; set; }

    public string? YAxisTitle { get; set; }

    public decimal? YAxisMin { get; set; }

    public decimal? YAxisMax { get; set; }

    public int ValuePrecision { get; set; } = 2;

    public int TooltipPrecision { get; set; } = 3;

    public string DateTimeLabelFormat { get; set; } = "HH:mm";

    public string TooltipDateTimeFormat { get; set; } = "dd.MM.yyyy HH:mm";

    public bool RotateXAxisLabels { get; set; } = true;

    public bool ShowToolbar { get; set; } = true;

    public bool EnableZoom { get; set; } = true;

    public bool ShowLegend { get; set; } = true;

    public CdaChartLegendPosition LegendPosition { get; set; } = CdaChartLegendPosition.Bottom;

    public bool ShowDataLabels { get; set; }

    public bool UseAlternatingGridRows { get; set; } = true;

    public bool FillAreaToOrigin { get; set; } = true;

    public double FillOpacity { get; set; } = 0.35;

    public int StrokeWidth { get; set; } = 2;

    public bool Animate { get; set; }

    public bool Stacked { get; set; }

    public IReadOnlyList<string> Palette { get; set; } = CdaChartPalette.Default;
}
