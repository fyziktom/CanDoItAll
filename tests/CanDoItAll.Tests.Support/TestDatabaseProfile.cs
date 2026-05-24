namespace CanDoItAll.Tests.Support;

public sealed class TestDatabaseProfile
{
    public TestDatabaseProfile(
        string profileKey,
        string environmentRootPath,
        string profileRootPath,
        TestDatabaseProviderKind provider,
        string connectionString,
        string workspaceRootPath,
        string managerArtifactsRootPath,
        string? databasePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(managerArtifactsRootPath);

        ProfileKey = profileKey;
        EnvironmentRootPath = environmentRootPath;
        ProfileRootPath = profileRootPath;
        Provider = provider;
        ConnectionString = connectionString;
        WorkspaceRootPath = workspaceRootPath;
        ManagerArtifactsRootPath = managerArtifactsRootPath;
        DatabasePath = databasePath;
    }

    public string ProfileKey { get; }

    public string EnvironmentRootPath { get; }

    public string ProfileRootPath { get; }

    public TestDatabaseProviderKind Provider { get; }

    public string ConnectionString { get; }

    public string WorkspaceRootPath { get; }

    public string ManagerArtifactsRootPath { get; }

    public string? DatabasePath { get; }

    public string ManagedFilesRootPath => Path.Combine(WorkspaceRootPath, "managed-files");

    public string ExportsRootPath => Path.Combine(WorkspaceRootPath, "exports");

    public string EvidenceRootPath => Path.Combine(WorkspaceRootPath, "evidence");

    public IReadOnlyDictionary<string, string?> CreateConfigurationValues(IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Database:Provider"] = Provider switch
            {
                TestDatabaseProviderKind.PostgreSql => "Postgres",
                TestDatabaseProviderKind.InMemory => "InMemory",
                _ => throw new InvalidOperationException($"Unsupported provider '{Provider}'.")
            },
            ["Database:ConnectionString"] = ConnectionString,
            ["Storage:WorkspaceRoot"] = WorkspaceRootPath,
            ["Storage:ManagedFilesFolder"] = "managed-files",
            ["Storage:ExportsFolder"] = "exports",
            ["Storage:EvidenceFolder"] = "evidence",
            ["Storage:ManagerArtifactsFolder"] = ManagerArtifactsRootPath,
            ["Workbench:MaxWarmTabs"] = "3",
            ["Workbench:SleepAfterMinutes"] = "15",
            ["Workbench:BrowserStorageKey"] = "candoitall.workbench.session",
            ["DevelopmentManager:TuningModeEnabled"] = "true",
            ["DevelopmentManager:ReviewBeforeSend"] = "true",
            ["DevelopmentManager:ManagerBaseUrl"] = "http://127.0.0.1:6407"
        };

        if (overrides is not null)
        {
            foreach (var pair in overrides)
            {
                values[pair.Key] = pair.Value;
            }
        }

        return values;
    }

    public IReadOnlyDictionary<string, string> CreateEnvironmentVariables(IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in CreateConfigurationValues(overrides))
        {
            if (pair.Value is null)
            {
                continue;
            }

            variables[pair.Key.Replace(":", "__", StringComparison.Ordinal)] = pair.Value;
        }

        return variables;
    }
}
