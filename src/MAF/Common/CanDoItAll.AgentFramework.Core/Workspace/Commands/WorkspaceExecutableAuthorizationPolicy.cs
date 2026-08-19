namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkspaceExecutableAuthorizationPolicy
{
    private readonly LocalHostPlatform platform;
    private readonly string? pathExtensions;

    public WorkspaceExecutableAuthorizationPolicy()
        : this(
            LocalHostPlatformExtensions.CaptureCurrent(),
            Environment.GetEnvironmentVariable("PATHEXT"))
    {
    }

    internal WorkspaceExecutableAuthorizationPolicy(
        LocalHostPlatform platform,
        string? pathExtensions)
    {
        this.platform = platform;
        this.pathExtensions = pathExtensions;
    }

    public bool IsAllowedResolvedPath(
        string resolvedExecutablePath,
        IReadOnlyCollection<string> allowedExecutableNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedExecutablePath);
        return IsAllowedFileName(
            GetFileName(resolvedExecutablePath),
            allowedExecutableNames);
    }

    public bool IsAllowedCommandName(
        string command,
        IReadOnlyCollection<string> allowedExecutableNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return IsAllowedFileName(GetFileName(command.Trim()), allowedExecutableNames);
    }

    private bool IsAllowedFileName(
        string executableFileName,
        IReadOnlyCollection<string> allowedExecutableNames)
    {
        ArgumentNullException.ThrowIfNull(allowedExecutableNames);
        var comparer = platform == LocalHostPlatform.Windows
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        foreach (var allowedName in allowedExecutableNames)
        {
            if (string.IsNullOrWhiteSpace(allowedName))
            {
                continue;
            }

            var allowedFileName = GetFileName(allowedName.Trim());
            if (comparer.Equals(executableFileName, allowedFileName))
            {
                return true;
            }

            if (platform != LocalHostPlatform.Windows || Path.HasExtension(allowedFileName))
            {
                continue;
            }

            if (WorkspaceExecutableLocator
                .GetCandidateFileNames(allowedFileName, platform, pathExtensions)
                .Contains(executableFileName, comparer))
            {
                return true;
            }
        }

        return false;
    }

    private string GetFileName(string path)
    {
        if (platform != LocalHostPlatform.Windows)
        {
            return Path.GetFileName(path);
        }

        var separatorIndex = Math.Max(
            path.LastIndexOf('\\'),
            path.LastIndexOf('/'));
        return separatorIndex < 0
            ? path
            : path[(separatorIndex + 1)..];
    }
}
