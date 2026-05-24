using System.ComponentModel.DataAnnotations;

namespace CanDoItAll.Infrastructure.Configuration;

public sealed class DatabaseOptions
{
    public string Provider { get; set; } = "PostgreSql";

    public string? ConnectionString { get; set; }
}

public sealed class StorageOptions
{
    public string WorkspaceRoot { get; set; } = ".artifacts/workspace";

    public string ManagedFilesFolder { get; set; } = "managed-files";

    public string ExportsFolder { get; set; } = "exports";

    public string EvidenceFolder { get; set; } = "evidence";

    public string ManagerArtifactsFolder { get; set; } = ".artifacts/codex-manager";
}

public sealed class WorkbenchOptions
{
    [Range(1, 200)]
    public int MaxWarmTabs { get; set; } = 8;

    [Range(1, 240)]
    public int SleepAfterMinutes { get; set; } = 20;

    public string BrowserStorageKey { get; set; } = "candoitall.workbench.session";
}

public sealed class DevelopmentManagerOptions
{
    public bool TuningModeEnabled { get; set; }

    public bool ReviewBeforeSend { get; set; } = true;

    public string ManagerBaseUrl { get; set; } = "http://127.0.0.1:6407";
}

public sealed class ControlPlaneOptions
{
    public string? RootPath { get; set; }

    public string? IpfsApiBaseUrl { get; set; }
}
