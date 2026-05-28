namespace CanDoItAll.Manager;

public sealed class ManagerOptions
{
    public string WorkspaceRoot { get; set; } = "..\\..\\..\\..\\..";

    public string WatchProjectPath { get; set; } = "src\\CanDoItAll.Web\\CanDoItAll.Web.csproj";

    public string TailwindWorkspacePath { get; set; } = "Tailwind";

    public string TailwindInputPath { get; set; } = "Tailwind\\input.css";

    public string TailwindOutputPath { get; set; } = "src\\CanDoItAll.Web\\wwwroot\\css\\output.css";

    public string[] TailwindContentWatchPaths { get; set; } = ["src"];

    public int TailwindWatchDebounceMilliseconds { get; set; } = 150;

    public string WatchLaunchProfile { get; set; } = "https";

    public string[] WatchUrls { get; set; } = [];

    public string[] ReadinessUrls { get; set; } = [];

    public int ReadinessTimeoutSeconds { get; set; } = 90;

    public bool AutoStartWatch { get; set; } = true;

    public bool AutoStartTailwindWatch { get; set; } = true;

    public bool CleanupWorkspaceProcessesOnStart { get; set; } = true;

    public bool WatchDetailedErrorsEnabled { get; set; } = true;

    public bool WatchEchoOutputToConsole { get; set; }

    public bool WatchSkipRestore { get; set; } = true;

    public bool WatchDisableAppHost { get; set; } = true;

    public bool WatchDisableBuildServers { get; set; }

    public bool WatchDisableSharedCompilation { get; set; }

    public bool WatchSuppressBrowserRefresh { get; set; }

    public bool TailwindEchoOutputToConsole { get; set; } = true;

    public bool TailwindInstallDependenciesIfMissing { get; set; } = true;

    public bool TuningModeEnabled { get; set; }

    public bool ReviewBeforeSend { get; set; } = true;

    public string ArtifactsRoot { get; set; } = ".artifacts\\codex-manager";

    public string CapsuleArtifactsRoot { get; set; } = ".artifacts\\codex-capsules";

    public string TuningCommand { get; set; } = string.Empty;

    public string TuningArguments { get; set; } = "--input \"{requestPath}\"";

    public string? TuningWorkingDirectory { get; set; }

    public int AttachmentSizeLimitBytes { get; set; } = 5 * 1024 * 1024;
}
