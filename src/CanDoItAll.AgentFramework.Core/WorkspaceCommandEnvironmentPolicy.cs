using System.Collections;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class WorkspaceCommandEnvironmentPolicy
{
    private static readonly HashSet<string> DefaultEnvironmentNames = new(StringComparer.OrdinalIgnoreCase)
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
        "WINDIR"
    };

    private static readonly string[] DefaultEnvironmentPrefixes =
    [
        "DOTNET_",
        "MSBUILD",
        "NUGET_",
        "PIP_",
        "POWERSHELL_",
        "PSMODULE",
        "PYTHON",
        "VIRTUAL_ENV"
    ];

    public IReadOnlyDictionary<string, string?> BuildEnvironmentVariables()
    {
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is not string name)
            {
                continue;
            }

            if (!DefaultEnvironmentNames.Contains(name)
                && !DefaultEnvironmentPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            environment[name] = entry.Value?.ToString();
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
