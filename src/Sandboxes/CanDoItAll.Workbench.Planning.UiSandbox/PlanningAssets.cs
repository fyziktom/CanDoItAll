namespace CanDoItAll.Workbench.Planning.UiSandbox;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<PlanningAssetMode>))]
public enum PlanningAssetMode { Parity, Fast }

public static class PlanningAssets {
#if PLANNING_FAST_ASSETS
    public const PlanningAssetMode Mode = PlanningAssetMode.Fast;
    public const string ThemePath = "css/planning-fast.css";
#else
    public const PlanningAssetMode Mode = PlanningAssetMode.Parity;
    public const string ThemePath = "css/output.css";
#endif

    public static void ValidateRequestedMode(string? requested) {
        if (requested is null) {
            return;
        }
        if (!Enum.TryParse<PlanningAssetMode>(requested, out var mode) || !Enum.IsDefined(mode) || mode != Mode) {
            throw new InvalidOperationException("The requested planning asset mode does not match this build. Use --property:PlanningAssetMode=Parity or --property:PlanningAssetMode=Fast with the matching launch profile.");
        }
    }
}
