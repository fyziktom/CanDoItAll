using CanDoItAll.Components.Common;

namespace CanDoItAll.Components.BaseLib;

public enum WorkspacePanelTone
{
    Default,
    Ghost
}

public enum MetricCardSize
{
    Small,
    Medium,
    Large
}

public enum PriceBarTone
{
    Default,
    Success
}

public enum LayoutGap
{
    None,
    XSmall,
    Small,
    Medium,
    Large,
    XLarge,
    XXLarge,
    Tiny
}

public enum LayoutBreakpoint
{
    None,
    Sm,
    Md,
    Lg,
    Xl,
    TwoXl
}

internal static class LayoutValueResolver
{
    public static string ResolveGap(LayoutGap gap)
    {
        return gap switch
        {
            LayoutGap.None => "0rem",
            LayoutGap.Tiny => "0.25rem",
            LayoutGap.XSmall => "0.375rem",
            LayoutGap.Small => "0.5rem",
            LayoutGap.Medium => "0.75rem",
            LayoutGap.Large => "1rem",
            LayoutGap.XLarge => "1.25rem",
            LayoutGap.XXLarge => "1.5rem",
            _ => "1rem"
        };
    }

    public static string ResolveAlignValue(AlignItems alignItems)
    {
        return alignItems switch
        {
            AlignItems.Center => "center",
            AlignItems.End => "flex-end",
            AlignItems.Stretch => "stretch",
            _ => "flex-start"
        };
    }

    public static string ResolveJustifyValue(JustifyContent justifyContent)
    {
        return justifyContent switch
        {
            JustifyContent.Center => "center",
            JustifyContent.End => "flex-end",
            JustifyContent.SpaceBetween => "space-between",
            JustifyContent.SpaceAround => "space-around",
            _ => "flex-start"
        };
    }

    public static string PrefixUtility(LayoutBreakpoint breakpoint, string utility)
    {
        return breakpoint switch
        {
            LayoutBreakpoint.None => utility,
            LayoutBreakpoint.Sm => $"sm:{utility}",
            LayoutBreakpoint.Md => $"md:{utility}",
            LayoutBreakpoint.Lg => $"lg:{utility}",
            LayoutBreakpoint.Xl => $"xl:{utility}",
            LayoutBreakpoint.TwoXl => $"2xl:{utility}",
            _ => utility
        };
    }
}
