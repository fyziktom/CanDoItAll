using System.Collections;

namespace CanDoItAll.AgentFramework.Core;

/// <summary>The toolchain whose settings a child process inherits, chosen from what actually runs.</summary>
internal enum WorkspaceProcessToolchain
{
    None,
    Dotnet,
    Python,
    PowerShell,
    Node,
    Docker,
    Git
}

public sealed class WorkspaceCommandEnvironmentPolicy
{
    private static readonly string[] CommonInheritedEnvironmentNames =
    [
        "HOME",
        "LANG",
        "LC_ALL",
        "LC_CTYPE",
        "PATH",
        "TEMP",
        "TMP",
        "TMPDIR",
        "TZ"
    ];

    private static readonly string[] WindowsInheritedEnvironmentNames =
    {
        "APPDATA",
        "COMPUTERNAME",
        "COMSPEC",
        "CommonProgramFiles",
        "CommonProgramFiles(x86)",
        "CommonProgramW6432",
        "LOCALAPPDATA",
        "NUMBER_OF_PROCESSORS",
        "OS",
        "PATHEXT",
        "PROCESSOR_ARCHITECTURE",
        "PROCESSOR_IDENTIFIER",
        "ProgramData",
        "ProgramFiles",
        "ProgramFiles(x86)",
        "ProgramW6432",
        "PSModulePath",
        "SYSTEMROOT",
        "SystemDrive",
        "SystemRoot",
        "USERDOMAIN",
        "USERNAME",
        "USERPROFILE",
        "WINDIR"
    };

    private static readonly string[] UnixInheritedEnvironmentNames =
    {
        "LOGNAME",
        "SHELL",
        "USER"
    };

    private static readonly string[] DotnetInheritedEnvironmentNames =
    {
        "DOTNET_CLI_HOME",
        "DOTNET_CLI_TELEMETRY_OPTOUT",
        "DOTNET_CLI_UI_LANGUAGE",
        "DOTNET_NOLOGO",
        "DOTNET_ROOT",
        "DOTNET_ROOT_ARM64",
        "DOTNET_ROOT_X64",
        "DOTNET_ROOT_X86",
        "DOTNET_SKIP_FIRST_TIME_EXPERIENCE",
        "DOTNET_USE_POLLING_FILE_WATCHER",
        "NUGET_CERT_REVOCATION_MODE",
        "NUGET_HTTP_CACHE_PATH",
        "NUGET_PACKAGES",
        "NUGET_PLUGINS_CACHE_PATH",
        "NUGET_XMLDOC_MODE"
    };

    private static readonly string[] PythonInheritedEnvironmentNames =
    {
        "PIP_CACHE_DIR",
        "PIP_DISABLE_PIP_VERSION_CHECK",
        "PIP_NO_INPUT",
        "PYTHONDONTWRITEBYTECODE",
        "PYTHONIOENCODING",
        "PYTHONUNBUFFERED",
        "PYTHONUTF8",
        "VIRTUAL_ENV"
    };

    private static readonly string[] PowerShellInheritedEnvironmentNames =
    {
        "PSModulePath",
        "POWERSHELL_TELEMETRY_OPTOUT",
        "POWERSHELL_UPDATECHECK"
    };

    private static readonly string[] NodeInheritedEnvironmentNames =
    {
        "npm_config_cache",
        "NPM_CONFIG_CACHE"
    };

    private static readonly string[] DockerInheritedEnvironmentNames =
    {
        "DOCKER_API_VERSION",
        "DOCKER_CERT_PATH",
        "DOCKER_CONFIG",
        "DOCKER_CONTEXT",
        "DOCKER_HOST",
        "DOCKER_TLS_VERIFY",
        "SSH_AUTH_SOCK"
    };

    // An empty value for these is worse than none: an empty TEMP makes Windows fall back to its own
    // folder, and an empty HOME breaks the dotnet CLI and NuGet on Linux and macOS.
    private static readonly string[] NeverEmptyEnvironmentNames =
    [
        "HOME",
        "PATH",
        "TEMP",
        "TMP",
        "TMPDIR",
        "USERPROFILE"
    ];

    private readonly LocalHostPlatform platform;
    private readonly StringComparer environmentNameComparer;
    private readonly HashSet<string> commonInheritedEnvironmentNames;
    private readonly IReadOnlyDictionary<string, string?>? currentEnvironmentOverride;
    private readonly WorkspaceProcessEnvironmentSettings? settingsOverride;

    public WorkspaceCommandEnvironmentPolicy()
        : this(LocalHostPlatformExtensions.CaptureCurrent())
    {
    }

    internal WorkspaceCommandEnvironmentPolicy(LocalHostPlatform platform)
        : this(platform, currentEnvironmentOverride: null)
    {
    }

