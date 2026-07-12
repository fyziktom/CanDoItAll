namespace CanDoItAll.AgentFramework.Mcp;

internal static class McpExecutableResolver
{
    public static string ResolveWorkingDirectory(string workingDirectory)
    {
        var candidate = string.IsNullOrWhiteSpace(workingDirectory)
            ? "."
            : workingDirectory.Trim();
        return Path.GetFullPath(Path.IsPathRooted(candidate)
            ? candidate
            : Path.Combine(Environment.CurrentDirectory, candidate));
    }

    public static string ResolveExecutablePath(string command)
    {
        var trimmed = command.Trim();
        if (Path.IsPathRooted(trimmed) ||
            trimmed.Contains(Path.DirectorySeparatorChar) ||
            trimmed.Contains(Path.AltDirectorySeparatorChar))
        {
            return trimmed;
        }

        foreach (var directory in EnumeratePathDirectories())
        {
            foreach (var candidate in EnumerateExecutableCandidates(trimmed))
            {
                var fullPath = Path.Combine(directory, candidate);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }

        return trimmed;
    }

    private static IEnumerable<string> EnumeratePathDirectories()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            yield break;
        }

        foreach (var item in path.Split(
                     Path.PathSeparator,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var directory = item.Trim();
            if (!string.IsNullOrWhiteSpace(directory))
            {
                yield return directory;
            }
        }
    }

    private static IEnumerable<string> EnumerateExecutableCandidates(string command)
    {
        if (!OperatingSystem.IsWindows() ||
            !string.IsNullOrWhiteSpace(Path.GetExtension(command)))
        {
            yield return command;
            yield break;
        }

        var pathExtensions = Environment.GetEnvironmentVariable("PATHEXT") ??
            ".COM;.EXE;.BAT;.CMD";
        foreach (var extension in pathExtensions.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            yield return command + extension.Trim();
        }

        yield return command;
    }
}
