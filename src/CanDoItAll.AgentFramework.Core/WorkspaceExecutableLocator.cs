namespace CanDoItAll.AgentFramework.Core;

internal sealed class WorkspaceExecutableLocator
{
    public string ResolveExecutablePath(IReadOnlyList<string> candidateNames)
    {
        foreach (var candidateName in candidateNames.Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            if (TryResolveExecutablePath(candidateName, out var resolvedPath))
            {
                return resolvedPath;
            }
        }

        throw new InvalidOperationException($"Unable to resolve any of the requested executables: {string.Join(", ", candidateNames)}.");
    }

    private static bool TryResolveExecutablePath(string executableName, out string resolvedPath)
    {
        var candidateFileNames = Path.HasExtension(executableName)
            ? new[] { executableName }
            : new[] { executableName, executableName + ".exe", executableName + ".cmd", executableName + ".bat" };

        foreach (var pathDirectory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var candidateFileName in candidateFileNames)
            {
                var candidatePath = Path.Combine(pathDirectory, candidateFileName);
                if (File.Exists(candidatePath))
                {
                    resolvedPath = candidatePath;
                    return true;
                }
            }
        }

        resolvedPath = string.Empty;
        return false;
    }
}
