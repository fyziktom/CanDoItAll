namespace CanDoItAll.Mcp.DotNetWatch.Configuration;

public sealed class McpServerOptions
{
    public ServerOptions Server { get; set; } = new();

    public DefaultAppOptions DefaultApp { get; set; } = new();

    public HealthOptions Health { get; set; } = new();

    public BuildOptions Build { get; set; } = new();

    public TestOptions Tests { get; set; } = new();

    public LogOptions Logs { get; set; } = new();

    public ProcessOptions Process { get; set; } = new();

    public WaitOptions Waits { get; set; } = new();

    public SecurityOptions Security { get; set; } = new();
}

public sealed class ServerOptions
{
    public string Name { get; set; } = "CanDoItAll.Mcp.DotNetWatch";

    public string WorkspaceRoot { get; set; } = ".";

    public string SolutionPath { get; set; } = "CanDoItAll.slnx";
}

public sealed class DefaultAppOptions
{
    public string ProjectPath { get; set; } = string.Empty;

    public string? WorkingDirectory { get; set; }

    public AppRunMode Mode { get; set; } = AppRunMode.WatchRun;

    public string Configuration { get; set; } = "Debug";

    public string? Framework { get; set; }

    public string? LaunchProfile { get; set; }

    public string[] Arguments { get; set; } = [];

    public string[] Urls { get; set; } = [];

    public Dictionary<string, string> EnvironmentOverlay { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class HealthOptions
{
    public bool Enabled { get; set; } = true;

    public string[] Urls { get; set; } = [];

    public int TimeoutMs { get; set; } = 5_000;

    public int PollIntervalMs { get; set; } = 500;

    public int StableSuccessCount { get; set; } = 1;

    public bool AcceptInsecureLocalhostHttps { get; set; } = true;

    public string[] AllowedHosts { get; set; } = ["localhost", "127.0.0.1", "::1"];
}

public sealed class BuildOptions
{
    public string DefaultTargetPath { get; set; } = "CanDoItAll.slnx";

    public WhenAppRunningPolicy DefaultWhenAppRunning { get; set; } = WhenAppRunningPolicy.StopAndResume;

    public int DefaultTimeoutMs { get; set; } = 30 * 60 * 1_000;

    public string[] ExtraArguments { get; set; } = [];
}

public sealed class TestOptions
{
    public string? DefaultTargetPath { get; set; }

    public WhenAppRunningPolicy DefaultWhenAppRunning { get; set; } = WhenAppRunningPolicy.StopAndResume;

    public int DefaultTimeoutMs { get; set; } = 30 * 60 * 1_000;

    public string RunnerPreference { get; set; } = "Auto";

    public string? DefaultFilter { get; set; }

    public string[] Projects { get; set; } = [];
}

public sealed class LogOptions
{
    public int BufferCapacity { get; set; } = 5_000;

    public bool PersistToFile { get; set; } = true;

    public string Folder { get; set; } = ".mcp-state/logs";

    public int MaxFileSizeMb { get; set; } = 50;

    public bool RedactionEnabled { get; set; } = true;

    public bool IncludeSystemEvents { get; set; } = true;
}

public sealed class ProcessOptions
{
    public int GracefulStopTimeoutMs { get; set; } = 1_000;

    public int ForceKillAfterMs { get; set; } = 5_000;

    public bool CleanupStaleManagedProcessesOnStartup { get; set; } = true;

    public string RegistryPath { get; set; } = ".mcp-state/process-registry.json";

    public bool UsePollingFileWatcher { get; set; }
}

public sealed class WaitOptions
{
    public int DefaultAppWaitTimeoutMs { get; set; } = 120_000;

    public int DefaultOperationWaitTimeoutMs { get; set; } = 30 * 60 * 1_000;

    public int DefaultPollIntervalMs { get; set; } = 500;

    public int DefaultQuietPeriodMs { get; set; } = 2_000;
}

public sealed class SecurityOptions
{
    public string[] AllowedProjectRoots { get; set; } = ["src", "tests", "tools"];

    public bool AllowExternalHealthHosts { get; set; }

    public string[] AllowedEnvironmentKeys { get; set; } =
    [
        "ASPNETCORE_ENVIRONMENT",
        "ASPNETCORE_URLS",
        "DOTNET_ENVIRONMENT",
        "DOTNET_USE_POLLING_FILE_WATCHER",
        "DetailedErrors"
    ];
}
