namespace CanDoItAll.Mcp.DotNetWatch.Runtime.LaunchSpecs;

public abstract record AppLaunchSpec(
    string LogicalAppId,
    AppLaunchType LaunchType,
    RuntimeLaneKind LaneKind,
    string WorkingDirectory,
    string? ProjectPath,
    string? EntryPath,
    string Configuration,
    string? Framework,
    string? LaunchProfile,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> EnvironmentOverlay,
    IReadOnlyList<string> Urls,
    IReadOnlyList<Uri> HealthUrls)
{
    public AppRunMode CompatibilityMode => LaneKind == RuntimeLaneKind.SourceWatch
        ? AppRunMode.WatchRun
        : AppRunMode.RunOnce;
}

public sealed record ProjectLaunchSpec(
    string LogicalAppId,
    RuntimeLaneKind LaneKind,
    string ProjectPath,
    string WorkingDirectory,
    string Configuration,
    string? Framework,
    string? LaunchProfile,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> EnvironmentOverlay,
    IReadOnlyList<string> Urls,
    IReadOnlyList<Uri> HealthUrls)
    : AppLaunchSpec(
        LogicalAppId,
        AppLaunchType.Project,
        LaneKind,
        WorkingDirectory,
        ProjectPath,
        null,
        Configuration,
        Framework,
        LaunchProfile,
        Arguments,
        EnvironmentOverlay,
        Urls,
        HealthUrls);

public sealed record PublishedDllLaunchSpec(
    string LogicalAppId,
    RuntimeLaneKind LaneKind,
    string ProjectPath,
    string EntryPath,
    string WorkingDirectory,
    string Configuration,
    string? Framework,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> EnvironmentOverlay,
    IReadOnlyList<string> Urls,
    IReadOnlyList<Uri> HealthUrls,
    string? SlotId)
    : AppLaunchSpec(
        LogicalAppId,
        AppLaunchType.PublishedDll,
        LaneKind,
        WorkingDirectory,
        ProjectPath,
        EntryPath,
        Configuration,
        Framework,
        null,
        Arguments,
        EnvironmentOverlay,
        Urls,
        HealthUrls);

public sealed record ExecutableLaunchSpec(
    string LogicalAppId,
    RuntimeLaneKind LaneKind,
    string EntryPath,
    string WorkingDirectory,
    string? ProjectPath,
    string Configuration,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> EnvironmentOverlay,
    IReadOnlyList<string> Urls,
    IReadOnlyList<Uri> HealthUrls)
    : AppLaunchSpec(
        LogicalAppId,
        AppLaunchType.Executable,
        LaneKind,
        WorkingDirectory,
        ProjectPath,
        EntryPath,
        Configuration,
        null,
        null,
        Arguments,
        EnvironmentOverlay,
        Urls,
        HealthUrls);
