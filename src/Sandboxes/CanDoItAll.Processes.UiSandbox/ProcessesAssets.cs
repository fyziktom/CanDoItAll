namespace CanDoItAll.Processes.UiSandbox;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ProcessesAssetMode>))]
public enum ProcessesAssetMode { Parity, Fast }

public static class ProcessesAssets {
#if PROCESSES_FAST_ASSETS
    public const ProcessesAssetMode Mode = ProcessesAssetMode.Fast;
    public const string ThemePath = "css/processes-fast.css";
#else
    public const ProcessesAssetMode Mode = ProcessesAssetMode.Parity;
    public const string ThemePath = "css/output.css";
#endif

    public static void ValidateRequestedMode(string? requested) {
        if (requested is null) {
            return;
        }
        if (!Enum.TryParse<ProcessesAssetMode>(requested, out var mode) || !Enum.IsDefined(mode) || mode != Mode) {
            throw new InvalidOperationException("The requested processes asset mode does not match this build. Use --property:ProcessesAssetMode=Parity or --property:ProcessesAssetMode=Fast with the matching launch profile.");
        }
    }
}
