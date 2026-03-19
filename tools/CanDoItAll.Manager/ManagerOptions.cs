namespace CanDoItAll.Manager;

public sealed class ManagerOptions
{
    public string WorkspaceRoot { get; set; } = "..\\..\\..\\..\\..";

    public string WatchProjectPath { get; set; } = "src\\CanDoItAll.Web\\CanDoItAll.Web.csproj";

    public string WatchLaunchProfile { get; set; } = "https";

    public string[] ReadinessUrls { get; set; } = [];

    public int ReadinessTimeoutSeconds { get; set; } = 90;

    public bool AutoStartWatch { get; set; } = true;

    public bool TuningModeEnabled { get; set; }

    public bool ReviewBeforeSend { get; set; } = true;

    public string ArtifactsRoot { get; set; } = ".artifacts\\codex-manager";

    public string CapsuleArtifactsRoot { get; set; } = ".artifacts\\codex-capsules";

    public string TuningCommand { get; set; } = string.Empty;

    public string TuningArguments { get; set; } = "--input \"{requestPath}\"";

    public string? TuningWorkingDirectory { get; set; }

    public int AttachmentSizeLimitBytes { get; set; } = 5 * 1024 * 1024;
}
