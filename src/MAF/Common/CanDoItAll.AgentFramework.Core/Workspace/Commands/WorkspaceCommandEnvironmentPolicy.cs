using System.Collections;

namespace CanDoItAll.AgentFramework.Core;

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

    private readonly StringComparer environmentNameComparer;
    private readonly HashSet<string> commonInheritedEnvironmentNames;
    private readonly IReadOnlyDictionary<string, string?>? currentEnvironmentOverride;

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
        IReadOnlyDictionary<string, string?>? currentEnvironmentOverride)
    {
        environmentNameComparer = platform.EnvironmentNameComparer();
        this.currentEnvironmentOverride = currentEnvironmentOverride is null
            ? null
            : new Dictionary<string, string?>(currentEnvironmentOverride, environmentNameComparer);
        commonInheritedEnvironmentNames = new HashSet<string>(CommonInheritedEnvironmentNames, environmentNameComparer);
        commonInheritedEnvironmentNames.UnionWith(
            platform == LocalHostPlatform.Windows
                ? WindowsInheritedEnvironmentNames
                : UnixInheritedEnvironmentNames);
    }

    public StringComparer EnvironmentNameComparer => environmentNameComparer;

    public IReadOnlyDictionary<string, string?> BuildEnvironmentVariables()
    {
        var source = new Dictionary<string, string?>(environmentNameComparer);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is not string name)
            {
                continue;
            }

            source[name] = entry.Value?.ToString();
        }

        return BuildEnvironmentVariables(source, toolName: null);
    }

    internal IReadOnlyDictionary<string, string?> BuildEnvironmentVariables(
        IReadOnlyDictionary<string, string?> source,
        string? toolName = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var inheritedEnvironmentNames = new HashSet<string>(commonInheritedEnvironmentNames, environmentNameComparer);
        inheritedEnvironmentNames.UnionWith(GetToolSpecificEnvironmentNames(toolName));
        var environment = new Dictionary<string, string?>(environmentNameComparer);
        foreach (var pair in source)
        {
            if (inheritedEnvironmentNames.Contains(pair.Key))
            {
                environment[pair.Key] = pair.Value;
            }
        }

        return environment;
    }

    public IReadOnlyDictionary<string, string?> MergeEnvironmentVariables(
        IReadOnlyDictionary<string, string?>? environmentVariables,
        string? toolName = null)
    {
        var merged = new Dictionary<string, string?>(
            BuildEnvironmentVariables(currentEnvironmentOverride ?? ReadCurrentEnvironment(), toolName),
            environmentNameComparer);
        if (environmentVariables is null)
        {
            return merged;
        }

        foreach (var pair in environmentVariables)
        {
            merged[pair.Key] = pair.Value;
        }

        return merged;
    }

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

    private static IReadOnlyList<string> GetToolSpecificEnvironmentNames(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return [];
        }

        if (toolName.StartsWith("workspace_dotnet_", StringComparison.OrdinalIgnoreCase))
        {
            return DotnetInheritedEnvironmentNames;
        }

        if (string.Equals(toolName, "workspace_python_run_file", StringComparison.OrdinalIgnoreCase))
        {
            return PythonInheritedEnvironmentNames;
        }

        if (string.Equals(toolName, "workspace_pwsh_run_script", StringComparison.OrdinalIgnoreCase))
        {
            return PowerShellInheritedEnvironmentNames;
        }

        if (string.Equals(toolName, "docker", StringComparison.OrdinalIgnoreCase))
        {
            return DockerInheritedEnvironmentNames;
        }

        return [];
    }
}
