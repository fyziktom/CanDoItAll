namespace CanDoItAll.Components.Charts;

public static class CdaChartPalette
{
    public static readonly IReadOnlyList<string> Energetic =
    [
        "#0f766e",
        "#2563eb",
        "#dc2626",
        "#d97706",
        "#7c3aed",
        "#0891b2",
        "#16a34a",
        "#db2777"
    ];

    public static readonly IReadOnlyList<string> Calm =
    [
        "#2563eb",
        "#0f766e",
        "#7c3aed",
        "#c2410c",
        "#0891b2",
        "#65a30d"
    ];

    public static IReadOnlyList<string> Default => Energetic;
}
