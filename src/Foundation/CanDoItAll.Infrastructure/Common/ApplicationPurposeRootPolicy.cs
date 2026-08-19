using System.Text.Json.Serialization;

namespace CanDoItAll.Infrastructure;

public sealed record ApplicationRootEnvironment(
    HostPlatformFamily PlatformFamily,
    string HomeDirectory,
    string LocalApplicationData,
    string TemporaryRoot,
    IReadOnlyDictionary<string, string?> EnvironmentVariables)
{
    public static ApplicationRootEnvironment CaptureCurrent()
    {
        return new ApplicationRootEnvironment(
            HostPathContext.CaptureCurrent().PlatformFamily,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Path.GetTempPath(),
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["XDG_CONFIG_HOME"] = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"),
                ["XDG_DATA_HOME"] = Environment.GetEnvironmentVariable("XDG_DATA_HOME"),
                ["XDG_STATE_HOME"] = Environment.GetEnvironmentVariable("XDG_STATE_HOME"),
                ["XDG_RUNTIME_DIR"] = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR")
            });
    }
}

public sealed record ApplicationPurposeRoots(
    string WorkspaceRoot,
    string ControlPlaneRoot,
    string DataProtectionKeysRoot,
    string StateRoot,
    string LogsRoot,
    string RuntimeTemporaryRoot);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ApplicationPurposeRootKind
{
    Workspace,
    ControlPlane,
    DatabaseProfiles,
    DataProtectionKeys,
    State,
    Logs,
    RuntimeTemporary
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ApplicationPurposeRootConfigurationSource
{
    PlatformDefault,
    ExplicitConfiguration,
    ActiveDatabaseProfile,
    DerivedFromControlPlaneRoot,
    OwnerResolved
}

public interface IApplicationPurposeRootConfigurationSource
{
    ApplicationPurposeRootConfigurationSource GetConfigurationSource(
        ApplicationPurposeRootKind purpose);
}

public static class ApplicationPurposeRootPolicy
{
    public static ApplicationPurposeRoots Resolve(ApplicationRootEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return environment.PlatformFamily switch
        {
            HostPlatformFamily.Windows => ResolveWindows(environment),
            HostPlatformFamily.Linux => ResolveLinux(environment),
            HostPlatformFamily.MacOS => ResolveMacOS(environment),
            _ => throw new PlatformNotSupportedException("Application roots are not defined for the current host platform.")
        };
    }

    public static ApplicationPurposeRoots ResolveCurrent()
        => Resolve(ApplicationRootEnvironment.CaptureCurrent());

    private static ApplicationPurposeRoots ResolveWindows(ApplicationRootEnvironment environment)
    {
        string baseRoot = RequireAbsolute(environment.LocalApplicationData, environment.PlatformFamily, "Local application data");
        string applicationRoot = Combine(environment.PlatformFamily, baseRoot, "CanDoItAll");
        string controlPlaneRoot = Combine(environment.PlatformFamily, applicationRoot, "control-plane");
        return new ApplicationPurposeRoots(
            Combine(environment.PlatformFamily, applicationRoot, "workspace"),
            controlPlaneRoot,
            Combine(environment.PlatformFamily, controlPlaneRoot, "dataprotection-keys"),
            Combine(environment.PlatformFamily, applicationRoot, "state"),
            Combine(environment.PlatformFamily, applicationRoot, "logs"),
            Combine(
                environment.PlatformFamily,
                RequireAbsolute(environment.TemporaryRoot, environment.PlatformFamily, "Temporary root"),
                "CanDoItAll",
                "runtime"));
    }

    private static ApplicationPurposeRoots ResolveLinux(ApplicationRootEnvironment environment)
    {
        string dataRoot = ResolveVariableOrDefault(
            environment,
            "XDG_DATA_HOME",
            () => Combine(environment.PlatformFamily, ResolveLinuxHome(environment), ".local", "share"));
        string configRoot = ResolveVariableOrDefault(
            environment,
            "XDG_CONFIG_HOME",
            () => Combine(environment.PlatformFamily, ResolveLinuxHome(environment), ".config"));
        string stateBase = ResolveVariableOrDefault(
            environment,
            "XDG_STATE_HOME",
            () => Combine(environment.PlatformFamily, ResolveLinuxHome(environment), ".local", "state"));
        string runtimeRoot = environment.EnvironmentVariables.TryGetValue("XDG_RUNTIME_DIR", out string? configuredRuntimeRoot) &&
                             !string.IsNullOrWhiteSpace(configuredRuntimeRoot)
            ? Combine(
                environment.PlatformFamily,
                RequireAbsolute(configuredRuntimeRoot, environment.PlatformFamily, "XDG_RUNTIME_DIR"),
                "candoitall")
            : Combine(
                environment.PlatformFamily,
                RequireAbsolute(environment.TemporaryRoot, environment.PlatformFamily, "Temporary root"),
                "candoitall-runtime");
        string applicationDataRoot = Combine(environment.PlatformFamily, dataRoot, "candoitall");
        string controlPlaneRoot = Combine(environment.PlatformFamily, configRoot, "candoitall", "control-plane");
        string stateRoot = Combine(environment.PlatformFamily, stateBase, "candoitall");
        return new ApplicationPurposeRoots(
            Combine(environment.PlatformFamily, applicationDataRoot, "workspace"),
            controlPlaneRoot,
            Combine(environment.PlatformFamily, applicationDataRoot, "dataprotection-keys"),
            stateRoot,
            Combine(environment.PlatformFamily, stateRoot, "logs"),
            runtimeRoot);
    }

    private static ApplicationPurposeRoots ResolveMacOS(ApplicationRootEnvironment environment)
    {
        string home = RequireAbsolute(environment.HomeDirectory, environment.PlatformFamily, "Home directory");
        string applicationRoot = RequireAbsolute(environment.LocalApplicationData, environment.PlatformFamily, "Application Support root");
        applicationRoot = Combine(environment.PlatformFamily, applicationRoot, "CanDoItAll");
        string controlPlaneRoot = Combine(environment.PlatformFamily, applicationRoot, "control-plane");
        string temporaryRoot = ResolveMacOSTemporaryRoot(environment);
        return new ApplicationPurposeRoots(
            Combine(environment.PlatformFamily, applicationRoot, "workspace"),
            controlPlaneRoot,
            Combine(environment.PlatformFamily, applicationRoot, "dataprotection-keys"),
            Combine(environment.PlatformFamily, applicationRoot, "state"),
            Combine(environment.PlatformFamily, home, "Library", "Logs", "CanDoItAll"),
            Combine(
                environment.PlatformFamily,
                temporaryRoot,
                "CanDoItAll",
                "runtime"));
    }

    private static string ResolveMacOSTemporaryRoot(ApplicationRootEnvironment environment)
    {
        string temporaryRoot = RequireAbsolute(
            environment.TemporaryRoot,
            environment.PlatformFamily,
            "Temporary root");
        // macOS exposes these temporary roots through system symlinks, while managed roots reject link traversal.
        return temporaryRoot switch
        {
            "/var" or "/tmp" => $"/private{temporaryRoot}",
            _ when temporaryRoot.StartsWith("/var/", StringComparison.Ordinal) ||
                temporaryRoot.StartsWith("/tmp/", StringComparison.Ordinal) => $"/private{temporaryRoot}",
            _ => temporaryRoot
        };
    }

    private static string ResolveVariableOrDefault(
        ApplicationRootEnvironment environment,
        string variableName,
        Func<string> defaultValueFactory)
    {
        return environment.EnvironmentVariables.TryGetValue(variableName, out string? configuredValue) &&
               !string.IsNullOrWhiteSpace(configuredValue)
            ? RequireAbsolute(configuredValue, environment.PlatformFamily, variableName)
            : defaultValueFactory();
    }

    private static string ResolveLinuxHome(ApplicationRootEnvironment environment)
        => RequireAbsolute(environment.HomeDirectory, environment.PlatformFamily, "Home directory");

    private static string RequireAbsolute(string value, HostPlatformFamily platform, string description)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{description} is unavailable. Configure an explicit application root for this host profile.");
        }

        string trimmed = value.Trim().TrimEnd('/', '\\');
        SharedKernel.PhysicalPathSyntax syntax = SharedKernel.PhysicalPathSyntaxClassifier.Classify(trimmed);
        bool valid = platform == HostPlatformFamily.Windows
            ? syntax is SharedKernel.PhysicalPathSyntax.WindowsDriveAbsolute or
                SharedKernel.PhysicalPathSyntax.WindowsUnc
            : syntax == SharedKernel.PhysicalPathSyntax.UnixAbsolute;
        if (!valid)
        {
            throw new InvalidOperationException($"{description} is not an absolute path for {platform}.");
        }

        return trimmed;
    }

    private static string Combine(HostPlatformFamily platform, string root, params string[] segments)
    {
        char separator = platform == HostPlatformFamily.Windows ? '\\' : '/';
        return segments.Aggregate(
            root.TrimEnd('/', '\\'),
            (current, segment) => $"{current}{separator}{segment.Trim('/', '\\')}");
    }
}
