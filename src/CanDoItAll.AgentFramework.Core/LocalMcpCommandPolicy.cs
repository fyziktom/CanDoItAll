namespace CanDoItAll.AgentFramework.Core;

public static class LocalMcpCommandPolicy
{
    private static readonly HashSet<string> AllowedCommandNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "dotnet",
        "dotnet.exe",
        "node",
        "node.exe",
        "npx",
        "npx.cmd",
        "npx.exe",
        "npx.ps1",
        "powershell",
        "powershell.exe",
        "pwsh",
        "pwsh.exe",
        "python",
        "python.exe",
        "python3",
        "python3.exe",
        "uv",
        "uv.exe",
        "uvx",
        "uvx.exe"
    };

    public static bool IsAllowed(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        return AllowedCommandNames.Contains(Path.GetFileName(command.Trim()));
    }

    public static string DescribeAllowedCommands()
    {
        return string.Join(", ", AllowedCommandNames.OrderBy(item => item, StringComparer.OrdinalIgnoreCase));
    }
}
