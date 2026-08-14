namespace CanDoItAll.AgentFramework.Core;

public static class LocalMcpCommandPolicy
{
    private static readonly string[] AllowedCommandNames =
    [
        "dotnet",
        "node",
        "npx",
        "powershell",
        "pwsh",
        "python",
        "python3",
        "uv",
        "uvx"
    ];

    public static bool IsAllowed(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        return new WorkspaceExecutableAuthorizationPolicy()
            .IsAllowedCommandName(command, AllowedCommandNames);
    }

    public static bool IsResolvedExecutableAllowed(string resolvedExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(resolvedExecutablePath))
        {
            return false;
        }

        return new WorkspaceExecutableAuthorizationPolicy()
            .IsAllowedResolvedPath(resolvedExecutablePath, AllowedCommandNames);
    }

    public static string DescribeAllowedCommands()
    {
        return string.Join(", ", AllowedCommandNames.Order(StringComparer.Ordinal));
    }
}
