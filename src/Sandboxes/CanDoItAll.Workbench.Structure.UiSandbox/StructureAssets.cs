namespace CanDoItAll.Workbench.Structure.UiSandbox;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<StructureAssetMode>))]
public enum StructureAssetMode { Parity, Fast }

public static class StructureAssets {
#if STRUCTURE_FAST_ASSETS
    public const StructureAssetMode Mode = StructureAssetMode.Fast;
    public const string ThemePath = "css/structure-fast.css";
#else
    public const StructureAssetMode Mode = StructureAssetMode.Parity;
    public const string ThemePath = "css/output.css";
#endif

    public static void ValidateRequestedMode(string? requested) {
        if (requested is null) {
            return;
        }
        if (!Enum.TryParse<StructureAssetMode>(requested, out var mode) || !Enum.IsDefined(mode) || mode != Mode) {
            throw new InvalidOperationException("The requested structure asset mode does not match this build. Use --property:StructureAssetMode=Parity or --property:StructureAssetMode=Fast with the matching launch profile.");
        }
    }
}
