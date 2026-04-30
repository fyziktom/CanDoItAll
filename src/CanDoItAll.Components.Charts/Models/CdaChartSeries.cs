namespace CanDoItAll.Components.Charts;

public sealed record class CdaChartSeries
{
    public string Name { get; init; } = "Series";

    public CdaChartType? Type { get; init; }

    public IReadOnlyList<CdaChartPoint> Points { get; init; } = [];

    public string? Color { get; init; }

    public bool? ShowDataLabels { get; init; }

    public int? StrokeWidth { get; init; }

    public int StrokeDash { get; init; }

    public bool Hidden { get; init; }

    public Func<CdaChartPoint, string?>? PointColorSelector { get; init; }
}
