namespace CanDoItAll.Manager;

public sealed record ManagedServiceSnapshot(
    string Key,
    string Name,
    string Health,
    bool IsOk,
    string Summary,
    IReadOnlyList<string> Links,
    string ConfiguredLabel,
    IReadOnlyList<string> ConfiguredTargets,
    string ActiveLabel,
    IReadOnlyList<string> ActiveTargets);

public sealed record WatchStatusViewModel(
    int State,
    string StateName,
    string Summary,
    long LastEventId,
    long LastLogId,
    int? ExpectedWatchIteration,
    int? ConfirmedWatchIteration,
    DateTimeOffset StartedAtUtc,
    IReadOnlyList<string> ActiveUrls);

public sealed record TailwindStatusViewModel(
    int State,
    string StateName,
    string Summary,
    long LastLogId,
    DateTimeOffset StartedAtUtc,
    string WorkspacePath,
    string InputFilePath,
    string OutputFilePath,
    bool OutputExists,
    DateTimeOffset? OutputLastWriteUtc);

public sealed record ManagerStatusResponse(
    string Name,
    string Environment,
    string SessionToken,
    string WorkspaceRoot,
    string WatchProjectPath,
    IReadOnlyList<string> ConfiguredApplicationUrls,
    WatchStatusViewModel Watch,
    TailwindStatusViewModel Tailwind,
    IReadOnlyList<ManagedServiceSnapshot> Services,
    DateTimeOffset TimestampUtc);

public static class ManagerStatusResponseFactory
{
    public static string ResolveWorkspaceRoot(string baseDirectory, ManagerOptions options)
        => Path.GetFullPath(Path.Combine(baseDirectory, options.WorkspaceRoot));

    public static string ResolveWatchProjectPath(string workspaceRoot, ManagerOptions options)
        => Path.GetFullPath(Path.Combine(workspaceRoot, options.WatchProjectPath));

    public static string ResolveTailwindWorkspacePath(string workspaceRoot, ManagerOptions options)
        => Path.GetFullPath(Path.Combine(workspaceRoot, options.TailwindWorkspacePath));

    public static string ResolveTailwindInputPath(string workspaceRoot, ManagerOptions options)
        => Path.GetFullPath(Path.Combine(workspaceRoot, options.TailwindInputPath));

    public static string ResolveTailwindOutputPath(string workspaceRoot, ManagerOptions options)
        => Path.GetFullPath(Path.Combine(workspaceRoot, options.TailwindOutputPath));

    public static IReadOnlyList<string> ResolveConfiguredApplicationUrls(string watchProjectPath, ManagerOptions options)
    {
        var explicitUrls = WorkspaceRuntimeProcessTools.GetExplicitWatchUrls(options);
        if (explicitUrls.Count > 0)
        {
            return explicitUrls;
        }

        return NormalizeTargets(LaunchProfileSettingsResolver.ResolveApplicationUrls(watchProjectPath, options.WatchLaunchProfile));
    }