    internal WorkspaceCommandEnvironmentPolicy(
        LocalHostPlatform platform,
        IReadOnlyDictionary<string, string?>? currentEnvironmentOverride,
        WorkspaceProcessEnvironmentSettings? settings = null)
    {
        this.platform = platform;
        environmentNameComparer = platform.EnvironmentNameComparer();
        this.currentEnvironmentOverride = currentEnvironmentOverride is null
            ? null
            : new Dictionary<string, string?>(currentEnvironmentOverride, environmentNameComparer);
        settingsOverride = settings;
        commonInheritedEnvironmentNames = new HashSet<string>(CommonInheritedEnvironmentNames, environmentNameComparer);
        commonInheritedEnvironmentNames.UnionWith(
            platform == LocalHostPlatform.Windows
                ? WindowsInheritedEnvironmentNames
                : UnixInheritedEnvironmentNames);
    }

    public StringComparer EnvironmentNameComparer => environmentNameComparer;

    private WorkspaceProcessEnvironmentSettings Settings => settingsOverride ?? WorkspaceProcessEnvironmentSettings.Current;

    public IReadOnlyDictionary<string, string?> BuildEnvironmentVariables()
        => BuildEnvironmentVariables(ReadCurrentEnvironment(), toolName: null);

    internal IReadOnlyDictionary<string, string?> BuildEnvironmentVariables(
        IReadOnlyDictionary<string, string?> source,
        string? toolName = null,
        string? executable = null,
        bool extendedEnvironmentAllowed = true)
    {
        ArgumentNullException.ThrowIfNull(source);
        var inheritedEnvironmentNames = ResolveInheritedNames(toolName, executable, extendedEnvironmentAllowed);
        var environment = new Dictionary<string, string?>(environmentNameComparer);
        foreach (var pair in source)
        {
            if (!inheritedEnvironmentNames.Contains(pair.Key) ||
                (IsNeverEmpty(pair.Key) && string.IsNullOrWhiteSpace(pair.Value)))
            {
                continue;
            }

            environment[pair.Key] = pair.Value;
        }

        return environment;
    }

    /// <summary>
    /// Composes a child process environment: the inherited allowlist, the settings of the toolchain
    /// that actually runs (from <paramref name="executable"/>, otherwise from <paramref name="toolName"/>),
    /// the operator's network-trust and extra names when <paramref name="extendedEnvironmentAllowed"/>,
    /// then the caller's explicit values. A null explicit value unsets the variable; an empty value is
    /// never set for TEMP, TMP, TMPDIR, HOME, USERPROFILE or PATH. On Linux and macOS a missing HOME is
    /// replaced by the configured toolchain home.
    /// </summary>
    public IReadOnlyDictionary<string, string?> MergeEnvironmentVariables(
        IReadOnlyDictionary<string, string?>? environmentVariables,
        string? toolName = null,
        string? executable = null,
        bool extendedEnvironmentAllowed = true)
    {
        var merged = new Dictionary<string, string?>(
            BuildEnvironmentVariables(
                currentEnvironmentOverride ?? ReadCurrentEnvironment(),
                toolName,
                executable,
                extendedEnvironmentAllowed),
            environmentNameComparer);
        if (environmentVariables is not null)
        {
            foreach (var pair in environmentVariables)
            {
                if (pair.Value is null)
                {
                    merged.Remove(pair.Key);
                    continue;
                }

                if (IsNeverEmpty(pair.Key) && string.IsNullOrWhiteSpace(pair.Value))
                {
                    continue;
                }

                merged[pair.Key] = pair.Value;
            }
        }

        ApplyUnixHomeGuarantee(merged);
        return merged;
    }

