using System.ComponentModel.DataAnnotations;

namespace CanDoItAll.Infrastructure.Configuration;

public sealed class DatabaseOptions
{
    public string Provider { get; set; } = "PostgreSql";

    public string? ConnectionString { get; set; }

    public bool EnableEntityFrameworkConsoleLogging { get; set; }
}

public sealed class PostgreSqlStartupReadinessOptions
{
    public const string SectionName = "Database:PostgreSqlStartupReadiness";

    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan MaximumRetryDelay { get; set; } = TimeSpan.FromSeconds(10);
}

public sealed class StorageOptions
{
    public string? WorkspaceRoot { get; set; }

    public string ManagedFilesFolder { get; set; } = "managed-files";

    public string ExportsFolder { get; set; } = "exports";

    public string EvidenceFolder { get; set; } = "evidence";

    public string? ManagerArtifactsFolder { get; set; }
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

    public string? DataProtectionKeysPath { get; set; }

    public string? StateRootPath { get; set; }

    public string? LogsRootPath { get; set; }

    public string? RuntimeTemporaryRootPath { get; set; }

    public string? IpfsApiBaseUrl { get; set; }
}

public enum DataProtectionKeyProtectionProvider
{
    Auto,
    Dpapi,
    Certificate,
    UnprotectedDevelopment
}

public sealed class DataProtectionKeyProtectionOptions
{
    public const string SectionName = "DataProtection:KeyProtection";

    public DataProtectionKeyProtectionProvider Provider { get; set; } = DataProtectionKeyProtectionProvider.Auto;

    public string? CertificatePath { get; set; }

    public string? CertificatePasswordEnvironmentVariable { get; set; }

    public List<string> PreviousCertificatePaths { get; set; } = [];
}