    public static ManagerStatusResponse Create(
        string environmentName,
        string sessionToken,
        string workspaceRoot,
        string watchProjectPath,
        WatchStatusSnapshot watch,
        TailwindWatchStatusSnapshot tailwind,
        ManagerOptions options,
        string managerBaseUrl,
        DateTimeOffset? timestampUtc = null)
    {
        var configuredApplicationUrls = ResolveConfiguredApplicationUrls(watchProjectPath, options);
        var activeUrls = FilterDisplayTargets(watch.ActiveUrls, configuredApplicationUrls);
        var tailwindWorkspacePath = ResolveTailwindWorkspacePath(workspaceRoot, options);
        var tailwindInputPath = ResolveTailwindInputPath(workspaceRoot, options);
        var tailwindOutputPath = ResolveTailwindOutputPath(workspaceRoot, options);

        return new ManagerStatusResponse(
            "CanDoItAll.Manager",
            environmentName,
            sessionToken,
            workspaceRoot,
            watchProjectPath,
            configuredApplicationUrls,
            new WatchStatusViewModel(
                (int)watch.State,
                watch.State.ToString(),
                watch.Summary,
                watch.LastEventId,
                watch.LastLogId,
                watch.ExpectedWatchIteration,
                watch.ConfirmedWatchIteration,
                watch.StartedAtUtc,
                activeUrls),
            new TailwindStatusViewModel(
                (int)tailwind.State,
                tailwind.State.ToString(),
                tailwind.Summary,
                tailwind.LastLogId,
                tailwind.StartedAtUtc,
                tailwindWorkspacePath,
                tailwindInputPath,
                tailwindOutputPath,
                tailwind.OutputExists,
                tailwind.OutputLastWriteUtc),
            BuildServices(managerBaseUrl, configuredApplicationUrls, activeUrls, watch, tailwind, tailwindInputPath, tailwindOutputPath),
            timestampUtc ?? DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<string> NormalizeTargets(IEnumerable<string> targets)
        => targets
            .Where(target => !string.IsNullOrWhiteSpace(target))
            .Select(target => target.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<string> FilterDisplayTargets(IEnumerable<string> targets, IReadOnlyList<string> configuredApplicationUrls)
    {
        var normalizedTargets = NormalizeTargets(targets);
        if (configuredApplicationUrls.Count == 0)
        {
            return normalizedTargets;
        }

        var configuredHosts = configuredApplicationUrls
            .Select(TryGetSchemeHost)
            .Where(host => !string.IsNullOrWhiteSpace(host))
            .Select(host => host!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return normalizedTargets
            .Where(target => ShouldDisplayTarget(target, configuredHosts))
            .ToArray();
    }

    private static bool ShouldDisplayTarget(string target, ISet<string> configuredHosts)
    {
        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!uri.IsDefaultPort)
        {
            return true;
        }

        var schemeHost = TryGetSchemeHost(target);
        return string.IsNullOrWhiteSpace(schemeHost) || !configuredHosts.Contains(schemeHost);
    }

    private static string? TryGetSchemeHost(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return $"{uri.Scheme}://{uri.Host}";
    }

    private static IReadOnlyList<ManagedServiceSnapshot> BuildServices(
        string managerBaseUrl,
        IReadOnlyList<string> configuredApplicationUrls,
        IReadOnlyList<string> activeUrls,
        WatchStatusSnapshot watch,
        TailwindWatchStatusSnapshot tailwind,
        string tailwindInputPath,
        string tailwindOutputPath)
        =>
        [
            new ManagedServiceSnapshot(
                "manager",
                "CanDoItAll.Manager",
                "Ok",
                true,
                $"Manager is responding on {managerBaseUrl}.",
                [managerBaseUrl],
                "Configured URLs",
                [managerBaseUrl],
                "Active URLs",
                [managerBaseUrl]),
            BuildWatchedApplicationService(configuredApplicationUrls, activeUrls, watch),
            BuildTailwindService(tailwind, tailwindInputPath, tailwindOutputPath)
        ];

    private static ManagedServiceSnapshot BuildWatchedApplicationService(
        IReadOnlyList<string> configuredApplicationUrls,
        IReadOnlyList<string> activeUrls,
        WatchStatusSnapshot watch)
    {
        var missingConfiguredUrls = configuredApplicationUrls
            .Except(activeUrls, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var links = (activeUrls.Count > 0 ? activeUrls : configuredApplicationUrls)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var (health, isOk, summary) = watch.State switch
        {
            WatchState.Ready when configuredApplicationUrls.Count == 0
                => ("Ok", true, "Application is ready. No configured URLs were declared."),
            WatchState.Ready when missingConfiguredUrls.Length == 0
                => ("Ok", true, "Application is ready on the configured launch profile URLs."),
            WatchState.Ready when activeUrls.Count > 0
                => ("Degraded", false, $"Application is running, but these configured URLs are missing: {string.Join(", ", missingConfiguredUrls)}."),
            WatchState.Ready
                => ("Degraded", false, "Application reported ready, but no active listening URLs were captured."),
            WatchState.Building or WatchState.Starting or WatchState.Launching or WatchState.Restarting or WatchState.HotReloadApplied
                => ("Starting", false, watch.Summary),
            WatchState.BuildFailed or WatchState.RuntimeFaulted or WatchState.Stopped
                => ("Error", false, watch.Summary),
            _ => ("Starting", false, watch.Summary)
        };

        return new ManagedServiceSnapshot(
            "web",
            "CanDoItAll.Web",
            health,
            isOk,
            summary,
            links,
            "Configured URLs",
            configuredApplicationUrls,
            "Active URLs",
            activeUrls);
    }

    private static ManagedServiceSnapshot BuildTailwindService(
        TailwindWatchStatusSnapshot tailwind,
        string tailwindInputPath,
        string tailwindOutputPath)
    {
        var activeTargets = tailwind.OutputExists ? [tailwindOutputPath] : Array.Empty<string>();
        var (health, isOk, summary) = tailwind.State switch
        {
            TailwindWatchState.Ready when tailwind.OutputExists
                => ("Ok", true, tailwind.Summary),
            TailwindWatchState.Ready
                => ("Degraded", false, "Tailwind watch is running, but the CSS output file is missing."),
            TailwindWatchState.Starting
                => ("Starting", false, tailwind.Summary),
            TailwindWatchState.Faulted or TailwindWatchState.Stopped
                => ("Error", false, tailwind.Summary),
            _ => ("Starting", false, tailwind.Summary)
        };

        return new ManagedServiceSnapshot(
            "tailwind",
            "Tailwind watch",
            health,
            isOk,
            summary,
            [],
            "Inputs",
            [tailwindInputPath],
            "Outputs",
            activeTargets);
    }
}