    /// <summary>
    /// Names (never values) of the host's proxy and certificate variables that are withheld because
    /// network trust is off. Empty when network trust is on or the host has none.
    /// </summary>
    public IReadOnlyList<string> DescribeWithheldNetworkTrustNames()
    {
        if (Settings.NetworkTrust)
        {
            return [];
        }

        var current = currentEnvironmentOverride ?? ReadCurrentEnvironment();
        return WorkspaceProcessEnvironmentSettings.NetworkTrustNames
            .Where(name => current.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
            .Distinct(environmentNameComparer)
            .ToArray();
    }

    /// <summary>
    /// True when <paramref name="name"/> reaches a child process of this toolchain without being set
    /// explicitly: the inherited allowlist and the toolchain's own settings. Operator extensions are
    /// not included, so a script that reads them needs the agent's environment permission.
    /// </summary>
    internal bool IsBaseInheritedName(string name, string? toolName, string? executable = null)
        => ResolveInheritedNames(toolName, executable, extendedEnvironmentAllowed: false).Contains(name);

    internal static WorkspaceProcessToolchain ResolveToolchain(string? toolName, string? executable)
    {
        var fileName = string.IsNullOrWhiteSpace(executable)
            ? string.Empty
            : WorkspaceLaunchExplanations.ProgramFileName(executable).ToLowerInvariant();
        var name = fileName.EndsWith(".exe", StringComparison.Ordinal) ||
                   fileName.EndsWith(".cmd", StringComparison.Ordinal) ||
                   fileName.EndsWith(".bat", StringComparison.Ordinal)
            ? fileName[..^4]
            : fileName;
        var fromExecutable = name switch
        {
            "dotnet" => WorkspaceProcessToolchain.Dotnet,
            "py" or "pythonw" or "conda" => WorkspaceProcessToolchain.Python,
            _ when name.StartsWith("python", StringComparison.Ordinal) => WorkspaceProcessToolchain.Python,
            "pwsh" or "powershell" => WorkspaceProcessToolchain.PowerShell,
            "node" or "npm" or "npx" => WorkspaceProcessToolchain.Node,
            "docker" => WorkspaceProcessToolchain.Docker,
            "git" => WorkspaceProcessToolchain.Git,
            _ => WorkspaceProcessToolchain.None
        };
        return fromExecutable != WorkspaceProcessToolchain.None
            ? fromExecutable
            : ResolveToolchainFromToolName(toolName);
    }

    private HashSet<string> ResolveInheritedNames(
        string? toolName,
        string? executable,
        bool extendedEnvironmentAllowed)
    {
        var names = new HashSet<string>(commonInheritedEnvironmentNames, environmentNameComparer);
        names.UnionWith(GetToolchainEnvironmentNames(ResolveToolchain(toolName, executable)));
        if (extendedEnvironmentAllowed)
        {
            var settings = Settings;
            if (settings.NetworkTrust)
            {
                names.UnionWith(WorkspaceProcessEnvironmentSettings.NetworkTrustNames);
            }

            names.UnionWith(settings.AdditionalInheritedNames.Where(
                name => !WorkspaceProcessEnvironmentSettings.IsDeniedName(name)));
        }

        return names;
    }

    private void ApplyUnixHomeGuarantee(Dictionary<string, string?> merged)
    {
        if (platform == LocalHostPlatform.Windows ||
            (merged.TryGetValue("HOME", out var home) && !string.IsNullOrWhiteSpace(home)) ||
            Settings.ToolchainHomePath is not { } toolchainHome)
        {
            return;
        }

        TryEnsurePrivateDirectory(toolchainHome);
        merged["HOME"] = toolchainHome;
        if (!merged.TryGetValue("DOTNET_CLI_HOME", out var cliHome) || string.IsNullOrWhiteSpace(cliHome))
        {
            merged["DOTNET_CLI_HOME"] = toolchainHome;
        }
    }

    private static void TryEnsurePrivateDirectory(string path)
    {
        try
        {
            LocalHostPlatformExtensions.CreatePrivateDirectory(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // The toolchain reports its own error; the environment still names the configured home.
        }
    }

    private bool IsNeverEmpty(string name)
        => NeverEmptyEnvironmentNames.Contains(name, environmentNameComparer);

    private Dictionary<string, string?> ReadCurrentEnvironment()
    {
        var source = new Dictionary<string, string?>(environmentNameComparer);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string name)
            {
                source[name] = entry.Value?.ToString();
            }
        }

        return source;
    }

    private static WorkspaceProcessToolchain ResolveToolchainFromToolName(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return WorkspaceProcessToolchain.None;
        }

        if (toolName.StartsWith("workspace_dotnet_", StringComparison.OrdinalIgnoreCase))
        {
            return WorkspaceProcessToolchain.Dotnet;
        }

        if (string.Equals(toolName, "workspace_python_run_file", StringComparison.OrdinalIgnoreCase))
        {
            return WorkspaceProcessToolchain.Python;
        }

        if (string.Equals(toolName, "workspace_pwsh_run_script", StringComparison.OrdinalIgnoreCase))
        {
            return WorkspaceProcessToolchain.PowerShell;
        }

        return string.Equals(toolName, "docker", StringComparison.OrdinalIgnoreCase)
            ? WorkspaceProcessToolchain.Docker
            : WorkspaceProcessToolchain.None;
    }

    private static IReadOnlyList<string> GetToolchainEnvironmentNames(WorkspaceProcessToolchain toolchain)
        => toolchain switch
        {
            WorkspaceProcessToolchain.Dotnet => DotnetInheritedEnvironmentNames,
            WorkspaceProcessToolchain.Python => PythonInheritedEnvironmentNames,
            WorkspaceProcessToolchain.PowerShell => PowerShellInheritedEnvironmentNames,
            WorkspaceProcessToolchain.Node => NodeInheritedEnvironmentNames,
            WorkspaceProcessToolchain.Docker => DockerInheritedEnvironmentNames,
            _ => []
        };
}
