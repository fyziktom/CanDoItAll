namespace CanDoItAll.Workbench.Insights.UiSandbox;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<InsightsAssetMode>))]
public enum InsightsAssetMode { Parity, Fast }

public static class InsightsAssets {
#if INSIGHTS_FAST_ASSETS
    public const InsightsAssetMode Mode = InsightsAssetMode.Fast;
    public const string ThemePath = "css/insights-fast.css";
#else
    public const InsightsAssetMode Mode = InsightsAssetMode.Parity;
    public const string ThemePath = "css/output.css";
#endif

    public static void ValidateRequestedMode(string? requested) {
        if (requested is null) {
            return;
        }
        if (!Enum.TryParse<InsightsAssetMode>(requested, out var mode) || !Enum.IsDefined(mode) || mode != Mode) {
            throw new InvalidOperationException("The requested insights asset mode does not match this build. Use --property:InsightsAssetMode=Parity or --property:InsightsAssetMode=Fast with the matching launch profile.");
        }
    }
}
