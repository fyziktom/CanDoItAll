namespace CanDoItAll.Components.OverlayLib;

public class OverlayWindowState
{
    public bool IsVisible { get; set; } = true;

    public bool IsMinimized { get; set; }

    public double? Left { get; set; }

    public double? Top { get; set; }

    public double? Width { get; set; }

    public double? Height { get; set; }

    public bool HasCustomGeometry
        => Left.HasValue || Top.HasValue || Width.HasValue || Height.HasValue;

    public OverlayWindowState Clone()
        => new()
        {
            IsVisible = IsVisible,
            IsMinimized = IsMinimized,
            Left = Left,
            Top = Top,
            Width = Width,
            Height = Height
        };

    public static OverlayWindowState Normalize(OverlayWindowState? value)
    {
        var normalized = value?.Clone() ?? new OverlayWindowState();
        normalized.Left = NormalizeDimension(normalized.Left);
        normalized.Top = NormalizeDimension(normalized.Top);
        normalized.Width = NormalizeDimension(normalized.Width);
        normalized.Height = NormalizeDimension(normalized.Height);
        return normalized;
    }

    public static bool AreEquivalent(OverlayWindowState? left, OverlayWindowState? right)
    {
        var normalizedLeft = Normalize(left);
        var normalizedRight = Normalize(right);
        return normalizedLeft.IsVisible == normalizedRight.IsVisible &&
               normalizedLeft.IsMinimized == normalizedRight.IsMinimized &&
               Nullable.Equals(normalizedLeft.Left, normalizedRight.Left) &&
               Nullable.Equals(normalizedLeft.Top, normalizedRight.Top) &&
               Nullable.Equals(normalizedLeft.Width, normalizedRight.Width) &&
               Nullable.Equals(normalizedLeft.Height, normalizedRight.Height);
    }

    private static double? NormalizeDimension(double? value)
        => value.HasValue && value.Value > 0
            ? Math.Round(value.Value, 2, MidpointRounding.AwayFromZero)
            : null;
}
