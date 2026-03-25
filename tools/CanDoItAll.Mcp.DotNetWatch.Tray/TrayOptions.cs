namespace CanDoItAll.Mcp.DotNetWatch.Tray;

internal sealed record TrayOptions(
    string RepoRoot,
    string SettingsPath,
    string WrapperPath,
    string ShadowManifestPath,
    string LogDirectory,
    TimeSpan PollInterval,
    string? HeadlessCommand)
{
    public static TrayOptions Parse(string[] args)
    {
        string? repoRoot = null;
        string? settingsPath = null;
        string? wrapperPath = null;
        string? shadowManifestPath = null;
        string? headlessCommand = null;
        var pollInterval = TimeSpan.FromSeconds(10);

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--repo-root" when i < args.Length - 1:
                    repoRoot = Path.GetFullPath(args[++i]);
                    break;

                case "--settings-path" when i < args.Length - 1:
                    settingsPath = Path.GetFullPath(args[++i]);
                    break;

                case "--wrapper-path" when i < args.Length - 1:
                    wrapperPath = Path.GetFullPath(args[++i]);
                    break;

                case "--shadow-manifest-path" when i < args.Length - 1:
                    shadowManifestPath = Path.GetFullPath(args[++i]);
                    break;

                case "--poll-interval-seconds" when i < args.Length - 1 && int.TryParse(args[++i], out var seconds) && seconds > 0:
                    pollInterval = TimeSpan.FromSeconds(seconds);
                    break;

                case "--headless-command" when i < args.Length - 1:
                    headlessCommand = args[++i];
                    break;
            }
        }

        repoRoot ??= ResolveRepoRoot();
        settingsPath ??= Path.Combine(repoRoot, "CanDoItAll.Mcp.DotNetWatch.settings.json");
        wrapperPath ??= Path.Combine(repoRoot, "tools", "CanDoItAll.Mcp.DotNetWatch", "Start-CanDoItAllDotNetWatchMcp.ps1");
        shadowManifestPath ??= Path.Combine(repoRoot, ".artifacts", "mcp-server-shadow", "current.json");

        if (!File.Exists(settingsPath))
        {
            throw new InvalidOperationException($"Could not locate settings file '{settingsPath}'.");
        }

        if (!File.Exists(wrapperPath))
        {
            throw new InvalidOperationException($"Could not locate wrapper script '{wrapperPath}'.");
        }

        var logDirectory = Path.Combine(repoRoot, ".mcp-state", "logs");
        Directory.CreateDirectory(logDirectory);

        return new TrayOptions(
            repoRoot,
            settingsPath,
            wrapperPath,
            shadowManifestPath,
            logDirectory,
            pollInterval,
            headlessCommand);
    }

    public string TrayLogPath => Path.Combine(LogDirectory, "mcp-dotnetwatch-tray.log");

    public string WorkspaceLogDirectory => LogDirectory;

    public string BackendCatalogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CanDoItAll.Mcp.DotNetWatch",
        "backend-catalog");

    private static string ResolveRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        var directory = new DirectoryInfo(current);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not infer the CanDoItAll repo root. Pass --repo-root.");
    }
}
