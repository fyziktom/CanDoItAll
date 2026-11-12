namespace CanDoItAll.Workbench.Execution.UiSandbox;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ExecutionAssetMode>))]
public enum ExecutionAssetMode { Parity, Fast }

public static class ExecutionAssets {
#if EXECUTION_FAST_ASSETS
    public const ExecutionAssetMode Mode = ExecutionAssetMode.Fast;
    public const string ThemePath = "css/execution-fast.css";
#else
    public const ExecutionAssetMode Mode = ExecutionAssetMode.Parity;
    public const string ThemePath = "css/output.css";
#endif

    public static void ValidateRequestedMode(string? requested) {
        if (requested is null) {
            return;
        }
        if (!Enum.TryParse<ExecutionAssetMode>(requested, out var mode) || !Enum.IsDefined(mode) || mode != Mode) {
            throw new InvalidOperationException("The requested execution asset mode does not match this build. Use --property:ExecutionAssetMode=Parity or --property:ExecutionAssetMode=Fast with the matching launch profile.");
        }
    }
}
