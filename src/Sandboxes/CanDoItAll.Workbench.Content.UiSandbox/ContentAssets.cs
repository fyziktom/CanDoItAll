namespace CanDoItAll.Workbench.Content.UiSandbox;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ContentAssetMode>))]
public enum ContentAssetMode { Parity, Fast }

public static class ContentAssets {
#if CONTENT_FAST_ASSETS
    public const ContentAssetMode Mode = ContentAssetMode.Fast;
    public const string ThemePath = "css/content-fast.css";
#else
    public const ContentAssetMode Mode = ContentAssetMode.Parity;
    public const string ThemePath = "css/output.css";
#endif

    public static void ValidateRequestedMode(string? requested) {
        if (requested is null) {
            return;
        }
        if (!Enum.TryParse<ContentAssetMode>(requested, out var mode) || !Enum.IsDefined(mode) || mode != Mode) {
            throw new InvalidOperationException("The requested content asset mode does not match this build. Use --property:ContentAssetMode=Parity or --property:ContentAssetMode=Fast with the matching launch profile.");
        }
    }
}
