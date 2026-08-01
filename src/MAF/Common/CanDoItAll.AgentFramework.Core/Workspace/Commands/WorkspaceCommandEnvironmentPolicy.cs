using System.Collections;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class WorkspaceCommandEnvironmentPolicy
{
    private static readonly HashSet<string> InheritedEnvironmentNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "APPDATA",
        "COMPUTERNAME",
        "COMSPEC",
        "CommonProgramFiles",
        "CommonProgramFiles(x86)",
        "CommonProgramW6432",
        "HOME",
        "LOCALAPPDATA",
        "NUMBER_OF_PROCESSORS",
        "OS",
        "PATH",
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
        "TEMP",
        "TMP",
        "USERDOMAIN",
        "USERNAME",
        "USERPROFILE",
        "WINDIR",
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
        "NUGET_XMLDOC_MODE",
        "PIP_CACHE_DIR",
        "PIP_DISABLE_PIP_VERSION_CHECK",
        "PIP_NO_INPUT",
        "POWERSHELL_TELEMETRY_OPTOUT",
        "POWERSHELL_UPDATECHECK",
        "PYTHONDONTWRITEBYTECODE",
        "PYTHONIOENCODING",
        "PYTHONUNBUFFERED",
        "PYTHONUTF8",
        "VIRTUAL_ENV"
    };

    public IReadOnlyDictionary<string, string?> BuildEnvironmentVariables()
    {
        var source = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is not string name)
            {
                continue;
            }

            source[name] = entry.Value?.ToString();
        }

        return BuildEnvironmentVariables(source);
    }

    internal IReadOnlyDictionary<string, string?> BuildEnvironmentVariables(
        IReadOnlyDictionary<string, string?> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in source)
        {
            if (InheritedEnvironmentNames.Contains(pair.Key))
            {
                environment[pair.Key] = pair.Value;
            }
        }

        return environment;
    }

    public IReadOnlyDictionary<string, string?> MergeEnvironmentVariables(IReadOnlyDictionary<string, string?>? environmentVariables)
    {
        var merged = new Dictionary<string, string?>(BuildEnvironmentVariables(), StringComparer.OrdinalIgnoreCase);
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
}
