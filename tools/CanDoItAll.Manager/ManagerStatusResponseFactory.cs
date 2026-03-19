namespace CanDoItAll.Manager;

public sealed record ManagedServiceSnapshot(
    string Key,
    string Name,
    string Health,
    bool IsOk,
    string Summary,
    IReadOnlyList<string> Links,
    IReadOnlyList<string> ExpectedUrls,
    IReadOnlyList<string> ActiveUrls);

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

public sealed record ManagerStatusResponse(
    string Name,
    string Environment,
    string SessionToken,
    string WorkspaceRoot,
    string WatchProjectPath,
    IReadOnlyList<string> ConfiguredApplicationUrls,
    WatchStatusViewModel Watch,
    IReadOnlyList<ManagedServiceSnapshot> Services,
    DateTimeOffset TimestampUtc);

public static class ManagerStatusResponseFactory
{
    public static string ResolveWorkspaceRoot(string baseDirectory, ManagerOptions options)
        => Path.GetFullPath(Path.Combine(baseDirectory, options.WorkspaceRoot));

    public static string ResolveWatchProjectPath(string workspaceRoot, ManagerOptions options)
        => Path.GetFullPath(Path.Combine(workspaceRoot, options.WatchProjectPath));

    public static IReadOnlyList<string> ResolveConfiguredApplicationUrls(string watchProjectPath, ManagerOptions options)
    {
        var explicitUrls = WorkspaceRuntimeProcessTools.GetExplicitWatchUrls(options);
        if (explicitUrls.Count > 0)
        {
            return explicitUrls;
        }

        return NormalizeUrls(LaunchProfileSettingsResolver.ResolveApplicationUrls(watchProjectPath, options.WatchLaunchProfile));
    }

    public static ManagerStatusResponse Create(
        string environmentName,
        string sessionToken,
        string workspaceRoot,
        string watchProjectPath,
        WatchStatusSnapshot watch,
        ManagerOptions options,
        string managerBaseUrl,
        DateTimeOffset? timestampUtc = null)
    {
        var configuredApplicationUrls = ResolveConfiguredApplicationUrls(watchProjectPath, options);
        var activeUrls = FilterDisplayUrls(watch.ActiveUrls, configuredApplicationUrls);

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
            BuildServices(managerBaseUrl, configuredApplicationUrls, activeUrls, watch),
            timestampUtc ?? DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<string> NormalizeUrls(IEnumerable<string> urls)
        => urls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<string> FilterDisplayUrls(IEnumerable<string> urls, IReadOnlyList<string> configuredApplicationUrls)
    {
        var normalizedUrls = NormalizeUrls(urls);
        if (configuredApplicationUrls.Count == 0)
        {
            return normalizedUrls;
        }

        var configuredHosts = configuredApplicationUrls
            .Select(TryGetSchemeHost)
            .Where(host => !string.IsNullOrWhiteSpace(host))
            .Select(host => host!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return normalizedUrls
            .Where(url => ShouldDisplayUrl(url, configuredHosts))
            .ToArray();
    }

    private static bool ShouldDisplayUrl(string url, ISet<string> configuredHosts)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!uri.IsDefaultPort)
        {
            return true;
        }

        var schemeHost = TryGetSchemeHost(url);
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
        WatchStatusSnapshot watch)
        =>
        [
            new ManagedServiceSnapshot(
                "manager",
                "CanDoItAll.Manager",
                "Ok",
                true,
                $"Manager is responding on {managerBaseUrl}.",
                [managerBaseUrl],
                [managerBaseUrl],
                [managerBaseUrl]),
            BuildWatchedApplicationService(configuredApplicationUrls, activeUrls, watch)
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
            configuredApplicationUrls,
            activeUrls);
    }
}
