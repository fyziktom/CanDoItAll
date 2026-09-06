namespace CanDoItAll.AgentFramework.UiSandbox;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<CatalogAssetMode>))]
public enum CatalogAssetMode { Parity, Fast }

public static class CatalogAssets {
#if CATALOG_FAST_ASSETS
    public const CatalogAssetMode Mode = CatalogAssetMode.Fast;
    public const string ThemePath = "css/catalog-fast.css";
#else
    public const CatalogAssetMode Mode = CatalogAssetMode.Parity;
    public const string ThemePath = "css/output.css";
#endif

    public static void ValidateRequestedMode(string? requested) {
        if (requested is null) {
            return;
        }
        if (!Enum.TryParse<CatalogAssetMode>(requested, out var mode) || !Enum.IsDefined(mode) || mode != Mode) {
            throw new InvalidOperationException("The requested catalog asset mode does not match this build. Use --property:CatalogAssetMode=Parity or --property:CatalogAssetMode=Fast with the matching launch profile.");
        }
    }
}
