namespace CanDoItAll.Components.Charts;

public sealed record class CdaChartPoint
{
    public CdaChartPoint()
    {
    }

    public CdaChartPoint(string category, decimal value, string? color = null, string? note = null)
    {
        Category = category;
        Value = value;
        Color = color;
        Note = note;
    }

    public CdaChartPoint(DateTime timestamp, decimal value, string? color = null, string? note = null)
    {
        Timestamp = timestamp;
        Value = value;
        Color = color;
        Note = note;
    }

    public CdaChartPoint(double numericX, decimal value, string? color = null, string? note = null)
    {
        NumericX = numericX;
        Value = value;
        Color = color;
        Note = note;
    }

    public string? Category { get; init; }

    public DateTime? Timestamp { get; init; }

    public double? NumericX { get; init; }

    public decimal Value { get; init; }

    public string? Color { get; init; }

    public string? Note { get; init; }

    internal object XValue
    {
        get
        {
            if (Timestamp is { } timestamp)
            {
                return timestamp;
            }

            if (NumericX is { } numericX)
            {
                return numericX;
            }

            return string.IsNullOrWhiteSpace(Category) ? string.Empty : Category;
        }
    }
}
