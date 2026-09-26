namespace CanDoItAll.AgentFramework.Core;

internal enum LocalHostPlatform
{
    Windows,
    Linux,
    MacOS
}

internal static class LocalHostPlatformExtensions
{
    public static LocalHostPlatform CaptureCurrent()
        => OperatingSystem.IsWindows()
            ? LocalHostPlatform.Windows
            : OperatingSystem.IsLinux()
                ? LocalHostPlatform.Linux
                : OperatingSystem.IsMacOS()
                    ? LocalHostPlatform.MacOS
                    : throw new PlatformNotSupportedException(
                        "Local process execution is supported only on Windows, Linux, and macOS.");

    public static StringComparer EnvironmentNameComparer(this LocalHostPlatform platform)
        => platform == LocalHostPlatform.Windows
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    /// <summary>
    /// Creates a directory only the current user can use: owner-only mode (0700) on Linux and macOS; on
    /// Windows the folder inherits the ACL of its parent (for example the user profile).
    /// </summary>
    public static void CreatePrivateDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
            return;
        }

        Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
}
