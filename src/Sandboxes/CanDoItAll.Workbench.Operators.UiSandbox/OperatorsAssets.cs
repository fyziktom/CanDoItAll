namespace CanDoItAll.Workbench.Operators.UiSandbox;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<OperatorsAssetMode>))]
public enum OperatorsAssetMode { Parity, Fast }

public static class OperatorsAssets {
#if OPERATORS_FAST_ASSETS
    public const OperatorsAssetMode Mode = OperatorsAssetMode.Fast;
    public const string ThemePath = "css/operators-fast.css";
#else
    public const OperatorsAssetMode Mode = OperatorsAssetMode.Parity;
    public const string ThemePath = "css/output.css";
#endif

    public static void ValidateRequestedMode(string? requested) {
        if (requested is null) {
            return;
        }
        if (!Enum.TryParse<OperatorsAssetMode>(requested, out var mode) || !Enum.IsDefined(mode) || mode != Mode) {
            throw new InvalidOperationException("The requested operators asset mode does not match this build. Use --property:OperatorsAssetMode=Parity or --property:OperatorsAssetMode=Fast with the matching launch profile.");
        }
    }
}
